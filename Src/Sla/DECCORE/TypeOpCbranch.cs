using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpCbranch : TypeOp
    {
        public TypeOpCbranch(TypeFactory t)
        {
            opflags = (PcodeOp::special | PcodeOp::branch | PcodeOp::coderef | PcodeOp::nocollapse);
            behave = new OpBehavior(CPUI_CBRANCH, false, true); // Dummy behavior
        }

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
        {
            Datatype* td;

            if (slot == 1)
                return tlst.getBase(op.getIn(1).getSize(), TYPE_BOOL); // Second param is bool
            td = tlst.getTypeCode();
            AddrSpace* spc = op.getIn(0).getSpace();
            return tlst.getTypePointer(op.getIn(0).getSize(), td, spc.getWordSize()); // First parameter is code pointer
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCbranch(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s << name << ' ';
            Varnode::printRaw(s, op.getIn(0)); // Print the distant (non-fallthru) destination
            s << " if (";
            Varnode::printRaw(s, op.getIn(1));
            if (op.isBooleanFlip() ^ op.isFallthruTrue())
                s << " == 0)";
            else
                s << " != 0)";
        }
    }
}
