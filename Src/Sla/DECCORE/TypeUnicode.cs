using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief The unicode data-type: i.e. wchar
    ///
    /// This supports encoding elements that are wider than 1-byte
    internal class TypeUnicode  : TypeBase
    {
        // Unicode character type
        /// Set unicode property flags
        /// Properties that specify which encoding this type uses are set based
        /// on the size of the data-type. I.e. select UTF8, UTF16, or UTF32
        private void setflags()
        {
            if (size == 2)
                // 16-bit UTF16 encoding of unicode character
                flags |= Properties.utf16;
            else if (size == 4)
                // 32-bit UTF32 encoding of unicode character
                flags |= Properties.utf32;
            else if (size == 1)
                // This ultimately should be UTF8 but we default to basic char
                flags |= Properties.chartype;
        }

        // friend class TypeFactory;
        /// Restore \b this unicode data-type from a stream
        /// Parse a \<type> tag for properties of the data-type.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        internal void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            //  uint elemId = decoder.openElement();
            decodeBasic(decoder);
            // Get endianness flag from architecture, rather than specific type encoding
            setflags();
            submeta = (metatype == type_metatype.TYPE_INT)
                ? sub_metatype.SUB_INT_UNICODE
                : sub_metatype.SUB_UINT_UNICODE;
            //  decoder.closeElement(elemId);
        }

        /// For use with decode
        public TypeUnicode()
            : base(0, type_metatype.TYPE_INT)
        {
        }

        /// Construct from another TypeUnicode
        public TypeUnicode(TypeUnicode op)
            : base(op)
        {
        }

        /// Construct given name,size, meta-type
        public TypeUnicode(string nm,int sz, type_metatype m)
            : base(sz, m, nm)
        {
            setflags();         // Set special unicode UTF flags
            submeta = (m == type_metatype.TYPE_INT)
                ? sub_metatype.SUB_INT_UNICODE
                : sub_metatype.SUB_UINT_UNICODE;
        }

        internal override Datatype clone() => new TypeUnicode(this);
    
        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (typedefImm != (Datatype)null) {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeBool(AttributeId.ATTRIB_UTF, true);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }
    }
}
