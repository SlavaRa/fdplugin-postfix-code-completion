using ASCompletion.Completion;
using ASCompletion.Model;
using NUnit.Framework;
using PluginCore.Helpers;

namespace PostfixCodeCompletion.Helpers
{
    [TestFixture]
    public class ScintillaControlHelperTests : TestBase
    {
        [TestFixtureSetUp]
        public void AS3GeneratorTestsSetup() => Sci.ConfigurationLanguage = "as3";

        [Test]
        public void GetExpressionStartPosition()
        {
            Sci.Text = " new Sprite().numChildren.$(EntryPoint)";
            SnippetHelper.PostProcessSnippets(Sci, 0);
            var position = ScintillaControlHelper.GetExpressionStartPosition(Sci, Sci.CurrentPos, new ASResult {Member = new MemberModel()});
            Assert.AreEqual(1, position);
        }
    }
}