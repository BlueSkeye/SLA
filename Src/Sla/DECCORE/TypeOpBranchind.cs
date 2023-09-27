using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the BRANCHIND op-code
    internal class TypeOpBranchind : TypeOp
    {
        public TypeOpBranchind(TypeFactory t)
            : base(t, OpCode.CPUI_BRANCHIND,"switch")
        {
            opflags = PcodeOp.Flags.special | PcodeOp.Flags.branch | PcodeOp.Flags.nocollapse;
            behave = new OpBehavior(OpCode.CPUI_BRANCHIND, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBranchind(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s.Write($"{name} ");
            Varnode.printRaw(s, op.getIn(0));
        }
    }
}
