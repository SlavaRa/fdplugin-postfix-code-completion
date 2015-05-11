using System;
using System.Collections;
using System.Collections.Generic;
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
        private string settingFilename;
        private ListBox completionList;

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
            else Settings = (Settings)ObjectSerializer.Deserialize(settingFilename, Settings);
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.UIStarted);
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        void SaveSettings()
        {
            ObjectSerializer.Serialize(settingFilename, Settings);
        }

        #endregion

        #region Event Handlers

        void OnCompletionListVisibleChanged(object o, EventArgs args)
        {
            if (!completionList.Visible) return;
            string templates = TemplateUtils.GetTemplatesDir();
            if (!Directory.Exists(templates)) return;
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            int position = GetLeftDotPosition(sci);
            ASResult expr = ASComplete.GetExpressionType(sci, position);
            MemberModel target = expr != null ? expr.Member : null;
            if (target == null) return;
            AddCompletionItems(TemplateType.Member, typeof(PostfixCompletionItem), target);
            if (GetTargetIsNullable(target)) AddCompletionItems(TemplateType.Nullable, typeof (NullablePostfixCompletionItem), target);
            if (GetTargetIsCollection(target)) AddCompletionItems(TemplateType.Collection, typeof(CollectionPostfixCompletionItem), target);
        }

        internal static int GetLeftDotPosition(ScintillaControl sci)
        {
            for (int i = sci.CurrentPos - 1; i >= 0; --i)
            {
                if ((char) sci.CharAt(i) == '.') return i;
            }
            return -1;
        }

        static bool GetTargetIsNullable(MemberModel target)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    return !new List<string>(new[] { "int", "uint", "Number" }).Contains(target.Type);
                case "haxe":
                    return true;
                default:
                    return false;
            }
        }

        static bool GetTargetIsCollection(MemberModel target)
        {
            string type = target.Type;
            ContextFeatures features = ASContext.Context.Features;
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    if (type == features.arrayKey) target.Type = string.Format("{0}.<{1}>", target.Type, features.objectKey);
                    else if (type.Contains("@")) target.Type = string.Format("{0}>", type.Replace("@", ".<"));
                    return type.Contains(".<");
                default:
                    return type == features.arrayKey;
            }
        }

        void AddCompletionItems(TemplateType templateType, Type itemType, MemberModel target)
        {
            foreach (KeyValuePair<string, string> pathToTemplate in TemplateUtils.GetTemplates(templateType))
            {
                string fileName = Path.GetFileNameWithoutExtension(pathToTemplate.Key);
                ConstructorInfo constructorInfo = itemType.GetConstructor(new[] {typeof(string), typeof(string), typeof(MemberModel)});
                PostfixCompletionItem item = (PostfixCompletionItem) constructorInfo.Invoke(new object[] { fileName, pathToTemplate.Value, target });
                completionList.Items.Add(item);
                #region NOTE slavara: completionList.allItems.Add(item);
                FieldInfo member = typeof(CompletionList).GetField("allItems", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                member.FieldType.GetMethod("Add").Invoke(member.GetValue(typeof(ICollection)), new object[] { item });
                #endregion
            }
        }

        #endregion
    }

    class PostfixCompletionItem : ICompletionListItem
    {
        readonly string template;
        readonly MemberModel target;

        public PostfixCompletionItem(string label, string template, MemberModel target)
        {
            Label = label;
            this.template = template;
            this.target = target;
        }

        public string Label { get; private set; }

        public virtual string Pattern { get { return "$(Member)"; }}

        public string Value
        {
            get
            {
                ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
                int position = PluginMain.GetLeftDotPosition(sci);
                sci.SetSel(position, sci.CurrentPos);
                sci.ReplaceSel(string.Empty);
                int lineNum = sci.CurrentLine;
                int pos = sci.PositionFromLine(lineNum) + sci.GetLineIndentation(lineNum) / sci.Indent;
                sci.SetSel(pos, sci.CurrentPos);
                string snippet = template.Replace(Pattern, sci.SelText);
                snippet = ASCompletion.Completion.TemplateUtils.ToDeclarationString(target, snippet);
                snippet = ArgsProcessor.ProcessCodeStyleLineBreaks(snippet);
                sci.ReplaceSel(string.Empty);
                SnippetHelper.InsertSnippetText(sci, pos, snippet);
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
                    int lineNum = sci.CurrentLine;
                    int indent = sci.GetLineIndentation(lineNum) / sci.Indent;
                    int pos = sci.PositionFromLine(lineNum) + indent;
                    string line = sci.GetLine(lineNum);
                    line = line.Substring(indent, sci.CurrentPos - pos);
                    line = line.Substring(0, line.LastIndexOf('.'));
                    description = template.Replace(SnippetHelper.BOUNDARY, string.Empty);
                    description = description.Replace(Pattern, line);
                    description = ASCompletion.Completion.TemplateUtils.ToDeclarationString(target, description);
                    description = ArgsProcessor.ProcessCodeStyleLineBreaks(description);
                    description = description.Replace(SnippetHelper.ENTRYPOINT, "|");
                    description = description.Replace(SnippetHelper.EXITPOINT, "|");
                }
                return description;
            }
        }

        public new string ToString() { return Description; }
    }

    class NullablePostfixCompletionItem : PostfixCompletionItem
    {
        public NullablePostfixCompletionItem(string label, string template, MemberModel target) : base(label, template, target)
        {
        }

        public override string Pattern { get { return "$(Nullable)"; }}
    }

    class CollectionPostfixCompletionItem : PostfixCompletionItem
    {
        public CollectionPostfixCompletionItem(string label, string template, MemberModel target)
            : base(label, template.Replace(TemplateUtils.PATTERN_COLLECTION_ITEM_TYPE, Regex.Match(target.Type, "<([^]]+)>").Groups[1].Value), target)
        {
        }

        public override string Pattern { get { return TemplateUtils.PATTERN_COLLECTION; }}
    }
}