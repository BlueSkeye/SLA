using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ghidra
{
    /// \brief Space for storing internal PcodeOp pointers as addresses
    ///
    /// It is convenient and efficient to replace the formally encoded
    /// branch target addresses with a pointer to the actual PcodeOp
    /// being branched to.  This special \b iop space allows a PcodeOp
    /// pointer to be encoded as an address so it can be stored as
    /// part of an input varnode, in place of the target address, in
    /// a \e branching operation.  The pointer is encoded as an offset
    /// within the \b fspec space.
    internal class IopSpace : AddrSpace
    {
        /// Reserved name for the iop space
        public const string NAME = "iop";

        /// Constructor for the \b iop space.
        /// There is only one such space, and it is considered internal
        /// to the model, i.e. the Translate engine should never generate
        /// addresses in this space.
        /// \param m is the associated address space manager
        /// \param t is the associated processor translator
        /// \param ind is the associated index
        public IopSpace(AddrSpaceManager m, Translate t, int4 ind)
            : base(m, t, IPTR_IOP, NAME,sizeof(void*),1, ind,0,1)
        {
            clearFlags(heritaged | does_deadcode | big_endian);
            if (HOST_ENDIAN == 1)       // Endianness always set to host
                setFlags(big_endian);
        }

        public override void encodeAttributes(Encoder encoder, uintb offset)
        {
            encoder.writeString(ATTRIB_SPACE, "iop");
        }

        public override void encodeAttributes(Encoder encoder, uintb offset, int4 size)
        {
            encoder.writeString(ATTRIB_SPACE, "iop");
        }

        public override void printRaw(TextWriter s, uintb offset)
        {               // Print info about op this address refers to
            BlockBasic* bs;
            BlockBasic* bl;
            PcodeOp* op = (PcodeOp*)(uintp)offset; // Treat offset as op

            if (!op->isBranch())
            {   // op parameter for CPUI_INDIRECT
                s << op->getSeqNum();
                return;
            }
            bs = op->getParent();
            if (bs->sizeOut() == 2)     // We print the non-fallthru condition
                bl = (BlockBasic*)(op->isFallthruTrue() ? bs->getOut(0) : bs->getOut(1));
            else
                bl = (BlockBasic*)bs->getOut(0);
            s << "code_" << bl->getStart().getShortcut();
            bl->getStart().printRaw(s);
        }

        public override void saveXml(TextWriter s)
        {
            throw LowlevelError("Should never encode iop space to stream");
        }

        public override void decode(Decoder decoder)
        {
            throw LowlevelError("Should never decode iop space from stream");
        }
    }
}
