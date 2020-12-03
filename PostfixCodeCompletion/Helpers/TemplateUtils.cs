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

namespace PostfixCodeCompletion.Helpers
{
    internal static class TemplateUtils
    {
        public const string PatternBlock = @"\$\([^\)]*{0}.*?\)";
        public const string PatternTBlock = @"[^\$]*?\$\({0}\)";
        internal const string PostfixGenerators = "PostfixGenerators";
        internal const string PatternMember = "PCCMember";
        internal const string PatternNullable = "PCCNullable";
        internal const string PatternCollection = "PCCCollection";
        internal const string PatternCollectionKeyType = "$(CollectionKeyType)";
        internal const string PatternCollectionItemType = "$(CollectionItemType)";
        internal const string PatternHash = "PCCHash";
        internal const string PatternBool = "PCCBoolean";
        internal const string PatternNumber = "PCCNumber";
        internal const string PatternString = "PCCString";
        internal const string PatternType = "PCCType";
        public static Settings Settings { get; set; }

        static readonly List<string> Templates = new List<string>
        {
            PatternMember,
            PatternNullable,
            PatternCollection,
            PatternHash,
            PatternBool,
            PatternNumber,
            PatternString,
            PatternType
        };
        
        internal static bool GetHasTemplates()
            => PluginBase.MainForm.CurrentDocument?.SciControl is { } sci
               && GetHasTemplates(sci.ConfigurationLanguage.ToLower());

        internal static bool GetHasTemplates(string language)
            => GetHasTemplates(PathHelper.SnippetDir, language)
               || Settings.CustomSnippetDirectories.Any(it => GetHasTemplates(it.Path, language));

        static bool GetHasTemplates(string snippetPath, string language)
        {
            snippetPath = GetTemplatesDir(snippetPath, language);
            return Directory.Exists(snippetPath) && Directory.EnumerateFiles(snippetPath, "*.fds").Any();
        }

        static string GetTemplatesDir(string snippetPath)
        {
            return PluginBase.MainForm.CurrentDocument?.SciControl is { } sci
                ? GetTemplatesDir(snippetPath, sci.ConfigurationLanguage.ToLower())
                : null;
        }

        static string GetTemplatesDir(string snippetPath, string language) => Path.Combine(snippetPath, language, PostfixGenerators);

        internal static Dictionary<string, string> GetTemplates(string type)
        {
            var result = new Dictionary<string, string>();
            var paths = Settings.CustomSnippetDirectories.Select(it => GetTemplatesDir(it.Path)).ToList();
            paths.Add(GetTemplatesDir(PathHelper.SnippetDir));
            paths.RemoveAll(s => !Directory.Exists(s));
            if (paths.Count == 0) return result;
            var pattern = Templates.Contains(type)
                ? string.Format(PatternBlock, type)
                : string.Format(PatternTBlock, type);
            foreach (var path in paths)
            {
                foreach (var file in Directory.GetFiles(path, "*.fds"))
                {
                    var snippet = GetSnippet(file);
                    snippet = GetTemplate(snippet, type);
                    if (!Regex.IsMatch(snippet, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)) continue;
                    result.Add(file, snippet.Replace("\r\n", "\n"));
                }
            }
            return result;
        }

        internal static string GetTemplate(string snippet, string[] types)
        {
            var result = string.Empty;
            foreach (var type in types)
            {
                var template = GetTemplate(snippet, type);
                if (!string.IsNullOrEmpty(template) && template != result) result = template;
            }
            return result;
        }

        internal static string GetTemplate(string snippet, string type)
        {
            var marker = $"#pcc:{type}";
            var startIndex = snippet.IndexOfOrdinal(marker);
            if (startIndex != -1)
            {
                startIndex += marker.Length;
                snippet = snippet.Remove(0, startIndex);
            }
            startIndex = snippet.IndexOfOrdinal("#pcc:");
            if (startIndex != -1) return snippet.Remove(startIndex);
            if (snippet.Contains(type)) return snippet;
            return string.Empty;
        }

        static string GetSnippet(string file)
        {
            using var reader = new StreamReader(File.OpenRead(file));
            var content = reader.ReadToEnd();
            reader.Close();
            return content;
        }

        internal static KeyValuePair<string, string> GetVarNameToQualifiedName(ASResult expr)
        {
            string type = null;
            var member = expr.Member;
            if (member?.Type != null) type = member.Type;
            else
            {
                var cType = expr.Type;
                if (cType?.Name != null) type = cType.QualifiedName;
            }
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var lineNum = sci.CurrentLine;
            var word = Reflector.ASGenerator.GetStatementReturnType(sci, sci.GetLine(lineNum), sci.PositionFromLine(lineNum))?.Word;
            var value = string.Empty;
            if (member?.Name != null) value = Reflector.ASGenerator.GuessVarName(member.Name, type);
            if (!string.IsNullOrEmpty(word) && char.IsDigit(word[0])) word = null;
            if (!string.IsNullOrEmpty(word) && (string.IsNullOrEmpty(type) || Regex.IsMatch(type, "(<[^]]+>)"))) word = null;
            if (!string.IsNullOrEmpty(type) && type == ASContext.Context.Features.voidKey) type = null;
            if (string.IsNullOrEmpty(value)) value = Reflector.ASGenerator.GuessVarName(word, type);
            if (!string.IsNullOrEmpty(value) && value == word) value = $"{value}1";
            return new KeyValuePair<string, string>(value, type);
        }

        internal static string ProcessTemplate(string pattern, string template, ASResult expr)
        {
            return pattern switch
            {
                PatternCollection => ProcessCollectionTemplate(template, expr),
                PatternHash => ProcessHashTemplate(template, expr),
                _ => template
            };
        }

        internal static string ProcessMemberTemplate(string template, ASResult expr)
        {
            var varNameToQualifiedName = GetVarNameToQualifiedName(expr);
            var name = varNameToQualifiedName.Key;
            var type = varNameToQualifiedName.Value;
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Name", name);
            if (ASContext.Context is Context && Settings != null && Settings.DisableTypeDeclaration) type = null;
            if (!string.IsNullOrEmpty(type)) type = MemberModel.FormatType(Reflector.ASGenerator.GetShortType(type));
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Type", type);
            return template;
        }

        internal static string ProcessCollectionTemplate(string template, ASResult expr)
        {
            var type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
            if (type.Contains("@")) type = $"{type.Replace("@", ".<")}>";
            type = Regex.Match(type, "<([^]]+)>").Groups[1].Value;
            type = Reflector.ASGenerator.GetShortType(type);
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower())
            {
                case "as2":
                case "as3":
                    if (string.IsNullOrEmpty(type)) type = ASContext.Context.Features.dynamicKey;
                    template = template.Replace(PatternCollectionKeyType, "int");
                    break;
            }
            return template.Replace(PatternCollectionItemType, type);
        }

        internal static string ProcessHashTemplate(string template, ASResult expr)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower())
            {
                case "as2":
                case "as3":
                    var type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
                    type = Reflector.ASGenerator.GetShortType(type);
                    var features = ASContext.Context.Features;
                    var objectKey = features.objectKey;
                    if (type == objectKey || type == "Dictionary")
                    {
                        template = template.Replace(PatternCollectionKeyType, type == objectKey ? features.stringKey : objectKey);
                        template = template.Replace(PatternCollectionItemType, features.dynamicKey);
                    }
                    break;
            }
            return template;
        }

        internal static string GetDescription(ASResult expr, string template, string pccpattern)
        {
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
            var exprStartPosition = ASGenerator.GetStartOfStatement(expr);
            var lineNum = sci.CurrentLine;
            var line = sci.GetLine(lineNum);
            var snippet = line.Substring(exprStartPosition - sci.PositionFromLine(lineNum), position - exprStartPosition);
            var result = template.Replace(SnippetHelper.BOUNDARY, string.Empty);
            result = Regex.Replace(result, string.Format(PatternBlock, pccpattern), snippet, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            result = ProcessMemberTemplate(result, expr);
            result = ArgsProcessor.ProcessCodeStyleLineBreaks(result);
            result = result.Replace(SnippetHelper.ENTRYPOINT, "|");
            result = result.Replace(SnippetHelper.EXITPOINT, "|");
            return result;
        }

        internal static void InsertSnippetText(ASResult expr, string template, string pccpattern)
        {
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
            sci.SetSel(position, sci.CurrentPos);
            sci.ReplaceSel(string.Empty);
            position = ASGenerator.GetStartOfStatement(expr);
            sci.SetSel(position, sci.CurrentPos);
            var snippet = Regex.Replace(template, string.Format(PatternBlock, pccpattern), sci.SelText, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            snippet = ProcessMemberTemplate(snippet, expr);
            snippet = ArgsProcessor.ProcessCodeStyleLineBreaks(snippet);
            sci.ReplaceSel(string.Empty);
            SnippetHelper.InsertSnippetText(sci, position, snippet);
        }
    }
}