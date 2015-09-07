using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using FlashDevelop.Utilities;
using HaXeContext;
using PluginCore;
using PluginCore.Helpers;
using ScintillaNet;

namespace PostfixCodeCompletion.Helpers
{
    static class TemplateUtils
    {
        public const string PATTERN_BLOCK = @"\$\([^\)]*{0}.*?\)";
        public const string PATTERN_T_BLOCK = @"[^\$]*?\$\({0}\)";
        internal const string POSTFIX_GENERATORS = "PostfixGenerators";
        internal const string PATTERN_MEMBER = "Member";
        internal const string PATTERN_NULLABLE = "Nullable";
        internal const string PATTERN_COLLECTION = "Collection";
        internal const string PATTERN_COLLECTION_KEY_TYPE = "$(CollectionKeyType)";
        internal const string PATTERN_COLLECTION_ITEM_TYPE = "$(CollectionItemType)";
        internal const string PATTERN_HASH = "Hash";
        internal const string PATTERN_BOOL = "Boolean";
        internal const string PATTERN_NUMBER = "Number";
        internal const string PATTERN_STRING = "String";
        public static Settings Settings { get; set; }

        static readonly List<string> Templates = new List<string>
        {
            PATTERN_MEMBER,
            PATTERN_NULLABLE,
            PATTERN_COLLECTION,
            PATTERN_HASH,
            PATTERN_BOOL,
            PATTERN_NUMBER,
            PATTERN_STRING
        };

        internal static bool GetHasTemplates()
        {
            return GetHasTemplates(PathHelper.SnippetDir)
                || Settings.CustomSnippetDirectories.Any(it => GetHasTemplates(it.Path));
        }

        static bool GetHasTemplates(string path)
        {
            path = GetTemplatesDir(path);
            return Directory.Exists(path) && Directory.GetFiles(path, "*.fds").Length > 0;
        }

        static string GetTemplatesDir(string path)
        {
            path = Path.Combine(path, PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower());
            path = Path.Combine(path, POSTFIX_GENERATORS);
            return path;
        }

        internal static Dictionary<string, string> GetTemplates(string type)
        {
            string pattern = Templates.Contains(type) ? string.Format(PATTERN_BLOCK, type) : string.Format(PATTERN_T_BLOCK, type);
            Dictionary<string, string> result = new Dictionary<string, string>();
            List<string> paths = Settings.CustomSnippetDirectories.Select(it => GetTemplatesDir(it.Path)).ToList();
            paths.Add(GetTemplatesDir(PathHelper.SnippetDir));
            paths.RemoveAll(s => !Directory.Exists(s));
            foreach (string path in paths)
            {
                foreach (string file in Directory.GetFiles(path, "*.fds"))
                {
                    string content = GetFileContent(file);
                    string marker = "#pcc:" + type;
                    int startIndex = content.IndexOf(marker, StringComparison.Ordinal);
                    if (startIndex != -1)
                    {
                        startIndex += marker.Length;
                        content = content.Remove(0, startIndex);
                    }
                    startIndex = content.IndexOf("#pcc:", StringComparison.Ordinal);
                    if (startIndex != -1) content = content.Remove(startIndex);
                    if (!Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)) continue;
                    result.Add(file, content.Replace("\r\n", "\n"));
                }
            }
            return result;
        }

        static string GetFileContent(string file)
        {
            string content;
            using (StreamReader reader = new StreamReader(File.OpenRead(file)))
            {
                content = reader.ReadToEnd();
                reader.Close();
            }
            return content;
        }

        internal static KeyValuePair<string, string> GetVarNameToQualifiedName(ASResult expr)
        {
            string type = null;
            string varname = string.Empty;
            string word = string.Empty;
            MemberModel member = expr.Member;
            if (member != null && member.Type != null) type = member.Type;
            else
            {
                ClassModel cType = expr.Type;
                if (cType != null && cType.Name != null) type = cType.QualifiedName;
            }
            if (member != null && member.Name != null) varname = Reflector.ASGeneratorGuessVarName(member.Name, type);
            if (!string.IsNullOrEmpty(word) && char.IsDigit(word[0])) word = null;
            if (!string.IsNullOrEmpty(word) && (string.IsNullOrEmpty(type) || Regex.IsMatch(type, "(<[^]]+>)"))) word = null;
            if (!string.IsNullOrEmpty(type) && type == ASContext.Context.Features.voidKey) type = null;
            if (string.IsNullOrEmpty(varname)) varname = Reflector.ASGeneratorGuessVarName(word, type);
            if (!string.IsNullOrEmpty(varname) && varname == word && varname.Length == 1) varname = varname + "1";
            return new KeyValuePair<string, string>(varname, type);
        }

        internal static string ProcessMemberTemplate(string template, ASResult expr)
        {
            var varNameToQualifiedName = GetVarNameToQualifiedName(expr);
            string name = varNameToQualifiedName.Key.ToLower();
            string type = varNameToQualifiedName.Value;
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Name", name);
            if (ASContext.Context is Context && Settings != null && Settings.DisableTypeDeclaration) type = null;
            if (!string.IsNullOrEmpty(type)) type = MemberModel.FormatType(Reflector.ASGeneratorGetShortType(type));
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Type", type);
            return template;
        }

        internal static string ProcessCollectionTemplate(string template, ASResult expr)
        {
            string type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
            if (type.Contains("@")) type = type.Replace("@", ".<") + ">";
            type = Regex.Match(type, "<([^]]+)>").Groups[1].Value;
            type = Reflector.ASGeneratorGetShortType(type);
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    if (string.IsNullOrEmpty(type)) type = "*";
                    template = template.Replace(PATTERN_COLLECTION_KEY_TYPE, "int");
                    break;
            }
            return template.Replace(PATTERN_COLLECTION_ITEM_TYPE, type);
        }

        internal static string ProcessHashTemplate(string template, ASResult expr)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    string type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
                    type = Reflector.ASGeneratorGetShortType(type);
                    string objectKey = ASContext.Context.Features.objectKey;
                    if (type == objectKey || type == "Dictionary")
                    {
                        template = template.Replace(PATTERN_COLLECTION_KEY_TYPE, type == objectKey ? "String" : objectKey);
                        template = template.Replace(PATTERN_COLLECTION_ITEM_TYPE, "*");
                    }
                    break;
            }
            return template;
        }

        internal static string GetDescription(ASResult expr, string template, string pccpattern)
        {
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            int position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
            int exprStartPosition = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
            int lineNum = sci.CurrentLine;
            string line = sci.GetLine(lineNum);
            string snippet = line.Substring(exprStartPosition - sci.PositionFromLine(lineNum), position - exprStartPosition);
            string result = template.Replace(SnippetHelper.BOUNDARY, string.Empty);
            result = Regex.Replace(result, string.Format(PATTERN_BLOCK, pccpattern), snippet, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            result = ProcessMemberTemplate(result, expr);
            result = ArgsProcessor.ProcessCodeStyleLineBreaks(result);
            result = result.Replace(SnippetHelper.ENTRYPOINT, "|");
            result = result.Replace(SnippetHelper.EXITPOINT, "|");
            return result;
        }

        internal static void InsertSnippetText(ASResult expr, string template, string pccpattern)
        {
            ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            int position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
            sci.SetSel(position, sci.CurrentPos);
            sci.ReplaceSel(string.Empty);
            position = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
            sci.SetSel(position, sci.CurrentPos);
            string snippet = Regex.Replace(template, string.Format(PATTERN_BLOCK, pccpattern), sci.SelText, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            snippet = ProcessMemberTemplate(snippet, expr);
            snippet = ArgsProcessor.ProcessCodeStyleLineBreaks(snippet);
            sci.ReplaceSel(string.Empty);
            SnippetHelper.InsertSnippetText(sci, position, snippet);
        }
    }
}