using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using FlashDebugger;
using PluginCore.Localization;

namespace PostfixCodeCompletion
{
    [Serializable]
    internal class Settings
    {
        Folder[] customSnippetDirectories;

        [LocalizedCategory("FlashDevelop.Category.Paths")]
        [DisplayName("Custom Snippet Directories"), DefaultValue("")]
        [Editor(typeof(ArrayEditor), typeof(UITypeEditor))]
        public Folder[] CustomSnippetDirectories
        {
            get { return customSnippetDirectories ?? (customSnippetDirectories = new Folder[] {}); }
            set { customSnippetDirectories = value; }
        }

        bool disableTypeDeclaration = true;

        [Category("Haxe"), DisplayName("Disable type declaration for variables"), DefaultValue(true)]
        public bool DisableTypeDeclaration
        {
            get { return disableTypeDeclaration; }
            set { disableTypeDeclaration = value; }
        }

        [Category("Advanced"), DisplayName("Features of languages")]
        [Editor(typeof(ArrayEditor), typeof(UITypeEditor))]
        public LanguageFeatures[] LanguageFeatures { get; set; } = {
            new LanguageFeatures {Language = "as3", Numeric = new[] {"int", "uint"}},
            new LanguageFeatures {Language = "haxe", Numeric = new [] {"Int", "Uint"}}
        };
    }

    [Serializable]
    [DefaultProperty(nameof(Language))]
    internal class LanguageFeatures
    {
        [DisplayName("Language name")]
        public string Language { get; set; }

        [DisplayName("Numeric types")]
        public string[] Numeric { get; set; }
    }
}