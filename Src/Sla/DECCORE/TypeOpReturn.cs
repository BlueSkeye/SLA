using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the RETURN op-code
    internal class TypeOpReturn : TypeOp
    {
        public TypeOpReturn(TypeFactory t)
        {
            opflags = PcodeOp.Flags.special | PcodeOp.Flags.returns | PcodeOp.Flags.nocollapse | PcodeOp::no_copy_propagation;
            behave = new OpBehavior(OpCode.CPUI_RETURN, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opReturn(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s.Write(name);
            if (op.numInput() >= 1) {
                s.Write('(');
                Varnode.printRaw(s, op.getIn(0));
                s.Write(')');
            }
            if (op.numInput() > 1) {
                s.Write(' ');
                Varnode.printRaw(s, op.getIn(1));
                for (int i = 2; i < op.numInput(); ++i) {
                    s.Write(',');
                    Varnode.printRaw(s, op.getIn(i));
                }
            }
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            FuncProto fp;
            Datatype ct;

            if (slot == 0)
                return base.getInputLocal(op, slot);

            // Get data-types of return input parameters
            BlockBasic? bb = op.getParent();
            if (bb == (BlockBasic)null)
                return base.getInputLocal(op, slot);

            fp = bb.getFuncdata().getFuncProto();    // Prototype of function we are in

            //  if (!fp.isOutputLocked()) return TypeOp::getInputLocal(op,slot);
            ct = fp.getOutputType();
            if (ct.getMetatype() == type_metatype.TYPE_VOID || (ct.getSize() != op.getIn(slot).getSize()))
                return base.getInputLocal(op, slot);
            return ct;
        }
    }
}
