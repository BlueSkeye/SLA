using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A field within a structure or union
    internal class TypeField
    {
        /// Id for identifying \b this within its containing structure or union
        public int ident;
        /// Offset (into containing structure or union) of subfield
        public int offset;
        /// Name of subfield
        public string name;
        /// Data-type of subfield
        public Datatype type;

        /// Restore \b this field from a stream
        /// Construct from a \<field> element.
        /// \param decoder is the stream decoder
        /// \param typegrp is the TypeFactory for parsing data-type info
        public TypeField(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_FIELD);
            ident = -1;
            offset = -1;
            while(true) {
                AttributeId attrib = decoder.getNextAttributeId();
                if (attrib == 0) break;
                if (attrib == AttributeId.ATTRIB_NAME)
                    name = decoder.readString();
                else if (attrib == AttributeId.ATTRIB_OFFSET) {
                    offset = (int)decoder.readSignedInteger();
                }
                else if (attrib == AttributeId.ATTRIB_ID) {
                    ident = (int)decoder.readSignedInteger();
                }
            }
            type = typegrp.decodeType(decoder);
            if (string.IsNullOrEmpty(name))
                throw new LowlevelError("name attribute must not be empty in <field> tag");
            if (offset < 0)
                throw new LowlevelError("offset attribute invalid for <field> tag");
            if (ident < 0)
                ident = offset; // By default the id is the offset
            decoder.closeElement(elemId);
        }

        /// Construct from components
        public TypeField(int id, int off,string nm,Datatype ct)
        {
            ident=id;
            offset=off;
            name=nm;
            type=ct;
        }

        /// Compare based on offset
        public static bool operator <(TypeField op1, TypeField op2)
        {
            return (op1.offset<op2.offset);
        }

        public static bool operator >(TypeField op1, TypeField op2)
        {
            return (op1.offset > op2.offset);
        }

        /// Encode \b this field to a stream
        /// Encode a formal description of \b this as a \<field> element.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_FIELD);
            encoder.writeString(AttributeId.ATTRIB_NAME, name);
            encoder.writeSignedInteger(AttributeId.ATTRIB_OFFSET, offset);
            if (ident != offset)
                encoder.writeSignedInteger(AttributeId.ATTRIB_ID, ident);
            type.encodeRef(encoder);
            encoder.closeElement(ElementId.ELEM_FIELD);
        }
    }
}
