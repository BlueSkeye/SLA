
namespace Sla.DECCORE
{
    /// Compare two Datatype pointers: first by name, then by id
    internal class DatatypeNameCompare : IComparer<Datatype>
    {
        internal static readonly DatatypeNameCompare Instance = new DatatypeNameCompare();

        private DatatypeNameCompare()
        {
        }

        public int Compare(Datatype? a, Datatype? b)
        {
            if (null == a) throw new BugException();
            if (null == b) throw new BugException();
            int res = a.getName().CompareTo(b.getName());
            if (res != 0) return res;
            return a.getId().CompareTo(b.getId());
        }
    }
}
