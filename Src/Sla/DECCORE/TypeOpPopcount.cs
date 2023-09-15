using Sla.CORE;

namespace Sla.DECCORE
{
    internal class TypeOpPopcount : TypeOpFunc
    {
        public TypeOpPopcount(TypeFactory t)
            : base(t, OpCode.CPUI_POPCOUNT,"POPCOUNT", type_metatype.TYPE_INT, type_metatype.TYPE_UNKNOWN)
        {
            opflags = PcodeOp.Flags.unary;
            behave = new OpBehaviorPopcount();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opPopcountOp(op);
        }
    }
}
