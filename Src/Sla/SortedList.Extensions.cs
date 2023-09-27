
using Sla.EXTRA;

namespace Sla
{
    internal static partial class Extensions
    {
        internal static bool empty<K, V>(this SortedList<K, V> list)
            where K : notnull
            => 0 != list.Count;

        internal static IBiDirEnumerator<V> GetRangeEnumerator<K,V>(this SortedList<K, V> list,
            int initialPositionIncluded, int finalPositionIncluded)
            where K : notnull
        {
            throw new NotImplementedException();
        }

        // Returns the index of the first list item which key is greater or equal to searchedKey.
        // If no such item exist, the method returns list.Count (which is an invalid index)
        internal static int lower_bound<K, V>(this SortedList<K, V> list, K searchedKey)
            where K : notnull, IComparable<K>
        {
            // TODO : Use a more efficient (binary search) algorithm.
            int listCount = list.Count;
            for(int index = 0; index < listCount; index++) {
                if (0 <= list.ElementAt(index).Key.CompareTo(searchedKey)) {
                    return index;
                }
            }
            return listCount;
        }

        // Remove any item which key is greater or equal to lowestRemovedKey and strictly less than
        // lowestNotRemovedKey
        internal static void RemoveRange<K, V>(this SortedList<K, V> from, K lowestRemovedKey,
            K lowestNotRemovedKey)
            where K : notnull, IComparable<K>
        {
            if (0 < lowestRemovedKey.CompareTo(lowestNotRemovedKey)) {
                throw new ArgumentException();
            }
            int listCount = from.Count;
            int firstRemovedIndex = from.lower_bound(lowestRemovedKey);
            if (firstRemovedIndex >= listCount) {
                // Nothing to remove.
                return;
            }
            int firstNonRemovedIndex = from.lower_bound(lowestNotRemovedKey);
            from.RemoveRange(firstRemovedIndex, firstNonRemovedIndex);
        }

        internal static void RemoveRange<K, V>(this SortedList<K, V> from, int firstRemovedIndex,
            int firstNonRemovedIndex)
            where K : notnull, IComparable<K>
        {
            if (0 > firstRemovedIndex) {
                throw new ArgumentOutOfRangeException();
            }
            int listCount = from.Count;
            if (firstNonRemovedIndex > listCount) {
                throw new ArgumentOutOfRangeException();
            }
            if (firstRemovedIndex > firstNonRemovedIndex) {
                throw new ArgumentOutOfRangeException();
            }
            int removeCount = firstNonRemovedIndex - firstRemovedIndex;
            for (int index = 0; index < removeCount; index++) {
                // We always remove at the same index.
                from.RemoveAt(firstRemovedIndex);
            }
        }
        
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
