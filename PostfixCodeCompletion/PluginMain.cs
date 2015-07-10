using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using FlashDevelop;
using FlashDevelop.Utilities;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Helpers;
using ScintillaNet;
using TemplateUtils = PostfixCodeCompletion.Helpers.TemplateUtils;

namespace PostfixCodeCompletion
{
    public class PluginMain : IPlugin
    {
        string settingFilename;
        static ListBox completionList;
        static CompilerCompletionHandler completionModeHandler;

        #region Required Properties

        public int Api
        {
            get { return 1; }
        }

        public string Name
        {
            get { return "PostfixCodeCompletion"; }
        }

        public string Guid
        {
            get { return "21d9ab3e-93e4-4460-9298-c62f87eed7ba"; }
        }

        public string Help
        {
            get { return ""; }
        }

        public string Author
        {
            get { return "SlavaRa"; }
        }

        public string Description
        {
            get { return "Postfix code completion helps reduce backward caret jumps as you write code"; }
        }

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
                    completionList = PluginBase.MainForm.Controls.OfType<ListBox>().FirstOrDefault();
                    completionList.VisibleChanged += OnCompletionListVisibleChanged;
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
                            completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                            UpdateCompletionList(expr);
                            completionList.VisibleChanged += OnCompletionListVisibleChanged;
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
            EventManager.AddEventHandler(this, EventType.UIStarted);
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
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (sci.ConfigurationLanguage.ToLower() != "haxe" || expr.Context == null ||
                sci.CharAt(expr.Context.Position) != '.')
                return;
            if (completionModeHandler == null)
                completionModeHandler = new CompilerCompletionHandler(createHaxeProcess(""));
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
            CompletionList.Show(items, false);
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

        static List<ICompletionListItem> GetPostfixCompletionItems(MemberModel target, ASResult expr)
        {
            List<ICompletionListItem> result = new List<ICompletionListItem>();
            result.AddRange(GetCompletionItems(typeof (TPostfixCompletionItem), target, expr));
            result.AddRange(GetCompletionItems(TemplateType.Member, typeof (PostfixCompletionItem), expr));
            if (GetTargetIsNullable(target))
                result.AddRange(GetCompletionItems(TemplateType.Nullable, typeof (NullablePostfixCompletionItem), expr));
            if (GetTargetIsCollection(target))
                result.AddRange(GetCompletionItems(TemplateType.Collection, typeof (CollectionPostfixCompletionItem),
                    expr));
            if (GetTargetIsHash(target))
                result.AddRange(GetCompletionItems(TemplateType.Hash, typeof (HashPostfixCompletionItem), expr));
            if (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower() == "haxe")
            {
                ClassModel type = expr.Type != null && !string.IsNullOrEmpty(expr.Type.Type) &&
                                  expr.Type.Type != ASContext.Context.Features.voidKey
                    ? expr.Type
                    : null;
                if (type != null)
                {
                    if (GetTargetIsCollection(type))
                        result.AddRange(GetCompletionItems(TemplateType.Collection,
                            typeof (CollectionPostfixCompletionItem), expr));
                    if (GetTargetIsHash(type))
                        result.AddRange(GetCompletionItems(TemplateType.Hash, typeof (HashPostfixCompletionItem), expr));
                }
            }
            if (GetTargetIsBoolean(target))
                result.AddRange(GetCompletionItems(TemplateType.Boolean, typeof (BooleanPostfixCompletionItem), expr));
            if (GetTargetIsNumber(target))
                result.AddRange(GetCompletionItems(TemplateType.Number, typeof (NumberPostfixCompletionItem), expr));
            if (GetTargetIsString(target))
                result.AddRange(GetCompletionItems(TemplateType.String, typeof (StringPostfixCompletionItem), expr));
            return result.Distinct().ToList();
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

        static void OnFunctionTypeResult(HaxeComplete hc, HaxeCompleteResult result, HaxeCompleteStatus status)
        {
            switch (status)
            {
                case HaxeCompleteStatus.ERROR:
                    TraceManager.AddAsync(hc.Errors, -3);
                    if (hc.AutoHide) completionList.Hide();
                    break;
                case HaxeCompleteStatus.TYPE:
                    completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                    ASResult expr = hc.Expr;
                    if (result.Type is ClassModel)
                    {
                        expr.Type = (ClassModel) result.Type;
                        expr.Member = null;
                        UpdateCompletionList(expr.Type, expr);
                    }
                    else
                    {
                        expr.Type = null;
                        expr.Member = result.Type;
                        UpdateCompletionList(expr.Member, expr);
                    }
                    completionList.VisibleChanged += OnCompletionListVisibleChanged;
                    break;
            }
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
                           ||
                           type.Contains("Vector<") &&
                           Reflector.ASGeneratorCleanType(type) == Reflector.ASGeneratorCleanType("Vector<T>");
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
                            foreach (MemberModel member in classModel.Members)
                            {
                                if (isIteratorOrIterable(member)) return true;
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

        static IEnumerable<ICompletionListItem> GetCompletionItems(Type itemType, ASResult expr)
        {
            return GetCompletionItems(itemType, GetPostfixCompletionTarget(expr), expr);
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(Type itemType, MemberModel target,
            ASResult expr)
        {
            return GetCompletionItems(TemplateUtils.GetTemplates(target.Type), itemType, expr);
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(TemplateType templateType, Type itemType,
            ASResult expr)
        {
            return GetCompletionItems(TemplateUtils.GetTemplates(templateType), itemType, expr);
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(Dictionary<string, string> templates,
            Type itemType, ASResult expr)
        {
            List<ICompletionListItem> result = new List<ICompletionListItem>();
            foreach (KeyValuePair<string, string> pathToTemplate in templates)
            {
                string fileName = Path.GetFileNameWithoutExtension(pathToTemplate.Key);
                ConstructorInfo constructorInfo =
                    itemType.GetConstructor(new[] {typeof (string), typeof (string), typeof (ASResult)});
                result.Add(
                    (PostfixCompletionItem) constructorInfo.Invoke(new object[] {fileName, pathToTemplate.Value, expr}));
            }
            return result;
        }

        static Process createHaxeProcess(string args)
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
                Timer timer = new Timer {Interval = 100};
                timer.Tick += (o, args) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (completionList.Visible) UpdateCompletionList();
                };
                timer.Start();
                return;
            }
            if (!Reflector.ASCompleteHandleDotCompletion(sci, true) || CompletionList.Active) return;
            ASResult expr = GetPostfixCompletionExpr();
            if (expr == null || expr.IsNull()) return;
            completionList.VisibleChanged -= OnCompletionListVisibleChanged;
            UpdateCompletionList(expr);
            completionList.VisibleChanged += OnCompletionListVisibleChanged;
        }

        static void OnCompletionListVisibleChanged(object o, EventArgs args)
        {
            if (completionList.Visible) UpdateCompletionList();
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

        public virtual string Pattern { get { return TemplateUtils.PATTERN_MEMBER; }}

        public string Value
        {
            get
            {
                ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
                int position = sci.CurrentPos;
                for (int i = sci.CurrentPos; i > 0; i--)
                {
                    if ((char) sci.CharAt(i) != '.') continue;
                    position = i;
                    break;
                }
                sci.SetSel(position, sci.CurrentPos);
                sci.ReplaceSel(string.Empty);
                position = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, Expr);
                sci.SetSel(position, sci.CurrentPos);
                var pattern = this is TPostfixCompletionItem
                        ? string.Format(TemplateUtils.PATTERN_T_BLOCK, Pattern)
                        : string.Format(TemplateUtils.PATTERN_BLOCK, Pattern);
                string snippet = Regex.Replace(template, pattern, sci.SelText, RegexOptions.IgnoreCase | RegexOptions.Multiline);
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
        }

        string description;
        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(description))
                {
                    ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
                    int dotPosition = sci.CurrentPos - 1;
                    for (int i = sci.CurrentPos; i > 0; i--)
                    {
                        if ((char)sci.CharAt(i) != '.') continue;
                        dotPosition = i;
                        break;
                    }
                    int exprStartPosition = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, Expr);
                    int lineNum = Reflector.ScintillaControlCurrentLine;
                    string line = sci.GetLine(lineNum);
                    string snippet = line.Substring(exprStartPosition - sci.PositionFromLine(lineNum), dotPosition - exprStartPosition);
                    description = template.Replace(SnippetHelper.BOUNDARY, string.Empty);
                    var pattern = this is TPostfixCompletionItem
                        ? string.Format(TemplateUtils.PATTERN_T_BLOCK, Pattern)
                        : string.Format(TemplateUtils.PATTERN_BLOCK, Pattern);
                    description = Regex.Replace(description, pattern, snippet, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    description = TemplateUtils.ProcessMemberTemplate(description, Expr);
                    description = ArgsProcessor.ProcessCodeStyleLineBreaks(description);
                    description = description.Replace(SnippetHelper.ENTRYPOINT, "|");
                    description = description.Replace(SnippetHelper.EXITPOINT, "|");
                }
                return description;
            }
        }

        public new string ToString() { return Description; }

        public override bool Equals(object obj)
        {
            if (!(obj is PostfixCompletionItem)) return false;
            PostfixCompletionItem other = (PostfixCompletionItem)obj;
            return other.Label == Label && other.Expr == Expr;
        }

        public override int GetHashCode()
        {
            return Label.GetHashCode() ^ Expr.GetHashCode();
        }
    }

    class NullablePostfixCompletionItem : PostfixCompletionItem
    {
        public NullablePostfixCompletionItem(string label, string template, ASResult expr) : base(label, template, expr)
        {
        }

        public override string Pattern { get { return TemplateUtils.PATTERN_NULLABLE; }}
    }

    class CollectionPostfixCompletionItem : PostfixCompletionItem
    {
        public CollectionPostfixCompletionItem(string label, string template, ASResult expr)
            : base(label, TemplateUtils.ProcessCollectionTemplate(template, expr), expr)
        {
        }

        public override string Pattern { get { return TemplateUtils.PATTERN_COLLECTION; }}
    }

    class HashPostfixCompletionItem : PostfixCompletionItem
    {
        public HashPostfixCompletionItem(string label, string template, ASResult expr)
            : base(label, TemplateUtils.ProcessHashTemplate(template, expr), expr)
        {
        }

        public override string Pattern { get { return TemplateUtils.PATTERN_COLLECTION_OR_HASH; }}
    }

    class BooleanPostfixCompletionItem : PostfixCompletionItem
    {
        public BooleanPostfixCompletionItem(string label, string template, ASResult expr) : base(label, template, expr)
        {
        }

        public override string Pattern { get { return TemplateUtils.PATTERN_BOOLEAN; }}
    }

    class NumberPostfixCompletionItem : PostfixCompletionItem
    {
        public NumberPostfixCompletionItem(string label, string template, ASResult expr) : base(label, template, expr)
        {
        }

        public override string Pattern { get { return TemplateUtils.PATTERN_NUMBER; } }
    }

    class StringPostfixCompletionItem : PostfixCompletionItem
    {
        public StringPostfixCompletionItem(string label, string template, ASResult expr) : base(label, template, expr)
        {
        }

        public override string Pattern { get { return TemplateUtils.PATTERN_STRING; } }
    }

    class TPostfixCompletionItem : PostfixCompletionItem
    {
        public TPostfixCompletionItem(string label, string template, ASResult expr) : base(label, template, expr)
        {
        }

        public override string Pattern
        {
            get
            {
                if (Expr.Member != null) return Expr.Member.Type;
                if (Expr.Type != null) return Expr.Type.Type;
                return null;
            }
        }
    }
}