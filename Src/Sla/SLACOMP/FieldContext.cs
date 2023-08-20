using Sla.SLEIGH;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief Helper function holding properties of a \e context field prior to calculating the context layout
    ///
    /// This holds the concrete Varnode reprensenting the context field's physical storage and the
    /// properties of the field itself, prior to the final ContextField being allocated.
    internal struct FieldContext
    {
        internal VarnodeSymbol sym;     ///< The concrete Varnode representing physical storage for the field
        internal FieldQuality qual;     ///< Qualities of the field, as parsed

        /// Sort context fields based on their least significant bit boundary
        /// Sort based on the containing Varnode, then on the bit boundary
        /// \param op2 is a field to compare with \b this
        /// \return \b true if \b this should be sorted before the other field
        public static bool operator <(FieldContext op1, FieldContext op2)
        {
            return (op1.sym.getName() != op2.sym.getName())
                ? (0 > string.Compare(op1.sym.getName(), op2.sym.getName()))
                : (op1.qual.low < op2.qual.low);
        }

        internal FieldContext(VarnodeSymbol s, FieldQuality q)
        {
            sym = s;
            qual = q;
        }
    }
}
