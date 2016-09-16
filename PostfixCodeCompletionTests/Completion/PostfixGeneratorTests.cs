using System.Collections.Generic;
using System.Reflection;
using AS3Context;
using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using ASCompletion.Settings;
using NSubstitute;
using NUnit.Framework;
using PluginCore.Helpers;
using PostfixCodeCompletion.TestUtils;

namespace PostfixCodeCompletion.Completion
{
    [TestFixture]
    internal class PostfixGeneratorTests : TestBase
    {
        [TestFixture]
        public class GeneratorJob : PostfixGeneratorTests
        {
            [TestFixtureSetUp]
            public void GenerateJobSetup()
            {
                var pluginMain = Substitute.For<ASCompletion.PluginMain>();
                var pluginUI = new PluginUI(pluginMain);
                pluginMain.MenuItems.Returns(new List<System.Windows.Forms.ToolStripItem>());
                pluginMain.Settings.Returns(new GeneralSettings());
                pluginMain.Panel.Returns(pluginUI);
                #region ASContext.GlobalInit(pluginMain);
                var method = typeof(ASContext).GetMethod("GlobalInit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                method.Invoke(null, new[] {pluginMain});
                #endregion
                ASContext.Context = Substitute.For<IASContext>();
                CurrentDocument.SciControl.Returns(Sci);
                Helpers.TemplateUtils.Settings = new Settings();
            }

            static string ConvertWinNewlineToUnix(string s) => s.Replace("\r\n", "\n");

            static string ReadAllText(string path) => ConvertWinNewlineToUnix(TestFile.ReadAllText(path));

            protected string Generate(string sourceText, ClassModel type, string template, string pccpattern)
            {
                Sci.Text = sourceText;
                SnippetHelper.PostProcessSnippets(Sci, 0);
                var context = new AS3Context.Context(new AS3Settings());
                ASContext.Context.Features.Returns(context.Features);
                ASContext.Context.IsFileValid.Returns(true);
                var currentModel = new FileModel {Context = ASContext.Context};
                new ASFileParser().ParseSrc(currentModel, Sci.Text);
                var currentClass = currentModel.Classes[0];
                ASContext.Context.CurrentClass.Returns(currentClass);
                ASContext.Context.CurrentModel.Returns(currentModel);
                ASContext.Context.CurrentMember.Returns(currentClass.Members[0]);
                ASContext.Context.GetVisibleExternalElements().Returns(_ => context.GetVisibleExternalElements());
                ASContext.Context.GetCodeModel(null).ReturnsForAnyArgs(x =>
                {
                    var src = x[0] as string;
                    return string.IsNullOrEmpty(src) ? null : context.GetCodeModel(src);
                });
                ASContext.Context.ResolveType(null, null).ReturnsForAnyArgs(type);
                ASContext.Context
                    .When(x => x.ResolveTopLevelElement(Arg.Any<string>(), Arg.Any<ASResult>()))
                    .Do(x =>
                    {
                        var result = x.ArgAt<ASResult>(1);
                        result.Type = type;
                    });
                var expr = Helpers.CompleteHelper.GetCurrentExpressionType();
                var tmp = Helpers.TemplateUtils.GetTemplate(template, new[] {type.Type, pccpattern});
                if (!string.IsNullOrEmpty(tmp)) template = tmp;
                template = template.Replace("$(ItmUniqueVar)", ASComplete.FindFreeIterator(ASContext.Context, ASContext.Context.CurrentClass, new ASResult().Context));
                template = Helpers.TemplateUtils.ProcessTemplate(pccpattern, template, expr);
                Helpers.TemplateUtils.InsertSnippetText(expr, template, pccpattern);
                return ConvertWinNewlineToUnix(Sci.Text);
            }

            [TestFixture]
            public class AS3GeneratorTests : GeneratorJob
            {
                [TestFixtureSetUp]
                public void AS3GeneratorTestsSetup() => Sci.ConfigurationLanguage = "as3";

                public IEnumerable<TestCaseData> Const
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromString.as"))
                                .SetName("const from \"\".|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromUInt.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromUInt.as"))
                                .SetName("const from 1.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNumber.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNumber.as"))
                                .SetName("const from 10.0.|");
                        /*yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromInt.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromInt.as"))
                                .SetName("const from -1.|")
                                .Ignore();*/
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromArray.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromArray.as"))
                                .SetName("const from [].|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromObject.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromObject.as"))
                                .SetName("const from {}.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewObject.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewObject.as"))
                                .SetName("const from new Object().|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewVectorInt.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewVectorInt.as"))
                                .SetName("const from new Vector.<int>().|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewVectorInt_short.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewVectorInt_short.as"))
                                .SetName("const from new <int>[].|");
                    }
                }

                public IEnumerable<TestCaseData> Var
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "String", Type = "String" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromString.as"))
                                .SetName("var from \"\".|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromUInt.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Number", Type = "Number" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromUInt.as"))
                                .SetName("var from 1.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNumber.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Number", Type = "Number" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNumber.as"))
                                .SetName("var from 10.0.|");
                        /*yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromInt.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromInt.as"))
                                .SetName("var from -1.|")
                                .Ignore();*/
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromArray.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Array", Type = "Array" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromArray.as"))
                                .SetName("var from [].|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromObject.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromObject.as"))
                                .SetName("var from {}.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNewObject.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNewObject.as"))
                                .SetName("var from new Object().|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNewVectorInt.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNewVectorInt.as"))
                                .SetName("var from new Vector.<int>().|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNewVectorInt_short.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNewVectorInt_short.as"))
                                .SetName("var from new <int>[].|");
                    }
                }

                public IEnumerable<TestCaseData> Constructor
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "String", Type = "String" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.constructor.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConstructor_fromString.as"))
                                .SetName("constructor from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Notnull
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "String", Type = "String" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.notnull.fds"),
                                    Helpers.TemplateUtils.PatternNullable)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateNotNull_fromString.as"))
                                .SetName("notnull from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Null
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "String", Type = "String" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.null.fds"),
                                    Helpers.TemplateUtils.PatternNullable)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateNull_fromString.as"))
                                .SetName("null from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Par
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "String", Type = "String" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.par.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGeneratePar_fromString.as"))
                                .SetName("par from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Sel
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "String", Type = "String" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.sel.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateSel_fromString.as"))
                                .SetName("sel from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Return
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "String", Type = "String" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.return.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateReturn_fromString.as"))
                                .SetName("return from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> If
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromBoolean.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Boolean", Type = "Boolean" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.if.fds"),
                                    Helpers.TemplateUtils.PatternBool)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateIf_fromBoolean.as"))
                                .SetName("if from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Else
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromBoolean.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Boolean", Type = "Boolean" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.else.fds"),
                                    Helpers.TemplateUtils.PatternBool)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateElse_fromBoolean.as"))
                                .SetName("else from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Not
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromBoolean.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Boolean", Type = "Boolean" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.not.fds"),
                                    Helpers.TemplateUtils.PatternBool)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateNot_fromBoolean.as"))
                                .SetName("not from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Foreach
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromArray.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Array", Type = "Array" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.foreach.fds"),
                                    Helpers.TemplateUtils.PatternCollection)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForeach_fromArray.as"))
                                .SetName("foreach from array.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromArrayAccess.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Array", Type = "Array" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.foreach.fds"),
                                    Helpers.TemplateUtils.PatternCollection)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForeach_fromArrayAccess.as"))
                                .SetName("foreach from [].|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromObject.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.foreach.fds"),
                                    Helpers.TemplateUtils.PatternHash)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForeach_fromObject.as"))
                                .SetName("foreach from object.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromObjectInitializer.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.foreach.fds"),
                                    Helpers.TemplateUtils.PatternHash)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForeach_fromObjectInitializer.as"))
                                .SetName("foreach from {}.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromDictionary.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Dictionary", Type = "flash.utils.Dictionary" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.foreach.fds"),
                                    Helpers.TemplateUtils.PatternHash)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForeach_fromDictionary.as"))
                                .SetName("foreach from dictionary.|");
                    }
                }

                public IEnumerable<TestCaseData> Forin
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromObject.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.forin.fds"),
                                    Helpers.TemplateUtils.PatternHash)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForin_fromObject.as"))
                                .SetName("forin from object.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromObjectInitializer.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.forin.fds"),
                                    Helpers.TemplateUtils.PatternHash)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForin_fromObjectInitializer.as"))
                                .SetName("forin from {}.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromDictionary.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Dictionary", Type = "flash.utils.Dictionary" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.forin.fds"),
                                    Helpers.TemplateUtils.PatternHash)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForin_fromDictionary.as"))
                                .SetName("forin from dictionary.|");
                    }
                }

                public IEnumerable<TestCaseData> For
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromArray.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Array", Type = "Array" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.for.fds"),
                                    Helpers.TemplateUtils.PatternCollection)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateFor_fromArray.as"))
                                .SetName("for from array.|");
                    }
                }

                public IEnumerable<TestCaseData> Forr
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromArray.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Array", Type = "Array" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.forr.fds"),
                                    Helpers.TemplateUtils.PatternCollection)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForr_fromArray.as"))
                                .SetName("forr from array.|");
                        yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromArrayAccess.as"),
                                    new ClassModel { InFile = new FileModel(), Name = "Array", Type = "Array" },
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.forr.fds"),
                                    Helpers.TemplateUtils.PatternCollection)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateForr_fromArrayAccess.as"))
                                .SetName("forr from [].|");
                    }
                }

                [Test, TestCaseSource("Const"), TestCaseSource("Var"), TestCaseSource("Constructor"), TestCaseSource("Notnull"), TestCaseSource("Null"), TestCaseSource("Par"), TestCaseSource("Return"),
                       TestCaseSource("If"), TestCaseSource("Else"), TestCaseSource("Not"), 
                       TestCaseSource("Foreach"), TestCaseSource("Forin"), TestCaseSource("For"), TestCaseSource("Forr")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern) => Generate(sourceText, type, template, pccpattern);
            }
        }
    }
}