using Sla.CORE;

namespace Sla.DECCORE
{
    internal class TypeOpPtradd : TypeOp
    {
        public TypeOpPtradd(TypeFactory t)
            : base(t, OpCode.CPUI_PTRADD,"+")

        {
            opflags = PcodeOp.Flags.ternary | PcodeOp.Flags.nocollapse;
            addlflags = OperationType.arithmetic_op;
            behave = new OpBehavior(OpCode.CPUI_PTRADD, false); // Dummy behavior
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            return tlst.getBase(op.getIn(slot).getSize(), type_metatype.TYPE_INT); // For type propagation, treat same as INT_ADD
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            return tlst.getBase(op.getOut().getSize(), type_metatype.TYPE_INT);    // For type propagation, treat same as INT_ADD
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return op.getIn(0).getHighTypeReadFacing(op);     // Cast to the input data-type
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

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if ((inslot == 2) || (outslot == 2)) return (Datatype)null; // Don't propagate along this edge
            if ((inslot != -1) && (outslot != -1)) return (Datatype)null; // Must propagate input <. output
            type_metatype metain = alttype.getMetatype();
            if (metain != type_metatype.TYPE_PTR) return (Datatype)null;
            Datatype newtype;
            if (inslot == -1)       // Propagating output to input
                newtype = op.getIn(outslot).getTempType();    // Don't propagate pointer types this direction
            else
                newtype = TypeOpIntAdd.propagateAddIn2Out(alttype, tlst, op, inslot);
            return newtype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opPtradd(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode.printRaw(s, op.getOut());
            s.Write(" = ");
            Varnode.printRaw(s, op.getIn(0));
            s.Write($" {name} ");
            Varnode.printRaw(s, op.getIn(1));
            s.Write("(*");
            Varnode.printRaw(s, op.getIn(2));
            s.Write(')');
        }
    }
}
