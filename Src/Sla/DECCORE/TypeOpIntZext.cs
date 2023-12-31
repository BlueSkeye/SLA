﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_ZEXT op-code
    internal class TypeOpIntZext : TypeOpFunc
    {
        public TypeOpIntZext(TypeFactory t)
            : base(t, OpCode.CPUI_INT_ZEXT,"ZEXT", type_metatype.TYPE_UINT, type_metatype.TYPE_UINT)
        {
            opflags = PcodeOp.Flags.unary;
            behave = new OpBehaviorIntZext();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntZext(op, readOp);
        }

        public override string getOperatorName(PcodeOp op)
        {
            TextWriter s = new StringWriter();

            s.Write($"{name}{op.getIn(0).getSize()}{op.getOut().getSize()}");
            return s.ToString();
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Datatype reqtype = op.inputTypeLocal(slot);
            if (castStrategy.checkIntPromotionForExtension(op))
                return reqtype;
            Datatype curtype = op.getIn(slot).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, curtype, true, false);
        }
    }
}
