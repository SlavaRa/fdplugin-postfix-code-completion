using System.Collections.Generic;
using System.Reflection;
using ASCompletion.Completion;
using ASCompletion.Context;
using PluginCore;
using PluginCore.Controls;
using ScintillaNet;

namespace PostfixCodeCompletion.Helpers
{
    static class Reflector
    {
        #region CompletionList.allItems.AddRange(items);
        internal static List<ICompletionListItem> CompletionListAllItems()
        {
            FieldInfo member = typeof(CompletionList).GetField("allItems", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return ((List<ICompletionListItem>) member.GetValue(typeof (List<ICompletionListItem>)));
        }
        #endregion

        #region ASGenerator.CleanType(type)
        internal static string ASGeneratorCleanType(string type)
        {
            MethodInfo methodInfo = typeof(ASGenerator).GetMethod("CleanType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)methodInfo.Invoke(null, new object[] { type });
        }
        #endregion

        #region ASGenerator.GetShortType(type)
        internal static string ASGeneratorGetShortType(string type)
        {
            
            MethodInfo methodInfo = typeof(ASGenerator).GetMethod("GetShortType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)methodInfo.Invoke(null, new object[] { type });
        }
        #endregion

        #region ASGenerator.GetStatementReturnType(sci, ASContext.Context.CurrentClass, line, positionFromLine).resolve
        internal static ASResult ASGeneratorGetStatementReturnType(ScintillaControl sci, string line, int positionFromLine)
        {
            MethodInfo methodInfo = typeof(ASGenerator).GetMethod("GetStatementReturnType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            object returnType = methodInfo.Invoke(null, new object[] { sci, ASContext.Context.CurrentClass, line, positionFromLine });
            ASResult expr = returnType != null ? (ASResult)returnType.GetType().GetField("resolve").GetValue(returnType) : null;
            return expr;
        }
        #endregion
    }
}