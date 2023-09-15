using Sla.CORE;
using Sla.EXTRA;

namespace Sla.DECCORE
{
    /// \brief A Symbol representing an executable function
    /// This Symbol owns the Funcdata object for the function it represents. The formal
    /// Symbol is thus associated with all the meta-data about the function.
    internal class FunctionSymbol : Symbol
    {
        /// The underlying meta-data object for the function
        private Funcdata fd;
        /// Minimum number of bytes to consume with the start address
        private int consumeSize;
        
        ~FunctionSymbol()
        {
            //if (fd != (Funcdata)null)
            //    delete fd;
        }


        /// Build the data-type associated with \b this Symbol
        private void buildType()
        {
            TypeFactory* types = scope.getArch().types;
            type = types.getTypeCode();
            flags |= Varnode.varnode_flags.namelock | Varnode.varnode_flags.typelock;
        }

        /// Construct given the name
        /// Build a function \e shell, made up of just the name of the function and
        /// a placeholder data-type, without the underlying Funcdata object.
        /// A SymbolEntry for a function has a small size starting at the entry address,
        /// in order to deal with non-contiguous functions.
        /// We need a size (slightly) larger than 1 to accommodate pointer constants that encode
        /// extra information in the lower bit(s) of an otherwise aligned pointer.
        /// If the encoding is not initially detected, it is interpreted
        /// as a straight address that comes up 1 (or more) bytes off of the start of the function
        /// In order to detect this, we need to lay down a slightly larger size than 1
        /// \param sc is the Scope that will contain the new Symbol
        /// \param nm is the name of the new Symbol
        /// \param size is the number of bytes a SymbolEntry should consume
        public FunctionSymbol(Scope sc, string nm, int size)
            : base(sc)
        {
            fd = null;
            consumeSize = size;
            buildType();
            name = nm;
            displayName = nm;
        }

        /// Constructor for use with decode
        public FunctionSymbol(Scope sc, int size)
            : base(sc)
        {
            fd = (Funcdata)null;
            consumeSize = size;
            buildType();
        }

        /// Get the underlying Funcdata object
        public Funcdata getFunction()
        {
            if (fd != (Funcdata)null) return fd;
            SymbolEntry* entry = getFirstWholeMap();
            fd = new Funcdata(name, displayName, scope, entry.getAddr(), this);
            return fd;
        }

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (fd != (Funcdata)null)
                fd.encode(encoder, symbolId, false);   // Save the function itself
            else
            {
                encoder.openElement(ElementId.ELEM_FUNCTIONSHELL);
                encoder.writeString(AttributeId.ATTRIB_NAME, name);
                if (symbolId != 0)
                    encoder.writeUnsignedInteger(AttributeId.ATTRIB_ID, symbolId);
                encoder.closeElement(ElementId.ELEM_FUNCTIONSHELL);
            }
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.peekElement();
            if (elemId == ElementId.ELEM_FUNCTION) {
                fd = new Funcdata("", "", scope, new Address(), this);
                try {
                    symbolId = fd.decode(decoder);
                }
                catch (RecovError err) {
                    // Caused by a duplicate scope name. Preserve the address so we can find the original symbol
                    throw new DuplicateFunctionError(fd.getAddress(), fd.getName());
                }
                name = fd.getName();
                displayName = fd.getDisplayName();
                if (consumeSize < fd.getSize())
                {
                    if ((fd.getSize() > 1) && (fd.getSize() <= 8))
                        consumeSize = fd.getSize();
                }
            }
            else
            {           // functionshell
                decoder.openElement();
                symbolId = 0;
                while(true) {
                    uint attribId = decoder.getNextAttributeId();
                    if (attribId == 0) break;
                    if (attribId == AttributeId.ATTRIB_NAME)
                        name = decoder.readString();
                    else if (attribId == AttributeId.ATTRIB_ID) {
                        symbolId = decoder.readUnsignedInteger();
                    }
                    else if (attribId == AttributeId.ATTRIB_LABEL) {
                        displayName = decoder.readString();
                    }
                }
                decoder.closeElement(elemId);
            }
        }

        public override int getBytesConsumed() => consumeSize;
    }
}
