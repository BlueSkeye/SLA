﻿using ghidra;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Datatype object representing a pointer
    internal class TypePointer : Datatype
    {
        // friend class TypeFactory;
        /// Type being pointed to
        protected Datatype ptrto;
        /// If non-null, the address space \b this is intented to point into
        protected AddrSpace spaceid;
        /// What size unit does the pointer address
        protected uint4 wordsize;

        /// Restore \b this pointer data-type from a stream
        /// Parse a \<type> element with a child describing the data-type being pointed to
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        protected void decode(Decoder decoder, TypeFactory typegrp)
        {
            //  uint4 elemId = decoder.openElement();
            decodeBasic(decoder); ;
            decoder.rewindAttributes();
            for (; ; )
            {
                uint4 attrib = decoder.getNextAttributeId();
                if (attrib == 0) break;
                if (attrib == ATTRIB_WORDSIZE)
                {
                    wordsize = decoder.readUnsignedInteger();
                }
                else if (attrib == ATTRIB_SPACE)
                {
                    spaceid = decoder.readSpace();
                }
            }
            ptrto = typegrp.decodeType(decoder);
            calcSubmeta();
            if (name.size() == 0)       // Inherit only if no name
                flags |= ptrto->getInheritable();
            //  decoder.closeElement(elemId);
        }

        /// Calculate specific submeta for \b this pointer
        /// Pointers to structures may require a specific \b submeta
        protected void calcSubmeta()
        {
            type_metatype ptrtoMeta = ptrto->getMetatype();
            if (ptrtoMeta == TYPE_STRUCT)
            {
                if (ptrto->numDepend() > 1 || ptrto->isIncomplete())
                    submeta = SUB_PTR_STRUCT;
                else
                    submeta = SUB_PTR;
            }
            else if (ptrtoMeta == TYPE_UNION)
            {
                submeta = SUB_PTR_STRUCT;
            }
            if (ptrto->needsResolution() && ptrtoMeta != TYPE_PTR)
                flags |= needs_resolution;      // Inherit needs_resolution, but only if not a pointer
        }

        /// Internal constructor for use with decode
        protected TypePointer()
            : base(0, TYPE_PTR)
        {
            ptrto = (Datatype*)0;
            wordsize = 1;
            spaceid = (AddrSpace*)0;
        }
        
        /// Construct from another TypePointer
        public TypePointer(TypePointer op)
            : base(op)
        {
            ptrto = op.ptrto;
            wordsize = op.wordsize;
            spaceid = op.spaceid;
        }

        /// Construct from a size, pointed-to type, and wordsize
        public TypePointer(int4 s, Datatype pt, uint4 ws)
            : base(s, TYPE_PTR)
        {
            ptrto = pt;
            flags = ptrto->getInheritable();
            wordsize = ws;
            spaceid = (AddrSpace*)0;
            calcSubmeta();
        }

        /// Construct from a pointed-to type and an address space attribute
        public TypePointer(Datatype pt, AddrSpace spc)
            : base(spc->getAddrSize(), TYPE_PTR)
        {
            ptrto = pt;
            flags = ptrto->getInheritable();
            spaceid = spc;
            wordsize = spc->getWordSize();
            calcSubmeta();
        }

        /// Get the pointed-to Datatype
        public Datatype getPtrTo() => ptrto;

        /// Get the size of the addressable unit being pointed to
        public uint4 getWordSize() => wordsize;

        /// Get any address space associated with \b this pointer
        public AddrSpace getSpace() => spaceid;

        public override void printRaw(TextWriter s)
        {
            ptrto->printRaw(s);
            s << " *";
            if (spaceid != (AddrSpace*)0)
            {
                s << '(' << spaceid->getName() << ')';
            }
        }

        public override int4 numDepend() => 1;

        public override Datatype getDepend(int4 index) => ptrto;

        public override void printNameBase(TextWriter s) 
        {
            s << 'p';
            ptrto->printNameBase(s);
        }

        public override int4 compare(Datatype op, int4 level)
        {
            int4 res = Datatype::compare(op, level);
            if (res != 0) return res;
            // Both must be pointers
            TypePointer* tp = (TypePointer*)&op;
            if (wordsize != tp->wordsize) return (wordsize < tp->wordsize) ? -1 : 1;
            if (spaceid != tp->spaceid)
            {
                if (spaceid == (AddrSpace*)0) return 1; // Pointers with address space come earlier
                if (tp->spaceid == (AddrSpace*)0) return -1;
                return (spaceid->getIndex() < tp->spaceid->getIndex()) ? -1 : 1;
            }
            level -= 1;
            if (level < 0)
            {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            return ptrto->compare(*tp->ptrto, level); // Compare whats pointed to
        }

        public override int4 compareDependency(Datatype op)
        {
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypePointer* tp = (TypePointer*)&op;    // Both must be pointers
            if (ptrto != tp->ptrto) return (ptrto < tp->ptrto) ? -1 : 1;    // Compare absolute pointers
            if (wordsize != tp->wordsize) return (wordsize < tp->wordsize) ? -1 : 1;
            if (spaceid != tp->spaceid)
            {
                if (spaceid == (AddrSpace*)0) return 1; // Pointers with address space come earlier
                if (tp->spaceid == (AddrSpace*)0) return -1;
                return (spaceid->getIndex() < tp->spaceid->getIndex()) ? -1 : 1;
            }
            return (op.getSize() - size);
        }

        public override Datatype clone() => new TypePointer(this);

        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype*)0)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            if (wordsize != 1)
                encoder.writeUnsignedInteger(ATTRIB_WORDSIZE, wordsize);
            if (spaceid != (AddrSpace*)0)
                encoder.writeSpace(ATTRIB_SPACE, spaceid);
            ptrto->encodeRef(encoder);
            encoder.closeElement(ELEM_TYPE);
        }

        /// \brief Find a sub-type pointer given an offset into \b this
        ///
        /// Add a constant offset to \b this pointer.
        /// If there is a valid component at that offset, return a pointer
        /// to the data-type of the component or NULL otherwise.
        /// This routine only goes down one level at most. Pass back the
        /// renormalized offset relative to the new data-type.  If \b this is
        /// a pointer to (into) a container, the data-type of the container is passed back,
        /// with the offset into the container.
        /// \param off is a reference to the offset to add
        /// \param par is used to pass back the container
        /// \param parOff is used to pass back the offset into the container
        /// \param allowArrayWrap is \b true if the pointer should be treated as a pointer to an array
        /// \param typegrp is the factory producing the (possibly new) data-type
        /// \return a pointer datatype for the component or NULL
        public override TypePointer downChain(uintb off, TypePointer par, uintb parOff, bool allowArrayWrap,
            TypeFactory typegrp)
        {
            int4 ptrtoSize = ptrto->getSize();
            if (off >= ptrtoSize)
            {   // Check if we are wrapping
                if (ptrtoSize != 0 && !ptrto->isVariableLength())
                {   // Check if pointed-to is wrappable
                    if (!allowArrayWrap)
                        return (TypePointer*)0;
                    intb signOff = (intb)off;
                    sign_extend(signOff, size * 8 - 1);
                    signOff = signOff % ptrtoSize;
                    if (signOff < 0)
                        signOff = signOff + ptrtoSize;
                    off = signOff;
                    if (off == 0)       // If we've wrapped and are now at zero
                        return this;        // consider this going down one level
                }
            }

            type_metatype meta = ptrto->getMetatype();
            bool isArray = (meta == TYPE_ARRAY);
            if (isArray || meta == TYPE_STRUCT)
            {
                par = this;
                parOff = off;
            }

            Datatype* pt = ptrto->getSubType(off, &off);
            if (pt == (Datatype*)0)
                return (TypePointer*)0;
            if (!isArray)
                return typegrp.getTypePointerStripArray(size, pt, wordsize);
            return typegrp.getTypePointer(size, pt, wordsize);
        }

        public override bool isPtrsubMatching(uintb off)
        {
            if (ptrto->getMetatype() == TYPE_SPACEBASE)
            {
                uintb newoff = AddrSpace::addressToByte(off, wordsize);
                ptrto->getSubType(newoff, &newoff);
                if (newoff != 0)
                    return false;
            }
            else if (ptrto->getMetatype() == TYPE_ARRAY || ptrto->getMetatype() == TYPE_STRUCT)
            {
                int4 sz = off;
                int4 typesize = ptrto->getSize();
                if ((typesize <= AddrSpace::addressToByteInt(sz, wordsize)) && (typesize != 0))
                    return false;
            }
            else if (ptrto->getMetatype() == TYPE_UNION)
            {
                // A PTRSUB reaching here cannot be used for a union field resolution
                // These are created by ActionSetCasts::resolveUnion
                return false;   // So we always return false
            }
            else
                return false;   // Not a pointer to a structured data-type
            return true;
        }

        public override Datatype resolveInFlow(PcodeOp op, int4 slot)
        {
            if (ptrto->getMetatype() == TYPE_UNION)
            {
                Funcdata* fd = op->getParent()->getFuncdata();
                const ResolvedUnion* res = fd->getUnionField(this, op, slot);
                if (res != (ResolvedUnion*)0)
                    return res->getDatatype();
                ScoreUnionFields scoreFields(*fd->getArch()->types,this,op,slot);
                fd->setUnionField(this, op, slot, scoreFields.getResult());
                return scoreFields.getResult().getDatatype();
            }
            return this;
        }

        public override Datatype findResolve(PcodeOp op, int4 slot)
        {
            if (ptrto->getMetatype() == TYPE_UNION)
            {
                const Funcdata* fd = op->getParent()->getFuncdata();
                const ResolvedUnion* res = fd->getUnionField(this, op, slot);
                if (res != (ResolvedUnion*)0)
                    return res->getDatatype();
            }
            return this;
        }
    }
}