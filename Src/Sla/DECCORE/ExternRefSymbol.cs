﻿using Sla.CORE;

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
            TypeFactory typegrp = scope.getArch().types ?? throw new ApplicationException();
            type = typegrp.getTypeCode();
            type = typegrp.getTypePointer(refaddr.getAddrSize(), type, refaddr.getSpace().getWordSize());
            if (name.Length == 0) {
                // If a name was not already provided
                TextWriter s = new StringWriter();        // Give the reference a unique name
                s.Write(refaddr.getShortcut());
                refaddr.printRaw(s);
                name = s.ToString();
                name += "_exref"; // Indicate this is an external reference variable
            }
            if (displayName.Length == 0)
                displayName = name;
            flags |= Varnode.varnode_flags.externref | Varnode.varnode_flags.typelock;
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

        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_EXTERNREFSYMBOL);
            encoder.writeString(AttributeId.ATTRIB_NAME, name);
            refaddr.encode(encoder);
            encoder.closeElement(ElementId.ELEM_EXTERNREFSYMBOL);
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_EXTERNREFSYMBOL);
            name = string.Empty;           // Name is empty
            displayName = string.Empty;
            while(true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_NAME) // Unless we see it explicitly
                    name = decoder.readString();
                else if (attribId == AttributeId.ATTRIB_LABEL)
                    displayName = decoder.readString();
            }
            refaddr = Address.decode(decoder);
            decoder.closeElement(elemId);
            buildNameType();
        }
    }
}
