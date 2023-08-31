using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleZextCommute : Rule
    {
        public RuleZextCommute(string g)
            : base(g, 0, "zextcommute")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleZextCommute(getGroup());
        }

        /// \class RuleZextCommute
        /// \brief Commute INT_ZEXT with INT_RIGHT: `zext(V) >> W  =>  zext(V >> W)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_RIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode zextvn = op.getIn(0) ?? throw new ApplicationException();
            if (!zextvn.isWritten()) return 0;
            PcodeOp zextop = zextvn.getDef() ?? throw new ApplicationException();
            if (zextop.code() != OpCode.CPUI_INT_ZEXT) return 0;
            Varnode zextin = zextop.getIn(0) ?? throw new ApplicationException();
            if (zextin.isFree()) return 0;
            Varnode savn = op.getIn(1) ?? throw new ApplicationException();
            if ((!savn.isConstant()) && (savn.isFree()))
                return 0;

            PcodeOp newop = data.newOp(2, op.getAddr());
            data.opSetOpcode(newop, OpCode.CPUI_INT_RIGHT);
            Varnode newout = data.newUniqueOut(zextin.getSize(), newop);
            data.opRemoveInput(op, 1);
            data.opSetInput(op, newout, 0);
            data.opSetOpcode(op, OpCode.CPUI_INT_ZEXT);
            data.opSetInput(newop, zextin, 0);
            data.opSetInput(newop, savn, 1);
            data.opInsertBefore(newop, op);
            return 1;
        }
    }
}
