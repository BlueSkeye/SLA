using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the CALLOTHER op-code (user defined p-code operations)
    internal class TypeOpCallother : TypeOp
    {
        public TypeOpCallother(TypeFactory t)
        {
            opflags = PcodeOp.Flags.special | PcodeOp.Flags.call | PcodeOp.Flags.nocollapse;
            behave = new OpBehavior(OpCode.CPUI_CALLOTHER, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCallother(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode)null) {
                Varnode.printRaw(s, op.getOut());
                s.Write(" = ");
            }
            s.Write(getOperatorName(op));
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

        public override string getOperatorName(PcodeOp op)
        {
            BlockBasic? bb = op.getParent();
            if (bb != (BlockBasic)null) {
                Architecture glb = bb.getFuncdata().getArch();
                int index = (int)op.getIn(0).getOffset();
                UserPcodeOp userop = glb.userops.getOp(index);
                if (userop != (UserPcodeOp)null)
                    return userop.getOperatorName(op);
            }
            TextWriter res = new StringWriter();
            res.Write($"{base.getOperatorName(op)}[");
            op.getIn(0).printRaw(res);
            res.Write(']');
            return res.ToString();
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            if (!op.doesSpecialPropagation())
                return base.getInputLocal(op, slot);
            Architecture glb = tlst.getArch();
            VolatileWriteOp vw_op = glb.userops.getVolatileWrite(); // Check if this a volatile write op
            if ((vw_op.getIndex() == op.getIn(0).getOffset()) && (slot == 2)) {
                // And we are requesting slot 2
                Address addr = op.getIn(1).getAddr(); // Address of volatile memory
                int size = op.getIn(2).getSize(); // Size of memory being written
                uint vflags = 0;
                SymbolEntry? entry = glb.symboltab.getGlobalScope().queryProperties(addr, size, op.getAddr(), vflags);
                if (entry != (SymbolEntry)null) {
                    Datatype? res = entry.getSizedType(addr, size);
                    if (res != (Datatype)null)
                        return res;
                }
            }
            return base.getInputLocal(op, slot);
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            if (!op.doesSpecialPropagation())
                return base.getOutputLocal(op);
            Architecture glb = tlst.getArch();
            VolatileReadOp vr_op = glb.userops.getVolatileRead(); // Check if this a volatile read op
            if (vr_op.getIndex() == op.getIn(0).getOffset()) {
                Address addr = op.getIn(1).getAddr(); // Address of volatile memory
                int size = op.getOut().getSize(); // Size of memory being written
                uint vflags = 0;
                SymbolEntry? entry = glb.symboltab.getGlobalScope().queryProperties(addr, size, op.getAddr(), vflags);
                if (entry != (SymbolEntry)null) {
                    Datatype? res = entry.getSizedType(addr, size);
                    if (res != (Datatype)null)
                        return res;
                }
            }
            return base.getOutputLocal(op);
        }
    }
}
