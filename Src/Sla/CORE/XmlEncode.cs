using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief An XML based encoder
    /// The underlying transfer encoding is an XML document.  The encoder is initialized with a stream which will
    /// receive the XML document as calls are made on the encoder.
    public class XmlEncode : Encoder
    {
        // friend class XmlDecode;
        /// The stream receiving the encoded data
        private TextWriter outStream;

        ///< If \b true, new attributes can be written to the current element
        private bool elementTagIsOpen;

        /// Construct from a stream
        public XmlEncode(TextWriter s)
        {
            outStream = s;
            elementTagIsOpen = false;
        }

        public override void openElement(ElementId elemId)
        {
            if (elementTagIsOpen) {
                outStream.Write('>');
            }
            else {
                elementTagIsOpen = true;
            }
            outStream.Write('<');
            outStream.Write(elemId.getName());
        }

        public override void closeElement(ElementId elemId)
        {
            if (elementTagIsOpen) {
                outStream.Write("/>");
                elementTagIsOpen = false;
            }
            else {
                outStream.Write($"</{elemId.getName()}>");
            }
        }

        public override void writeBool(AttributeId attribId, bool val)
        {
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                // Special id indicating, text value
                if (elementTagIsOpen) {
                    outStream.Write('>');
                    elementTagIsOpen = false;
                }
                outStream.Write(val ? "true" : "false");
                return;
            }
            Xml.a_v_b(outStream, attribId.getName(), val);
        }

        public override void writeSignedInteger(AttributeId attribId, long val)
        {
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                // Special id indicating, text value
                if (elementTagIsOpen) {
                    outStream.Write('>');
                    elementTagIsOpen = false;
                }
                outStream.Write(val);
                return;
            }
            Xml.a_v_i(outStream, attribId.getName(), val);
        }

        public override void writeUnsignedInteger(AttributeId attribId, ulong val)
        {
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                // Special id indicating, text value
                if (elementTagIsOpen) {
                    outStream.Write('>');
                    elementTagIsOpen = false;
                }
                outStream.Write("0x{0:X}", val);
                return;
            }
            Xml.a_v_u(outStream, attribId.getName(), val);
        }

        public override void writeString(AttributeId attribId, string val)
        {
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                // Special id indicating, text value
                if (elementTagIsOpen) {
                    outStream.Write('>');
                    elementTagIsOpen = false;
                }
                Xml.xml_escape(outStream, val);
                return;
            }
            Xml.a_v(outStream, attribId.getName(), val);
        }

        public override void writeStringIndexed(AttributeId attribId, uint index, string val)
        {
            outStream.Write(' ');
            outStream.Write(attribId.getName());
            outStream.Write(index + 1);
            outStream.Write("=\"");
            Xml.xml_escape(outStream, val);
            outStream.Write("\"");
        }

        public override void writeSpace(AttributeId attribId, AddrSpace spc)
        {
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                // Special id indicating, text value
                if (elementTagIsOpen) {
                    outStream.Write('>');
                    elementTagIsOpen = false;
                }
                Xml.xml_escape(outStream, spc.getName());
                return;
            }
            Xml.a_v(outStream, attribId.getName(), spc.getName());
        }
    }
}
