using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSubvarCompZero : Rule
    {
        public RuleSubvarCompZero(string g)
            : base(g, 0, "subvar_compzero")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubvarCompZero(getGroup());
        }

        /// \class RuleSubvarCompZero
        /// \brief Perform SubvariableFlow analysis triggered by testing of a single bit
        ///
        /// Given a comparison (INT_EQUAL or INT_NOTEEQUAL_ to a constant,
        /// check that input has only 1 bit that can possibly be non-zero
        /// and that the constant is testing this.  This then triggers
        /// the full SubvariableFlow analysis.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_NOTEQUAL);
            oplist.Add(OpCode.CPUI_INT_EQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant())
                return 0;
            Varnode vn = op.getIn(0);
            ulong mask = vn.getNZMask();
            int bitnum = Globals.leastsigbit_set(mask);
            if (bitnum == -1)
                return 0;
            if ((mask >> bitnum) != 1)
                // Check if only one bit active
                return 0;

            // Check if the active bit is getting tested
            if (   (op.getIn(1).getOffset() != mask)
                && (op.getIn(1).getOffset() != 0))
                return 0;

            if (op.getOut().hasNoDescend())
                return 0;
            // We do a basic check that the stream from which it looks like
            // the bit is getting pulled is not fully consumed
            if (vn.isWritten())
            {
                PcodeOp andop = vn.getDef();
                if (andop.numInput() == 0)
                    return 0;
                Varnode vn0 = andop.getIn(0);
                switch (andop.code()) {
                    case OpCode.CPUI_INT_AND:
                    case OpCode.CPUI_INT_OR:
                    case OpCode.CPUI_INT_RIGHT:
                        if (vn0.isConstant())
                            return 0;
                        ulong mask0 = vn0.getConsume() & vn0.getNZMask();
                        ulong wholemask = Globals.calc_mask((uint)vn0.getSize()) & mask0;
                        // We really need a popcnt here
                        // We want: if the number of bits that are both consumed
                        // and not known to be zero are "big" then don't continue
                        // because it doesn't look like a few bits getting manipulated
                        // within a status register
                        if ((wholemask & 0xff) == 0xff)
                            return 0;
                        if ((wholemask & 0xff00) == 0xff00)
                            return 0;
                        break;
                    default:
                        break;
                }
            }

            SubvariableFlow subflow = new SubvariableFlow(data,vn,mask,false,false,false);
            if (!subflow.doTrace()) {
                return 0;
            }
            subflow.doReplacement();
            return 1;
        }
    }
}
