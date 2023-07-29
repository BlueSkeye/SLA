using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief A special space for encoding FuncCallSpecs
    ///
    /// It is efficient and convenient to store the main subfunction
    /// object (FuncCallSpecs) in the pcode operation which is actually making the
    /// call. This address space allows a FuncCallSpecs to be encoded
    /// as an address which replaces the formally encoded address of
    /// the function being called, when manipulating the operation
    /// internally.  The space stored in the encoded address is
    /// this special \b fspec space, and the offset is the actual
    /// value of the pointer
    internal class FspecSpace : AddrSpace
    {
        ///< Reserved name for the fspec space
        public const string NAME = "fspec";

        ///< Constructor
        /// Constructor for the \b fspec space.
        /// There is only one such space, and it is considered
        /// internal to the model, i.e. the Translate engine should never
        /// generate addresses in this space.
        /// \param m is the associated address space manager
        /// \param t is the associated processor translator
        /// \param ind is the index associated with the space
        public FspecSpace(AddrSpaceManager m, Translate t, int ind)
            : base(m, t, IPTR_FSPEC, NAME, sizeof(void*), 1, ind, 0, 1)
        {
            clearFlags(heritaged | does_deadcode | big_endian);
            if (HOST_ENDIAN == 1)       // Endianness always set by host
                setFlags(big_endian);
        }

        public override void encodeAttributes(Encoder encoder, uintb offset)
        {
            FuncCallSpecs* fc = (FuncCallSpecs*)(uintp)offset;

            if (fc.getEntryAddress().isInvalid())
                encoder.writeString(ATTRIB_SPACE, "fspec");
            else
            {
                AddrSpace* id = fc.getEntryAddress().getSpace();
                encoder.writeSpace(ATTRIB_SPACE, id);
                encoder.writeUnsignedInteger(ATTRIB_OFFSET, fc.getEntryAddress().getOffset());
            }
        }

        public override void encodeAttributes(Encoder encoder, uintb offset, int size)
        {
            FuncCallSpecs* fc = (FuncCallSpecs*)(uintp)offset;

            if (fc.getEntryAddress().isInvalid())
                encoder.writeString(ATTRIB_SPACE, "fspec");
            else
            {
                AddrSpace* id = fc.getEntryAddress().getSpace();
                encoder.writeSpace(ATTRIB_SPACE, id);
                encoder.writeUnsignedInteger(ATTRIB_OFFSET, fc.getEntryAddress().getOffset());
                encoder.writeSignedInteger(ATTRIB_SIZE, size);
            }
        }

        public override void printRaw(TextWriter s, uintb offset)
        {
            FuncCallSpecs* fc = (FuncCallSpecs*)(uintp)offset;

            if (fc.getName().size() != 0)
                s << fc.getName();
            else
            {
                s << "func_";
                fc.getEntryAddress().printRaw(s);
            }
        }

        public override void saveXml(StreamWriter s)
        {
            throw new LowlevelError("Should never encode fspec space to stream");
        }

        public override void decode(Decoder decoder)
        {
            throw new LowlevelError("Should never decode fspec space from stream");
        }
    }
}
