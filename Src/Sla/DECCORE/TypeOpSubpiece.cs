using Sla.DECCORE;
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
            : base(t, OpCode.CPUI_SUBPIECE,"SUB", type_metatype.TYPE_UNKNOWN, type_metatype.TYPE_UNKNOWN)
        {
            opflags = PcodeOp::binary;
            behave = new OpBehaviorSubpiece();
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            return (Datatype)null;        // Never need a cast into a SUBPIECE
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            Varnode outvn = op.getOut();
            TypeField field;
            Datatype* ct = op.getIn(0).getHighTypeReadFacing(op);
            int offset;
            int byteOff = computeByteOffsetForComposite(op);
            field = ct.findTruncation(byteOff, outvn.getSize(), op, 1, offset);   // Use artificial slot
            if (field != (TypeField)null) {
                if (outvn.getSize() == field.type.getSize())
                    return field.type;
            }
            Datatype* dt = outvn.getHighTypeDefFacing();   // SUBPIECE prints as cast to whatever its output is
            if (dt.getMetatype() != type_metatype.TYPE_UNKNOWN)
                return dt;
            return tlst.getBase(outvn.getSize(), type_metatype.TYPE_INT);   // If output is unknown, treat as cast to int
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if (inslot != 0 || outslot != -1) return (Datatype)null;  // Propagation must be from in0 to out
            int byteOff;
            int newoff;
            TypeField field;
            type_metatype meta = alttype.getMetatype();
            if (meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_PARTIALUNION)
            {
                // NOTE: We use an artificial slot here to store the field being truncated to
                // as the facing data-type for slot 0 is already to the parent (this type_metatype.TYPE_UNION)
                byteOff = computeByteOffsetForComposite(op);
                field = alttype.resolveTruncation(byteOff, op, 1, newoff);
            }
            else if (alttype.getMetatype() == type_metatype.TYPE_STRUCT)
            {
                int byteOff = computeByteOffsetForComposite(op);
                field = alttype.findTruncation(byteOff, outvn.getSize(), op, 1, newoff);
            }
            else
                return (Datatype)null;
            if (field != (TypeField)null && newoff == 0 && field.type.getSize() == outvn.getSize()) {
                return field.type;
            }
            return (Datatype)null;
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;

            s << name << dec << op.getIn(0).getSize() << op.getOut().getSize();
            return s.str();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opSubpiece(op);
        }

        /// \brief Compute the byte offset into an assumed composite data-type produced by the given OpCode.CPUI_SUBPIECE
        ///
        /// If the input Varnode is a composite data-type, the extracted result of the SUBPIECE represent a
        /// range of bytes starting at a particular offset within the data-type.  Return this offset, which
        /// depends on endianness of the input.
        /// \param op is the given OpCode.CPUI_SUBPIECE
        /// \return the byte offset into the composite represented by the output of the SUBPIECE
        public static int computeByteOffsetForComposite(PcodeOp op)
        {
            int outSize = op.getOut().getSize();
            int lsb = (int)op.getIn(1).getOffset();
            Varnode vn = op.getIn(0);
            int byteOff;
            if (vn.getSpace().isBigEndian())
                byteOff = vn.getSize() - outSize - lsb;
            else
                byteOff = lsb;
            return byteOff;
        }
    }
}
