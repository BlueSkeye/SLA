using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Sla.DECCORE
{
    /// \brief A Symbol that labels code internal to a function
    internal class LabSymbol : Symbol
    {
        /// Build placeholder data-type
        /// Label symbols don't really have a data-type, so we just put
        /// a size 1 placeholder.
        private void buildType()
        {
            type = scope.getArch().types.getBase(1, type_metatype.TYPE_UNKNOWN);
        }

        /// Construct given name
        /// \param sc is the Scope that will contain the new Symbol
        /// \param nm is the name of the new Symbol
        public LabSymbol(Scope sc, string nm)
            : base(sc)
        {
            buildType();
            name = nm;
            displayName = nm;
        }

        /// Constructor for use with decode
        /// \param sc is the Scope that will contain the new Symbol
        public LabSymbol(Scope sc)
            : base(sc)
        {
            buildType();
        }

        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_LABELSYM);
            encodeHeader(encoder);      // We never set category
            encoder.closeElement(ELEM_LABELSYM);
        }

        public override void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ELEM_LABELSYM);
            decodeHeader(decoder);
            decoder.closeElement(elemId);
        }
    }
}
