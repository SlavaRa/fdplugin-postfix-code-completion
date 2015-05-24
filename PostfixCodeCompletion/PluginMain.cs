﻿using System;
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
                case EventType.Keys:
                    Keys keys = ((KeyEvent) e).Value;
                    if (keys == (Keys.Control | Keys.Space))
                    {
                        e.Handled = ASComplete.OnShortcut(keys, PluginBase.MainForm.CurrentDocument.SciControl);
                        if (!CompletionList.Active)
                        {
                            completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                            UpdateCompletionList();
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
            else Settings = (Settings)ObjectSerializer.Deserialize(settingFilename, Settings);
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.UIStarted);
            EventManager.AddEventHandler(this, EventType.Keys, HandlingPriority.High);
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        void SaveSettings()
        {
            ObjectSerializer.Serialize(settingFilename, Settings);
        }

        void UpdateCompletionList()
        {
            string templates = TemplateUtils.GetTemplatesDir();
            if (!Directory.Exists(templates)) return;
            ASResult expr = GetPostfixCompletionExpr();
            MemberModel target = GetPostfixCompletionTarget(expr);
            if (target == null) return;
            List<ICompletionListItem> items = new List<ICompletionListItem>();
            items.AddRange(GetCompletionItems(TemplateType.Member, typeof(PostfixCompletionItem), expr));
            if (GetTargetIsNullable(target)) items.AddRange(GetCompletionItems(TemplateType.Nullable, typeof(NullablePostfixCompletionItem), expr));
            if (GetTargetIsCollection(target)) items.AddRange(GetCompletionItems(TemplateType.Collection, typeof(CollectionPostfixCompletionItem), expr));
            if (GetTargetIsHash(target)) items.AddRange(GetCompletionItems(TemplateType.Hash, typeof(HashPostfixCompletionItem), expr));
            if (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage == "haxe" && expr.Type != null)
            {
                ClassModel type = expr.Type;
                if (!string.IsNullOrEmpty(type.Type) && type.Type != ASContext.Context.Features.voidKey)
                {
                    if (GetTargetIsCollection(type)) items.AddRange(GetCompletionItems(TemplateType.Collection, typeof(CollectionPostfixCompletionItem), expr));
                    if (GetTargetIsHash(type)) items.AddRange(GetCompletionItems(TemplateType.Hash, typeof(HashPostfixCompletionItem), expr));
                }
            }
            if (GetTargetIsBoolean(target)) items.AddRange(GetCompletionItems(TemplateType.Boolean, typeof(BooleanPostfixCompletionItem), expr));
            if (GetTargetIsNumber(target)) items.AddRange(GetCompletionItems(TemplateType.Number, typeof(NumberPostfixCompletionItem), expr));
            if (GetTargetIsString(target)) items.AddRange(GetCompletionItems(TemplateType.String, typeof(StringPostfixCompletionItem), expr));
            if (completionList.Visible)
            {
                completionList.Items.AddRange(items.ToArray());
                Reflector.CompletionListAllItems().AddRange(items);
                completionList.Height = (Math.Min(completionList.Items.Count, 10) + 1)*completionList.ItemHeight;
            }
            else CompletionList.Show(items, false);
        }

        static ASResult GetPostfixCompletionExpr()
        {
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            int currentLine = Reflector.ScintillaControlCurrentLine;
            int positionFromLine = sci.PositionFromLine(currentLine);
            int position = -1;
            for (int i = sci.CurrentPos; i > positionFromLine; i--)
            {
                char c = (char)sci.CharAt(i);
                if (c != '.') continue;
                position = i;
                break;
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
            if (member != null && !string.IsNullOrEmpty(member.Type) && member.Type != ASContext.Context.Features.voidKey)
                return member;
            ClassModel type = expr.Type;
            if (type != null && !type.IsVoid() && !string.IsNullOrEmpty(type.Type) && type.Type != ASContext.Context.Features.voidKey)
                return type;
            return null;
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
                    return type == Reflector.ASGeneratorCleanType(arrayKey)
                        || type.Contains("Vector<") && Reflector.ASGeneratorCleanType(type) == Reflector.ASGeneratorCleanType("Vector<T>");
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
                    Func<MemberModel, bool> isIteratorOrIterable = (m) =>
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

        static IEnumerable<ICompletionListItem> GetCompletionItems(TemplateType templateType, Type itemType, ASResult expr)
        {
            List<ICompletionListItem> result = new List<ICompletionListItem>();
            foreach (KeyValuePair<string, string> pathToTemplate in TemplateUtils.GetTemplates(templateType))
            {
                string fileName = Path.GetFileNameWithoutExtension(pathToTemplate.Key);
                ConstructorInfo constructorInfo = itemType.GetConstructor(new[] {typeof (string), typeof (string), typeof (ASResult)});
                result.Add((PostfixCompletionItem) constructorInfo.Invoke(new object[] {fileName, pathToTemplate.Value, expr}));
            }
            return result;
        }

        #endregion

        #region Event Handlers

        void OnCompletionListVisibleChanged(object o, EventArgs args)
        {
            if (completionList.Visible) UpdateCompletionList();
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
                position = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
                sci.SetSel(position, sci.CurrentPos);
                string snippet = Regex.Replace(template, string.Format(TemplateUtils.PATTERN_BLOCK, Pattern), sci.SelText, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                snippet = TemplateUtils.ProcessMemberTemplate(snippet, expr);
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
                    int exprStartPosition = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
                    int lineNum = Reflector.ScintillaControlCurrentLine;
                    string line = sci.GetLine(lineNum);
                    string snippet = line.Substring(exprStartPosition - sci.PositionFromLine(lineNum), dotPosition - exprStartPosition);
                    description = template.Replace(SnippetHelper.BOUNDARY, string.Empty);
                    description = Regex.Replace(description, string.Format(TemplateUtils.PATTERN_BLOCK, Pattern), snippet, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    description = TemplateUtils.ProcessMemberTemplate(description, expr);
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
}