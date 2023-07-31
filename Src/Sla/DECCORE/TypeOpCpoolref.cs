using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

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
            opflags = PcodeOp::special | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_CPOOLREF, false, true); // Dummy behavior
        }

        // Never needs casting
        public override Datatype getOutputLocal(PcodeOp op)
        {
            List<ulong> refs;
            for (int i = 1; i < op.numInput(); ++i)
                refs.Add(op.getIn(i).getOffset());
            CPoolRecord* rec = cpool.getRecord(refs);
            if (rec == (CPoolRecord*)0)
                return TypeOp::getOutputLocal(op);
            if (rec.getTag() == CPoolRecord::instance_of)
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
            if (op.getOut() != (Varnode)null)
            {
                Varnode::printRaw(s, op.getOut());
                s << " = ";
            }
            s << getOperatorName(op);
            List<ulong> refs;
            for (int i = 1; i < op.numInput(); ++i)
                refs.Add(op.getIn(i).getOffset());
            CPoolRecord* rec = cpool.getRecord(refs);
            if (rec != (CPoolRecord*)0)
                s << '_' << rec.getToken();
            s << '(';
            Varnode::printRaw(s, op.getIn(0));
            for (int i = 2; i < op.numInput(); ++i)
            {
                s << ',';
                Varnode::printRaw(s, op.getIn(i));
            }
            s << ')';
        }
    }
}
