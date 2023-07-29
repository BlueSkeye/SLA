﻿using System;
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
        private void decode(Decoder decoder, TypeFactory typegrp)
        {
            //  uint4 elemId = decoder.openElement();
            decodeBasic(decoder);
            spaceid = decoder.readSpace(ATTRIB_SPACE);
            localframe = Address::decode(decoder);
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
            : base(0, TYPE_SPACEBASE)
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
            Scope* res = glb->symboltab->getGlobalScope();
            if (!localframe.isInvalid())
            { // If this spacebase is for a localframe
                Funcdata* fd = res->queryFunction(localframe);
                if (fd != (Funcdata*)0)
                    res = fd->getScopeLocal();
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
        public Address getAddress(uintb off, int4 sz, Address point)
        {
            uintb fullEncoding;
            // Currently a constant off of a global spacebase must be a full pointer encoding
            if (localframe.isInvalid())
                sz = -1;    // Set size to -1 to guarantee that full encoding recovery isn't launched
            return glb->resolveConstant(spaceid, off, sz, point, fullEncoding);
        }

        public override Datatype getSubType(uintb off, uintb newoff)
        {
            Scope* scope = getMap();
            off = AddrSpace::byteToAddress(off, spaceid->getWordSize());    // Convert from byte offset to address unit
                                                                            // It should always be the case that the given offset represents a full encoding of the
                                                                            // pointer, so the point of context is unused and the size is given as -1
            Address nullPoint;
            uintb fullEncoding;
            Address addr = glb->resolveConstant(spaceid, off, -1, nullPoint, fullEncoding);
            SymbolEntry* smallest;

            // Assume symbol being referenced is address tied so we use a null point of context
            // FIXME: A valid point of context may be necessary in the future
            smallest = scope->queryContainer(addr, 1, nullPoint);

            if (smallest == (SymbolEntry*)0)
            {
                *newoff = 0;
                return glb->types->getBase(1, TYPE_UNKNOWN);
            }
            *newoff = (addr.getOffset() - smallest->getAddr().getOffset()) + smallest->getOffset();
            return smallest->getSymbol()->getType();
        }

        public override Datatype nearestArrayedComponentForward(uintb off, uintb newoff, int4 elSize)
        {
            Scope* scope = getMap();
            off = AddrSpace::byteToAddress(off, spaceid->getWordSize());    // Convert from byte offset to address unit
                                                                            // It should always be the case that the given offset represents a full encoding of the
                                                                            // pointer, so the point of context is unused and the size is given as -1
            Address nullPoint;
            uintb fullEncoding;
            Address addr = glb->resolveConstant(spaceid, off, -1, nullPoint, fullEncoding);
            SymbolEntry* smallest = scope->queryContainer(addr, 1, nullPoint);
            Address nextAddr;
            Datatype* symbolType;
            if (smallest == (SymbolEntry*)0 || smallest->getOffset() != 0)
                nextAddr = addr + 32;
            else
            {
                symbolType = smallest->getSymbol()->getType();
                if (symbolType->getMetatype() == TYPE_STRUCT)
                {
                    uintb structOff = addr.getOffset() - smallest->getAddr().getOffset();
                    uintb dummyOff;
                    Datatype* res = symbolType->nearestArrayedComponentForward(structOff, &dummyOff, elSize);
                    if (res != (Datatype*)0)
                    {
                        *newoff = structOff;
                        return symbolType;
                    }
                }
                int4 sz = AddrSpace::byteToAddressInt(smallest->getSize(), spaceid->getWordSize());
                nextAddr = smallest->getAddr() + sz;
            }
            if (nextAddr < addr)
                return (Datatype*)0;        // Don't let the address wrap
            smallest = scope->queryContainer(nextAddr, 1, nullPoint);
            if (smallest == (SymbolEntry*)0 || smallest->getOffset() != 0)
                return (Datatype*)0;
            symbolType = smallest->getSymbol()->getType();
            *newoff = addr.getOffset() - smallest->getAddr().getOffset();
            if (symbolType->getMetatype() == TYPE_ARRAY)
            {
                *elSize = ((TypeArray*)symbolType)->getBase()->getSize();
                return symbolType;
            }
            if (symbolType->getMetatype() == TYPE_STRUCT)
            {
                uintb dummyOff;
                Datatype* res = symbolType->nearestArrayedComponentForward(0, &dummyOff, elSize);
                if (res != (Datatype*)0)
                    return symbolType;
            }
            return (Datatype*)0;
        }

        public override Datatype nearestArrayedComponentBackward(uintb off, uintb newoff, int4 elSize)
        {
            Datatype* subType = getSubType(off, newoff);
            if (subType == (Datatype*)0)
                return (Datatype*)0;
            if (subType->getMetatype() == TYPE_ARRAY)
            {
                *elSize = ((TypeArray*)subType)->getBase()->getSize();
                return subType;
            }
            if (subType->getMetatype() == TYPE_STRUCT)
            {
                uintb dummyOff;
                Datatype* res = subType->nearestArrayedComponentBackward(*newoff, &dummyOff, elSize);
                if (res != (Datatype*)0)
                    return subType;
            }
            return (Datatype*)0;
        }

        public override int4 compare(Datatype op,int4 level) => compareDependency(op);

        // For tree structure
        public override int4 compareDependency(Datatype op)
        {
            int4 res = Datatype::compareDependency(op);
            if (res != 0) return res;
            TypeSpacebase* tsb = (TypeSpacebase*)&op;
            if (spaceid != tsb->spaceid) return (spaceid < tsb->spaceid) ? -1 : 1;
            if (localframe.isInvalid()) return 0; // Global space base
            if (localframe != tsb->localframe) return (localframe < tsb->localframe) ? -1 : 1;
            return 0;
        }

        public override Datatype clone() => new TypeSpacebase(this);

        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype*)0)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeSpace(ATTRIB_SPACE, spaceid);
            localframe.encode(encoder);
            encoder.closeElement(ELEM_TYPE);
        }
    }
}