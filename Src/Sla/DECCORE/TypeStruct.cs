using Sla.CORE;
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
        internal List<TypeField> field;

        /// Establish fields for \b this
        /// Copy a list of fields into this structure, establishing its size.
        /// Should only be called once when constructing the type
        /// \param fd is the list of fields to copy in
        protected void setFields(List<TypeField> fd)
        {
            int end;
            // Need to calculate size
            size = 0;
            foreach (TypeField typeField in fd) {
                field.Add(typeField);
                end = typeField.offset + typeField.type.getSize();
                if (end > size)
                    size = end;
            }
            if (field.size() == 1) {
                // A single field
                if (field[0].type.getSize() == size)   // that fills the whole structure
                    flags |= Properties.needs_resolution;      // needs special attention
            }
        }

        /// Get index into field list
        /// Find the proper subfield given an offset. Return the index of that field
        /// or -1 if the offset is not inside a field.
        /// \param off is the offset into the structure
        /// \return the index into the field list or -1
        protected int getFieldIter(int off)
        {
            int min = 0;
            int max = field.size() - 1;

            while (min <= max) {
                int mid = (min + max) / 2;
                TypeField curfield = field[mid];
                if (curfield.offset > off)
                    max = mid - 1;
                else {
                    // curfield.offset <= off
                    if ((curfield.offset + curfield.type.getSize()) > off)
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
        protected int getLowerBoundField(int off)
        {
            if (field.empty()) return -1;
            int min = 0;
            int max = field.size() - 1;

            while (min < max) {
                int mid = (min + max + 1) / 2;
                if (field[mid].offset > off)
                    max = mid - 1;
                else {
                    // curfield.offset <= off
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
        internal void decodeFields(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            int maxoffset = 0;
            while (decoder.peekElement() != 0) {
                TypeField newField = new TypeField(decoder, typegrp);
                field.Add(newField);
                int trialmax = newField.offset + newField.type.getSize();
                if (trialmax > maxoffset)
                    maxoffset = trialmax;
                if (maxoffset > size) {
                    throw new LowlevelError(
                        $"Field {field.GetLastItem().name} does not fit in structure {name}");
                }
            }
            if (size == 0)      // We can decode an incomplete structure, indicated by 0 size
                flags |= Properties.type_incomplete;
            else
                markComplete();     // Otherwise the structure is complete
            if (field.size() == 1) {
                // A single field
                if (field[0].type.getSize() == size)   // that fills the whole structure
                    flags |= Properties.needs_resolution;      // needs special resolution
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
            : base(0, type_metatype.TYPE_STRUCT)
        {
            flags |= Properties.type_incomplete;
        }

        /// Beginning of fields
        public IEnumerator<TypeField> beginField() => field.GetEnumerator();

        ///// End of fields
        //public IEnumerator<TypeField> endField() => field.end();

        public override TypeField? findTruncation(int off, int sz, PcodeOp op, int slot, int newoff)
        {
            int i = getFieldIter(off);
            if (i < 0) return (TypeField)null;
            TypeField curfield  = field[i];
            int noff = off - curfield.offset;
            if (noff + sz > curfield.type.getSize()) // Requested piece spans more than one field
                return (TypeField)null;
            newoff = noff;
            return curfield;
        }

        public override Datatype? getSubType(ulong off, out ulong newoff)
        {
            // Go down one level to field that contains offset
            int i = getFieldIter((int)off);
            if (i < 0) return base.getSubType(off, out newoff);
            TypeField curfield = field[i];
            newoff = off - (uint)curfield.offset;
            return curfield.type;
        }

        public override Datatype? nearestArrayedComponentForward(ulong off, out ulong newoff, out int elSize)
        {
            int i = getLowerBoundField((int)off);
            i += 1;
            while (i < field.size()) {
                TypeField subfield = field[i];
                int diff = (int)((uint)subfield.offset - off);
                if (diff > 128) break;
                Datatype subtype = subfield.type;
                if (subtype.getMetatype() == type_metatype.TYPE_ARRAY) {
                    newoff = (ulong) (-diff);
                    elSize = ((TypeArray)subtype).getBase().getSize();
                    return subtype;
                }
                else {
                    ulong suboff;
                    Datatype? res = subtype.nearestArrayedComponentForward(0, out suboff, out elSize);
                    if (res != (Datatype)null) {
                        newoff = (ulong)(-diff);
                        return subtype;
                    }
                }
                i += 1;
            }
            return (Datatype)null;
        }

        public override Datatype? nearestArrayedComponentBackward(ulong off, ulong newoff, int elSize)
        {
            int i = getLowerBoundField((int)off);
            while (i >= 0) {
                TypeField subfield = field[i];
                int diff = (int)off - subfield.offset;
                if (diff > 128) break;
                Datatype subtype = subfield.type;
                if (subtype.getMetatype() == type_metatype.TYPE_ARRAY) {
                    newoff = (ulong)diff;
                    elSize = ((TypeArray)subtype).getBase().getSize();
                    return subtype;
                }
                else {
                    ulong suboff;
                    Datatype res = subtype.nearestArrayedComponentBackward(subtype.getSize(),
                        out suboff, out elSize);
                    if (res != (Datatype)null) {
                        newoff = (ulong)diff;
                        return subtype;
                    }
                }
                i -= 1;
            }
            return (Datatype)null;
        }

        public override int getHoleSize(int off)
        {
            int i = getLowerBoundField(off);
            if (i >= 0) {
                TypeField curfield = field[i];
                int newOff = off - curfield.offset;
                if (newOff < curfield.type.getSize())
                    return curfield.type.getHoleSize(newOff);
            }
            i += 1;             // advance to first field following off
            if (i < field.size()) {
                return field[i].offset - off;   // Distance to following field
            }
            return getSize() - off;     // Distance to end of structure
        }

        public override int numDepend() => field.size();

        public override Datatype getDepend(int index) => field[index].type;

        // For tree structure
        public override int compare(Datatype op, int level)
        {
            int res = Datatype.compare(op, level);
            if (res != 0) return res;
            TypeStruct ts = (TypeStruct)op;

            if (field.size() != ts.field.size()) return (ts.field.size() - field.size());
            IEnumerator<TypeField> iter1 = field.GetEnumerator();
            IEnumerator<TypeField> iter2 = ts.field.GetEnumerator();
            // Test only the name and first level metatype first
            while (iter1.MoveNext()) {
                if (!iter2.MoveNext()) throw new BugException();
                if (iter1.Current.offset != iter2.Current.offset)
                    return (iter1.Current.offset < iter2.Current.offset) ? -1 : 1;
                if (iter1.Current.name != iter2.Current.name)
                    return string.Compare(iter1.Current.name, iter2.Current.name);
                if (iter1.Current.type.getMetatype() != iter2.Current.type.getMetatype())
                    return (iter1.Current.type.getMetatype() < iter2.Current.type.getMetatype()) ? -1 : 1;
            }
            level -= 1;
            if (level < 0) {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            // If we are still equal, now go down deep into each field type
            iter1 = field.GetEnumerator();
            iter2 = ts.field.GetEnumerator();
            while (iter1.MoveNext()) {
                if (!iter2.MoveNext()) throw new BugException();
                if (iter1.Current.type != iter2.Current.type) {
                    // Short circuit recursive loops
                    int c = iter1.Current.type.compare(iter2.Current.type, level);
                    if (c != 0) return c;
                }
            }
            return 0;
        }

        // For tree structure
        public override int compareDependency(Datatype op)
        {
            int res = base.compareDependency(op);
            if (res != 0) return res;
            TypeStruct ts = (TypeStruct)op;

            if (field.size() != ts.field.size()) return (ts.field.size() - field.size());
            IEnumerator<TypeField> iter1 = field.GetEnumerator();
            IEnumerator<TypeField> iter2 = ts.field.GetEnumerator();
            // Test only the name and first level metatype first
            while (iter1.MoveNext()) {
                if (!iter2.MoveNext()) throw new BugException();
                if (iter1.Current.offset != iter2.Current.offset)
                    return (iter1.Current.offset < iter2.Current.offset) ? -1 : 1;
                if (iter1.Current.name != iter2.Current.name)
                    return string.Compare(iter1.Current.name, iter2.Current.name);
                Datatype fld1 = iter1.Current.type;
                Datatype fld2 = iter2.Current.type;
                if (fld1 != fld2)
                    return (fld1 < fld2) ? -1 : 1; // compare the pointers directly
            }
            return 0;
        }

        internal override Datatype clone() => new TypeStruct(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (typedefImm != (Datatype)null) {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            foreach (TypeField scannedField in field) {
                scannedField.encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_TYPE);
        }

        public override Datatype resolveInFlow(PcodeOp op, int slot)
        {
            Funcdata fd = op.getParent().getFuncdata();
            ResolvedUnion? res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion)null)
                return res.getDatatype();

            int fieldNum = scoreSingleComponent(this, op, slot);

            ResolvedUnion compFill = new ResolvedUnion(this, fieldNum, fd.getArch().types);
            fd.setUnionField(this, op, slot, compFill);
            return compFill.getDatatype();
        }

        public override Datatype findResolve(PcodeOp op, int slot)
        {
            Funcdata fd = op.getParent().getFuncdata();
            ResolvedUnion? res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion)null)
                return res.getDatatype();
            return field[0].type;       // If not calculated before, assume referring to field
        }

        public override int findCompatibleResolve(Datatype ct)
        {
            Datatype fieldType = field[0].type;
            if (ct.needsResolution() && !fieldType.needsResolution()) {
                if (ct.findCompatibleResolve(fieldType) >= 0)
                    return 0;
            }
            return (fieldType == ct) ? 0 : -1;
        }

        /// Assign field offsets given a byte alignment
        /// Assign an offset to fields in order so that each field starts at an aligned offset within the structure
        /// \param list is the list of fields
        /// \param align is the given alignment
        public static void assignFieldOffsets(List<TypeField> list, int align)
        {
            int offset = 0;
            foreach (TypeField scannedField in list) {
                if (scannedField.offset != -1) continue;
                int cursize = scannedField.type.getSize();
                int curalign = 0;
                if (align > 1) {
                    curalign = align;
                    while ((curalign >> 1) >= cursize)
                        curalign >>= 1;
                    curalign -= 1;
                }
                if ((offset & curalign) != 0)
                    offset = (offset - (offset & curalign) + (curalign + 1));
                scannedField.offset = offset;
                scannedField.ident = offset;
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
        public static int scoreSingleComponent(Datatype parent, PcodeOp op, int slot)
        {
            if (op.code() == OpCode.CPUI_COPY || op.code() == OpCode.CPUI_INDIRECT) {
                Varnode vn = (slot == 0) ? op.getOut() : op.getIn(0);
                if (vn.isTypeLock() && vn.getType() == parent)
                    return -1;  // COPY of the structure directly, use whole structure
            }
            else if ((op.code() == OpCode.CPUI_LOAD && slot == -1) || (op.code() == OpCode.CPUI_STORE && slot == 2)) {
                Varnode vn = op.getIn(1);
                if (vn.isTypeLock()) {
                    Datatype ct = vn.getTypeReadFacing(op);
                    if (ct.getMetatype() == type_metatype.TYPE_PTR && ((TypePointer)ct).getPtrTo() == parent)
                        return -1;  // LOAD or STORE of the structure directly, use whole structure
                }
            }
            else if (op.isCall()) {
                Funcdata fd = op.getParent().getFuncdata();
                FuncCallSpecs? fc = fd.getCallSpecs(op);
                if (fc != (FuncCallSpecs)null) {
                    ProtoParameter param = (ProtoParameter)null;
                    if (slot >= 1 && fc.isInputLocked())
                        param = fc.getParam(slot - 1);
                    else if (slot < 0 && fc.isOutputLocked())
                        param = fc.getOutput();
                    if (param != (ProtoParameter)null && param.getType() == parent)
                        return -1;  // Function signature refers to parent directly, resolve to parent
                }
            }
            return 0;   // In all other cases resolve to the component
        }
    }
}
