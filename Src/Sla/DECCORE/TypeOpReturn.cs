using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the RETURN op-code
    internal class TypeOpReturn : TypeOp
    {
        public TypeOpReturn(TypeFactory t)
        {
            opflags = PcodeOp::special | PcodeOp::returns | PcodeOp::nocollapse | PcodeOp::no_copy_propagation;
            behave = new OpBehavior(CPUI_RETURN, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opReturn(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s << name;
            if (op->numInput() >= 1)
            {
                s << '(';
                Varnode::printRaw(s, op->getIn(0));
                s << ')';
            }
            if (op->numInput() > 1)
            {
                s << ' ';
                Varnode::printRaw(s, op->getIn(1));
                for (int4 i = 2; i < op->numInput(); ++i)
                {
                    s << ',';
                    Varnode::printRaw(s, op->getIn(i));
                }
            }
        }

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
        {
            FuncProto fp;
            Datatype* ct;

            if (slot == 0)
                return TypeOp::getInputLocal(op, slot);

            // Get data-types of return input parameters
            BlockBasic bb = op->getParent();
            if (bb == (BlockBasic*)0)
                return TypeOp::getInputLocal(op, slot);

            fp = &bb->getFuncdata()->getFuncProto();    // Prototype of function we are in

            //  if (!fp->isOutputLocked()) return TypeOp::getInputLocal(op,slot);
            ct = fp->getOutputType();
            if (ct->getMetatype() == TYPE_VOID || (ct->getSize() != op->getIn(slot)->getSize()))
                return TypeOp::getInputLocal(op, slot);
            return ct;
        }
    }
}
