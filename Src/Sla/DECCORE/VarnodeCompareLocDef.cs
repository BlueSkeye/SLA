using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Compare two Varnode pointers by location then definition
    internal class VarnodeCompareLocDef : IComparer<Varnode>
    {
        internal static readonly VarnodeCompareLocDef Instance = new VarnodeCompareLocDef();

        private VarnodeCompareLocDef() { }

        /// Compare by location then by definition.
        /// This is the same as the normal varnode compare, but we distinguish identical frees by their
        /// pointer address.  Thus varsets defined with this comparison act like multisets for free varnodes
        /// and like unique sets for everything else (with respect to the standard varnode comparison)
        /// \param a is the first Varnode to compare
        /// \param b is the second Varnode to compare
        /// \return true if \b a occurs earlier than \b b
        public int Compare/*()*/(Varnode? a, Varnode? b)
        {
            if (null == a) throw new ArgumentNullException(nameof(a));
            if (null == b) throw new ArgumentNullException(nameof(b));
            if (a.getAddr() != b.getAddr()) return (a.getAddr().CompareTo(b.getAddr()));
            if (a.getSize() != b.getSize()) return (a.getSize().CompareTo(b.getSize()));
            Varnode.varnode_flags f1 = a.getFlags() & (Varnode.varnode_flags.input | Varnode.varnode_flags.written);
            Varnode.varnode_flags f2 = b.getFlags() & (Varnode.varnode_flags.input | Varnode.varnode_flags.written);
            if (f1 != f2) return (f1 - 1).CompareTo(f2 - 1); // -1 forces free varnodes to come last
            if (f1 == Varnode.varnode_flags.written) {
                if (a.getDef().getSeqNum() != b.getDef().getSeqNum())
                    return a.getDef().getSeqNum().CompareTo(b.getDef().getSeqNum());
            }
            else if (f1 == 0)
                // both are free
                //    return (a < b);		// compare pointers
                (a.getCreateIndex().CompareTo(b.getCreateIndex());

            return 0;
        }
    }
}
