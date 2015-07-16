using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using FlashDevelop;
using FlashDevelop.Utilities;
using HaXeContext;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Helpers;
using ProjectManager.Projects.Haxe;
using ScintillaNet;
using TemplateUtils = PostfixCodeCompletion.Helpers.TemplateUtils;

namespace PostfixCodeCompletion
{
    public class PluginMain : IPlugin
    {
        string settingFilename;
        static IHaxeCompletionHandler completionModeHandler;
        static int completionListItemCount;

        #region Required Properties

        public int Api { get { return 1; }}

        public string Name { get { return "PostfixCodeCompletion"; }}

        public string Guid { get { return "21d9ab3e-93e4-4460-9298-c62f87eed7ba"; }}

        public string Help { get { return ""; }}

        public string Author { get { return "SlavaRa"; }}

        public string Description { get { return "Postfix code completion helps reduce backward caret jumps as you write code"; }}

        public object Settings { get; private set; }

        #endregion

        #region Required Methods

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
            InitBasics();
            LoadSettings();
            AddEventHandlers();
        }

        /// <summary>
        /// Disposes the plugin
        /// </summary>
        public void Dispose()
        {
            if (completionModeHandler != null) completionModeHandler.Stop();
            SaveSettings();
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
        {
            switch (e.Type)
            {
                case EventType.UIStarted:
                    Reflector.CompletionListCompletionList().VisibleChanged += OnCompletionListVisibleChanged;
                    break;
                case EventType.Command:
                    if (((DataEvent) e).Action == ProjectManager.ProjectManagerEvents.Project)
                    {
                        if (!(PluginBase.CurrentProject is HaxeProject)) return;
                        Context context = (Context) ASContext.GetLanguageContext("haxe");
                        ((HaXeSettings)context.Settings).CompletionModeChanged += OnHaxeCompletionModeChanged;
                        OnHaxeCompletionModeChanged();
                    }
                    break;
                case EventType.Keys:
                    Keys keys = ((KeyEvent) e).Value;
                    if (keys == (Keys.Control | Keys.Space))
                    {
                        ASResult expr = GetPostfixCompletionExpr();
                        if (expr == null || expr.IsNull()) return;
                        e.Handled = ASComplete.OnShortcut(keys, PluginBase.MainForm.CurrentDocument.SciControl);
                        if (!CompletionList.Active)
                        {
                            Reflector.CompletionListCompletionList().VisibleChanged -= OnCompletionListVisibleChanged;
                            UpdateCompletionList(expr);
                            Reflector.CompletionListCompletionList().VisibleChanged += OnCompletionListVisibleChanged;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region Custom Methods

        /// <summary>
        /// Initializes important variables
        /// </summary>
        void InitBasics()
        {
            string dataPath = Path.Combine(PathHelper.DataDir, Name);
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
            settingFilename = Path.Combine(dataPath, "Settings.fdb");
        }

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        void LoadSettings()
        {
            Settings = new Settings();
            if (!File.Exists(settingFilename)) SaveSettings();
            else Settings = (Settings) ObjectSerializer.Deserialize(settingFilename, Settings);
            TemplateUtils.Settings = (Settings) Settings;
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.UIStarted | EventType.Command);
            EventManager.AddEventHandler(this, EventType.Keys, HandlingPriority.High);
            UITools.Manager.OnCharAdded += OnCharAdded;
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        void SaveSettings()
        {
            ObjectSerializer.Serialize(settingFilename, Settings);
        }

        static void UpdateCompletionList()
        {
            UpdateCompletionList(GetPostfixCompletionExpr());
        }

        static void UpdateCompletionList(ASResult expr)
        {
            if (expr == null || expr.IsNull()) return;
            MemberModel target = GetPostfixCompletionTarget(expr);
            if (target != null)
            {
                UpdateCompletionList(target, expr);
                return;
            }
            if (completionModeHandler == null) return;
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (sci.ConfigurationLanguage.ToLower() != "haxe" || expr.Context == null || sci.CharAt(expr.Context.Position) != '.')
                return;
            HaxeComplete hc = new HaxeComplete(sci, expr, false, completionModeHandler, HaxeCompilerService.TYPE);
            hc.GetPositionType(OnFunctionTypeResult);
        }

        static void UpdateCompletionList(MemberModel target, ASResult expr)
        {
            if (target == null) return;
            string templates = TemplateUtils.GetTemplatesDir();
            if (!Directory.Exists(templates)) return;
            List<ICompletionListItem> items = GetPostfixCompletionItems(target, expr);
            List<ICompletionListItem> allItems = Reflector.CompletionListAllItems();
            if (allItems != null)
            {
                allItems = new List<ICompletionListItem>(allItems);
                foreach (ICompletionListItem item in items)
                {
                    if (!allItems.Exists(it => it.Label == item.Label)) allItems.Add(item);
                }
                items = allItems;
            }
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            string word = sci.GetWordLeft(sci.CurrentPos - 1, false);
            CompletionList.Show(items, false, word);
            completionListItemCount = Reflector.CompletionListCompletionList().Items.Count;
            Reflector.CompletionListCompletionList().SelectedValueChanged += OnCompletionListSelectedValueChanged;
        }

        static ASResult GetPostfixCompletionExpr()
        {
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            int currentLine = Reflector.ScintillaControlCurrentLine;
            int positionFromLine = sci.PositionFromLine(currentLine);
            int position = -1;
            string characters =
                ScintillaControl.Configuration.GetLanguage(sci.ConfigurationLanguage).characterclass.Characters;
            for (int i = sci.CurrentPos; i > positionFromLine; i--)
            {
                char c = (char) sci.CharAt(i);
                if (c == '.')
                {
                    position = i;
                    break;
                }
                if (c > ' ' && !characters.Contains(c) && c != '$') break;
            }
            if (position == -1) return null;
            position -= positionFromLine;
            string line = sci.GetLine(currentLine);
            line = line.Remove(position);
            line = line.Insert(position, ";");
            return Reflector.ASGeneratorGetStatementReturnType(sci, line, positionFromLine);
        }

        static MemberModel GetPostfixCompletionTarget(ASResult expr)
        {
            if (expr == null || expr.IsNull()) return null;
            MemberModel member = expr.Member;
            if (member != null &&
                (!string.IsNullOrEmpty(member.Type) && member.Type != ASContext.Context.Features.voidKey))
                return member;
            ClassModel type = expr.Type;
            if (type != null && !type.IsVoid() && !string.IsNullOrEmpty(type.Type) &&
                type.Type != ASContext.Context.Features.voidKey)
                return type;
            return null;
        }

        static List<ICompletionListItem> GetPostfixCompletionItems(MemberModel target, ASResult expr)
        {
            List<ICompletionListItem> result = new List<ICompletionListItem>();
            if (expr.Member != null) result.AddRange(GetCompletionItems(expr.Member.Type, target, expr));
            else if (expr.Type != null) result.AddRange(GetCompletionItems(expr.Type.Type, target, expr));
            result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_MEMBER, expr));
            if (GetTargetIsNullable(target))
            {
                result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_NULLABLE, expr));
            }
            if (GetTargetIsCollection(target))
            {
                result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_COLLECTION, expr));
            }
            if (GetTargetIsHash(target))
            {
                result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_COLLECTION_OR_HASH, expr));
            }
            if (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower() == "haxe")
            {
                ClassModel type = expr.Type != null && !string.IsNullOrEmpty(expr.Type.Type) &&
                                  expr.Type.Type != ASContext.Context.Features.voidKey
                    ? expr.Type
                    : null;
                if (type != null)
                {
                    if (GetTargetIsCollection(type))
                    {
                        result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_COLLECTION, expr));
                    }
                    if (GetTargetIsHash(type))
                    {
                        result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_COLLECTION_OR_HASH, expr));
                    }
                }
            }
            if (GetTargetIsBoolean(target))
            {
                result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_BOOLEAN, expr));
            }
            if (GetTargetIsNumber(target))
            {
                result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_NUMBER, expr));
            }
            if (GetTargetIsString(target))
            {
                result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_STRING, expr));
            }
            return result.Distinct().ToList();
        }

        static bool GetTargetIsNullable(MemberModel target)
        {
            return !GetTargetIsNumber(target) && target.Type != ASContext.Context.Features.booleanKey;
        }

        static bool GetTargetIsCollection(MemberModel target)
        {
            string type = target.Type;
            string arrayKey = ASContext.Context.Features.arrayKey;
            if (type == arrayKey) return true;
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    return type.Contains("Vector.<") || type.Contains(string.Format("{0}@", arrayKey));
                case "haxe":
                    return Reflector.ASGeneratorCleanType(type) == Reflector.ASGeneratorCleanType(arrayKey)
                           || (type.Contains("Vector<") && Reflector.ASGeneratorCleanType(type) == Reflector.ASGeneratorCleanType("Vector<T>"));
                default:
                    return false;
            }
        }

        static bool GetTargetIsHash(MemberModel target)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    string type = target.Type;
                    return type == ASContext.Context.Features.objectKey || type == "Dictionary";
                case "haxe":
                    Func<MemberModel, bool> isIteratorOrIterable = m =>
                    {
                        string cleanType = Reflector.ASGeneratorCleanType(m.Type);
                        return cleanType == "Iterator" || cleanType == "Iterable";
                    };
                    if (isIteratorOrIterable(target)) return true;
                    if (target is ClassModel)
                    {
                        ClassModel classModel = target as ClassModel;
                        while (classModel != null && !classModel.IsVoid())
                        {
                            if (classModel.Members.Cast<MemberModel>().Any(member => isIteratorOrIterable(member)))
                            {
                                return true;
                            }
                            classModel.ResolveExtends();
                            classModel = classModel.Extends;
                        }
                    }
                    break;
            }
            return false;
        }

        static bool GetTargetIsBoolean(MemberModel target)
        {
            return target.Type == ASContext.Context.Features.booleanKey;
        }

        static bool GetTargetIsNumber(MemberModel target)
        {
            string type = target.Type;
            if (type == ASContext.Context.Features.numberKey) return true;
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    return type == "int" || type == "uint";
                case "haxe":
                    return type == "Int" || type == "UInt";
                default:
                    return false;
            }
        }

        static bool GetTargetIsString(MemberModel target)
        {
            return target.Type == "String";
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(string pattern, MemberModel target, ASResult expr)
        {
            return GetCompletionItems(TemplateUtils.GetTemplates(target.Type), pattern, expr);
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(string pattern, ASResult expr)
        {
            return GetCompletionItems(TemplateUtils.GetTemplates(pattern), pattern, expr);
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(Dictionary<string, string> templates, string pattern, ASResult expr)
        {
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            Bitmap itemIcon = null;
            bool haxeStringCode = false;
            bool isHaxe = sci.ConfigurationLanguage.ToLower() == "haxe";
            if (isHaxe && GetPostfixCompletionTarget(expr).Type == "String")
            {
                int pos = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
                haxeStringCode = sci.CharAt(pos) == '"' && sci.CharAt(pos + 1) != '\\' && sci.CharAt(pos + 2) == '"';
                if (haxeStringCode) itemIcon = (Bitmap) ASContext.Panel.GetIcon(PluginUI.ICON_PROPERTY);
            }
            List<ICompletionListItem> result = new List<ICompletionListItem>();
            foreach (KeyValuePair<string, string> pathToTemplate in templates)
            {
                string fileName = Path.GetFileNameWithoutExtension(pathToTemplate.Key);
                if (isHaxe && fileName == "code" && !haxeStringCode) continue;
                string template = pathToTemplate.Value;
                switch (pattern)
                {
                    case TemplateUtils.PATTERN_COLLECTION:
                        template = TemplateUtils.ProcessCollectionTemplate(template, expr);
                        break;
                    case TemplateUtils.PATTERN_COLLECTION_OR_HASH:
                        template = TemplateUtils.ProcessHashTemplate(template, expr);
                        break;
                }
                PostfixCompletionItem item = new PostfixCompletionItem(fileName, template, expr)
                {
                    Pattern = pattern
                };
                if (isHaxe && fileName == "code" && itemIcon != null)
                {
                    item.Icon = itemIcon;
                    itemIcon = null;
                }
                result.Add(item);
            }
            return result;
        }

        static Process CreateHaxeProcess(string args)
        {
            var process = Path.Combine(PluginBase.CurrentProject.CurrentSDK, "haxe.exe");
            if (!File.Exists(process)) return null;
            Process result = new Process
            {
                StartInfo =
                {
                    FileName = process,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };
            return result;
        }

        #endregion

        #region Event Handlers

        static void OnCharAdded(ScintillaControl sender, int value)
        {
            if ((char) value != '.') return;
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (ASComplete.OnChar(sci, value, false))
            {
                if (Reflector.CompletionListCompletionList().Visible) UpdateCompletionList();
                return;
            }
            if (!Reflector.ASCompleteHandleDotCompletion(sci, true) || CompletionList.Active) return;
            ASResult expr = GetPostfixCompletionExpr();
            if (expr == null || expr.IsNull()) return;
            Reflector.CompletionListCompletionList().VisibleChanged -= OnCompletionListVisibleChanged;
            UpdateCompletionList(expr);
            Reflector.CompletionListCompletionList().VisibleChanged += OnCompletionListVisibleChanged;
        }

        static void OnCompletionListVisibleChanged(object o, EventArgs args)
        {
            if (Reflector.CompletionListCompletionList().Visible) UpdateCompletionList();
            else Reflector.CompletionListCompletionList().SelectedValueChanged -= OnCompletionListSelectedValueChanged;
        }

        static void OnCompletionListSelectedValueChanged(object sender, EventArgs args)
        {
            Reflector.CompletionListCompletionList().SelectedValueChanged -= OnCompletionListSelectedValueChanged;
            if (completionListItemCount != Reflector.CompletionListCompletionList().Items.Count) UpdateCompletionList();
        }

        static void OnHaxeCompletionModeChanged()
        {
            if (completionModeHandler != null)
            {
                completionModeHandler.Stop();
                completionModeHandler = null;
            }
            if (!(PluginBase.CurrentProject is HaxeProject)) return;
            HaXeSettings settings = (HaXeSettings)((Context) ASContext.GetLanguageContext("haxe")).Settings;
            switch (settings.CompletionMode)
            {
                case HaxeCompletionModeEnum.CompletionServer:
                    if (settings.CompletionServerPort < 1024) completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(""));
                    else
                    {
                        completionModeHandler = new CompletionServerCompletionHandler(
                                CreateHaxeProcess("--wait " + settings.CompletionServerPort),
                                settings.CompletionServerPort);
                        ((CompletionServerCompletionHandler)completionModeHandler).FallbackNeeded += OnHaxeContextFallbackNeeded;
                    }
                    break;
                default:
                    completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(""));
                    break;
            }
        }

        static void OnHaxeContextFallbackNeeded(bool notSupported)
        {
            TraceManager.AddAsync("This SDK does not support server mode");
            if (completionModeHandler != null)
            {
                completionModeHandler.Stop();
                completionModeHandler = null;
            }
            completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(""));
        }

        static void OnFunctionTypeResult(HaxeComplete hc, HaxeCompleteResult result, HaxeCompleteStatus status)
        {
            switch (status)
            {
                case HaxeCompleteStatus.ERROR:
                    TraceManager.AddAsync(hc.Errors, -3);
                    if (hc.AutoHide) CompletionList.Hide();
                    break;
                case HaxeCompleteStatus.TYPE:
                    Reflector.CompletionListCompletionList().VisibleChanged -= OnCompletionListVisibleChanged;
                    ASResult expr = hc.Expr;
                    if (result.Type is ClassModel)
                    {
                        expr.Type = (ClassModel)result.Type;
                        expr.Member = null;
                        UpdateCompletionList(expr.Type, expr);
                    }
                    else
                    {
                        expr.Type = ASContext.Context.ResolveType(result.Type.Type, result.Type.InFile);
                        expr.Member = result.Type;
                        UpdateCompletionList(expr.Member, expr);
                    }
                    Reflector.CompletionListCompletionList().VisibleChanged += OnCompletionListVisibleChanged;
                    break;
            }
        }

        #endregion
    }

    class PostfixCompletionItem : ICompletionListItem
    {
        readonly string template;
        protected readonly ASResult Expr;

        public PostfixCompletionItem(string label, string template, ASResult expr)
        {
            Label = label;
            this.template = template;
            Expr = expr;
        }

        public string Label { get; private set; }

        string pattern;
        public virtual string Pattern
        {
            get { return pattern ?? TemplateUtils.PATTERN_MEMBER; }
            set { pattern = value; }
        }

        public string Value
        {
            get
            {
                ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
                int position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
                sci.SetSel(position, sci.CurrentPos);
                sci.ReplaceSel(string.Empty);
                position = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, Expr);
                sci.SetSel(position, sci.CurrentPos);
                string snippet = Regex.Replace(template, string.Format(TemplateUtils.PATTERN_BLOCK, Pattern), sci.SelText, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                snippet = TemplateUtils.ProcessMemberTemplate(snippet, Expr);
                snippet = ArgsProcessor.ProcessCodeStyleLineBreaks(snippet);
                sci.ReplaceSel(string.Empty);
                SnippetHelper.InsertSnippetText(sci, position, snippet);
                return null;
            }
        }

        Bitmap icon;
        public Bitmap Icon
        {
            get { return icon ?? (icon = (Bitmap) Globals.MainForm.FindImage("341")); }
            set { icon = value; }
        }

        string description;
        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(description))
                {
                    ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
                    int position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
                    int exprStartPosition = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, Expr);
                    int lineNum = Reflector.ScintillaControlCurrentLine;
                    string line = sci.GetLine(lineNum);
                    string snippet = line.Substring(exprStartPosition - sci.PositionFromLine(lineNum), position - exprStartPosition);
                    description = template.Replace(SnippetHelper.BOUNDARY, string.Empty);
                    description = Regex.Replace(description, string.Format(TemplateUtils.PATTERN_BLOCK, Pattern), snippet, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    description = TemplateUtils.ProcessMemberTemplate(description, Expr);
                    description = ArgsProcessor.ProcessCodeStyleLineBreaks(description);
                    description = description.Replace(SnippetHelper.ENTRYPOINT, "|");
                    description = description.Replace(SnippetHelper.EXITPOINT, "|");
                }
                return description;
            }
        }

        public new string ToString() { return Description; }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (!(obj is PostfixCompletionItem)) return false;
            PostfixCompletionItem other = (PostfixCompletionItem)obj;
            return other.Label == Label && other.Expr == Expr;
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            return Label.GetHashCode() ^ Expr.GetHashCode();
        }
    }
}