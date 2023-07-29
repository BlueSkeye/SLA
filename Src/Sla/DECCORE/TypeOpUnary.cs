using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A generic unary operator: one input and one output
    ///
    /// All unary op-codes have data-type for the input value and a
    /// data-type for the output value
    internal class TypeOpUnary : TypeOp
    {
        private type_metatype metaout;  ///< The metatype of the output
        private type_metatype metain;       ///< The metatype of the input
        
        protected override void setMetatypeIn(type_metatype val)
        {
            metain = val;
        }

        protected override void setMetatypeOut(type_metatype val)
        {
            metaout = val;
        }
        
        public TypeOpUnary(TypeFactory t, OpCode opc, string n, type_metatype mout, type_metatype min)
            : base(t, opc, n)
        {
            metaout = mout;
            metain = min;
        }

        public override Datatype getOutputLocal(PcodeOp op) => tlst->getBase(op->getOut()->getSize(), metaout);

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
            => tlst->getBase(op->getIn(slot)->getSize(), metain);

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode::printRaw(s, op->getOut());
            s << " = " << getOperatorName(op) << ' ';
            Varnode::printRaw(s, op->getIn(0));
        }
    }
}
