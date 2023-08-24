
namespace Sla
{
    internal static partial class SortedSet
    {
        /// <summary>Returns an iterator pointing to the first element in the range [first,last) which does not compare less than val.</summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal static IEnumerator<T> lower_bound<T>(this SortedSet<T> from, T value)
            where T : class
        {
        }
    }
}
