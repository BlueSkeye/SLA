using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Datatype object representing an array of elements
    internal class TypeArray : Datatype
    {
        // protected: friend class TypeFactory;
        /// type of which we have an array
        protected Datatype arrayof;
        /// Number of elements in the array
        protected int4 arraysize;

        /// Restore \b this array from a stream
        /// Parse a \<type> element with a child describing the array element data-type.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        protected void decode(Decoder decoder, TypeFactory typegrp)
        {
            //  uint4 elemId = decoder.openElement();
            decodeBasic(decoder);
            arraysize = -1;
            decoder.rewindAttributes();
            for (; ; )
            {
                uint4 attrib = decoder.getNextAttributeId();
                if (attrib == 0) break;
                if (attrib == ATTRIB_ARRAYSIZE)
                {
                    arraysize = decoder.readSignedInteger();
                }
            }
            arrayof = typegrp.decodeType(decoder);
            if ((arraysize <= 0) || (arraysize * arrayof->getSize() != size))
                throw LowlevelError("Bad size for array of type " + arrayof->getName());
            if (arraysize == 1)
                flags |= needs_resolution;      // Array of size 1 needs special treatment
                                                //  decoder.closeElement(elemId);
        }

        /// Internal constructor for decode
        protected TypeArray()
            : base(0, TYPE_ARRAY)
        {
            arraysize = 0;
            arrayof = (Datatype*)0;
        }

        /// Construct from another TypeArray
        public TypeArray(TypeArray op)
            : base(op)
        {
            arrayof = op.arrayof;
            arraysize = op.arraysize;
        }

        /// Construct given an array size and element data-type
        public TypeArray(int4 n, Datatype ao)
        {
            arraysize = n;
            arrayof = ao;
            // A varnode which is an array of size 1, should generally always be treated
            // as the element data-type
            if (n == 1)
                flags |= needs_resolution;
        }

        /// Get the element data-type
        public Datatype getBase() => arrayof;

        /// Get the number of elements
        public int4 numElements() => arraysize;

        /// Figure out what a byte range overlaps
        /// Given some contiguous piece of the array, figure out which element overlaps
        /// the piece, and pass back the element index and the renormalized offset
        /// \param off is the offset into the array
        /// \param sz is the size of the piece (in bytes)
        /// \param newoff is a pointer to the renormalized offset to pass back
        /// \param el is a pointer to the array index to pass back
        /// \return the element data-type or NULL if the piece overlaps more than one
        public Datatype getSubEntry(int4 off, int4 sz, int4 newoff, int4 el)
        {
            int4 noff = off % arrayof->getSize();
            int4 nel = off / arrayof->getSize();
            if (noff + sz > arrayof->getSize()) // Requesting parts of more then one element
                return (Datatype*)0;
            *newoff = noff;
            *el = nel;
            return arrayof;
        }

        public override void printRaw(TextWriter s)
        {
            arrayof->printRaw(s);
            s << " [" << dec << arraysize << ']';
        }

        public override Datatype getSubType(uintb off, uintb newoff)
        {
            // Go down exactly one level, to type of element
            *newoff = off % arrayof->getSize();
            return arrayof;
        }

        public override int4 getHoleSize(int4 off)
        {
            int4 newOff = off % arrayof->getSize();
            return arrayof->getHoleSize(newOff);
        }

        public override int4 numDepend() => 1;

        public override Datatype getDepend(int4 index) => arrayof;

        public override void printNameBase(TextWriter s) 
        {
            s << 'a';
            arrayof->printNameBase(s);
        }

        // For tree structure
        public override int4 compare(Datatype op, int4 level)
        {
            int4 res = Datatype::compare(op, level);
            if (res != 0) return res;
            level -= 1;
            if (level < 0)
            {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            TypeArray* ta = (TypeArray*)&op;    // Both must be arrays
            return arrayof->compare(*ta->arrayof, level); // Compare array elements
        }

        // For tree structure
        public override int4 compareDependency(Datatype op)
        {
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypeArray* ta = (TypeArray*)&op;    // Both must be arrays
            if (arrayof != ta->arrayof) return (arrayof < ta->arrayof) ? -1 : 1;    // Compare absolute pointers
            return (op.getSize() - size);
        }

        public override Datatype clone() => new TypeArray(this);

        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype*)0)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeSignedInteger(ATTRIB_ARRAYSIZE, arraysize);
            arrayof->encodeRef(encoder);
            encoder.closeElement(ELEM_TYPE);
        }

        public override Datatype resolveInFlow(PcodeOp op, int4 slot)
        {
            Funcdata* fd = op->getParent()->getFuncdata();
            const ResolvedUnion* res = fd->getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0)
                return res->getDatatype();

            int4 fieldNum = TypeStruct::scoreSingleComponent(this, op, slot);

            ResolvedUnion compFill(this, fieldNum,* fd->getArch()->types);
            fd->setUnionField(this, op, slot, compFill);
            return compFill.getDatatype();
        }

        public override Datatype findResolve(PcodeOp op, int4 slot)
        {
            const Funcdata* fd = op->getParent()->getFuncdata();
            const ResolvedUnion* res = fd->getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0)
                return res->getDatatype();
            return arrayof;     // If not calculated before, assume referring to the element
        }

        public override int4 findCompatibleResolve(Datatype ct)
        {
            if (ct->needsResolution() && !arrayof->needsResolution())
            {
                if (ct->findCompatibleResolve(arrayof) >= 0)
                    return 0;
            }
            if (arrayof == ct)
                return 0;
            return -1;
        }
    }
}
