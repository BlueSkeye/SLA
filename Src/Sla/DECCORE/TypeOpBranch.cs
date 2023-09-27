using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the BRANCH op-code
    internal class TypeOpBranch : TypeOp
    {
        public TypeOpBranch(TypeFactory t)
            : base(t, OpCode.CPUI_BRANCH,"goto")
        {
            opflags = (PcodeOp.Flags.special | PcodeOp.Flags.branch | PcodeOp.Flags.coderef |
                PcodeOp.Flags.nocollapse);
            behave = new OpBehavior(OpCode.CPUI_BRANCH, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBranch(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s.Write($"{name} ");
            Varnode.printRaw(s, op.getIn(0));
        }
    }
}
