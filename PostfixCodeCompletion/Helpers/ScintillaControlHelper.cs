using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using PluginCore;
using ScintillaNet;

namespace PostfixCodeCompletion.Helpers
{
    internal static class ScintillaControlHelper
    {
        internal static int GetWordLeftStartPosition(ScintillaControl sci, int position)
        {
            var skipWhiteSpace = true;
            var characters = ScintillaControl.Configuration.GetLanguage(sci.ConfigurationLanguage).characterclass.Characters;
            while (position >= 0)
            {
                var c = (char) sci.CharAt(position);
                if (c <= ' ')
                {
                    if (!skipWhiteSpace) return position + 1;
                }
                else if (!characters.Contains(c)) return position + 1;
                else skipWhiteSpace = false;
                position--;
            }
            return position;
        }

        internal static int GetDotLeftStartPosition(ScintillaControl sci, int position)
        {
            for (var i = sci.CurrentPos; i > 0; i--)
            {
                if ((char)sci.CharAt(i) != '.') continue;
                position = i;
                break;
            }
            return position;
        }
    }

    internal class CompleteHelper
    {
        internal static Settings Settings { private get; set; }

        internal static ASResult GetCurrentExpressionType()
        {
            var doc = PluginBase.MainForm.CurrentDocument;
            if (doc == null || !ASContext.Context.IsFileValid) return null;
            var sci = doc.SciControl;
            var language = sci.ConfigurationLanguage;
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
            return Reflector.ASGenerator.GetStatementReturnType(sci, line, positionFromLine).Resolve;
        }

        internal static MemberModel GetCompletionTarget(ASResult expr)
        {
            if (expr == null || expr.IsNull()) return null;
            var member = expr.Member;
            var voidKey = ASContext.Context.Features.voidKey;
            if (!string.IsNullOrEmpty(member?.Type) && member.Type != voidKey) return member;
            var type = expr.Type;
            if (type != null && !type.IsVoid() && !string.IsNullOrEmpty(type.Type) && type.Type != voidKey)
                return type;
            return null;
        }

        internal static IEnumerable<ICompletionListItem> GetCompletionItems(string pattern, MemberModel target, ASResult expr)
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

        internal static IEnumerable<ICompletionListItem> GetCompletionItems(string pattern, ASResult expr) => GetCompletionItems(TemplateUtils.GetTemplates(pattern), pattern, expr);

        internal static IEnumerable<ICompletionListItem> GetCompletionItems(Dictionary<string, string> templates, string pattern, ASResult expr)
        {
            var result = new List<ICompletionListItem>();
            if (templates.Count == 0) return result;
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            Bitmap itemIcon = null;
            var haxeStringCode = false;
            var isHaxe = sci.ConfigurationLanguage.ToLower() == "haxe";
            if (isHaxe)
            {
                var target = GetCompletionTarget(expr);
                if (target?.Type == ASContext.Context.Features.stringKey)
                {
                    var pos = ASGenerator.GetStartOfStatement(sci, sci.CurrentPos, expr);
                    haxeStringCode = sci.CharAt(pos) == '"' && sci.CharAt(pos + 1) != '\\' && sci.CharAt(pos + 2) == '"';
                    if (haxeStringCode) itemIcon = (Bitmap) ASContext.Panel.GetIcon(PluginUI.ICON_PROPERTY);
                }
            }
            foreach (var pathToTemplate in templates)
            {
                var fileName = Path.GetFileNameWithoutExtension(pathToTemplate.Key);
                if (isHaxe && fileName == "code" && !haxeStringCode) continue;
                var template = TemplateUtils.ProcessTemplate(pattern, pathToTemplate.Value, expr);
                var item = new PostfixCompletionItem(fileName, template, expr) {Pattern = pattern};
                if (isHaxe && fileName == "code" && itemIcon != null)
                {
                    item.Icon = itemIcon;
                    itemIcon = null;
                }
                result.Add(item);
            }
            return result;
        }

        internal static List<ICompletionListItem> GetCompletionItems(MemberModel target, ASResult expr)
        {
            var result = new List<ICompletionListItem>();
            if (expr.Member != null) result.AddRange(GetCompletionItems(expr.Member.Type, target, expr));
            else if (expr.Type != null) result.AddRange(GetCompletionItems(expr.Type.Type, target, expr));
            result.AddRange(GetCompletionItems(TemplateUtils.PatternMember, expr));
            if (IsNullable(target)) result.AddRange(GetCompletionItems(TemplateUtils.PatternNullable, expr));
            if (IsCollection(target)) result.AddRange(GetCompletionItems(TemplateUtils.PatternCollection, expr));
            if (IsHash(target)) result.AddRange(GetCompletionItems(TemplateUtils.PatternHash, expr));
            if (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower() == "haxe")
            {
                var type = !string.IsNullOrEmpty(expr.Type?.Type) && expr.Type.Type != ASContext.Context.Features.voidKey ? expr.Type : null;
                if (type != null)
                {
                    if (IsCollection(type)) result.AddRange(GetCompletionItems(TemplateUtils.PatternCollection, expr));
                    if (IsHash(type)) result.AddRange(GetCompletionItems(TemplateUtils.PatternHash, expr));
                }
            }
            if (IsBoolean(target)) result.AddRange(GetCompletionItems(TemplateUtils.PatternBool, expr));
            if (IsNumber(target)) result.AddRange(GetCompletionItems(TemplateUtils.PatternNumber, expr));
            if (IsString(target)) result.AddRange(GetCompletionItems(TemplateUtils.PatternString, expr));
            if (IsType(target)) result.AddRange(GetCompletionItems(TemplateUtils.PatternType, expr));
            return result.Distinct().ToList();
        }

        internal static bool IsNullable(MemberModel target) => !IsNumber(target) && !IsBoolean(target);

        internal static bool IsBoolean(MemberModel target) => target.Type == ASContext.Context.Features.booleanKey;

        internal static bool IsString(MemberModel target) => target.Type == ASContext.Context.Features.stringKey;

        internal static bool IsType(MemberModel target) => target.Flags == FlagType.Class;

        internal static bool IsCollection(MemberModel target)
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

        internal static bool IsHash(MemberModel target)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower())
            {
                case "as2":
                case "as3":
                    var type = target.Type;
                    return type == ASContext.Context.Features.objectKey || type == "Dictionary";
                case "haxe":
                    Func<MemberModel, bool> isIterator = member =>
                    {
                        var cleanType = Reflector.ASGenerator.CleanType(member.Type);
                        return cleanType == "Iterator" || cleanType == "Iterable";
                    };
                    if (isIterator(target)) return true;
                    if (target is ClassModel)
                    {
                        var classModel = target as ClassModel;
                        while (classModel != null && !classModel.IsVoid())
                        {
                            if (classModel.Members.Items.Any(isIterator)) return true;
                            classModel.ResolveExtends();
                            classModel = classModel.Extends;
                        }
                    }
                    return false;
                default: return false;
            }
        }

        internal static bool IsNumber(MemberModel target)
        {
            var type = target is ClassModel ? ((ClassModel)target).QualifiedName : target.Type;
            if (type == ASContext.Context.Features.numberKey) return true;
            var language = PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower();
            var features = Settings.LanguageFeatures.First(it => it.Language == language);
            return features != null && features.Numeric.Contains(type);
        }
    }

    internal class PostfixCompletionItem : ICompletionListItem
    {
        readonly string template;
        readonly ASResult expr;

        public PostfixCompletionItem(string label, string template, ASResult expr)
        {
            Label = label;
            this.template = template;
            this.expr = expr;
        }

        public string Label { get; }

        string pattern;
        public virtual string Pattern
        {
            get { return pattern ?? TemplateUtils.PatternMember; }
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
            get { return icon ?? (icon = (Bitmap)PluginBase.MainForm.FindImage("341")); }
            set { icon = value; }
        }

        string description;
        public string Description => description ?? (description = TemplateUtils.GetDescription(expr, template, Pattern));

        public new string ToString() => Description;

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
        public override int GetHashCode() => Label.GetHashCode() ^ expr.GetHashCode();
    }
}