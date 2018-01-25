using System.Collections.Generic;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using ASCompletion.TestUtils;
using NSubstitute;
using NUnit.Framework;
using PostfixCodeCompletion.Helpers;
using PostfixCodeCompletion.TestUtils;
using TemplateUtils = PostfixCodeCompletion.Helpers.TemplateUtils;

namespace PostfixCodeCompletion.Completion
{
    [TestFixture]
    internal class PostfixGeneratorTests : ASCompleteTests
    {
        [TestFixture]
        public class GeneratorJob : PostfixGeneratorTests
        {
            [TestFixtureSetUp]
            public void GenerateJobSetup() => TemplateUtils.Settings = new Settings();

            static string ConvertWinNewlineToUnix(string s) => s.Replace("\r\n", "\n");

            static string ReadCode(string fileName) => ConvertWinNewlineToUnix(TestFile.ReadAllText($"PostfixCodeCompletion.Test_Files.generated.as3.{fileName}.as"));
            static string ReadSnippet(string fileName) => ConvertWinNewlineToUnix(TestFile.ReadAllText($"PostfixCodeCompletion.Test_Snippets.as3.postfixgenerators.{fileName}.fds"));

            protected string Generate(string sourceText, ClassModel type, string template, string pccpattern)
            {
                SetSrc(sci, sourceText);
                ASContext.Context
                    .When(x => x.ResolveTopLevelElement(Arg.Any<string>(), Arg.Any<ASResult>()))
                    .Do(x =>
                    {
                        var result = x.ArgAt<ASResult>(1);
                        result.Type = type;
                    });
                var expr = CompleteHelper.GetCurrentExpressionType();
                var tmp = TemplateUtils.GetTemplate(template, new[] {type.Type, pccpattern});
                if (!string.IsNullOrEmpty(tmp)) template = tmp;
                template = template.Replace("$(ItmUniqueVar)", ASComplete.FindFreeIterator(ASContext.Context, ASContext.Context.CurrentClass, new ASResult().Context));
                template = TemplateUtils.ProcessTemplate(pccpattern, template, expr);
                TemplateUtils.InsertSnippetText(expr, template, pccpattern);
                return ConvertWinNewlineToUnix(sci.Text);
            }

            [TestFixture]
            public class AS3GeneratorTests : GeneratorJob
            {
                [TestFixtureSetUp]
                public void Setup()
                {
                    ASContext.Context.SetAs3Features();
                    sci.ConfigurationLanguage = "as3";
                }

                public TestCaseData GetTestCaseFromArray(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromArray"),
                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                    ReadSnippet(patternPath),
                    TemplateUtils.PatternCollection);

                public TestCaseData GetTestCaseFromArrayInitializer(string patternPath) => GetTestCaseFromArrayInitializer(patternPath, TemplateUtils.PatternCollection);
                public TestCaseData GetTestCaseFromArrayInitializer(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromArrayInitializer"),
                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromMultilineArrayInitializer(string patternPath) => GetTestCaseFromMultilineArrayInitializer(patternPath, TemplateUtils.PatternCollection);
                public TestCaseData GetTestCaseFromMultilineArrayInitializer(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromMultilineArrayInitializer"),
                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromVector(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromVector"),
                    new ClassModel {InFile = new FileModel(), Name = "Vector.<Object>", Type = "Vector.<Object>"},
                    ReadSnippet(patternPath),
                    TemplateUtils.PatternCollection);

                public TestCaseData GetTestCaseFromVectorInitializer(string patternPath) => GetTestCaseFromVectorInitializer(patternPath, TemplateUtils.PatternCollection);
                public TestCaseData GetTestCaseFromVectorInitializer(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromVectorInitializer"),
                    new ClassModel {InFile = new FileModel(), Name = "Vector.<Object>", Type = "Vector.<Object>"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromBoolean(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromBoolean"),
                    new ClassModel {InFile = new FileModel(), Name = "Boolean", Type = "Boolean"},
                    ReadSnippet(patternPath),
                    TemplateUtils.PatternBool);

                public TestCaseData GetTestCaseFromDictionary(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromDictionary"),
                    new ClassModel {InFile = new FileModel(), Name = "Dictionary", Type = "flash.utils.Dictionary"},
                    ReadSnippet(patternPath),
                    TemplateUtils.PatternHash);

                public TestCaseData GetTestCaseFromUInt(string patternPath) => GetTestCaseFromUInt(patternPath, TemplateUtils.PatternHash);
                public TestCaseData GetTestCaseFromUInt(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromUInt"),
                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromNumber(string patternPath) => GetTestCaseFromNumber(patternPath, TemplateUtils.PatternHash);
                public TestCaseData GetTestCaseFromNumber(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromNumber"),
                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromObject(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromObject"),
                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                    ReadSnippet(patternPath),
                    TemplateUtils.PatternHash);

                public TestCaseData GetTestCaseFromObjectInitializer(string patternPath) => GetTestCaseFromObjectInitializer(patternPath, TemplateUtils.PatternHash);
                public TestCaseData GetTestCaseFromObjectInitializer(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromObjectInitializer"),
                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromString(string patternPath) => GetTestCaseFromString(patternPath, TemplateUtils.PatternString);
                public TestCaseData GetTestCaseFromString(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromString"),
                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public IEnumerable<TestCaseData> Constructor
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("constructor", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConstructor_fromString"))
                                .SetName("constructor from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> If
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("if")
                                .Returns(ReadCode("AfterGenerateIf_fromBoolean"))
                                .SetName("if from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Else
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("else")
                                .Returns(ReadCode("AfterGenerateElse_fromBoolean"))
                                .SetName("else from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Null
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("null", TemplateUtils.PatternNullable)
                                .Returns(ReadCode("AfterGenerateNull_fromString"))
                                .SetName("\"\".null|");
                    }
                }

                public IEnumerable<TestCaseData> Notnull
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("notnull", TemplateUtils.PatternNullable)
                                .Returns(ReadCode("AfterGenerateNotNull_fromString"))
                                .SetName("\"\".notnull|");
                    }
                }

                public IEnumerable<TestCaseData> Not
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("not")
                                .Returns(ReadCode("AfterGenerateNot_fromBoolean"))
                                .SetName("true.not|");
                    }
                }

                public IEnumerable<TestCaseData> Foreach
                {
                    get
                    {
                        yield return
                            GetTestCaseFromArray("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromArray"))
                                .SetName("array.foreach|");
                        yield return
                            GetTestCaseFromArrayInitializer("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromArrayInitializer"))
                                .SetName("[].foreach|");
                        yield return
                            GetTestCaseFromObject("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromObject"))
                                .SetName("object.foreach|");
                        yield return
                            GetTestCaseFromObjectInitializer("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromObjectInitializer"))
                                .SetName("{}.foreach|");
                        yield return
                            GetTestCaseFromDictionary("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromDictionary"))
                                .SetName("dictionary.foreach|");
                        yield return
                            GetTestCaseFromVector("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromVector"))
                                .SetName("vector.foreach|");
                        yield return
                            GetTestCaseFromString("foreach")
                                .Returns(ReadCode("AfterGenerateString.foreach"))
                                .SetName("'string'.foreach|");
                    }
                }

                public IEnumerable<TestCaseData> Forin
                {
                    get
                    {
                        yield return
                            GetTestCaseFromObject("forin")
                                .Returns(ReadCode("AfterGenerateForin_fromObject"))
                                .SetName("object.forin|");
                        yield return
                            GetTestCaseFromObjectInitializer("forin")
                                .Returns(ReadCode("AfterGenerateForin_fromObjectInitializer"))
                                .SetName("{}.forin|");
                        yield return
                            GetTestCaseFromDictionary("forin")
                                .Returns(ReadCode("AfterGenerateForin_fromDictionary"))
                                .SetName("dictionary.forin|");
                    }
                }

                public IEnumerable<TestCaseData> For
                {
                    get
                    {
                        yield return
                            GetTestCaseFromArray("for")
                                .Returns(ReadCode("AfterGenerateFor_fromArray"))
                                .SetName("array.for|");
                        yield return
                            GetTestCaseFromArrayInitializer("for")
                                .Returns(ReadCode("AfterGenerateFor_fromArrayInitializer"))
                                .SetName("[].for|");
                        yield return
                            GetTestCaseFromVector("for")
                                .Returns(ReadCode("AfterGenerateFor_fromVector"))
                                .SetName("vector.for|");
                        yield return
                            GetTestCaseFromVectorInitializer("for")
                                .Returns(ReadCode("AfterGenerateFor_fromVectorInitializer"))
                                .SetName("new <Object>[].for|");
                        yield return
                            GetTestCaseFromNumber("for", TemplateUtils.PatternNumber)
                                .Returns(ReadCode("AfterGenerateFor_fromNumber"))
                                .SetName("10.0.for|");
                        yield return
                            GetTestCaseFromString("for")
                                .Returns(ReadCode("AfterGenerateString.for"))
                                .SetName("'string'.for")
                                .SetDescription("https://github.com/SlavaRa/fdplugin-postfix-code-completion/issues/71");
                    }
                }

                public IEnumerable<TestCaseData> Forr
                {
                    get
                    {
                        yield return
                            GetTestCaseFromArray("forr")
                                .Returns(ReadCode("AfterGenerateForr_fromArray"))
                                .SetName("array.forr|");
                        yield return
                            GetTestCaseFromArrayInitializer("forr")
                                .Returns(ReadCode("AfterGenerateForr_fromArrayInitializer"))
                                .SetName("[].forr|");
                        yield return
                            GetTestCaseFromNumber("forr", TemplateUtils.PatternNumber)
                                .Returns(ReadCode("AfterGenerateForr_fromNumber"))
                                .SetName("10.0.forr|");
                        yield return
                            GetTestCaseFromString("forr")
                                .Returns(ReadCode("AfterGenerateString.forr"))
                                .SetName("'string'.forr|")
                                .SetDescription("https://github.com/SlavaRa/fdplugin-postfix-code-completion/issues/71");
                    }
                }

                public IEnumerable<TestCaseData> Var
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("var", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromString"))
                                .SetName("\"\".var|");
                        yield return
                            GetTestCaseFromUInt("var", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromUInt"))
                                .SetName("1.var|");
                        //yield return
                        //    GetTestCaseFromNumber("var", TemplateUtils.PatternMember)
                        //        .Returns(ReadCode("AfterGenerateVar_fromNumber"))
                        //        .SetName("10.0.var|")
                        //        .Ignore();
                        /*yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateVar_fromInt"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadSnippet("var"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadCode(
                                        "AfterGenerateVar_fromInt"))
                                .SetName("-1.var|")
                                .Ignore();*/
                        yield return
                            GetTestCaseFromArrayInitializer("var", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromArray"))
                                .SetName("[].var|");
                        yield return
                            GetTestCaseFromObjectInitializer("var", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromObject"))
                                .SetName("{}.var|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateVar_fromNewObject"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadSnippet("var"),
                                    TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromNewObject"))
                                .SetName("new Object().var|");
                        yield return
                            new TestCaseData(
                                ReadCode("BeforeGenerateVar_fromNewVectorInt"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadSnippet("var"),
                                    TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromNewVectorInt"))
                                .SetName("new Vector.<int>().var|");
                        yield return
                            GetTestCaseFromVectorInitializer("var", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromVectorInitializer"))
                                .SetName("new <Object>[].var|");
                    }
                }

                public IEnumerable<TestCaseData> Const
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("const", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromString"))
                                .SetName("\"\".const|");
                        yield return
                            GetTestCaseFromUInt("const", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromUInt"))
                                .SetName("1.const|");
                        //yield return
                        //    GetTestCaseFromNumber("const", TemplateUtils.PatternMember)
                        //        .Returns(ReadCode("AfterGenerateConst_fromNumber"))
                        //        .SetName("10.0.const|")
                        //        .Ignore();
                        /*yield return
                            new TestCaseData(
                                    ReadCode(
                                        "BeforeGenerateConst_fromInt"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadSnippet("const"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadCode(
                                        "AfterGenerateConst_fromInt"))
                                .SetName("-1.const|")
                                .Ignore();*/
                        yield return
                            GetTestCaseFromArrayInitializer("const", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromArray"))
                                .SetName("[].const|");
                        yield return
                            GetTestCaseFromObjectInitializer("const", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromObject"))
                                .SetName("{}.const|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateConst_fromNewObject"),
                                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                    ReadSnippet("const"),
                                    TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromNewObject"))
                                .SetName("new Object().const|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateConst_fromNewVectorInt"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadSnippet("const"),
                                    TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromNewVectorInt"))
                                .SetName("new Vector.<int>().const|");
                        yield return
                            GetTestCaseFromVectorInitializer("const", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromVectorInitializer"))
                                .SetName("new <Object>[].const|");
                    }
                }

                public IEnumerable<TestCaseData> New
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerate_fromType"),
                                    new ClassModel {InFile = new FileModel(), Name = "Type", Type = "Type"},
                                    ReadSnippet("new"),
                                    TemplateUtils.PatternType)
                                .Returns(ReadCode("AfterGenerateNew_fromType"))
                                .SetName("Type.new|");
                    }
                }

                public IEnumerable<TestCaseData> Par
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("par", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGeneratePar_fromString"))
                                .SetName("\"\".par|");
                    }
                }

                public IEnumerable<TestCaseData> Return
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("return", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateReturn_fromString"))
                                .SetName("\"\".return|");
                        yield return
                            GetTestCaseFromMultilineArrayInitializer("return", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateReturn_fromMultilineArrayInitializer"))
                                .SetName("[].return|");
                    }
                }

                public IEnumerable<TestCaseData> While
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("while")
                                .Returns(ReadCode("AfterGenerateWhile_fromBoolean"))
                                .SetName("true.while|");
                    }
                }

                public IEnumerable<TestCaseData> DoWhile
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("dowhile")
                                .Returns(ReadCode("AfterGenerateDowhile_fromBoolean"))
                                .SetName("true.dowhile|");
                    }
                }

                public IEnumerable<TestCaseData> Sel
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("sel", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateSel_fromString"))
                                .SetName("\"\".sel|");
                    }
                }

                public IEnumerable<TestCaseData> Trace
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("trace", TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateTrace_fromString"))
                                .SetName("\"\".trace|");
                    }
                }

                [Test, TestCaseSource(nameof(Const)), TestCaseSource(nameof(Var)), TestCaseSource(nameof(Constructor)), TestCaseSource(nameof(Par)), TestCaseSource(nameof(Return)),
                       TestCaseSource(nameof(If)), TestCaseSource(nameof(Else)), TestCaseSource(nameof(Not)), TestCaseSource(nameof(Notnull)), TestCaseSource(nameof(Null)),
                       TestCaseSource(nameof(Foreach)), TestCaseSource(nameof(Forin)), TestCaseSource(nameof(For)), TestCaseSource(nameof(Forr)),
                       TestCaseSource(nameof(New)),
                       TestCaseSource(nameof(While)), TestCaseSource(nameof(DoWhile)),
                       TestCaseSource(nameof(Trace))]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern) => Generate(sourceText, type, template, pccpattern);
            }
        }
    }
}