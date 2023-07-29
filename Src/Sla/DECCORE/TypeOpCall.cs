using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the CALL op-code
    internal class TypeOpCall : TypeOp
    {
        public TypeOpCall(TypeFactory t)
        {
            opflags = (PcodeOp::special | PcodeOp::call | PcodeOp::has_callspec | PcodeOp::coderef | PcodeOp::nocollapse);
            behave = new OpBehavior(CPUI_CALL, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCall(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode*)0)
            {
                Varnode::printRaw(s, op.getOut());
                s << " = ";
            }
            s << name << ' ';
            Varnode::printRaw(s, op.getIn(0));
            if (op.numInput() > 1)
            {
                s << '(';
                Varnode::printRaw(s, op.getIn(1));
                for (int4 i = 2; i < op.numInput(); ++i)
                {
                    s << ',';
                    Varnode::printRaw(s, op.getIn(i));
                }
                s << ')';
            }
        }

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
        {
            FuncCallSpecs fc;
            Varnode vn;
            Datatype* ct;

            vn = op.getIn(0);
            if ((slot == 0) || (vn.getSpace().getType() != IPTR_FSPEC))// Do we have a prototype to look at
                return TypeOp::getInputLocal(op, slot);

            // Get types of call input parameters
            fc = FuncCallSpecs::getFspecFromConst(vn.getAddr());
            // Its false to assume that the parameter symbol corresponds
            // to the varnode in the same slot, but this is easiest until
            // we get giant sized parameters working properly
            ProtoParameter* param = fc.getParam(slot - 1);
            if (param != (ProtoParameter*)0)
            {
                if (param.isTypeLocked())
                {
                    ct = param.getType();
                    if ((ct.getMetatype() != TYPE_VOID) && (ct.getSize() <= op.getIn(slot).getSize())) // parameter may not match varnode
                        return ct;
                }
                else if (param.isThisPointer())
                {
                    // Known "this" pointer is effectively typelocked even if the prototype as a whole isn't
                    ct = param.getType();
                    if (ct.getMetatype() == TYPE_PTR && ((TypePointer*)ct).getPtrTo().getMetatype() == TYPE_STRUCT)
                        return ct;
                }
            }
            return TypeOp::getInputLocal(op, slot);
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            FuncCallSpecs fc;
            Varnode vn;
            Datatype* ct;

            vn = op.getIn(0);      // Varnode containing pointer to fspec
            if (vn.getSpace().getType() != IPTR_FSPEC) // Do we have a prototype to look at
                return TypeOp::getOutputLocal(op);

            fc = FuncCallSpecs::getFspecFromConst(vn.getAddr());
            if (!fc.isOutputLocked()) return TypeOp::getOutputLocal(op);
            ct = fc.getOutputType();
            if (ct.getMetatype() == TYPE_VOID) return TypeOp::getOutputLocal(op);
            return ct;
        }
    }
}
