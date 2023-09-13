using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_LEFT op-code
    internal class TypeOpIntLeft : TypeOpBinary
    {
        public TypeOpIntLeft(TypeFactory t)
            : base(t, OpCode.CPUI_INT_LEFT,"<<", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = inherits_sign | inherits_sign_zero | shift_op;
            behave = new OpBehaviorIntLeft();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntLeft(op);
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            if (slot == 1)
                return tlst.getBaseNoChar(op.getIn(1).getSize(), type_metatype.TYPE_INT);
            return base.getInputLocal(op, slot);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            Datatype res1 = op.getIn(0).getHighTypeReadFacing(op);
            if (res1.getMetatype() == type_metatype.TYPE_BOOL)
                res1 = tlst.getBase(res1.getSize(), type_metatype.TYPE_INT);
            return res1;
        }
    }
}
