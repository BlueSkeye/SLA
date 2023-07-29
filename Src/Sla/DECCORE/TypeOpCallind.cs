using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the CALLIND op-code
    internal class TypeOpCallind : TypeOp
    {
        public TypeOpCallind(TypeFactory t)
        {
            opflags = PcodeOp::special | PcodeOp::call | PcodeOp::has_callspec | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_CALLIND, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCallind(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode*)0)
            {
                Varnode::printRaw(s, op.getOut());
                s << " = ";
            }
            s << name;
            Varnode::printRaw(s, op.getIn(0));
            if (op.numInput() > 1)
            {
                s << '(';
                Varnode::printRaw(s, op.getIn(1));
                for (int i = 2; i < op.numInput(); ++i)
                {
                    s << ',';
                    Varnode::printRaw(s, op.getIn(i));
                }
                s << ')';
            }
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            Datatype* td;
            FuncCallSpecs fc;
            Datatype* ct;

            if (slot == 0)
            {
                td = tlst.getTypeCode();
                AddrSpace* spc = op.getAddr().getSpace();
                return tlst.getTypePointer(op.getIn(0).getSize(), td, spc.getWordSize()); // First parameter is code pointer
            }
            fc = op.getParent().getFuncdata().getCallSpecs(op);
            if (fc == (FuncCallSpecs*)0)
                return TypeOp::getInputLocal(op, slot);
            ProtoParameter* param = fc.getParam(slot - 1);
            if (param != (ProtoParameter*)0)
            {
                if (param.isTypeLocked())
                {
                    ct = param.getType();
                    if (ct.getMetatype() != TYPE_VOID)
                        return ct;
                }
                else if (param.isThisPointer())
                {
                    ct = param.getType();
                    if (ct.getMetatype() == TYPE_PTR && ((TypePointer*)ct).getPtrTo().getMetatype() == TYPE_STRUCT)
                        return ct;
                }
            }
            return TypeOp::getInputLocal(op, slot);
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            FuncCallSpecs* fc;
            Datatype* ct;

            fc = op.getParent().getFuncdata().getCallSpecs(op);
            if (fc == (FuncCallSpecs*)0)
                return TypeOp::getOutputLocal(op);
            if (!fc.isOutputLocked()) return TypeOp::getOutputLocal(op);
            ct = fc.getOutputType();
            if (ct.getMetatype() == TYPE_VOID) return TypeOp::getOutputLocal(op);
            return ct;
        }
    }
}
