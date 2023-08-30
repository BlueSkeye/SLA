using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Datatype object representing an array of elements
    internal class TypeArray : Datatype
    {
        // protected: friend class TypeFactory;
        /// type of which we have an array
        protected Datatype? arrayof;
        /// Number of elements in the array
        protected int arraysize;

        /// Restore \b this array from a stream
        /// Parse a \<type> element with a child describing the array element data-type.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        internal void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            //  ElementId elemId = decoder.openElement();
            decodeBasic(decoder);
            arraysize = -1;
            decoder.rewindAttributes();
            while(true) {
                AttributeId attrib = decoder.getNextAttributeId();
                if (attrib == 0) break;
                if (attrib == AttributeId.ATTRIB_ARRAYSIZE) {
                    arraysize = (int)decoder.readSignedInteger();
                }
            }
            arrayof = typegrp.decodeType(decoder);
            if ((arraysize <= 0) || (arraysize * arrayof.getSize() != size))
                throw new LowlevelError("Bad size for array of type " + arrayof.getName());
            if (arraysize == 1)
                flags |= Properties.needs_resolution;      // Array of size 1 needs special treatment
                                                //  decoder.closeElement(elemId);
        }

        /// Internal constructor for decode
        internal TypeArray()
            : base(0, type_metatype.TYPE_ARRAY)
        {
            arraysize = 0;
            arrayof = (Datatype)null;
        }

        /// Construct from another TypeArray
        public TypeArray(TypeArray op)
            : base(op)
        {
            arrayof = op.arrayof;
            arraysize = op.arraysize;
        }

        /// Construct given an array size and element data-type
        public TypeArray(int n, Datatype ao)
        {
            arraysize = n;
            arrayof = ao;
            // A varnode which is an array of size 1, should generally always be treated
            // as the element data-type
            if (n == 1)
                flags |= Properties.needs_resolution;
        }

        /// Get the element data-type
        public Datatype? getBase() => arrayof;

        /// Get the number of elements
        public int numElements() => arraysize;

        /// Figure out what a byte range overlaps
        /// Given some contiguous piece of the array, figure out which element overlaps
        /// the piece, and pass back the element index and the renormalized offset
        /// \param off is the offset into the array
        /// \param sz is the size of the piece (in bytes)
        /// \param newoff is a pointer to the renormalized offset to pass back
        /// \param el is a pointer to the array index to pass back
        /// \return the element data-type or NULL if the piece overlaps more than one
        public Datatype getSubEntry(int off, int sz, int newoff, int el)
        {
            if (null == arrayof) throw new BugException();
            int noff = off % arrayof.getSize();
            int nel = off / arrayof.getSize();
            if (noff + sz > arrayof.getSize()) // Requesting parts of more then one element
                return (Datatype)null;
            newoff = noff;
            el = nel;
            return arrayof;
        }

        public override void printRaw(TextWriter s)
        {
            if (null == arrayof) throw new BugException();
            arrayof.printRaw(s);
            s.Write($"[{arraysize}]");
        }

        public override Datatype getSubType(ulong off, out ulong newoff)
        {
            if (null == arrayof) throw new BugException();
            // Go down exactly one level, to type of element
            newoff = off % arrayof.getSize();
            return arrayof;
        }

        public override int getHoleSize(int off)
        {
            if (null == arrayof) throw new BugException();
            int newOff = off % arrayof.getSize();
            return arrayof.getHoleSize(newOff);
        }

        public override int numDepend() => 1;

        public override Datatype? getDepend(int index) => arrayof;

        public override void printNameBase(TextWriter s) 
        {
            if (null == arrayof) throw new BugException();
            s.Write('a');
            arrayof.printNameBase(s);
        }

        // For tree structure
        public override int compare(Datatype op, int level)
        {
            if (null == arrayof) throw new BugException();
            int res = base.compare(op, level);
            if (res != 0) return res;
            level -= 1;
            if (level < 0) {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            TypeArray ta = (TypeArray)op;    // Both must be arrays
            return arrayof.compare(ta.arrayof, level); // Compare array elements
        }

        // For tree structure
        public override int compareDependency(Datatype op)
        {
            if (null == arrayof) throw new BugException();
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypeArray ta = (TypeArray)op;    // Both must be arrays
            // Compare absolute pointers
            if (arrayof != ta.arrayof) return (this.arrayof < ta.arrayof) ? -1 : 1;
            return (op.getSize() - size);
        }

        internal override Datatype clone() => new TypeArray(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (null == arrayof) throw new BugException();
            if (typedefImm != (Datatype)null) {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeSignedInteger(AttributeId.ATTRIB_ARRAYSIZE, arraysize);
            arrayof.encodeRef(encoder);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }

        public override Datatype resolveInFlow(PcodeOp op, int slot)
        {
            Funcdata fd = op.getParent().getFuncdata();
            ResolvedUnion? res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion)null)
                return res.getDatatype();
            int fieldNum = TypeStruct.scoreSingleComponent(this, op, slot);
            ResolvedUnion compFill = new ResolvedUnion(this, fieldNum, fd.getArch().types);
            fd.setUnionField(this, op, slot, compFill);
            return compFill.getDatatype();
        }

        public override Datatype? findResolve(PcodeOp op, int slot)
        {
            Funcdata fd = op.getParent().getFuncdata();
            ResolvedUnion? res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion)null)
                return res.getDatatype();
            return arrayof;     // If not calculated before, assume referring to the element
        }

        public override int findCompatibleResolve(Datatype ct)
        {
            if (null == arrayof) throw new BugException();
            if (ct.needsResolution() && !arrayof.needsResolution()) {
                if (ct.findCompatibleResolve(arrayof) >= 0)
                    return 0;
            }
            if (arrayof == ct)
                return 0;
            return -1;
        }
    }
}
