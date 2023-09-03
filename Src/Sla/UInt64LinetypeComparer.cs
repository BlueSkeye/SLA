
namespace Sla
{
    internal class UInt64LinetypeComparer : IComparer<System.UInt64>
    {
        internal static UInt64LinetypeComparer Instance =
            new UInt64LinetypeComparer();

        private UInt64LinetypeComparer()
        {
        }

        public int Compare(ulong x, ulong y)
        {
            return x.CompareTo(y);
        }
    }
}
