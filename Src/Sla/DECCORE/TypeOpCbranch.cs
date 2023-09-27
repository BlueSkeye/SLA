using Sla.CORE;

namespace Sla.DECCORE
{
    internal class TypeOpCbranch : TypeOp
    {
        public TypeOpCbranch(TypeFactory t)
            : base(t, OpCode.CPUI_CBRANCH,"goto")
        {
            opflags = (PcodeOp.Flags.special | PcodeOp.Flags.branch | PcodeOp.Flags.coderef |
                PcodeOp.Flags.nocollapse);
            behave = new OpBehavior(OpCode.CPUI_CBRANCH, false, true); // Dummy behavior
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            Datatype td;

            if (slot == 1)
                return tlst.getBase(op.getIn(1).getSize(), type_metatype.TYPE_BOOL); // Second param is bool
            td = tlst.getTypeCode();
            AddrSpace spc = op.getIn(0).getSpace();
            return tlst.getTypePointer(op.getIn(0).getSize(), td, spc.getWordSize()); // First parameter is code pointer
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCbranch(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s.Write($"{name} ");
            Varnode.printRaw(s, op.getIn(0)); // Print the distant (non-fallthru) destination
            s.Write(" if (");
            Varnode.printRaw(s, op.getIn(1));
            if (op.isBooleanFlip() ^ op.isFallthruTrue())
                s.Write(" == 0)");
            else
                s.Write(" != 0)");
        }
    }
}
