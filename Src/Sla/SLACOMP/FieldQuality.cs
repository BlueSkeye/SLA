using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief Qualities associated (via parsing) with a token or context \b field
    ///
    /// An object of this class accumulates properties of a field as they
    /// are parsed in of a \b define \b token block prior to formally allocating the
    /// TokenField or FieldContext object.
    internal struct FieldQuality
    {
        internal string name;        ///< Name of the field
        internal uint4 low;      ///< The least significant bit of the field within the token
        internal uint4 high;     ///< The most significant bit of the field within the token
        internal bool signext;       ///< \b true if the field's value is signed
        internal bool flow;      ///< \b true if the context \b flows for this field.
        internal bool hex;       ///< \b true if the field value is displayed in hex

        /// Establish default qualities for the field, which can then be overridden
        /// by further parsing.  A name and bit range must always be explicitly given.
        /// \param nm is the parsed name for the field
        /// \param l is the parsed lower bound of the bit range
        /// \param h is the parse upper bound of the bit range
        internal FieldQuality(string nm, uintb l, uintb h)
        {
            name = *nm;
            low = *l;
            high = *h;
            signext = false;
            flow = true;
            hex = true;
            delete nm;
            delete l;
            delete h;
        }
    }
}
