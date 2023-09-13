
using System.ComponentModel;

namespace Sla
{
    internal static partial class Extensions
    {
        // C++ : Returns an iterator pointing to the first element in the range [first,last)
        // which compares greater than val.
        internal static int upper_bound<K,V>(this SortedList<K, V> list, K searchedKey)
            where K : notnull, IComparable<K>
        {
            IList<K> keys = list.Keys;
            int keyCount = keys.Count;
            IComparer<K> comparer = list.Comparer;
            // Binary search.
            int lowerBound = 0;
            int upperBound = keyCount - 1;
            int result = keyCount;
            while (lowerBound < upperBound) {
                int searchedIndex = (lowerBound + upperBound) / 2;
                if (0 >= list.ElementAt(searchedIndex).Key.CompareTo(searchedKey)) {
                    lowerBound = searchedIndex + 1;
                }
                else {
                    result = upperBound;
                    upperBound = searchedIndex - 1;
                }
            }
            return result;
        }
    }
}
