using FlashDevelop;
using NSubstitute;
using NUnit.Framework;
using PluginCore;
using ScintillaNet;
using ScintillaNet.Enums;

namespace PostfixCodeCompletion
{
    [TestFixture]
    public class TestBase
    {
        protected MainForm MainForm;
        protected ISettings Settings;
        protected ITabbedDocument CurrentDocument;
        protected ScintillaControl Sci;

        [TestFixtureSetUp]
        public void FixtureSetUp()
        {
            MainForm = new MainForm();
            Settings = Substitute.For<ISettings>();
            Settings.UseTabs = true;
            Settings.IndentSize = 4;
            Settings.SmartIndentType = SmartIndent.CPP;
            Settings.TabIndents = true;
            Settings.TabWidth = 4;
            CurrentDocument = Substitute.For<ITabbedDocument>();
            MainForm.Settings = Settings;
            MainForm.CurrentDocument = CurrentDocument;
            MainForm.StandaloneMode = false;
            PluginBase.Initialize(MainForm);
            FlashDevelop.Managers.ScintillaManager.LoadConfiguration();
            Sci = GetBaseScintillaControl();
        }

        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            Settings = null;
            CurrentDocument = null;
            MainForm.Dispose();
            MainForm = null;
        }

        protected ScintillaControl GetBaseScintillaControl() => new ScintillaControl
        {
            Encoding = System.Text.Encoding.UTF8,
            CodePage = 65001,
            Indent = Settings.IndentSize,
            Lexer = 3,
            StyleBits = 7,
            IsTabIndents = Settings.TabIndents,
            IsUseTabs = Settings.UseTabs,
            TabWidth = Settings.TabWidth
        };
    }
}