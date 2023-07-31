using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the EXTRACT op-code
    internal class TypeOpExtract : TypeOpFunc
    {
        public TypeOpExtract(TypeFactory t)
            : base(t, OpCode.CPUI_EXTRACT,"EXTRACT", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp::ternary;
            behave = new OpBehavior(CPUI_EXTRACT, false);   // Dummy behavior
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            if (slot == 0)
                return tlst.getBase(op.getIn(slot).getSize(), type_metatype.TYPE_UNKNOWN);
            return TypeOpFunc::getInputLocal(op, slot);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opExtractOp(op);
        }
    }
}
