
namespace Sla.DECCORE
{
    // Compare two Varnode pointers by definition then location
    internal class VarnodeCompareDefLoc // : IComparer<Varnode>
    {
        internal static readonly VarnodeCompareDefLoc Instance = new VarnodeCompareDefLoc();

        private VarnodeCompareDefLoc() { }

        /// Functional comparison operator
        /// Compare by definition then by location.
        /// This is different than the standard ordering but we still allow multiple identical frees.
        /// \param a is the first Varnode to compare
        /// \param b is the second Varnode to compare
        /// \return true if \b a occurs earlier than \b b
        public static int Compare(Varnode? a, Varnode? b)
        {
            if (null == a) throw new ArgumentNullException(nameof(a));
            if (null == b) throw new ArgumentNullException(nameof(b));

            Varnode.varnode_flags f1 = (a.getFlags() & (Varnode.varnode_flags.input | Varnode.varnode_flags.written));
            Varnode.varnode_flags f2 = (b.getFlags() & (Varnode.varnode_flags.input | Varnode.varnode_flags.written));
            if (f1 != f2) return (f1 - 1).CompareTo(f2 - 1);
            // NOTE: The -1 forces free varnodes to come last
            if (f1 == Varnode.varnode_flags.written) {
                int result = a.getDef().getSeqNum().CompareTo(b.getDef().getSeqNum());
                if (0 != result)
                    return result;
            }
            if (a.getAddr() != b.getAddr()) return (a.getAddr().CompareTo(b.getAddr()));
            if (a.getSize() != b.getSize()) return (a.getSize().CompareTo(b.getSize()));
            if (f1 == 0)
                // both are free
                //    return (a<b);		// Compare pointers
                return a.getCreateIndex().CompareTo(b.getCreateIndex());
            return -1;
        }
    }
}
