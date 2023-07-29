using ghidra;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A composite Datatype object: A \b structure with component \b fields
    internal class TypeStruct : Datatype
    {
        // friend class TypeFactory;
        /// The list of fields
        protected List<TypeField> field;

        /// Establish fields for \b this
        /// Copy a list of fields into this structure, establishing its size.
        /// Should only be called once when constructing the type
        /// \param fd is the list of fields to copy in
        protected void setFields(List<TypeField> fd)
        {
            vector<TypeField>::const_iterator iter;
            int4 end;
            // Need to calculate size
            size = 0;
            for (iter = fd.begin(); iter != fd.end(); ++iter)
            {
                field.push_back(*iter);
                end = (*iter).offset + (*iter).type->getSize();
                if (end > size)
                    size = end;
            }
            if (field.size() == 1)
            {           // A single field
                if (field[0].type->getSize() == size)   // that fills the whole structure
                    flags |= needs_resolution;      // needs special attention
            }
        }

        /// Get index into field list
        /// Find the proper subfield given an offset. Return the index of that field
        /// or -1 if the offset is not inside a field.
        /// \param off is the offset into the structure
        /// \return the index into the field list or -1
        protected int4 getFieldIter(int4 off)
        {
            int4 min = 0;
            int4 max = field.size() - 1;

            while (min <= max)
            {
                int4 mid = (min + max) / 2;
                TypeField curfield = field[mid];
                if (curfield.offset > off)
                    max = mid - 1;
                else
                {           // curfield.offset <= off
                    if ((curfield.offset + curfield.type->getSize()) > off)
                        return mid;
                    min = mid + 1;
                }
            }
            return -1;
        }

        /// Get index of last field before or equal to given offset
        /// The field returned may or may not contain the offset.  If there are no fields
        /// that occur earlier than the offset, return -1.
        /// \param off is the given offset
        /// \return the index of the nearest field or -1
        protected int4 getLowerBoundField(int4 off)
        {
            if (field.empty()) return -1;
            int4 min = 0;
            int4 max = field.size() - 1;

            while (min < max)
            {
                int4 mid = (min + max + 1) / 2;
                if (field[mid].offset > off)
                    max = mid - 1;
                else
                {           // curfield.offset <= off
                    min = mid;
                }
            }
            if (min == max && field[min].offset <= off)
                return min;
            return -1;
        }

        /// Restore fields from a stream
        /// Children of the structure element describe each field.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning the new structure
        protected void decodeFields(Decoder decoder, TypeFactory typegrp)
        {
            int4 maxoffset = 0;
            while (decoder.peekElement() != 0)
            {
                field.emplace_back(decoder, typegrp);
                int4 trialmax = field.back().offset + field.back().type->getSize();
                if (trialmax > maxoffset)
                    maxoffset = trialmax;
                if (maxoffset > size)
                {
                    ostringstream s;
                    s << "Field " << field.back().name << " does not fit in structure " + name;
                    throw new LowlevelError(s.str());
                }
            }
            if (size == 0)      // We can decode an incomplete structure, indicated by 0 size
                flags |= type_incomplete;
            else
                markComplete();     // Otherwise the structure is complete
            if (field.size() == 1)
            {           // A single field
                if (field[0].type->getSize() == size)   // that fills the whole structure
                    flags |= needs_resolution;      // needs special resolution
            }
        }

        /// Construct from another TypeStruct
        public TypeStruct(TypeStruct op)
            : base(op)
        {
            setFields(op.field);
            size = op.size;     // setFields might have changed the size
        }

        /// Construct incomplete/empty TypeStruct
        public TypeStruct()
            : base(0, TYPE_STRUCT)
        {
            flags |= type_incomplete;
        }

        /// Beginning of fields
        public IEnumerator<TypeField> beginField() => field.begin();

        /// End of fields
        public IEnumerator<TypeField> endField() => field.end();

        public override TypeField findTruncation(int4 off, int4 sz, PcodeOp op, int4 slot, int4 newoff)
        {
            int4 i;
            int4 noff;

            i = getFieldIter(off);
            if (i < 0) return (TypeField*)0;
            TypeField curfield  = field[i];
            noff = off - curfield.offset;
            if (noff + sz > curfield.type->getSize()) // Requested piece spans more than one field
                return (TypeField*)0;
            newoff = noff;
            return &curfield;
        }

        public override Datatype getSubType(uintb off, uintb newoff)
        {               // Go down one level to field that contains offset
            int4 i;

            i = getFieldIter(off);
            if (i < 0) return Datatype::getSubType(off, newoff);
            TypeField curfield = field[i];
            *newoff = off - curfield.offset;
            return curfield.type;
        }

        public override Datatype nearestArrayedComponentForward(uintb off, uintb newoff, int4 elSize)
        {
            int4 i = getLowerBoundField(off);
            i += 1;
            while (i < field.size())
            {
                TypeField subfield = field[i];
                int4 diff = subfield.offset - off;
                if (diff > 128) break;
                Datatype* subtype = subfield.type;
                if (subtype->getMetatype() == TYPE_ARRAY)
                {
                    *newoff = (intb) - diff;
                    *elSize = ((TypeArray*)subtype)->getBase()->getSize();
                    return subtype;
                }
                else
                {
                    uintb suboff;
                    Datatype* res = subtype->nearestArrayedComponentForward(0, &suboff, elSize);
                    if (res != (Datatype*)0)
                    {
                        *newoff = (intb) - diff;
                        return subtype;
                    }
                }
                i += 1;
            }
            return (Datatype*)0;
        }

        public override Datatype nearestArrayedComponentBackward(uintb off, uintb newoff, int4 elSize)
        {
            int4 i = getLowerBoundField(off);
            while (i >= 0)
            {
                TypeField subfield = field[i];
                int4 diff = (int4)off - subfield.offset;
                if (diff > 128) break;
                Datatype* subtype = subfield.type;
                if (subtype->getMetatype() == TYPE_ARRAY)
                {
                    *newoff = (intb)diff;
                    *elSize = ((TypeArray*)subtype)->getBase()->getSize();
                    return subtype;
                }
                else
                {
                    uintb suboff;
                    Datatype* res = subtype->nearestArrayedComponentBackward(subtype->getSize(), &suboff, elSize);
                    if (res != (Datatype*)0)
                    {
                        *newoff = (intb)diff;
                        return subtype;
                    }
                }
                i -= 1;
            }
            return (Datatype*)0;
        }

        public override int4 getHoleSize(int4 off)
        {
            int4 i = getLowerBoundField(off);
            if (i >= 0)
            {
                TypeField curfield = field[i];
                int4 newOff = off - curfield.offset;
                if (newOff < curfield.type->getSize())
                    return curfield.type->getHoleSize(newOff);
            }
            i += 1;             // advance to first field following off
            if (i < field.size())
            {
                return field[i].offset - off;   // Distance to following field
            }
            return getSize() - off;     // Distance to end of structure
        }

        public override int4 numDepend() => field.size();

        public override Datatype getDepend(int4 index) => field[index].type;

        // For tree structure
        public override int4 compare(Datatype op, int4 level)
        {
            int4 res = Datatype::compare(op, level);
            if (res != 0) return res;
            TypeStruct ts = (TypeStruct*)&op;
            vector<TypeField>::const_iterator iter1, iter2;

            if (field.size() != ts->field.size()) return (ts->field.size() - field.size());
            iter1 = field.begin();
            iter2 = ts->field.begin();
            // Test only the name and first level metatype first
            while (iter1 != field.end())
            {
                if ((*iter1).offset != (*iter2).offset)
                    return ((*iter1).offset < (*iter2).offset) ? -1 : 1;
                if ((*iter1).name != (*iter2).name)
                    return ((*iter1).name < (*iter2).name) ? -1 : 1;
                if ((*iter1).type->getMetatype() != (*iter2).type->getMetatype())
                    return ((*iter1).type->getMetatype() < (*iter2).type->getMetatype()) ? -1 : 1;
                ++iter1;
                ++iter2;
            }
            level -= 1;
            if (level < 0)
            {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            // If we are still equal, now go down deep into each field type
            iter1 = field.begin();
            iter2 = ts->field.begin();
            while (iter1 != field.end())
            {
                if ((*iter1).type != (*iter2).type)
                { // Short circuit recursive loops
                    int4 c = (*iter1).type->compare(*(*iter2).type, level);
                    if (c != 0) return c;
                }
                ++iter1;
                ++iter2;
            }
            return 0;
        }

        // For tree structure
        public override int4 compareDependency(Datatype op)
        {
            int4 res = Datatype::compareDependency(op);
            if (res != 0) return res;
            TypeStruct ts = (TypeStruct*)&op;
            vector<TypeField>::const_iterator iter1, iter2;

            if (field.size() != ts->field.size()) return (ts->field.size() - field.size());
            iter1 = field.begin();
            iter2 = ts->field.begin();
            // Test only the name and first level metatype first
            while (iter1 != field.end())
            {
                if ((*iter1).offset != (*iter2).offset)
                    return ((*iter1).offset < (*iter2).offset) ? -1 : 1;
                if ((*iter1).name != (*iter2).name)
                    return ((*iter1).name < (*iter2).name) ? -1 : 1;
                Datatype* fld1 = (*iter1).type;
                Datatype* fld2 = (*iter2).type;
                if (fld1 != fld2)
                    return (fld1 < fld2) ? -1 : 1; // compare the pointers directly
                ++iter1;
                ++iter2;
            }
            return 0;
        }

        public override Datatype clone() => new TypeStruct(this);

        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype*)0)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            vector<TypeField>::const_iterator iter;
            for (iter = field.begin(); iter != field.end(); ++iter)
            {
                (*iter).encode(encoder);
            }
            encoder.closeElement(ELEM_TYPE);
        }

        public override Datatype resolveInFlow(PcodeOp op, int4 slot)
        {
            Funcdata* fd = op->getParent()->getFuncdata();
            ResolvedUnion res = fd->getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0)
                return res->getDatatype();

            int4 fieldNum = scoreSingleComponent(this, op, slot);

            ResolvedUnion compFill(this, fieldNum,* fd->getArch()->types);
            fd->setUnionField(this, op, slot, compFill);
            return compFill.getDatatype();
        }

        public override Datatype findResolve(PcodeOp op, int4 slot)
        {
            Funcdata fd = op->getParent()->getFuncdata();
            ResolvedUnion res = fd->getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0)
                return res->getDatatype();
            return field[0].type;       // If not calculated before, assume referring to field
        }

        public override int4 findCompatibleResolve(Datatype ct)
        {
            Datatype* fieldType = field[0].type;
            if (ct->needsResolution() && !fieldType->needsResolution())
            {
                if (ct->findCompatibleResolve(fieldType) >= 0)
                    return 0;
            }
            if (fieldType == ct)
                return 0;
            return -1;
        }

        /// Assign field offsets given a byte alignment
        /// Assign an offset to fields in order so that each field starts at an aligned offset within the structure
        /// \param list is the list of fields
        /// \param align is the given alignment
        public override void assignFieldOffsets(List<TypeField> list, int4 align)
        {
            int4 offset = 0;
            vector<TypeField>::iterator iter;
            for (iter = list.begin(); iter != list.end(); ++iter)
            {
                if ((*iter).offset != -1) continue;
                int4 cursize = (*iter).type->getSize();
                int4 curalign = 0;
                if (align > 1)
                {
                    curalign = align;
                    while ((curalign >> 1) >= cursize)
                        curalign >>= 1;
                    curalign -= 1;
                }
                if ((offset & curalign) != 0)
                    offset = (offset - (offset & curalign) + (curalign + 1));
                (*iter).offset = offset;
                (*iter).ident = offset;
                offset += cursize;
            }
        }

        /// Determine best type fit for given PcodeOp use
        /// If this method is called, the given data-type has a single component that fills it entirely
        /// (either a field or an element). The indicated Varnode can be resolved either by naming the
        /// data-type or naming the component. This method returns an indication of the best fit:
        /// either 0 for the component or -1 for the data-type.
        /// \param parent is the given data-type with a single component
        /// \param op is the given PcodeOp using the Varnode
        /// \param slot is -1 if the Varnode is an output or >=0 indicating the input slot
        /// \return either 0 to indicate the field or -1 to indicate the structure
        public override int4 scoreSingleComponent(Datatype parent, PcodeOp op, int4 slot)
        {
            if (op->code() == CPUI_COPY || op->code() == CPUI_INDIRECT)
            {
                Varnode* vn;
                if (slot == 0)
                    vn = op->getOut();
                else
                    vn = op->getIn(0);
                if (vn->isTypeLock() && vn->getType() == parent)
                    return -1;  // COPY of the structure directly, use whole structure
            }
            else if ((op->code() == CPUI_LOAD && slot == -1) || (op->code() == CPUI_STORE && slot == 2))
            {
                Varnode* vn = op->getIn(1);
                if (vn->isTypeLock())
                {
                    Datatype* ct = vn->getTypeReadFacing(op);
                    if (ct->getMetatype() == TYPE_PTR && ((TypePointer*)ct)->getPtrTo() == parent)
                        return -1;  // LOAD or STORE of the structure directly, use whole structure
                }
            }
            else if (op->isCall())
            {
                Funcdata* fd = op->getParent()->getFuncdata();
                FuncCallSpecs* fc = fd->getCallSpecs(op);
                if (fc != (FuncCallSpecs*)0)
                {
                    ProtoParameter* param = (ProtoParameter*)0;
                    if (slot >= 1 && fc->isInputLocked())
                        param = fc->getParam(slot - 1);
                    else if (slot < 0 && fc->isOutputLocked())
                        param = fc->getOutput();
                    if (param != (ProtoParameter*)0 && param->getType() == parent)
                        return -1;  // Function signature refers to parent directly, resolve to parent
                }
            }
            return 0;   // In all other cases resolve to the component
        }
    }
}
