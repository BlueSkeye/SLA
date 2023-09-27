using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the CALL op-code
    internal class TypeOpCall : TypeOp
    {
        public TypeOpCall(TypeFactory t)
            : base(t, OpCode.CPUI_CALL,"call")
        {
            opflags = (PcodeOp.Flags.special | PcodeOp.Flags.call | PcodeOp.Flags.has_callspec |
                PcodeOp.Flags.coderef | PcodeOp.Flags.nocollapse);
            behave = new OpBehavior(OpCode.CPUI_CALL, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCall(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode)null) {
                Varnode.printRaw(s, op.getOut());
                s.Write(" = ");
            }
            s.Write($"{name} ");
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
            FuncCallSpecs fc;
            Varnode vn;
            Datatype ct;

            vn = op.getIn(0);
            if ((slot == 0) || (vn.getSpace().getType() != spacetype.IPTR_FSPEC))// Do we have a prototype to look at
                return base.getInputLocal(op, slot);

            // Get types of call input parameters
            fc = FuncCallSpecs.getFspecFromConst(vn.getAddr());
            // Its false to assume that the parameter symbol corresponds
            // to the varnode in the same slot, but this is easiest until
            // we get giant sized parameters working properly
            ProtoParameter? param = fc.getParam(slot - 1);
            if (param != (ProtoParameter)null) {
                if (param.isTypeLocked()) {
                    ct = param.getType();
                    if ((ct.getMetatype() != type_metatype.TYPE_VOID)
                        && (ct.getSize() <= op.getIn(slot).getSize())) // parameter may not match varnode
                        return ct;
                }
                else if (param.isThisPointer()) {
                    // Known "this" pointer is effectively typelocked even if the prototype as a whole isn't
                    ct = param.getType();
                    if (ct.getMetatype() == type_metatype.TYPE_PTR && ((TypePointer)ct).getPtrTo().getMetatype() == type_metatype.TYPE_STRUCT)
                        return ct;
                }
            }
            return base.getInputLocal(op, slot);
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            FuncCallSpecs fc;
            Varnode vn;
            Datatype ct;

            vn = op.getIn(0);      // Varnode containing pointer to fspec
            if (vn.getSpace().getType() != spacetype.IPTR_FSPEC) // Do we have a prototype to look at
                return base.getOutputLocal(op);

            fc = FuncCallSpecs.getFspecFromConst(vn.getAddr());
            if (!fc.isOutputLocked()) return base.getOutputLocal(op);
            ct = fc.getOutputType();
            if (ct.getMetatype() == type_metatype.TYPE_VOID) return base.getOutputLocal(op);
            return ct;
        }
    }
}
