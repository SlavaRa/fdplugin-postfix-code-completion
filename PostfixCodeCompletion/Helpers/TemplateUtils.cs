﻿using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ASCompletion.Completion;
using ASCompletion.Context;
using PluginCore;
using PluginCore.Helpers;

namespace PostfixCodeCompletion.Helpers
{
    static class TemplateUtils
    {
        public const string PATTERN_BLOCK = @"\$\([^\)]*{0}.*?\)";
        internal const string POSTFIX_GENERATORS = "PostfixGenerators";
        internal const string PATTERN_MEMBER = "Member";
        internal const string PATTERN_NULLABLE = "Nullable";
        internal const string PATTERN_COLLECTION = "Collection";
        internal const string PATTERN_COLLECTION_KEY_TYPE = "$(CollectionKeyType)";
        internal const string PATTERN_COLLECTION_ITEM_TYPE = "$(CollectionItemType)";
        internal const string PATTERN_COLLECTION_OR_HASH = "Hash";
        internal const string PATTERN_BOOLEAN = "Boolean";
        internal const string PATTERN_NUMBER = "Number";
        internal const string PATTERN_STRING = "String";

        private static readonly Dictionary<TemplateType, string> TemplateTypeToPattern = new Dictionary<TemplateType, string>()
        {
            {TemplateType.Member, PATTERN_MEMBER},
            {TemplateType.Nullable, PATTERN_NULLABLE},
            {TemplateType.Collection, PATTERN_COLLECTION},
            {TemplateType.Hash, PATTERN_COLLECTION_OR_HASH},
            {TemplateType.Boolean, PATTERN_BOOLEAN},
            {TemplateType.Number, PATTERN_NUMBER},
            {TemplateType.String, PATTERN_STRING}
        };

        internal static string GetTemplatesDir()
        {
            string lang = PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower();
            string path = Path.Combine(PathHelper.SnippetDir, lang);
            return Path.Combine(path, POSTFIX_GENERATORS);
        }

        internal static Dictionary<string, string> GetTemplates()
        {
            return GetTemplates(TemplateType.Any);
        }
        internal static Dictionary<string, string> GetTemplates(TemplateType type)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (string file in Directory.GetFiles(GetTemplatesDir(), "*.fds"))
            {
                string content;
                using (StreamReader reader = new StreamReader(File.OpenRead(file)))
                {
                    content = reader.ReadToEnd();
                    reader.Close();
                }
                if (type == TemplateType.Any
                    || !TemplateTypeToPattern.ContainsKey(type)
                    || !Regex.IsMatch(content, string.Format(PATTERN_BLOCK, TemplateTypeToPattern[type]), RegexOptions.IgnoreCase | RegexOptions.Multiline)) continue;
                result.Add(file, string.Format("{0}{1}{0}", SnippetHelper.BOUNDARY, content.Replace("\r\n", "\n")));
            }
            return result;
        }

        internal static string ProcessMemberTemplate(string template, ASResult expr)
        {
            string type;
            string name;
            if (expr.Member != null)
            {
                type = expr.Member.Type;
                name = expr.Member.Name;
            }
            else
            {
                type = expr.Type.QualifiedName;
                name = expr.Type.Name;
            }
            if (type.Contains("@")) type = type.Substring(0, type.IndexOf("@"));
            type = Reflector.ASGeneratorGetShortType(type);
            if (string.IsNullOrEmpty(name)) name = type;
            name = name.ToLower();
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Name", name);
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Type", type);
            return template;
        }

        internal static string ProcessCollectionTemplate(string template, ASResult expr)
        {
            string type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
            if (type.Contains("@")) type = string.Format("{0}>", type.Replace("@", ".<"));
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
    }

    enum TemplateType
    {
        Any,
        Member,
        Nullable,
        Collection,
        Hash,
        Boolean,
        Number,
        String
    }
}