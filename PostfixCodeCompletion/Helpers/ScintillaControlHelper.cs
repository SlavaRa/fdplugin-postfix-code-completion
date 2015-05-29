using System.Linq;
using ASCompletion.Completion;
using ScintillaNet;

namespace PostfixCodeCompletion.Helpers
{
    static class ScintillaControlHelper
    {
        internal static int GetExpressionStartPosition(ScintillaControl sci, int position, ASResult expr)
        {
            string characters = ScintillaControl.Configuration.GetLanguage(sci.ConfigurationLanguage).characterclass.Characters;
            int result = 0;
            int arrCount = 0;
            int parCount = 0;
            int genCount = 0;
            int braCount = 0;
            int dQuotes = 0;
            int sQuotes = 0;
            for (int i = position; i > 0; i--)
            {
                char c = (char)sci.CharAt(i - 1);
                if (c == ']') arrCount++;
                else if (c == '[' && arrCount > 0) arrCount--;
                else if (c == ')') parCount++;
                else if (c == '(' && parCount > 0) parCount--;
                else if (c == '>') genCount++;
                else if (c == '<' && genCount > 0) genCount--;
                else if (c == '}') braCount++;
                else if (c == '{' && braCount > 0) braCount--;
                else if (c == '\"' && sQuotes == 0)
                {
                    if (i <= 1 || (char) sci.CharAt(i - 2) == '\\') continue;
                    if (dQuotes == 0) dQuotes++;
                    else dQuotes--;
                }
                else if (c == '\'' && dQuotes == 0)
                {
                    if (i <= 1 || (char) sci.CharAt(i - 2) == '\\') continue;
                    if (sQuotes == 0) sQuotes++;
                    else sQuotes--;
                }
                else if (arrCount == 0 && parCount == 0 && genCount == 0 && braCount == 0
                         && dQuotes == 0 && sQuotes == 0
                         && !characters.Contains(c) && c != '.')
                {
                    result = i;
                    break;
                }
            }
            if (expr.Member == null && sci.GetWordLeft(result - 1, true) == "new")
                return GetWordLeftStartPosition(sci, result - 1, true);
            return result;
        }

        internal static int GetWordLeftStartPosition(ScintillaControl sci, int position, bool skipWhiteSpace)
        {
            string characters = ScintillaControl.Configuration.GetLanguage(sci.ConfigurationLanguage).characterclass.Characters;
            while (position >= 0)
            {
                char c = (char) sci.CharAt(position);
                if (c <= ' ')
                {
                    if (!skipWhiteSpace) return position + 1;
                }
                else if (!characters.Contains(c)) return position + 1;
                else skipWhiteSpace = false;
                position--;
            }
            return position;
        }
    }
}