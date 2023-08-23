using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the CPOOLREF op-code
    internal class TypeOpCpoolref : TypeOp
    {
        ///< The constant pool container
        private ConstantPool cpool;

        public TypeOpCpoolref(TypeFactory t)
            : base(t, OpCode.CPUI_CPOOLREF, "cpoolref")
        {
            cpool = t.getArch().cpool;
            opflags = PcodeOp.Flags.special | PcodeOp.Flags.nocollapse;
            behave = new OpBehavior(OpCode.CPUI_CPOOLREF, false, true); // Dummy behavior
        }

        // Never needs casting
        public override Datatype getOutputLocal(PcodeOp op)
        {
            List<ulong> refs = new List<ulong>();
            for (int i = 1; i < op.numInput(); ++i)
                refs.Add(op.getIn(i).getOffset());
            CPoolRecord? rec = cpool.getRecord(refs);
            if (rec == (CPoolRecord)null)
                return base.getOutputLocal(op);
            if (rec.getTag() == CPoolRecord.ConstantPoolTagTypes.instance_of)
                return tlst.getBase(1, type_metatype.TYPE_BOOL);
            return rec.getType();
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            return (Datatype)null;
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            return tlst.getBase(op.getIn(slot).getSize(), type_metatype.TYPE_INT);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCpoolRefOp(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode)null) {
                Varnode.printRaw(s, op.getOut());
                s.Write(" = ");
            }
            s.Write(getOperatorName(op));
            List<ulong> refs = new List<ulong>();
            for (int i = 1; i < op.numInput(); ++i)
                refs.Add(op.getIn(i).getOffset());
            CPoolRecord rec = cpool.getRecord(refs);
            if (rec != (CPoolRecord)null)
                s.Write($"_{rec.getToken()}");
            s.Write('(');
            Varnode.printRaw(s, op.getIn(0));
            for (int i = 2; i < op.numInput(); ++i) {
                s.Write(',');
                Varnode.printRaw(s, op.getIn(i));
            }
            s.Write(')');
        }
    }
}
