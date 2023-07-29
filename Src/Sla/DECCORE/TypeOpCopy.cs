using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the COPY op-code
    internal class TypeOpCopy : TypeOp
    {
        public TypeOpCopy(TypeFactory t)
        {
            opflags = PcodeOp::unary | PcodeOp::nocollapse;
            behave = new OpBehaviorCopy();
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op.getOut().getHighTypeDefFacing();   // Require input to be same type as output
            Datatype* curtype = op.getIn(0).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, curtype, false, true);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return op.getIn(0).getHighTypeReadFacing(op);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if ((inslot != -1) && (outslot != -1)) return (Datatype*)0; // Must propagate input <. output
            Datatype* newtype;
            if (invn.isSpacebase())
            {
                AddrSpace* spc = tlst.getArch().getDefaultDataSpace();
                newtype = tlst.getTypePointer(alttype.getSize(), tlst.getBase(1, TYPE_UNKNOWN), spc.getWordSize());
            }
            else
                newtype = alttype;
            return newtype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCopy(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode::printRaw(s, op.getOut());
            s << " = ";
            Varnode::printRaw(s, op.getIn(0));
        }
    }
}
