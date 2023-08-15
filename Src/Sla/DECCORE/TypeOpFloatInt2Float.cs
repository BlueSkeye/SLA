using Sla.CORE;

namespace Sla.DECCORE
{
    internal class TypeOpFloatInt2Float : TypeOpFunc
    {
        public TypeOpFloatInt2Float(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_INT2FLOAT,"INT2FLOAT", type_metatype.TYPE_FLOAT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatInt2Float(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatInt2Float(op);
        }
    }
}
