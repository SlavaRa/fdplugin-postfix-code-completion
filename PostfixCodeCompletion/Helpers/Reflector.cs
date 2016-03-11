using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using ASCompletion.Completion;
using ASCompletion.Context;
using PluginCore;
using PluginCore.Controls;
using ScintillaNet;

namespace PostfixCodeCompletion.Helpers
{
    static class Reflector
    {
        internal static readonly CompletionListReflector CompletionList = new CompletionListReflector();
        internal static readonly ASCompleteReflector ASComplete = new ASCompleteReflector();
        internal static readonly ASGeneratorReflector ASGenerator = new ASGeneratorReflector();
    }

    internal class CompletionListReflector
    {
        internal ListBox completionList
        {
            get
            {
                var member = typeof(CompletionList).GetField("completionList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                return (ListBox)member.GetValue(typeof(ListBox));
            }
        }

        internal List<ICompletionListItem> allItems
        {
            get
            {
                var member = typeof(CompletionList).GetField("allItems", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                return (List<ICompletionListItem>)member.GetValue(typeof(List<ICompletionListItem>));
            }
        }
    }

    internal class ASCompleteReflector
    {
        internal bool HandleDotCompletion(ScintillaControl sci, bool autoHide)
        {
            var methodInfo = typeof(ASComplete).GetMethod("HandleDotCompletion", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)methodInfo.Invoke(null, new object[] { sci, autoHide });
        }
    }

    internal class ASGeneratorReflector
    {
        internal string CleanType(string type)
        {
            var methodInfo = typeof(ASGenerator).GetMethod("CleanType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)methodInfo.Invoke(null, new object[] { type });
        }

        internal string GuessVarName(string name, string type)
        {
            var methodInfo = typeof(ASGenerator).GetMethod("GuessVarName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)methodInfo.Invoke(null, new object[] { name, type });
        }

        internal string GetShortType(string type)
        {
            var methodInfo = typeof(ASGenerator).GetMethod("GetShortType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)methodInfo.Invoke(null, new object[] { type });
        }

        internal ASResult GetStatementReturnType(ScintillaControl sci, string line, int positionFromLine)
        {
            var methodInfo = typeof(ASGenerator).GetMethod("GetStatementReturnType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            var returnType = methodInfo.Invoke(null, new object[] { sci, ASContext.Context.CurrentClass, line, positionFromLine });
            var expr = returnType != null
                ? (ASResult) returnType.GetType().GetField("resolve").GetValue(returnType)
                : null;
            return expr;
        }
    }
}