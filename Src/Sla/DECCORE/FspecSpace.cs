﻿using Sla.CORE;

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
            : base(m, t, spacetype.IPTR_FSPEC, NAME, (uint)IntPtr.Size, 1, ind, 0, 1)
        {
            clearFlags(Properties.heritaged | Properties.does_deadcode | Properties.big_endian);
            if (Globals.HOST_ENDIAN == 1)       // Endianness always set by host
                setFlags(Properties.big_endian);
        }

        public override void encodeAttributes(Encoder encoder, ulong offset)
        {
            FuncCallSpecs fc = (FuncCallSpecs)offset;

            if (fc.getEntryAddress().isInvalid())
                encoder.writeString(AttributeId.ATTRIB_SPACE, "fspec");
            else {
                AddrSpace id = fc.getEntryAddress().getSpace();
                encoder.writeSpace(AttributeId.ATTRIB_SPACE, id);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_OFFSET, fc.getEntryAddress().getOffset());
            }
        }

        public override void encodeAttributes(Encoder encoder, ulong offset, int size)
        {
            FuncCallSpecs fc = (FuncCallSpecs)offset;

            if (fc.getEntryAddress().isInvalid()) {
                encoder.writeString(AttributeId.ATTRIB_SPACE, "fspec");
            }
            else {
                AddrSpace id = fc.getEntryAddress().getSpace();
                encoder.writeSpace(AttributeId.ATTRIB_SPACE, id);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_OFFSET, fc.getEntryAddress().getOffset());
                encoder.writeSignedInteger(AttributeId.ATTRIB_SIZE, size);
            }
        }

        public override void printRaw(TextWriter s, ulong offset)
        {
            FuncCallSpecs fc = (FuncCallSpecs)offset;

            if (fc.getName().Length != 0)
                s.Write(fc.getName());
            else {
                s.Write("func_");
                fc.getEntryAddress().printRaw(s);
            }
        }

        public override void saveXml(TextWriter s)
        {
            throw new LowlevelError("Should never encode fspec space to stream");
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
            throw new LowlevelError("Should never decode fspec space from stream");
        }
    }
}
