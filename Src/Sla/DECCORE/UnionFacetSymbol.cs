using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Sla.DECCORE
{
    /// \brief A Symbol that forces a particular \e union field at a particular point in the body of a function
    ///
    /// This is an internal Symbol that users can create if they want to force a particular interpretation of a
    /// a \e union data-type.  It attaches to data-flow via the DynamicHash mechanism, which also allows it to attach
    /// to a specific read or write of the target Varnode.  Different reads (or write) of the same Varnode can have
    /// different symbols attached.  The Symbol's associated data-type will be the desired \e union to force.
    internal class UnionFacetSymbol : Symbol
    {
        /// Particular field to associate with Symbol access
        private int fieldNum;

        /// Constructor from components
        /// Create a symbol that forces a particular field of a union to propagate
        ///
        /// \param sc is the scope owning the new symbol
        /// \param nm is the name of the symbol
        /// \param unionDt is the union data-type being forced
        /// \param fldNum is the particular field to force (-1 indicates the whole union)
        public UnionFacetSymbol(Scope  sc, string nm, Datatype unionDt, int fldNum)
        {
            fieldNum = fldNum;
            category = union_facet;
        }

        /// Constructor for decode
        public UnionFacetSymbol(Scope sc)
            : base(sc)
        {
            fieldNum = -1;
            category = union_facet;
        }

        /// Get the particular field associate with \b this
        public int getFieldNumber() => fieldNum;
    
        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_FACETSYMBOL);
            encodeHeader(encoder);
            encoder.writeSignedInteger(ATTRIB_FIELD, fieldNum);
            encodeBody(encoder);
            encoder.closeElement(ELEM_FACETSYMBOL);
        }

        public override void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ELEM_FACETSYMBOL);
            decodeHeader(decoder);
            fieldNum = decoder.readSignedInteger(ATTRIB_FIELD);

            decodeBody(decoder);
            decoder.closeElement(elemId);
            Datatype* testType = type;
            if (testType.getMetatype() == TYPE_PTR)
                testType = ((TypePointer*)testType).getPtrTo();
            if (testType.getMetatype() != TYPE_UNION)
                throw new LowlevelError("<unionfacetsymbol> does not have a union type");
            if (fieldNum < -1 || fieldNum >= testType.numDepend())
                throw new LowlevelError("<unionfacetsymbol> field attribute is out of bounds");
        }
    }
}
