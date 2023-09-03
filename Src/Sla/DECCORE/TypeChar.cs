using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Base type for character data-types: i.e. char
    ///
    /// This is always presumed to be UTF-8 encoded
    internal class TypeChar : TypeBase
    {
        // friend class TypeFactory;
        /// Restore \b this char data-type from a stream
        /// Parse a \<type> element for attributes of the character data-type
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        internal void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            //  uint elemId = decoder.openElement();
            decodeBasic(decoder);
            submeta = (metatype == type_metatype.TYPE_INT)
                ? sub_metatype.SUB_INT_CHAR
                : sub_metatype.SUB_UINT_CHAR;
            //  decoder.closeElement(elemId);
        }

        /// Construct TypeChar copying properties from another data-type
        public TypeChar(TypeChar op)
            : base(op)
        {
            flags |= Datatype.Properties.chartype;
        }
        
        /// Construct a char (always 1-byte) given a name
        public TypeChar(string n)
            : base(1, type_metatype.TYPE_INT, n)
        {
            flags |= Datatype.Properties.chartype;
            submeta = sub_metatype.SUB_INT_CHAR;
        }

        internal override Datatype clone() => new TypeChar(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (typedefImm != (Datatype)null) {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeBool(AttributeId.ATTRIB_CHAR, true);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }
    }
}
