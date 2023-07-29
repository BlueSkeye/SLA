using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief A collection of overlapping Datatype objects: A \b union of component \b fields
    ///
    /// The individual components have \b field names, as with a structure, but for a union, the components all
    /// share the same memory.
    internal class TypeUnion : Datatype
    {
        // friend class TypeFactory;
        /// The list of fields
        protected List<TypeField> field;

        /// Establish fields for \b this
        /// Copy a list of fields into this union, establishing its size.
        /// Should only be called once when constructing the type.  TypeField \b offset is assumed to be 0.
        /// \param fd is the list of fields to copy in
        protected void setFields(List<TypeField> fd)
        {
            vector<TypeField>::const_iterator iter;
            // Need to calculate size
            size = 0;
            for (iter = fd.begin(); iter != fd.end(); ++iter)
            {
                field.push_back(*iter);
                int4 end = field.back().type.getSize();
                if (end > size)
                    size = end;
            }
        }

        /// Restore fields from a stream
        /// Parse children of the \<type> element describing each field.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning the new union
        protected void decodeFields(Decoder decoder, TypeFactory typegrp)
        {
            while (decoder.peekElement() != 0)
            {
                field.emplace_back(decoder, typegrp);
                if (field.back().offset + field.back().type.getSize() > size)
                {
                    ostringstream s;
                    s << "Field " << field.back().name << " does not fit in union " << name;
                    throw new LowlevelError(s.str());
                }
            }
            if (size == 0)      // We can decode an incomplete structure, indicated by 0 size
                flags |= type_incomplete;
            else
                markComplete();     // Otherwise the union is complete
        }

        /// Construct from another TypeUnion
        public TypeUnion(TypeUnion op)
            : base(op)
        {
            setFields(op.field);
            size = op.size;     // setFields might have changed the size
        }

        public TypeUnion()
            : base(0, TYPE_UNION)
        {
            flags |= (type_incomplete | needs_resolution);
        }  ///< Construct incomplete TypeUnion

        /// Get the i-th field of the union
        public TypeField getField(int4 i) => field[i];

        /// \param offset is the byte offset of the truncation
        /// \param sz is the number of bytes in the resulting truncation
        /// \param op is the PcodeOp reading the truncated value
        /// \param slot is the input slot being read
        /// \param newoff is used to pass back any remaining offset into the field which still must be resolved
        /// \return the field to use with truncation or null if there is no appropriate field
        public override TypeField findTruncation(int4 offset, int4 sz, PcodeOp op, int4 slot, int4 newoff)
        {
            // No new scoring is done, but if a cached result is available, return it.
            Funcdata fd = op.getParent().getFuncdata();
            ResolvedUnion res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0 && res.getFieldNum() >= 0)
            {
                TypeField field = getField(res.getFieldNum());
                newoff = offset - field.offset;
                if (newoff + sz > field.type.getSize())
                    return (TypeField*)0; // Truncation spans more than one field
                return field;
            }
            return (TypeField*)0;
        }

        //  virtual Datatype *getSubType(uintb off,uintb *newoff);

        public override int4 numDepend() => field.size();

        public override Datatype getDepend(int4 index) => field[index].type;

        // For tree structure
        public override int4 compare(Datatype op, int4 level)
        {
            int4 res = Datatype::compare(op, level);
            if (res != 0) return res;
            TypeUnion tu = (TypeUnion*)&op;
            vector<TypeField>::const_iterator iter1, iter2;

            if (field.size() != tu.field.size()) return (tu.field.size() - field.size());
            iter1 = field.begin();
            iter2 = tu.field.begin();
            // Test only the name and first level metatype first
            while (iter1 != field.end())
            {
                if ((*iter1).name != (*iter2).name)
                    return ((*iter1).name < (*iter2).name) ? -1 : 1;
                if ((*iter1).type.getMetatype() != (*iter2).type.getMetatype())
                    return ((*iter1).type.getMetatype() < (*iter2).type.getMetatype()) ? -1 : 1;
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
            iter2 = tu.field.begin();
            while (iter1 != field.end())
            {
                if ((*iter1).type != (*iter2).type)
                { // Short circuit recursive loops
                    int4 c = (*iter1).type.compare(*(*iter2).type, level);
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
            TypeUnion* tu = (TypeUnion*)&op;
            vector<TypeField>::const_iterator iter1, iter2;

            if (field.size() != tu.field.size()) return (tu.field.size() - field.size());
            iter1 = field.begin();
            iter2 = tu.field.begin();
            // Test only the name and first level metatype first
            while (iter1 != field.end())
            {
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

        public override Datatype clone() => new TypeUnion(this);

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
            Funcdata* fd = op.getParent().getFuncdata();
            ResolvedUnion* res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0)
                return res.getDatatype();
            ScoreUnionFields scoreFields(*fd.getArch().types,this,op,slot);
            fd.setUnionField(this, op, slot, scoreFields.getResult());
            return scoreFields.getResult().getDatatype();
        }

        public override Datatype findResolve(PcodeOp op, int4 slot)
        {
            Funcdata fd = op.getParent().getFuncdata();
            ResolvedUnion res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0)
                return res.getDatatype();
            return this;
        }

        public override int4 findCompatibleResolve(Datatype ct)
        {
            if (!ct.needsResolution())
            {
                for (int4 i = 0; i < field.size(); ++i)
                {
                    if (field[i].type == ct && field[i].offset == 0)
                        return i;
                }
            }
            else
            {
                for (int4 i = 0; i < field.size(); ++i)
                {
                    if (field[i].offset != 0) continue;
                    Datatype* fieldType = field[i].type;
                    if (fieldType.getSize() != ct.getSize()) continue;
                    if (fieldType.needsResolution()) continue;
                    if (ct.findCompatibleResolve(fieldType) >= 0)
                        return i;
                }
            }
            return -1;
        }

        public override TypeField resolveTruncation(int4 offset, PcodeOp op, int4 slot, int4 newoff)
        {
            Funcdata* fd = op.getParent().getFuncdata();
            ResolvedUnion res = fd.getUnionField(this, op, slot);
            if (res != (ResolvedUnion*)0)
            {
                if (res.getFieldNum() >= 0)
                {
                    TypeField field = getField(res.getFieldNum());
                    newoff = offset - field.offset;
                    return field;
                }
            }
            else if (op.code() == CPUI_SUBPIECE && slot == 1)
            {   // The slot is artificial in this case
                ScoreUnionFields scoreFields(*fd.getArch().types,this,offset,op);
                fd.setUnionField(this, op, slot, scoreFields.getResult());
                if (scoreFields.getResult().getFieldNum() >= 0)
                {
                    newoff = 0;
                    return getField(scoreFields.getResult().getFieldNum());
                }
            }
            else
            {
                ScoreUnionFields scoreFields(*fd.getArch().types,this,offset,op,slot);
                fd.setUnionField(this, op, slot, scoreFields.getResult());
                if (scoreFields.getResult().getFieldNum() >= 0)
                {
                    TypeField field = getField(scoreFields.getResult().getFieldNum());
                    newoff = offset - field.offset;
                    return field;
                }
            }
            return (TypeField*)0;
        }
    }
}
