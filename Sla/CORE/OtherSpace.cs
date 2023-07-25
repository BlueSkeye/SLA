using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief Special AddrSpace for special/user-defined address spaces
    public class OtherSpace : AddrSpace
    {
        ///< Reserved name for the address space
        public const string NAME = "OTHER";
        ///< Reserved index for the other space
        public const int INDEX = 1;

        /// Construct the \b other space, which is automatically constructed
        /// by the compiler, and is only constructed once.  The name should
        /// always by \b OTHER.
        /// \param m is the associated address space manager
        /// \param t is the associated processor translator
        /// \param ind is the integer identifier
        public OtherSpace(AddrSpaceManager m, Translate t, int ind)
            : base(m, t, spacetype.IPTR_PROCESSOR, NAME, sizeof(ulong), 1, INDEX, 0, 0)
        {
            clearFlags(Properties.heritaged | Properties.does_deadcode);
            setFlags(Properties.is_otherspace);
        }

        ///< For use with decode
        public OtherSpace(AddrSpaceManager m, Translate t)
            : base(m, t, spacetype.IPTR_PROCESSOR)
        {
            clearFlags(Properties.heritaged | Properties.does_deadcode);
            setFlags(Properties.is_otherspace);
        }

        public override void printRaw(StreamWriter s, ulong offset)
        {
            s.Write("0x{0:X}", offset);
        }

        public override void saveXml(StreamWriter s)
        {
            s.Write("<space_other");
            saveBasicAttributes(s);
            s.WriteLine("/>");
        }
    }
}
