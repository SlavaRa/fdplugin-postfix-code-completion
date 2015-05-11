using System.Collections.Generic;
using System.IO;
using PluginCore;
using PluginCore.Helpers;

namespace PostfixCodeCompletion.Helpers
{
    static class TemplateUtils
    {
        public const string POSTFIX_GENERATORS = "PostfixGenerators";
        public const string PATTERN_MEMBER = "$(Member)";
        public const string PATTERN_NULLABLE = "$(Nullable)";
        public const string PATTERN_COLLECTION = "$(Collection)";
        public const string PATTERN_COLLECTION_ITEM_TYPE = "$(CollectionItemType)";

        public static string GetTemplatesDir()
        {
            string lang = PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower();
            string path = Path.Combine(PathHelper.SnippetDir, lang);
            return Path.Combine(path, POSTFIX_GENERATORS);
        }

        public static Dictionary<string, string> GetTemplates()
        {
            return GetTemplates(TemplateType.Any);
        }
        public static Dictionary<string, string> GetTemplates(TemplateType type)
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
                result.Add(file, string.Format("{0}{1}{0}", SnippetHelper.BOUNDARY, content.Replace("\r\n", "\n")));
            }
            return result;
        }
    }

    enum TemplateType
    {
        Any,
        Member,
        Nullable,
        Collection
    }
}