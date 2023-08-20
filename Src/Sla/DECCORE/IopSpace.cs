using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
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
        public IopSpace(AddrSpaceManager m, Translate t, int ind)
            : base(m, t, spacetype.IPTR_IOP, NAME,sizeof(void*),1, ind,0,1)
        {
            clearFlags(Properties.heritaged | Properties.does_deadcode | Properties.big_endian);
            if (HOST_ENDIAN == 1)       // Endianness always set to host
                setFlags(Properties.big_endian);
        }

        public override void encodeAttributes(Sla.CORE.Encoder encoder, ulong offset)
        {
            encoder.writeString(AttributeId.ATTRIB_SPACE, "iop");
        }

        public override void encodeAttributes(Sla.CORE.Encoder encoder, ulong offset, int size)
        {
            encoder.writeString(AttributeId.ATTRIB_SPACE, "iop");
        }

        public override void printRaw(TextWriter s, ulong offset)
        {
            // Print info about op this address refers to
            BlockBasic bs;
            BlockBasic bl;
            PcodeOp op = (PcodeOp)(ulong)offset; // Treat offset as op

            if (!op.isBranch()) {   // op parameter for OpCode.CPUI_INDIRECT
                s.Write(op.getSeqNum());
                return;
            }
            bs = op.getParent();
            if (bs.sizeOut() == 2)     // We print the non-fallthru condition
                bl = (BlockBasic)(op.isFallthruTrue() ? bs.getOut(0) : bs.getOut(1));
            else
                bl = (BlockBasic)bs.getOut(0);
            s.Write($"code_{bl.getStart().getShortcut()}");
            bl.getStart().printRaw(s);
        }

        public override void saveXml(TextWriter s)
        {
            throw new LowlevelError("Should never encode iop space to stream");
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
            throw new LowlevelError("Should never decode iop space from stream");
        }
    }
}
