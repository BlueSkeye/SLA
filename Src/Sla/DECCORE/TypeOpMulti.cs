using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the MULTIEQUAL op-code
    internal class TypeOpMulti : TypeOp
    {
        public TypeOpMulti(TypeFactory t)
            : base(t, OpCode.CPUI_MULTIEQUAL,"?")

        {
            opflags = PcodeOp::special | PcodeOp::marker | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_MULTIEQUAL, false, true); // Dummy behavior
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if ((inslot != -1) && (outslot != -1))
            {
                return (Datatype)null; // Must propagate input <. output
            }
            Datatype* newtype;
            if (invn.isSpacebase())
            {
                AddrSpace* spc = tlst.getArch().getDefaultDataSpace();
                newtype = tlst.getTypePointer(alttype.getSize(), tlst.getBase(1, type_metatype.TYPE_UNKNOWN), spc.getWordSize());
            }
            else
                newtype = alttype;
            return newtype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opMultiequal(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode::printRaw(s, op.getOut());
            s << " = ";
            Varnode::printRaw(s, op.getIn(0));
            //  if (op.Input(0).isWritten())
            //    s << '(' << op.Input(0).Def().Start() << ')';
            if (op.numInput() == 1)
                s << ' ' << getOperatorName(op);
            for (int i = 1; i < op.numInput(); ++i)
            {
                s << ' ' << getOperatorName(op) << ' ';
                Varnode::printRaw(s, op.getIn(i));
                //    if (op.Input(i).isWritten())
                //      s << '(' << op.Input(i).Def().Start() << ')';
            }
        }
    }
}
