using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Compare two Varnode pointers by location then definition
    internal struct VarnodeCompareLocDef
    {
        /// Compare by location then by definition.
        /// This is the same as the normal varnode compare, but we distinguish identical frees by their
        /// pointer address.  Thus varsets defined with this comparison act like multisets for free varnodes
        /// and like unique sets for everything else (with respect to the standard varnode comparison)
        /// \param a is the first Varnode to compare
        /// \param b is the second Varnode to compare
        /// \return true if \b a occurs earlier than \b b
        internal static bool operator/*()*/(Varnode a, Varnode b)
        {
            uint f1, f2;

            if (a.getAddr() != b.getAddr()) return (a.getAddr() < b.getAddr());
            if (a.getSize() != b.getSize()) return (a.getSize() < b.getSize());
            f1 = a.getFlags() & (Varnode.varnode_flags.input | Varnode.varnode_flags.written);
            f2 = b.getFlags() & (Varnode.varnode_flags.input | Varnode.varnode_flags.written);
            if (f1 != f2) return ((f1 - 1) < (f2 - 1)); // -1 forces free varnodes to come last
            if (f1 == Varnode.varnode_flags.written)
            {
                if (a.getDef().getSeqNum() != b.getDef().getSeqNum())
                    return (a.getDef().getSeqNum() < b.getDef().getSeqNum());
            }
            else if (f1 == 0)       // both are free
                                    //    return (a < b);		// compare pointers
                return (a.getCreateIndex() < b.getCreateIndex());

            return false;
        }
    }
}
