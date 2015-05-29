using System;
using System.ComponentModel;

namespace PostfixCodeCompletion
{
    [Serializable]
    internal class Settings
    {
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