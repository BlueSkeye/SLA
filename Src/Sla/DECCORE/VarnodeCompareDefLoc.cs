using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Compare two Varnode pointers by definition then location
    internal struct VarnodeCompareDefLoc
    {
        /// Functional comparison operator
        /// Compare by definition then by location.
        /// This is different than the standard ordering but we still allow multiple identical frees.
        /// \param a is the first Varnode to compare
        /// \param b is the second Varnode to compare
        /// \return true if \b a occurs earlier than \b b
        internal static bool operator/*()*/(Varnode a, Varnode b)
        {
            uint f1, f2;

            f1 = (a.getFlags() & (Varnode::input | Varnode::written));
            f2 = (b.getFlags() & (Varnode::input | Varnode::written));
            if (f1 != f2) return ((f1 - 1) < (f2 - 1));
            // NOTE: The -1 forces free varnodes to come last
            if (f1 == Varnode::written)
            {
                if (a.getDef().getSeqNum() != b.getDef().getSeqNum())
                    return (a.getDef().getSeqNum() < b.getDef().getSeqNum());
            }
            if (a.getAddr() != b.getAddr()) return (a.getAddr() < b.getAddr());
            if (a.getSize() != b.getSize()) return (a.getSize() < b.getSize());
            if (f1 == 0)            // both are free
                                    //    return (a<b);		// Compare pointers
                return (a.getCreateIndex() < b.getCreateIndex());
            return false;
        }
    }
}
