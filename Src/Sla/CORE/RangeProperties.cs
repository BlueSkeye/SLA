using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A partially parsed description of a Range
    /// Class that allows \<range> tags to be parsed, when the address space doesn't yet exist
    public class RangeProperties
    {
        // friend class Range;
        internal string spaceName;       ///< Name of the address space containing the range
        internal ulong first;            ///< Offset of first byte in the Range
        internal ulong last;         ///< Offset of last byte in the Range
        internal bool isRegister;        ///< Range is specified a  register name
        internal bool seenLast;      ///< End of the range is actively specified

        public RangeProperties()
        {
            first = 0;
            last = 0;
            isRegister = false;
            seenLast = false;
        }

        ///< Restore \b this from an XML stream
        public void decode(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement();
            if ((elemId != ElementId.ELEM_RANGE) && (elemId != ElementId.ELEM_REGISTER)) {
                throw new DecoderError("Expecting <range> or <register> element");
            }
            while(true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_SPACE) {
                    spaceName = decoder.readString();
                }
                else if (attribId == AttributeId.ATTRIB_FIRST) {
                    first = decoder.readUnsignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_LAST) {
                    last = decoder.readUnsignedInteger();
                    seenLast = true;
                }
                else if (attribId == AttributeId.ATTRIB_NAME) {
                    spaceName = decoder.readString();
                    isRegister = true;
                }
            }
            decoder.closeElement(elemId);
        }
    }
}
