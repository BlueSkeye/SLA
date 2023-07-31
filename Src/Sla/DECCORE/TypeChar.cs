using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        protected void decode(Decoder decoder, TypeFactory typegrp)
        {
            //  uint elemId = decoder.openElement();
            decodeBasic(decoder);
            submeta = (metatype == type_metatype.TYPE_INT) ? SUB_INT_CHAR : SUB_UINT_CHAR;
            //  decoder.closeElement(elemId);
        }

        /// Construct TypeChar copying properties from another data-type
        public TypeChar(TypeChar op)
                  : base(op)
        {
            flags |= Datatype::chartype;
        }
        
        /// Construct a char (always 1-byte) given a name
        public TypeChar(string n)
            : base(1, type_metatype.TYPE_INT, n)
        {
            flags |= Datatype::chartype;
            submeta = SUB_INT_CHAR;
        }

        public override Datatype clone() => new TypeChar(this);

        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype)null)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeBool(ATTRIB_CHAR, true);
            encoder.closeElement(ELEM_TYPE);
        }
    }
}
