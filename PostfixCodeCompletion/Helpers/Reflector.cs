using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using PluginCore;
using PluginCore.Controls;
using ScintillaNet;

namespace PostfixCodeCompletion.Helpers
{
    internal static class Reflector
    {
        internal static readonly CompletionListReflector CompletionList = new CompletionListReflector();
        internal static readonly ASCompleteReflector ASComplete = new ASCompleteReflector();
        internal static readonly ASGeneratorReflector ASGenerator = new ASGeneratorReflector();
    }

    internal class CompletionListReflector
    {
        internal List<ICompletionListItem> AllItems
        {
            get
            {
                var fieldInfo = typeof(CompletionList).GetField("allItems", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                Debug.Assert(fieldInfo != null, "fieldInfo is null");
                return (List<ICompletionListItem>) fieldInfo.GetValue(typeof(List<ICompletionListItem>));
            }
        }
    }

    internal class ASCompleteReflector
    {
        internal bool HandleDotCompletion(ScintillaControl sci, bool autoHide)
        {
            var methodInfo = typeof(ASComplete).GetMethod("HandleDotCompletion", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(methodInfo != null, "methodInfo is null");
            return (bool) methodInfo.Invoke(null, new object[] {sci, autoHide});
        }
    }

    internal class ASGeneratorReflector
    {
        internal string CleanType(string type)
        {
            var methodInfo = typeof(ASGenerator).GetMethod("CleanType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(methodInfo != null, "methodInfo is null");
            return (string) methodInfo.Invoke(null, new object[] {type});
        }

        internal string GuessVarName(string name, string type)
        {
            var methodInfo = typeof(ASGenerator).GetMethod("GuessVarName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(methodInfo != null, "methodInfo != null");
            return (string) methodInfo.Invoke(null, new object[] {name, type});
        }

        internal string GetShortType(string type)
        {
            var methodInfo = typeof(ASGenerator).GetMethod("GetShortType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(methodInfo != null, "methodInfo is null");
            return (string) methodInfo.Invoke(null, new object[] {type});
        }

        internal StatementReturnType GetStatementReturnType(ScintillaControl sci, string line, int positionFromLine)
        {
            var currentClass = ASContext.Context.CurrentClass;
            if (currentClass.InFile.Context == null)
            {
                currentClass = (ClassModel) currentClass.Clone();
                var language = PluginBase.MainForm.SciConfig.GetLanguageFromFile(currentClass.InFile.BasePath);
                currentClass.InFile.Context = ASContext.GetLanguageContext(language) ?? ASContext.Context;
            }
            var methodInfo = typeof(ASGenerator).GetMethod("GetStatementReturnType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(methodInfo != null, "methodInfo is null");
            var returnType = methodInfo.Invoke(null, new object[] {sci, currentClass, line, positionFromLine});
            if (returnType == null) return null;
            var type = returnType.GetType();
            var result = new StatementReturnType
            {
                Resolve = (ASResult) type.GetField("resolve").GetValue(returnType),
                Position = (int) type.GetField("position").GetValue(returnType),
                Word = (string) type.GetField("word").GetValue(returnType)
            };
            return result;
        }
    }

    internal class StatementReturnType
    {
        public ASResult Resolve;
        public int Position;
        public string Word;
    }
}
