using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Special Datatype object used to describe pointers that index into the symbol table
    /// A TypeSpacebase treats a specific AddrSpace as "structure" that will get indexed in to.
    /// This facilitates type propagation from local symbols into the stack space and
    /// from global symbols into the RAM space.
    internal class TypeSpacebase : Datatype
    {
        // friend class TypeFactory;
        /// The address space we are treating as a structure
        private AddrSpace spaceid;
        /// Address of function whose symbol table is indexed (or INVALID for "global")
        private Address localframe;
        /// Architecture for accessing symbol table
        private Architecture glb;

        ///< Restore \b this spacebase data-type from a stream
        /// Parse the \<type> tag.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        internal void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            //  uint elemId = decoder.openElement();
            decodeBasic(decoder);
            spaceid = decoder.readSpace(AttributeId.ATTRIB_SPACE);
            localframe = Address.decode(decoder);
            //  decoder.closeElement(elemId);
        }

        /// Construct from another TypeSpacebase
        public TypeSpacebase(TypeSpacebase op)
            : base(op)
        {
            spaceid = op.spaceid; localframe = op.localframe; glb = op.glb;
        }

        /// Construct given an address space, scope, and architecture
        public TypeSpacebase(AddrSpace id, Address frame, Architecture g)
            : base(0, type_metatype.TYPE_SPACEBASE)
        {
            localframe = frame;
            spaceid = id;
            glb = g;
        }

        /// Get the symbol table indexed by \b this
        /// This data-type can index either a local or the global scope
        /// \return the symbol table Scope
        public Scope getMap()
        {
            Scope* res = glb.symboltab.getGlobalScope();
            if (!localframe.isInvalid())
            { // If this spacebase is for a localframe
                Funcdata* fd = res.queryFunction(localframe);
                if (fd != (Funcdata)null)
                    res = fd.getScopeLocal();
            }
            return res;
        }

        /// Construct an Address given an offset
        /// Return the Address being referred to by a specific offset relative
        /// to a pointer with \b this Datatype
        /// \param off is the offset relative to the pointer
        /// \param sz is the size of offset (as a pointer)
        /// \param point is a "context" reference for the request
        /// \return the referred to Address
        public Address getAddress(ulong off, int sz, Address point)
        {
            ulong fullEncoding;
            // Currently a constant off of a global spacebase must be a full pointer encoding
            if (localframe.isInvalid())
                sz = -1;    // Set size to -1 to guarantee that full encoding recovery isn't launched
            return glb.resolveConstant(spaceid, off, sz, point, fullEncoding);
        }

        public override Datatype getSubType(ulong off, ulong newoff)
        {
            Scope* scope = getMap();
            off = AddrSpace.byteToAddress(off, spaceid.getWordSize());    // Convert from byte offset to address unit
                                                                            // It should always be the case that the given offset represents a full encoding of the
                                                                            // pointer, so the point of context is unused and the size is given as -1
            Address nullPoint;
            ulong fullEncoding;
            Address addr = glb.resolveConstant(spaceid, off, -1, nullPoint, fullEncoding);
            SymbolEntry* smallest;

            // Assume symbol being referenced is address tied so we use a null point of context
            // FIXME: A valid point of context may be necessary in the future
            smallest = scope.queryContainer(addr, 1, nullPoint);

            if (smallest == (SymbolEntry)null)
            {
                *newoff = 0;
                return glb.types.getBase(1, type_metatype.TYPE_UNKNOWN);
            }
            *newoff = (addr.getOffset() - smallest.getAddr().getOffset()) + smallest.getOffset();
            return smallest.getSymbol().getType();
        }

        public override Datatype nearestArrayedComponentForward(ulong off, ulong newoff, int elSize)
        {
            Scope* scope = getMap();
            off = AddrSpace.byteToAddress(off, spaceid.getWordSize());    // Convert from byte offset to address unit
                                                                            // It should always be the case that the given offset represents a full encoding of the
                                                                            // pointer, so the point of context is unused and the size is given as -1
            Address nullPoint;
            ulong fullEncoding;
            Address addr = glb.resolveConstant(spaceid, off, -1, nullPoint, fullEncoding);
            SymbolEntry* smallest = scope.queryContainer(addr, 1, nullPoint);
            Address nextAddr;
            Datatype* symbolType;
            if (smallest == (SymbolEntry)null || smallest.getOffset() != 0)
                nextAddr = addr + 32;
            else
            {
                symbolType = smallest.getSymbol().getType();
                if (symbolType.getMetatype() == type_metatype.TYPE_STRUCT)
                {
                    ulong structOff = addr.getOffset() - smallest.getAddr().getOffset();
                    ulong dummyOff;
                    Datatype* res = symbolType.nearestArrayedComponentForward(structOff, &dummyOff, elSize);
                    if (res != (Datatype)null)
                    {
                        *newoff = structOff;
                        return symbolType;
                    }
                }
                int sz = AddrSpace::byteToAddressInt(smallest.getSize(), spaceid.getWordSize());
                nextAddr = smallest.getAddr() + sz;
            }
            if (nextAddr < addr)
                return (Datatype)null;        // Don't let the address wrap
            smallest = scope.queryContainer(nextAddr, 1, nullPoint);
            if (smallest == (SymbolEntry)null || smallest.getOffset() != 0)
                return (Datatype)null;
            symbolType = smallest.getSymbol().getType();
            *newoff = addr.getOffset() - smallest.getAddr().getOffset();
            if (symbolType.getMetatype() == type_metatype.TYPE_ARRAY)
            {
                *elSize = ((TypeArray*)symbolType).getBase().getSize();
                return symbolType;
            }
            if (symbolType.getMetatype() == type_metatype.TYPE_STRUCT)
            {
                ulong dummyOff;
                Datatype* res = symbolType.nearestArrayedComponentForward(0, &dummyOff, elSize);
                if (res != (Datatype)null)
                    return symbolType;
            }
            return (Datatype)null;
        }

        public override Datatype nearestArrayedComponentBackward(ulong off, ulong newoff, int elSize)
        {
            Datatype* subType = getSubType(off, newoff);
            if (subType == (Datatype)null)
                return (Datatype)null;
            if (subType.getMetatype() == type_metatype.TYPE_ARRAY)
            {
                *elSize = ((TypeArray*)subType).getBase().getSize();
                return subType;
            }
            if (subType.getMetatype() == type_metatype.TYPE_STRUCT)
            {
                ulong dummyOff;
                Datatype* res = subType.nearestArrayedComponentBackward(*newoff, &dummyOff, elSize);
                if (res != (Datatype)null)
                    return subType;
            }
            return (Datatype)null;
        }

        public override int compare(Datatype op,int level) => compareDependency(op);

        // For tree structure
        public override int compareDependency(Datatype op)
        {
            int res = Datatype::compareDependency(op);
            if (res != 0) return res;
            TypeSpacebase* tsb = (TypeSpacebase)&op;
            if (spaceid != tsb.spaceid) return (spaceid < tsb.spaceid) ? -1 : 1;
            if (localframe.isInvalid()) return 0; // Global space base
            if (localframe != tsb.localframe) return (localframe < tsb.localframe) ? -1 : 1;
            return 0;
        }

        public override Datatype clone() => new TypeSpacebase(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (typedefImm != (Datatype)null)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeSpace(AttributeId.ATTRIB_SPACE, spaceid);
            localframe.encode(encoder);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }
    }
}
