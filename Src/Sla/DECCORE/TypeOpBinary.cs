﻿using Sla.CORE;
using Sla.DECCORE;

namespace Sla.DECCORE
{
    /// \brief A generic binary operator: two inputs and one output
    ///
    /// All binary op-codes have a single data-type for input values
    /// and a data-type for the output value
    internal abstract class TypeOpBinary : TypeOp
    {
        private type_metatype metaout;  ///< The metatype of the output
        private type_metatype metain;       ///< The metatype of the inputs

        protected override void setMetatypeIn(type_metatype val)
        {
            metain = val;
        }

        protected override void setMetatypeOut(type_metatype val)
        {
            metaout = val;
        }

        public TypeOpBinary(TypeFactory t, OpCode opc, string n, type_metatype mout, type_metatype min)
            : base(t, opc, n)
        {
            metaout = mout;
            metain = min;
        }

        public override Datatype getOutputLocal(PcodeOp op) => tlst.getBase(op.getOut().getSize(), metaout);

        public override Datatype getInputLocal(PcodeOp op, int slot)
            => tlst.getBase(op.getIn(slot).getSize(), metain);

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode.printRaw(s, op.getOut());
            s.Write(" = ");
            Varnode.printRaw(s, op.getIn(0));
            s.Write($" {getOperatorName(op)} ");
            Varnode.printRaw(s, op.getIn(1));
        }
    }
}
