using System.Collections.Generic;
using System.Reflection;
using ASCompletion.Completion;
using PluginCore;
using PluginCore.Controls;

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

        #region ASGenerator.GetShortType(type)
        internal static string ASGeneratorGetShortType(string type)
        {
            
            MethodInfo methodInfo = typeof(ASGenerator).GetMethod("GetShortType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)methodInfo.Invoke(null, new object[] { type });
        }
        #endregion

        #region ASGenerator.CleanType(type)
        internal static string ASGeneratorCleanType(string type)
        {
            MethodInfo methodInfo = typeof(ASGenerator).GetMethod("CleanType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)methodInfo.Invoke(null, new object[] { type });
        }
        #endregion
    }
}