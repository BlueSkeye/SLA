using Sla.CORE;
using Sla.DECCORE;

namespace Sla.DECCORE
{
    /// \brief Datatype object representing a pointer
    internal class TypePointer : Datatype
    {
        // friend class TypeFactory;
        /// Type being pointed to
        protected Datatype ptrto;
        /// If non-null, the address space \b this is intented to point into
        protected AddrSpace? spaceid;
        /// What size unit does the pointer address
        protected uint wordsize;

        /// Restore \b this pointer data-type from a stream
        /// Parse a \<type> element with a child describing the data-type being pointed to
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        internal virtual void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            //  uint elemId = decoder.openElement();
            decodeBasic(decoder); ;
            decoder.rewindAttributes();
            while(true) {
                AttributeId attrib = decoder.getNextAttributeId();
                if (attrib == 0) break;
                if (attrib == AttributeId.ATTRIB_WORDSIZE) {
                    wordsize = (uint)decoder.readUnsignedInteger();
                }
                else if (attrib == AttributeId.ATTRIB_SPACE) {
                    spaceid = decoder.readSpace();
                }
            }
            ptrto = typegrp.decodeType(decoder);
            calcSubmeta();
            if (name.Length == 0)       // Inherit only if no name
                flags |= ptrto.getInheritable();
            //  decoder.closeElement(elemId);
        }

        /// Calculate specific submeta for \b this pointer
        /// Pointers to structures may require a specific \b submeta
        protected void calcSubmeta()
        {
            type_metatype ptrtoMeta = ptrto.getMetatype();
            if (ptrtoMeta == type_metatype.TYPE_STRUCT) {
                if (ptrto.numDepend() > 1 || ptrto.isIncomplete())
                    submeta = sub_metatype.SUB_PTR_STRUCT;
                else
                    submeta = sub_metatype.SUB_PTR;
            }
            else if (ptrtoMeta == type_metatype.TYPE_UNION) {
                submeta = sub_metatype.SUB_PTR_STRUCT;
            }
            if (ptrto.needsResolution() && ptrtoMeta != type_metatype.TYPE_PTR)
                flags |= Properties.needs_resolution;      // Inherit needs_resolution, but only if not a pointer
        }

        /// Internal constructor for use with decode
        internal TypePointer()
            : base(0, type_metatype.TYPE_PTR)
        {
            ptrto = (Datatype)null;
            wordsize = 1;
            spaceid = (AddrSpace)null;
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
        public TypePointer(int s, Datatype pt, uint ws)
            : base(s, type_metatype.TYPE_PTR)
        {
            ptrto = pt;
            flags = ptrto.getInheritable();
            wordsize = ws;
            spaceid = (AddrSpace)null;
            calcSubmeta();
        }

        /// Construct from a pointed-to type and an address space attribute
        public TypePointer(Datatype pt, AddrSpace spc)
            : base((int)spc.getAddrSize(), type_metatype.TYPE_PTR)
        {
            ptrto = pt;
            flags = ptrto.getInheritable();
            spaceid = spc;
            wordsize = spc.getWordSize();
            calcSubmeta();
        }

        /// Get the pointed-to Datatype
        public Datatype getPtrTo() => ptrto;

        /// Get the size of the addressable unit being pointed to
        public uint getWordSize() => wordsize;

        /// Get any address space associated with \b this pointer
        public AddrSpace? getSpace() => spaceid;

        public override void printRaw(TextWriter s)
        {
            ptrto.printRaw(s);
            s.Write(" *");
            if (spaceid != (AddrSpace)null) {
                s.Write($"({spaceid.getName()})");
            }
        }

        public override int numDepend() => 1;

        public override Datatype getDepend(int index) => ptrto;

        public override void printNameBase(TextWriter s) 
        {
            s.Write('p');
            ptrto.printNameBase(s);
        }

        public override int compare(Datatype op, int level)
        {
            int res = base.compare(op, level);
            if (res != 0) return res;
            // Both must be pointers
            TypePointer tp = (TypePointer)op;
            if (wordsize != tp.wordsize) return (wordsize < tp.wordsize) ? -1 : 1;
            if (spaceid != tp.spaceid) {
                if (spaceid == (AddrSpace)null) return 1; // Pointers with address space come earlier
                if (tp.spaceid == (AddrSpace)null) return -1;
                return (spaceid.getIndex() < tp.spaceid.getIndex()) ? -1 : 1;
            }
            level -= 1;
            if (level < 0)
            {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            return ptrto.compare(tp.ptrto, level); // Compare whats pointed to
        }

        public override int compareDependency(Datatype op)
        {
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypePointer tp = (TypePointer)op;    // Both must be pointers
            if (ptrto != tp.ptrto) return (ptrto < tp.ptrto) ? -1 : 1;    // Compare absolute pointers
            if (wordsize != tp.wordsize) return (wordsize < tp.wordsize) ? -1 : 1;
            if (spaceid != tp.spaceid)
            {
                if (spaceid == (AddrSpace)null) return 1; // Pointers with address space come earlier
                if (tp.spaceid == (AddrSpace)null) return -1;
                return (spaceid.getIndex() < tp.spaceid.getIndex()) ? -1 : 1;
            }
            return (op.getSize() - size);
        }

        internal override Datatype clone() => new TypePointer(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (typedefImm != (Datatype)null) {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            if (wordsize != 1)
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_WORDSIZE, wordsize);
            if (spaceid != (AddrSpace)null)
                encoder.writeSpace(AttributeId.ATTRIB_SPACE, spaceid);
            ptrto.encodeRef(encoder);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }

        /// \brief Find a sub-type pointer given an offset into \b this
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
        public virtual TypePointer downChain(ulong off, out TypePointer par, out ulong parOff,
            bool allowArrayWrap, TypeFactory typegrp)
        {
            int ptrtoSize = ptrto.getSize();
            if (off >= ptrtoSize)
            {   // Check if we are wrapping
                if (ptrtoSize != 0 && !ptrto.isVariableLength())
                {   // Check if pointed-to is wrappable
                    if (!allowArrayWrap)
                        return (TypePointer)null;
                    long signOff = (long)off;
                    Globals.sign_extend(signOff, size * 8 - 1);
                    signOff = signOff % ptrtoSize;
                    if (signOff < 0)
                        signOff = signOff + ptrtoSize;
                    off = signOff;
                    if (off == 0)       // If we've wrapped and are now at zero
                        return this;        // consider this going down one level
                }
            }

            type_metatype meta = ptrto.getMetatype();
            bool isArray = (meta == type_metatype.TYPE_ARRAY);
            if (isArray || meta == type_metatype.TYPE_STRUCT)
            {
                par = this;
                parOff = off;
            }

            Datatype? pt = ptrto.getSubType(off, out off);
            if (pt == (Datatype)null)
                return (TypePointer)null;
            if (!isArray)
                return typegrp.getTypePointerStripArray(size, pt, wordsize);
            return typegrp.getTypePointer(size, pt, wordsize);
        }

        public override bool isPtrsubMatching(ulong off)
        {
            switch (ptrto.getMetatype()) {
                case type_metatype.TYPE_SPACEBASE:
                    ulong newoff = AddrSpace.addressToByte(off, wordsize);
                    ptrto.getSubType(newoff, out newoff);
                    if (newoff != 0)
                        return false;
                    break;
                case type_metatype.TYPE_ARRAY:
                case type_metatype.TYPE_STRUCT:
                    int sz = (int)off;
                    int typesize = ptrto.getSize();
                    if ((typesize <= AddrSpace.addressToByteInt(sz, wordsize)) && (typesize != 0))
                        return false;
                    break;
                case type_metatype.TYPE_UNION:
                    // A PTRSUB reaching here cannot be used for a union field resolution
                    // These are created by ActionSetCasts::resolveUnion
                    return false;   // So we always return false
                default:
                    return false;   // Not a pointer to a structured data-type
            }
            return true;
        }

        public override Datatype resolveInFlow(PcodeOp op, int slot)
        {
            if (ptrto.getMetatype() == type_metatype.TYPE_UNION) {
                Funcdata fd = op.getParent().getFuncdata();
                ResolvedUnion? res = fd.getUnionField(this, op, slot);
                if (res != (ResolvedUnion)null)
                    return res.getDatatype();
                ScoreUnionFields scoreFields = new ScoreUnionFields(*fd.getArch().types,this,op,slot);
                fd.setUnionField(this, op, slot, scoreFields.getResult());
                return scoreFields.getResult().getDatatype();
            }
            return this;
        }

        public override Datatype findResolve(PcodeOp op, int slot)
        {
            if (ptrto.getMetatype() == type_metatype.TYPE_UNION) {
                Funcdata fd = op.getParent().getFuncdata();
                ResolvedUnion? res = fd.getUnionField(this, op, slot);
                if (res != (ResolvedUnion)null)
                    return res.getDatatype();
            }
            return this;
        }
    }
}
