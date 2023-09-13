using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla
{
    internal static partial class Extensions
    {
        internal static bool empty<K,V>(this Dictionary<K, V> dict)
            where K : notnull
        {
            return (0 == dict.Count);
        }
    }
}
