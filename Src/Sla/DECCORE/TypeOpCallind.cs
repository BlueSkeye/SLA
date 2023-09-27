using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the CALLIND op-code
    internal class TypeOpCallind : TypeOp
    {
        public TypeOpCallind(TypeFactory t)
            : base(t, OpCode.CPUI_CALLIND,"callind")
        {
            opflags = PcodeOp.Flags.special | PcodeOp.Flags.call | PcodeOp.Flags.has_callspec |
                PcodeOp.Flags.nocollapse;
            // Dummy behavior
            behave = new OpBehavior(OpCode.CPUI_CALLIND, false, true);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCallind(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode)null) {
                Varnode.printRaw(s, op.getOut());
                s.Write(" = ");
            }
            s.Write(name);
            Varnode.printRaw(s, op.getIn(0));
            if (op.numInput() > 1) {
                s.Write('(');
                Varnode.printRaw(s, op.getIn(1));
                for (int i = 2; i < op.numInput(); ++i) {
                    s.Write(',');
                    Varnode.printRaw(s, op.getIn(i));
                }
                s.Write(')');
            }
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            Datatype td;
            FuncCallSpecs fc;
            Datatype ct;

            if (slot == 0) {
                td = tlst.getTypeCode();
                AddrSpace spc = op.getAddr().getSpace();
                return tlst.getTypePointer(op.getIn(0).getSize(), td, spc.getWordSize()); // First parameter is code pointer
            }
            fc = op.getParent().getFuncdata().getCallSpecs(op);
            if (fc == (FuncCallSpecs)null)
                return base.getInputLocal(op, slot);
            ProtoParameter param = fc.getParam(slot - 1);
            if (param != (ProtoParameter)null) {
                if (param.isTypeLocked()) {
                    ct = param.getType();
                    if (ct.getMetatype() != type_metatype.TYPE_VOID)
                        return ct;
                }
                else if (param.isThisPointer()) {
                    ct = param.getType();
                    if (ct.getMetatype() == type_metatype.TYPE_PTR && ((TypePointer)ct).getPtrTo().getMetatype() == type_metatype.TYPE_STRUCT)
                        return ct;
                }
            }
            return base.getInputLocal(op, slot);
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            FuncCallSpecs? fc;
            Datatype ct;

            fc = op.getParent().getFuncdata().getCallSpecs(op);
            if (fc == (FuncCallSpecs)null)
                return base.getOutputLocal(op);
            if (!fc.isOutputLocked()) return base.getOutputLocal(op);
            ct = fc.getOutputType();
            if (ct.getMetatype() == type_metatype.TYPE_VOID) return base.getOutputLocal(op);
            return ct;
        }
    }
}
