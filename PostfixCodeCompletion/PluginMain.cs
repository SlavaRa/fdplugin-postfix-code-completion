using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using HaXeContext;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Completion;
using PostfixCodeCompletion.Helpers;
using ProjectManager;
using ProjectManager.Projects.Haxe;
using ScintillaNet;
using PluginUI = ASCompletion.PluginUI;
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
            completionModeHandler?.Stop();
            SaveSettings();
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
        {
            var completionList = Reflector.CompletionList.completionList;
            switch (e.Type)
            {
                case EventType.UIStarted:
                    completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                    completionList.VisibleChanged += OnCompletionListVisibleChanged;
                    break;
                case EventType.Command:
                    if (((DataEvent) e).Action == ProjectManagerEvents.Project)
                    {
                        if (!(PluginBase.CurrentProject is HaxeProject)) return;
                        var context = (Context) ASContext.GetLanguageContext("haxe");
                        if (context == null) return;
                        var settings = (HaXeSettings) context.Settings;
                        settings.CompletionModeChanged -= OnHaxeCompletionModeChanged;
                        settings.CompletionModeChanged += OnHaxeCompletionModeChanged;
                        OnHaxeCompletionModeChanged();
                    }
                    break;
                case EventType.Keys:
                    var keys = ((KeyEvent) e).Value;
                    if (keys == (Keys.Control | Keys.Space))
                    {
                        if (CompletionList.Active) return;
                        var expr = GetPostfixCompletionExpr();
                        if (expr == null || expr.IsNull()) return;
                        e.Handled = ASComplete.OnShortcut(keys, PluginBase.MainForm.CurrentDocument.SciControl);
                        completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                        UpdateCompletionList(expr);
                        completionList.VisibleChanged += OnCompletionListVisibleChanged;
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
            var dataPath = Path.Combine(PathHelper.DataDir, Name);
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
        void SaveSettings() => ObjectSerializer.Serialize(settingFilename, Settings);

        static void UpdateCompletionList()
        {
            var expr = GetPostfixCompletionExpr();
            UpdateCompletionList(expr);
        }

        static void UpdateCompletionList(ASResult expr)
        {
            if (expr == null || expr.IsNull()) return;
            var target = GetPostfixCompletionTarget(expr);
            if (target != null)
            {
                UpdateCompletionList(target, expr);
                return;
            }
            if (expr.Context == null || completionModeHandler == null) return;
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (sci.ConfigurationLanguage.ToLower() != "haxe" || sci.CharAt(expr.Context.Position) != '.') return;
            var hc = new HaxeComplete(sci, expr, false, completionModeHandler, HaxeCompilerService.Type);
            hc.GetPositionType(OnFunctionTypeResult);
        }

        static void UpdateCompletionList(MemberModel target, ASResult expr)
        {
            if (target == null) return;
            if (!TemplateUtils.GetHasTemplates()) return;
            var items = GetPostfixCompletionItems(target, expr);
            var allItems = Reflector.CompletionList.allItems;
            if (allItems != null)
            {
                var labels = new HashSet<string>();
                foreach (var item in allItems.OfType<PostfixCompletionItem>())
                {
                    labels.Add(item.Label);
                }
                foreach (var item in items)
                {
                    var label = item.Label;
                    if (!labels.Contains(label)) allItems.Add(item);
                }
                items = allItems;
            }
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var word = sci.GetWordLeft(sci.CurrentPos - 1, false);
            if (!string.IsNullOrEmpty(word))
            {
                items = items.FindAll(it =>
                {
                    var score = CompletionList.SmartMatch(it.Label, word, word.Length);
                    return score > 0 && score < 6;
                });
            }
            CompletionList.Show(items, false, word);
            var list = Reflector.CompletionList.completionList;
            completionListItemCount = list.Items.Count;
            list.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
            list.SelectedValueChanged += OnCompletionListSelectedValueChanged;
        }

        static ASResult GetPostfixCompletionExpr()
        {
            var doc = PluginBase.MainForm.CurrentDocument;
            if (doc == null || !doc.IsEditable) return null;
            var language = PluginBase.CurrentProject.Language;
            if (!ASContext.GetLanguageContext(language).IsFileValid || !TemplateUtils.GetHasTemplates(language)) return null;
            var sci = doc.SciControl;
            if (sci.PositionIsOnComment(sci.CurrentPos)) return null;
            var currentLine = sci.CurrentLine;
            var positionFromLine = sci.LineIndentPosition(currentLine);
            var position = -1;
            var characters = ScintillaControl.Configuration.GetLanguage(language).characterclass.Characters;
            for (var i = sci.CurrentPos; i > positionFromLine; i--)
            {
                var c = (char) sci.CharAt(i);
                if (c == '.')
                {
                    position = i;
                    break;
                }
                if (c > ' ' && !characters.Contains(c) && c != '$') break;
            }
            if (position == -1) return null;
            position -= positionFromLine;
            var line = sci.GetLine(currentLine).Trim();
            line = line.Remove(position);
            line = line.Insert(position, ";");
            return Reflector.ASGenerator.GetStatementReturnType(sci, line, positionFromLine);
        }

        static MemberModel GetPostfixCompletionTarget(ASResult expr)
        {
            if (expr == null || expr.IsNull()) return null;
            var member = expr.Member;
            var voidKey = ASContext.Context.Features.voidKey;
            if (member != null && !string.IsNullOrEmpty(member.Type) && member.Type != voidKey)
                return member;
            var type = expr.Type;
            if (type != null && !type.IsVoid() && !string.IsNullOrEmpty(type.Type) && type.Type != voidKey)
                return type;
            return null;
        }

        static List<ICompletionListItem> GetPostfixCompletionItems(MemberModel target, ASResult expr)
        {
            var result = new List<ICompletionListItem>();
            if (expr.Member != null) result.AddRange(GetCompletionItems(expr.Member.Type, target, expr));
            else if (expr.Type != null) result.AddRange(GetCompletionItems(expr.Type.Type, target, expr));
            result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_MEMBER, expr));
            if (IsNullable(target)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_NULLABLE, expr));
            if (IsCollection(target)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_COLLECTION, expr));
            if (IsHash(target)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_HASH, expr));
            if (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower() == "haxe")
            {
                var type = expr.Type != null && !string.IsNullOrEmpty(expr.Type.Type) &&
                                  expr.Type.Type != ASContext.Context.Features.voidKey
                    ? expr.Type
                    : null;
                if (type != null)
                {
                    if (IsCollection(type)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_COLLECTION, expr));
                    if (IsHash(type)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_HASH, expr));
                }
            }
            if (IsBoolean(target)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_BOOL, expr));
            if (IsNumber(target)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_NUMBER, expr));
            if (IsString(target)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_STRING, expr));
            if (IsType(target)) result.AddRange(GetCompletionItems(TemplateUtils.PATTERN_TYPE, expr));
            return result.Distinct().ToList();
        }

        static bool IsNullable(MemberModel target) => !IsNumber(target) && target.Type != ASContext.Context.Features.booleanKey;

        static bool IsCollection(MemberModel target)
        {
            var type = target.Type;
            var arrayKey = ASContext.Context.Features.arrayKey;
            if (type == arrayKey) return true;
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    return type.Contains("Vector.<") || type.Contains($"@{arrayKey}");
                case "haxe":
                    return Reflector.ASGenerator.CleanType(type) == Reflector.ASGenerator.CleanType(arrayKey)
                        || (type.Contains("Vector<") && Reflector.ASGenerator.CleanType(type) == Reflector.ASGenerator.CleanType("Vector<T>"));
                default:
                    return false;
            }
        }

        static bool IsHash(MemberModel target)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower())
            {
                case "as2":
                case "as3":
                    var type = target.Type;
                    return type == ASContext.Context.Features.objectKey || type == "Dictionary";
                case "haxe":
                    if (IsIteratorOrIterable(target)) return true;
                    if (target is ClassModel)
                    {
                        var classModel = target as ClassModel;
                        while (classModel != null && !classModel.IsVoid())
                        {
                            if (classModel.Members.Items.Any(IsIteratorOrIterable))
                                return true;
                            classModel.ResolveExtends();
                            classModel = classModel.Extends;
                        }
                    }
                    return false;
                default: return false;
            }
        }

        static bool IsIteratorOrIterable(MemberModel member)
        {
            var cleanType = Reflector.ASGenerator.CleanType(member.Type);
            return cleanType == "Iterator" || cleanType == "Iterable";
        }

        static bool IsBoolean(MemberModel target) => target.Type == ASContext.Context.Features.booleanKey;

        static bool IsNumber(MemberModel target)
        {
            var type = target.Type;
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

        static bool IsString(MemberModel target) => target.Type == ASContext.Context.Features.stringKey;

        static bool IsType(MemberModel target) => target.Flags == FlagType.Class;

        static IEnumerable<ICompletionListItem> GetCompletionItems(string pattern, MemberModel target, ASResult expr)
        {
            var type = ASContext.Context.ResolveType(target.Type, ASContext.Context.CurrentModel);
            Dictionary<string, string> templates = null;
            while(!type.IsVoid())
            {
                pattern = type.QualifiedName;
                templates = TemplateUtils.GetTemplates(type.QualifiedName);
                if (templates.Count > 0) break;
                type.ResolveExtends();
                type = type.Extends;
            }
            return GetCompletionItems(templates ?? new Dictionary<string, string>(), pattern, expr);
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(string pattern, ASResult expr)
        {
            return GetCompletionItems(TemplateUtils.GetTemplates(pattern), pattern, expr);
        }

        static IEnumerable<ICompletionListItem> GetCompletionItems(Dictionary<string, string> templates, string pattern, ASResult expr)
        {
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            Bitmap itemIcon = null;
            var haxeStringCode = false;
            var isHaxe = sci.ConfigurationLanguage.ToLower() == "haxe";
            if (isHaxe && GetPostfixCompletionTarget(expr).Type == ASContext.Context.Features.stringKey)
            {
                var pos = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
                haxeStringCode = sci.CharAt(pos) == '"' && sci.CharAt(pos + 1) != '\\' && sci.CharAt(pos + 2) == '"';
                if (haxeStringCode) itemIcon = (Bitmap) ASContext.Panel.GetIcon(PluginUI.ICON_PROPERTY);
            }
            var result = new List<ICompletionListItem>();
            foreach (var pathToTemplate in templates)
            {
                var fileName = Path.GetFileNameWithoutExtension(pathToTemplate.Key);
                if (isHaxe && fileName == "code" && !haxeStringCode) continue;
                var template = pathToTemplate.Value;
                switch (pattern)
                {
                    case TemplateUtils.PATTERN_COLLECTION:
                        template = TemplateUtils.ProcessCollectionTemplate(template, expr);
                        break;
                    case TemplateUtils.PATTERN_HASH:
                        template = TemplateUtils.ProcessHashTemplate(template, expr);
                        break;
                }
                var item = new PostfixCompletionItem(fileName, template, expr)
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
            var result = new Process
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
            try
            {
                if ((char) value != '.' || !TemplateUtils.GetHasTemplates()) return;
                var sci = PluginBase.MainForm.CurrentDocument.SciControl;
                if (sci.PositionIsOnComment(sci.CurrentPos)) return;
                if (ASComplete.OnChar(sci, value, false))
                {
                    if (Reflector.CompletionList.completionList.Visible) UpdateCompletionList();
                    return;
                }
                if (!Reflector.ASComplete.HandleDotCompletion(sci, true) || CompletionList.Active) return;
                var expr = GetPostfixCompletionExpr();
                if (expr == null || expr.IsNull()) return;
                Reflector.CompletionList.completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                UpdateCompletionList(expr);
                Reflector.CompletionList.completionList.VisibleChanged += OnCompletionListVisibleChanged;
            }
            catch (Exception e)
            {
                ErrorManager.ShowError(e);
            }
        }

        static void OnCompletionListVisibleChanged(object o, EventArgs args)
        {
            var list = Reflector.CompletionList.completionList;
            if (list.Visible) UpdateCompletionList();
            else list.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
        }

        static void OnCompletionListSelectedValueChanged(object sender, EventArgs args)
        {
            var list = Reflector.CompletionList.completionList;
            list.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
            if (completionListItemCount != list.Items.Count) UpdateCompletionList();
        }

        static void OnHaxeCompletionModeChanged()
        {
            if (completionModeHandler != null)
            {
                completionModeHandler.Stop();
                completionModeHandler = null;
            }
            if (!(PluginBase.CurrentProject is HaxeProject)) return;
            var settings = (HaXeSettings)((Context) ASContext.GetLanguageContext("haxe")).Settings;
            if (!IsValidHaxeSDK(settings.InstalledSDKs.FirstOrDefault(sdk => sdk.Path == PluginBase.CurrentProject.CurrentSDK))) return;
            switch (settings.CompletionMode)
            {
                case HaxeCompletionModeEnum.CompletionServer:
                    if (settings.CompletionServerPort < 1024) completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
                    else
                    {
                        completionModeHandler = new CompletionServerCompletionHandler(
                            CreateHaxeProcess($"--wait {settings.CompletionServerPort}"),
                            settings.CompletionServerPort
                        );
                        ((CompletionServerCompletionHandler)completionModeHandler).FallbackNeeded += OnHaxeContextFallbackNeeded;
                    }
                    break;
                default:
                    completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
                    break;
            }
        }

        static bool IsValidHaxeSDK(InstalledSDK sdk)
        {
            if (sdk == null) return false;
            var version = sdk.Version;
            var hyphenIndex = version.IndexOf('-');
            if (hyphenIndex >= 0) version = version.Substring(0, hyphenIndex);
            var numbers = version.Split('.');
            var major = numbers.Length >= 1 ? int.Parse(numbers[0]) : 0;
            var minor = numbers.Length >= 2 ? int.Parse(numbers[1]) : 0;
            return major >= 3 && minor >= 2;
        }

        static void OnHaxeContextFallbackNeeded(bool notSupported)
        {
            TraceManager.AddAsync("PCC: This SDK does not support server mode");
            if (completionModeHandler != null)
            {
                completionModeHandler.Stop();
                completionModeHandler = null;
            }
            completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
        }

        static void OnFunctionTypeResult(HaxeComplete hc, HaxeCompleteResult result, HaxeCompleteStatus status)
        {
            switch (status)
            {
                case HaxeCompleteStatus.Error:
                    TraceManager.AddAsync(hc.Errors, -3);
                    if (hc.AutoHide) CompletionList.Hide();
                    break;
                case HaxeCompleteStatus.Type:
                    var list = Reflector.CompletionList.completionList;
                    list.VisibleChanged -= OnCompletionListVisibleChanged;
                    var expr = hc.Expr;
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
                    list.VisibleChanged += OnCompletionListVisibleChanged;
                    break;
            }
        }

        #endregion
    }

    class PostfixCompletionItem : ICompletionListItem
    {
        readonly string template;
        readonly ASResult expr;

        public PostfixCompletionItem(string label, string template, ASResult expr)
        {
            Label = label;
            this.template = template;
            this.expr = expr;
        }

        public string Label { get; set; }

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
                TemplateUtils.InsertSnippetText(expr, template, Pattern);
                return null;
            }
        }

        Bitmap icon;
        public Bitmap Icon
        {
            get { return icon ?? (icon = (Bitmap) PluginBase.MainForm.FindImage("341")); }
            set { icon = value; }
        }

        string description;
        public string Description
        {
            get
            {
                return description ?? (description = TemplateUtils.GetDescription(expr, template, Pattern));
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
            var other = (PostfixCompletionItem)obj;
            return other.Label == Label && other.expr == expr;
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
            return Label.GetHashCode() ^ expr.GetHashCode();
        }
    }
}