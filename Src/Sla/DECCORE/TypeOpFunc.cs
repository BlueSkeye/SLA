using Sla.CORE;
using Sla.DECCORE;

namespace Sla.DECCORE
{
    /// \brief A generic functional operator.
    ///
    /// The operator takes one or more inputs (with the same data-type by default)
    /// and produces one output with  specific data-type
    internal abstract class TypeOpFunc : TypeOp
    {
        /// The metatype of the output
        private type_metatype metaout;
        /// The metatype of the inputs
        private type_metatype metain;
        
        protected override void setMetatypeIn(type_metatype val)
        {
            metain = val;
        }

        protected override void setMetatypeOut(type_metatype val)
        {
            metaout = val;
        }
        
        public TypeOpFunc(TypeFactory t, OpCode opc, string n,type_metatype mout, type_metatype min)
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
            s.Write($" = {getOperatorName(op)}(");
            Varnode.printRaw(s, op.getIn(0));
            for (int i = 1; i < op.numInput(); ++i) {
                s.Write(',');
                Varnode.printRaw(s, op.getIn(i));
            }
            s.Write(')');
        }
    }
}
