using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSelectCse : Rule
    {
        public RuleSelectCse(string g)
            : base(g,0,"selectcse")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null : new RuleSelectCse(getGroup());
        }

        /// \class RuleSelectCse
        /// \brief Look for common sub-expressions (built out of a restricted set of ops)
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
            oplist.Add(OpCode.CPUI_INT_SRIGHT); // For division optimization corrections
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn = op.getIn(0) ?? throw new ApplicationException();
            OpCode opc = op.code();
            PcodeOp otherop;
            uint hash;
            List<Tuple<uint, PcodeOp>> list = new List<Tuple<uint, PcodeOp>>();
            List<Varnode> vlist = new List<Varnode>();

            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                otherop = iter.Current;
                if (otherop.code() != opc) continue;
                hash = otherop.getCseHash();
                if (hash == 0) continue;
                list.Add(new Tuple<uint, PcodeOp>(hash, otherop));
            }
            if (list.size() <= 1) return 0;
            cseEliminateList(data, list, vlist);
            if (vlist.empty()) return 0;
            return 1;
        }
    }
}
