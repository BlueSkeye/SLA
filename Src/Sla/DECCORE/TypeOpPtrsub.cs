using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the PTRSUB op-code
    internal class TypeOpPtrsub : TypeOp
    {
        public TypeOpPtrsub(TypeFactory t)
            : base(t, OpCode.CPUI_PTRSUB,".")
        {
            // As an operation this is really addition
            // So it should be commutative
            // But the typing information doesn't really
            // allow this to be commutative.
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.nocollapse;
            addlflags = OperationType.arithmetic_op;
            behave = new OpBehavior(OpCode.CPUI_PTRSUB, false); // Dummy behavior
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            // Output is ptr to type of subfield
            return tlst.getBase(op.getOut().getSize(), type_metatype.TYPE_INT);
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            return tlst.getBase(op.getIn(slot).getSize(), type_metatype.TYPE_INT);
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            if (slot == 0) {
                // The operation expects the type of the VARNODE
                // not the (possibly different) type of the HIGH
                Datatype reqtype = op.getIn(0).getTypeReadFacing(op);
                Datatype curtype = op.getIn(0).getHighTypeReadFacing(op);
                return castStrategy.castStandard(reqtype, curtype, false, false);
            }
            return base.getInputCast(op, slot, castStrategy);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            TypePointer ptype = (TypePointer)op.getIn(0).getHighTypeReadFacing(op);
            if (ptype.getMetatype() == type_metatype.TYPE_PTR) {
                ulong offset = AddrSpace.addressToByte(op.getIn(1).getOffset(), ptype.getWordSize());
                ulong unusedOffset;
                TypePointer unusedParent;
                Datatype rettype = ptype.downChain(offset, out unusedParent, out unusedOffset,
                    false, tlst);
                if ((offset == 0) && (rettype != (Datatype)null))
                    return rettype;
                rettype = tlst.getBase(1, type_metatype.TYPE_UNKNOWN);
                return tlst.getTypePointer(op.getOut().getSize(), rettype, ptype.getWordSize());
            }
            return base.getOutputToken(op, castStrategy);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            // Must propagate input <. output
            if ((inslot != -1) && (outslot != -1)) return (Datatype)null;
            type_metatype metain = alttype.getMetatype();
            if (metain != type_metatype.TYPE_PTR) return (Datatype)null;
            Datatype newtype;
            if (inslot == -1)
                // Propagating output to input
                // Don't propagate pointer types this direction
                newtype = op.getIn(outslot).getTempType();
            else
                newtype = TypeOpIntAdd.propagateAddIn2Out(alttype, tlst, op, inslot);
            return newtype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opPtrsub(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode.printRaw(s, op.getOut());
            s.Write(" = ");
            Varnode.printRaw(s, op.getIn(0));
            s.Write($" {name} ");
            Varnode.printRaw(s, op.getIn(1));
        }
    }
}
