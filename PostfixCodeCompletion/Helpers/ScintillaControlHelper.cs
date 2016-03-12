using System.Linq;
using ASCompletion.Completion;
using ScintillaNet;

namespace PostfixCodeCompletion.Helpers
{
    static class ScintillaControlHelper
    {
        internal static int GetExpressionStartPosition(ScintillaControl sci, int position, ASResult expr)
        {
            var characters = ScintillaControl.Configuration.GetLanguage(sci.ConfigurationLanguage).characterclass.Characters;
            var result = 0;
            var arrCount = 0;
            var parCount = 0;
            var genCount = 0;
            var braCount = 0;
            var dQuotes = 0;
            var sQuotes = 0;
            for (var i = position; i > 0; i--)
            {
                var c = (char)sci.CharAt(i - 1);
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
            var characters = ScintillaControl.Configuration.GetLanguage(sci.ConfigurationLanguage).characterclass.Characters;
            while (position >= 0)
            {
                var c = (char) sci.CharAt(position);
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

        internal static int GetDotLeftStartPosition(ScintillaControl sci, int position)
        {
            for (var i = sci.CurrentPos; i > 0; i--)
            {
                if ((char)sci.CharAt(i) != '.') continue;
                position = i;
                break;
            }
            return position;
        }
    }
}