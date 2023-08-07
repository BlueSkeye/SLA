using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the NEW op-code
    internal class TypeOpNew : TypeOp
    {
        public TypeOpNew(TypeFactory t)
            : base(t, OpCode.CPUI_NEW,"new")
        {
            opflags = PcodeOp.Flags.special | PcodeOp::call | PcodeOp.Flags.nocollapse;
            behave = new OpBehavior(CPUI_NEW, false, true);     // Dummy behavior
        }

        // Never needs casting
        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            return (Datatype)null;
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if ((inslot != 0) || (outslot != -1)) return (Datatype)null;
            Varnode* vn0 = op.getIn(0);
            if (!vn0.isWritten()) return (Datatype)null;     // Don't propagate
            if (vn0.getDef().code() != OpCode.CPUI_CPOOLREF) return (Datatype)null;
            return alttype;     // Propagate cpool result as result of new operator
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opNewOp(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode)null)
            {
                Varnode::printRaw(s, op.getOut());
                s << " = ";
            }
            s << getOperatorName(op);
            s << '(';
            Varnode::printRaw(s, op.getIn(0));
            for (int i = 1; i < op.numInput(); ++i)
            {
                s << ',';
                Varnode::printRaw(s, op.getIn(i));
            }
            s << ')';
        }
    }
}
