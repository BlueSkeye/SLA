using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla
{
    internal static class Extensions
    {
        internal static T GetLastItem<T>(this List<T> from)
        {
            int lastItemIndex = from.Count - 1;

            if (lastItemIndex < 0) {
                throw new BugException();
            }
            return from[lastItemIndex];
        }

        internal static void RemoveLastItem<T>(this List<T> from)
        {
            int lastItemIndex = from.Count - 1;

            if (0 > lastItemIndex) {
                throw new InvalidOperationException();
            }
            from.RemoveAt(lastItemIndex);
        }
    }
}
