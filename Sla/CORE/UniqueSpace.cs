using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief The pool of temporary storage registers
    /// It is convenient both for modelling processor instructions
    /// in an RTL and for later transforming of the RTL to have a pool
    /// of temporary registers that can hold data but that aren't a
    /// formal part of the state of the processor. The UniqueSpace
    /// provides a specific location for this pool.  The analysis
    /// engine always creates exactly one of these spaces named
    /// \b unique.  
    public class UniqueSpace : AddrSpace
    {
        ///< Reserved name for the unique space
        public const string NAME = "unique";
        ///< Fixed size (in bytes) for unique space offsets
        public const uint SIZE = 4;

        ///< Constructor
        /// This is the constructor for the \b unique space, which is
        /// automatically constructed by the analysis engine, and
        /// constructed only once.  The name should always be \b unique.
        /// \param m is the associated address space manager
        /// \param t is the associated processor translator
        /// \param ind is the integer identifier
        /// \param fl are attribute flags (currently unused)
        public UniqueSpace(AddrSpaceManager m, Translate t, int ind, AddrSpace.Properties fl)
            : base(m, t, spacetype.IPTR_INTERNAL, NAME, SIZE, 1, ind, fl, 0)
        {
            setFlags(Properties.hasphysical);
        }

        ///< For use with decode
        public UniqueSpace(AddrSpaceManager m, Translate t)
            : base(m, t, spacetype.IPTR_INTERNAL)
        {
            setFlags(Properties.hasphysical);
        }

        public override void saveXml(StreamWriter s)
        {
            s.Write("<space_unique");
            saveBasicAttributes(s);
            s.WriteLine("/>");
        }
    }
}
