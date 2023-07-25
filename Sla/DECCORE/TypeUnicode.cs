using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private setflags()
        {
            if (size == 2)
                flags |= Datatype::utf16;   // 16-bit UTF16 encoding of unicode character
            else if (size == 4)
                flags |= Datatype::utf32;   // 32-bit UTF32 encoding of unicode character
            else if (size == 1)
                flags |= Datatype::chartype; // This ultimately should be UTF8 but we default to basic char
        }

        // friend class TypeFactory;
        /// Restore \b this unicode data-type from a stream
        /// Parse a \<type> tag for properties of the data-type.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        protected void decode(Decoder decoder, TypeFactory typegrp)
        {
            //  uint4 elemId = decoder.openElement();
            decodeBasic(decoder);
            // Get endianness flag from architecture, rather than specific type encoding
            setflags();
            submeta = (metatype == TYPE_INT) ? SUB_INT_UNICODE : SUB_UINT_UNICODE;
            //  decoder.closeElement(elemId);
        }

        /// For use with decode
        public TypeUnicode()
            : base(0, TYPE_INT)
        {
        }

        /// Construct from another TypeUnicode
        public TypeUnicode(TypeUnicode op)
            : base(op)
        {
        }

        /// Construct given name,size, meta-type
        public TypeUnicode(string nm,int4 sz, type_metatype m)
            : base(sz, m, nm)
        {
            setflags();         // Set special unicode UTF flags
            submeta = (m == TYPE_INT) ? SUB_INT_UNICODE : SUB_UINT_UNICODE;
        }

        public override Datatype clone() => new TypeUnicode(this);
    
        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype*)0)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeBool(ATTRIB_UTF, true);
            encoder.closeElement(ELEM_TYPE);
        }
    }
}
