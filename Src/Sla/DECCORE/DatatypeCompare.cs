
namespace Sla.DECCORE
{
    /// Compare two Datatype pointers for equivalence of their description
    internal class DatatypeCompare : IComparer<Datatype>
    {
        internal static readonly DatatypeCompare Instance = new DatatypeCompare();

        private DatatypeCompare()
        {
        }

        /// Comparison operator
        public int Compare(Datatype? a, Datatype? b)
        {
            if (null == a) throw new BugException();
            if (null == b) throw new BugException();
            int res = a.compareDependency(b);
            if (res != 0) return res;
            return a.getId().CompareTo(b.getId());
        }
    }
}
