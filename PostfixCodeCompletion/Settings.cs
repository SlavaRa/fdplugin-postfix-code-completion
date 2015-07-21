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

        [DefaultValue("")]
        [DisplayName("Custom Snippet Directories")]
        [LocalizedCategory("FlashDevelop.Category.Paths")]
        [Editor(typeof (ArrayEditor), typeof (UITypeEditor))]
        public Folder[] CustomSnippetDirectories
        {
            get { return customSnippetDirectories ?? (customSnippetDirectories = new Folder[] {}); }
            set { customSnippetDirectories = value; }
        }

        bool disableTypeDeclaration = true;

        [Category("Haxe")]
        [DisplayName("Disable type declaration for variables")]
        [DefaultValue(true)]
        public bool DisableTypeDeclaration
        {
            get { return disableTypeDeclaration; }
            set { disableTypeDeclaration = value; }
        }
    }
}