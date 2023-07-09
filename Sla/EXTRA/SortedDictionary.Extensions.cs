using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal static class SortedDictionaryExtensions
    {
        public static KeyValuePair<K, V>? BeforeUpperBound<K,V>(this SortedDictionary<K,V> collection,
            K key, out bool found, out bool exactMatch)
            where K : notnull, IComparable<K>
        {
            exactMatch = false;
            found = false;
            KeyValuePair<K, V>? result = default(KeyValuePair<K, V>);
            foreach(KeyValuePair<K,V> pair in collection) {
                int comparison = pair.Key.CompareTo(key);
                if (0 == comparison) {
                    found = true;
                    exactMatch = true;
                    return pair;
                }
                if (0 > comparison) {
                    found = true;
                    result = pair;
                }
                if (0 < comparison) {
                    break;
                }
            }
            return result;
        }
    }
}
