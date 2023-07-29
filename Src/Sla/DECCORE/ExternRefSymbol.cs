using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Sla.DECCORE
{
    /// \brief A function Symbol referring to an external location
    ///
    /// This Symbol is intended to label functions that have not been mapped directly into
    /// the image being analyzed. It holds a level of indirection between the address the
    /// image expects the symbol to be at and a \b placeholder address the system hangs
    /// meta-data on.
    internal class ExternRefSymbol : Symbol
    {
        /// The \e placeholder address for meta-data
        private Address refaddr;

        /// Create a name and data-type for the Symbol
        /// Build name, type, and flags based on the placeholder address
        private void buildNameType()
        {
            TypeFactory* typegrp = scope.getArch().types;
            type = typegrp.getTypeCode();
            type = typegrp.getTypePointer(refaddr.getAddrSize(), type, refaddr.getSpace().getWordSize());
            if (name.size() == 0)
            {   // If a name was not already provided
                ostringstream s;        // Give the reference a unique name
                s << refaddr.getShortcut();
                refaddr.printRaw(s);
                name = s.str();
                name += "_exref"; // Indicate this is an external reference variable
            }
            if (displayName.size() == 0)
                displayName = name;
            flags |= Varnode::externref | Varnode::typelock;
        }

        ~ExternRefSymbol()
        {
        }

        /// Construct given a \e placeholder address
        /// \param sc is the Scope containing the Symbol
        /// \param ref is the placeholder address where the system will hold meta-data
        /// \param nm is the name of the Symbol
        public ExternRefSymbol(Scope sc, Address @ref, string nm)
            : base(sc, nm, null)
        {
            refaddr = @ref;
            buildNameType();
        }

        /// For use with decode
        public ExternRefSymbol(Scope sc)
            : base(sc)
        {
        }

        ///< Return the \e placeholder address
        public Address getRefAddr() => refaddr;

        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_EXTERNREFSYMBOL);
            encoder.writeString(ATTRIB_NAME, name);
            refaddr.encode(encoder);
            encoder.closeElement(ELEM_EXTERNREFSYMBOL);
        }

        public override void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ELEM_EXTERNREFSYMBOL);
            name.clear();           // Name is empty
            displayName.clear();
            for (; ; )
            {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_NAME) // Unless we see it explicitly
                    name = decoder.readString();
                else if (attribId == ATTRIB_LABEL)
                    displayName = decoder.readString();
            }
            refaddr = Address::decode(decoder);
            decoder.closeElement(elemId);
            buildNameType();
        }
    }
}
