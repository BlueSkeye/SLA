using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpFloatInt2Float : TypeOpFunc
    {
        public TypeOpFloatInt2Float(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_INT2FLOAT,"INT2FLOAT", TYPE_FLOAT, TYPE_INT)
        {
            opflags = PcodeOp::unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatInt2Float(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatInt2Float(op);
        }
    }
}
