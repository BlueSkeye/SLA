using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla
{
    internal static class StringExtensions
    {
        internal static string Capitalize(this string candidate)
        {
            if (string.IsNullOrEmpty(candidate)) {
                return candidate;
            }
            switch (candidate.Length) {
                case 1:
                    return candidate.ToUpper();
                default:
                    return char.ToUpper(candidate[0]) + candidate.Substring(1);
            }
        }
        
        internal static bool empty(this string candidate)
        {
            return 0 == candidate.Length;
        }

        internal static string ReplaceCharacter(this string candidate, int index, char replacement)
        {
            StringBuilder builder = new StringBuilder(candidate);
            builder[index] = replacement;
            return builder.ToString();
        }
    }
}
