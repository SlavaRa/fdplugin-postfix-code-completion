using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ASCompletion.Completion;
using ASCompletion.Context;
using PluginCore;
using PluginCore.Helpers;

namespace PostfixCodeCompletion.Helpers
{
    static class TemplateUtils
    {
        internal const string POSTFIX_GENERATORS = "PostfixGenerators";
        internal const string PATTERN_MEMBER = "$(Member)";
        internal const string PATTERN_NULLABLE = "$(Nullable)";
        internal const string PATTERN_COLLECTION = "$(Collection)";
        internal const string PATTERN_COLLECTION_KEY_TYPE = "$(CollectionKeyType)";
        internal const string PATTERN_COLLECTION_ITEM_TYPE = "$(CollectionItemType)";
        internal const string PATTERN_COLLECTION_OR_HASH = "$(CollectionOrHash)";
        internal const string PATTERN_BOOLEAN = "$(Boolean)";

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
            string path = GetTemplatesDir();
            foreach (string file in Directory.GetFiles(path, "*.fds"))
            {
                string content;
                using (StreamReader reader = new StreamReader(File.OpenRead(file)))
                {
                    content = reader.ReadToEnd();
                    reader.Close();
                }
                if (type == TemplateType.Member && !content.Contains(PATTERN_MEMBER)) continue;
                if (type == TemplateType.Nullable && !content.Contains(PATTERN_NULLABLE)) continue;
                if (type == TemplateType.Collection && !content.Contains(PATTERN_COLLECTION)) continue;
                if (type == TemplateType.CollectionOrHash && !content.Contains(PATTERN_COLLECTION_OR_HASH)) continue;
                if (type == TemplateType.Boolean && !content.Contains(PATTERN_BOOLEAN)) continue;
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
            type = GetShortType(type);
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
            type = GetShortType(type);
            if (string.IsNullOrEmpty(type)) type = "*";
            template = template.Replace(PATTERN_COLLECTION_KEY_TYPE, "int");
            template = template.Replace(PATTERN_COLLECTION_ITEM_TYPE, type);
            return template;
        }

        internal static string ProcessCollectionOrHashTemplate(string template, ASResult expr)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage)
            {
                case "as2":
                case "as3":
                    string type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
                    type = GetShortType(type);
                    string objectKey = ASContext.Context.Features.objectKey;
                    if (type == objectKey || type == "Dictionary")
                    {
                        template = template.Replace(PATTERN_COLLECTION_KEY_TYPE, type == objectKey ? "String" : objectKey);
                        template = template.Replace(PATTERN_COLLECTION_ITEM_TYPE, "*");
                    }
                    else template = ProcessCollectionTemplate(template, expr);
                    break;
            }
            return template;
        }

        static string GetShortType(string type)
        {
            #region return ASGenerator.GetShortType(sci, type)
            MethodInfo methodInfo = typeof(ASGenerator).GetMethod("GetShortType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string) methodInfo.Invoke(null, new object[] { type });
            #endregion
        }
    }

    enum TemplateType
    {
        Any,
        Member,
        Nullable,
        Collection,
        CollectionOrHash,
        Boolean
    }
}