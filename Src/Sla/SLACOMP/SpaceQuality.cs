using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief Qualities associated (via parsing) with an address space
    ///
    /// An object of this class accumulates properties of an address space as they
    /// are parsed in the \b define statement prior to formally allocating the AddrSpace object.
    internal class SpaceQuality
    {
        /// \brief The type of space being defined
        internal enum Type
        {
            /// An address space representing normal, indexed, memory
            ramtype,
            /// An address space containing registers
            registertype
        }

        /// Name of the address space
        internal string name;
        /// Type of address space, \e ramtype or \e registertype
        internal uint type;
        /// Number of bytes required to index all bytes of the space
        internal uint size;
        /// Number of bytes in an addressable unit of the space
        internal uint wordsize;
        /// \b true if the new address space will be the default
        internal bool isdefault;

        /// Construct with the default qualities for an address space, which
        /// can then be overridden with further parsing.
        /// \param nm is the name of the address space
        internal SpaceQuality(string nm)
        {
            name = nm;
            type = ramtype;
            size = 0;
            wordsize = 1;
            isdefault = false;
        }
    }
}
