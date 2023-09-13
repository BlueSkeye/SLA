using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleEquality : Rule
    {
        public RuleEquality(string g)
            : base(g, 0, "equality")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleEquality(getGroup());
        }

        /// \class RuleEquality
        /// \brief Collapse INT_EQUAL and INT_NOTEQUAL:  `f(V,W) == f(V,W)  =>  true`
        ///
        /// If both inputs to an INT_EQUAL or INT_NOTEQUAL op are functionally equivalent,
        /// the op can be collapsed to a COPY of a \b true or \b false.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_EQUAL);
            oplist.Add(OpCode.CPUI_INT_NOTEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn;
            if (!PcodeOpBank.functionalEquality(op.getIn(0), op.getIn(1)))
                return 0;

            data.opSetOpcode(op, OpCode.CPUI_COPY);
            data.opRemoveInput(op, 1);
            vn = data.newConstant(1, (op.code() == OpCode.CPUI_INT_EQUAL) ? 1UL : 0);
            data.opSetInput(op, vn, 0);
            return 1;
        }
    }
}
