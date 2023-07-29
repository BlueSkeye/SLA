﻿using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the SUBPIECE op-code
    internal class TypeOpSubpiece : TypeOpFunc
    {
        public TypeOpSubpiece(TypeFactory t)
            : base(t, CPUI_SUBPIECE,"SUB", TYPE_UNKNOWN, TYPE_UNKNOWN)
        {
            opflags = PcodeOp::binary;
            behave = new OpBehaviorSubpiece();
        }

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            return (Datatype*)0;        // Never need a cast into a SUBPIECE
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            const Varnode* outvn = op->getOut();
            const TypeField* field;
            Datatype* ct = op->getIn(0)->getHighTypeReadFacing(op);
            int4 offset;
            int4 byteOff = computeByteOffsetForComposite(op);
            field = ct->findTruncation(byteOff, outvn->getSize(), op, 1, offset);   // Use artificial slot
            if (field != (const TypeField*)0) {
                if (outvn->getSize() == field->type->getSize())
                    return field->type;
            }
            Datatype* dt = outvn->getHighTypeDefFacing();   // SUBPIECE prints as cast to whatever its output is
            if (dt->getMetatype() != TYPE_UNKNOWN)
                return dt;
            return tlst->getBase(outvn->getSize(), TYPE_INT);   // If output is unknown, treat as cast to int
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int4 inslot, int4 outslot)
        {
            if (inslot != 0 || outslot != -1) return (Datatype*)0;  // Propagation must be from in0 to out
            int4 byteOff;
            int4 newoff;
            const TypeField* field;
            type_metatype meta = alttype->getMetatype();
            if (meta == TYPE_UNION || meta == TYPE_PARTIALUNION)
            {
                // NOTE: We use an artificial slot here to store the field being truncated to
                // as the facing data-type for slot 0 is already to the parent (this TYPE_UNION)
                byteOff = computeByteOffsetForComposite(op);
                field = alttype->resolveTruncation(byteOff, op, 1, newoff);
            }
            else if (alttype->getMetatype() == TYPE_STRUCT)
            {
                int4 byteOff = computeByteOffsetForComposite(op);
                field = alttype->findTruncation(byteOff, outvn->getSize(), op, 1, newoff);
            }
            else
                return (Datatype*)0;
            if (field != (const TypeField*)0 && newoff == 0 && field->type->getSize() == outvn->getSize()) {
                return field->type;
            }
            return (Datatype*)0;
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;

            s << name << dec << op->getIn(0)->getSize() << op->getOut()->getSize();
            return s.str();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opSubpiece(op);
        }

        /// \brief Compute the byte offset into an assumed composite data-type produced by the given CPUI_SUBPIECE
        ///
        /// If the input Varnode is a composite data-type, the extracted result of the SUBPIECE represent a
        /// range of bytes starting at a particular offset within the data-type.  Return this offset, which
        /// depends on endianness of the input.
        /// \param op is the given CPUI_SUBPIECE
        /// \return the byte offset into the composite represented by the output of the SUBPIECE
        public static int4 computeByteOffsetForComposite(PcodeOp op)
        {
            int4 outSize = op->getOut()->getSize();
            int4 lsb = (int4)op->getIn(1)->getOffset();
            const Varnode* vn = op->getIn(0);
            int byteOff;
            if (vn->getSpace()->isBigEndian())
                byteOff = vn->getSize() - outSize - lsb;
            else
                byteOff = lsb;
            return byteOff;
        }
    }
}