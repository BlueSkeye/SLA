using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RulePopcountBoolXor : Rule
    {
        public RulePopcountBoolXor(string g)
            : base(g, 0, "popcountboolxor")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RulePopcountBoolXor(getGroup());
        }

        /// \class RulePopcountBoolXor
        /// \brief Simplify boolean expressions that are combined through POPCOUNT
        ///
        /// Expressions involving boolean values (b1 and b2) are converted, such as:
        ///  - `popcount((b1 << 6) | (b2 << 2)) & 1  =>   b1 ^ b2`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_POPCOUNT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode outVn = op.getOut();
            IEnumerator<PcodeOp> iter = outVn.beginDescend();

            while (iter.MoveNext()) {
                PcodeOp baseOp = iter.Current;
                if (baseOp.code() != OpCode.CPUI_INT_AND) continue;
                Varnode tmpVn = baseOp.getIn(1);
                if (!tmpVn.isConstant()) continue;
                if (tmpVn.getOffset() != 1) continue;  // Masking 1 bit means we are checking parity of POPCOUNT input
                if (tmpVn.getSize() != 1) continue;    // Must be boolean sized output
                Varnode inVn = op.getIn(0);
                if (!inVn.isWritten()) return 0;
                int count = Globals.popcount(inVn.getNZMask());
                if (count == 1) {
                    int leastPos = Globals.leastsigbit_set(inVn.getNZMask());
                    int constRes;
                    Varnode? b1 = getBooleanResult(inVn, leastPos, out constRes);
                    if (b1 == (Varnode)null) continue;
                    data.opSetOpcode(baseOp, OpCode.CPUI_COPY);    // Recognized  Globals.popcount( b1 << #pos ) & 1
                    data.opRemoveInput(baseOp, 1);      // Simplify to  COPY(b1)
                    data.opSetInput(baseOp, b1, 0);
                    return 1;
                }
                if (count == 2) {
                    int pos0 = Globals.leastsigbit_set(inVn.getNZMask());
                    int pos1 = Globals.mostsigbit_set(inVn.getNZMask());
                    int constRes0;
                    Varnode? b1 = getBooleanResult(inVn, pos0, out constRes0);
                    if (b1 == (Varnode)null && constRes0 != 1) continue;
                    int constRes1;
                    Varnode? b2 = getBooleanResult(inVn, pos1, out constRes1);
                    if (b2 == (Varnode)null && constRes1 != 1) continue;
                    if (b1 == (Varnode)null && b2 == (Varnode)null) continue;
                    if (b1 == (Varnode)null)
                        b1 = data.newConstant(1, 1);
                    if (b2 == (Varnode)null)
                        b2 = data.newConstant(1, 1);
                    data.opSetOpcode(baseOp, OpCode.CPUI_INT_XOR); // Recognized  Globals.popcount ( b1 << #pos1 | b2 << #pos2 ) & 1
                    data.opSetInput(baseOp, b1, 0);
                    data.opSetInput(baseOp, b2, 1);
                    return 1;
                }
            }
            return 0;
        }

        /// \brief Extract boolean Varnode producing bit at given Varnode and position
        ///
        /// The boolean value may be shifted, extended and combined with other booleans through a
        /// series of operations. We return the Varnode that is the
        /// actual result of the boolean operation.  If the given Varnode is constant, return
        /// null but pass back whether the given bit position is 0 or 1.  If no boolean value can be
        /// found, return null and pass back -1.
        /// \param vn is the given Varnode containing the extended/shifted boolean
        /// \param bitPos is the bit position of the desired boolean value
        /// \param constRes is used to pass back a constant boolean result
        /// \return the boolean Varnode producing the desired value or null
        public static Varnode? getBooleanResult(Varnode vn, int bitPos, out int constRes)
        {
            constRes = -1;
            ulong mask = 1;
            mask <<= bitPos;
            Varnode vn0;
            Varnode vn1;
            int sa;
            while(true) {
                if (vn.isConstant()) {
                    constRes = (int)(vn.getOffset() >> bitPos) & 1;
                    return (Varnode)null;
                }
                if (!vn.isWritten()) return (Varnode)null;
                if (bitPos == 0 && vn.getSize() == 1 && vn.getNZMask() == mask)
                    return vn;
                PcodeOp op = vn.getDef() ?? throw new ApplicationException();
                switch (op.code()) {
                    case OpCode.CPUI_INT_AND:
                        if (!op.getIn(1).isConstant()) return (Varnode)null;
                        vn = op.getIn(0);
                        break;
                    case OpCode.CPUI_INT_XOR:
                    case OpCode.CPUI_INT_OR:
                        vn0 = op.getIn(0);
                        vn1 = op.getIn(1);
                        if ((vn0.getNZMask() & mask) != 0) {
                            if ((vn1.getNZMask() & mask) != 0)
                                // Don't have a unique path
                                return (Varnode)null;
                            vn = vn0;
                        }
                        else if ((vn1.getNZMask() & mask) != 0) {
                            vn = vn1;
                        }
                        else
                            return (Varnode)null;
                        break;
                    case OpCode.CPUI_INT_ZEXT:
                    case OpCode.CPUI_INT_SEXT:
                        vn = op.getIn(0);
                        if (bitPos >= vn.getSize() * 8) return (Varnode)null;
                        break;
                    case OpCode.CPUI_SUBPIECE:
                        sa = (int)op.getIn(1).getOffset() * 8;
                        bitPos += sa;
                        mask <<= sa;
                        vn = op.getIn(0);
                        break;
                    case OpCode.CPUI_PIECE:
                        vn0 = op.getIn(0);
                        vn1 = op.getIn(1);
                        sa = (int)vn1.getSize() * 8;
                        if (bitPos >= sa) {
                            vn = vn0;
                            bitPos -= sa;
                            mask >>= sa;
                        }
                        else {
                            vn = vn1;
                        }
                        break;
                    case OpCode.CPUI_INT_LEFT:
                        vn1 = op.getIn(1);
                        if (!vn1.isConstant()) return (Varnode)null;
                        sa = (int)vn1.getOffset();
                        if (sa > bitPos) return (Varnode)null;
                        bitPos -= sa;
                        mask >>= sa;
                        vn = op.getIn(0);
                        break;
                    case OpCode.CPUI_INT_RIGHT:
                    case OpCode.CPUI_INT_SRIGHT:
                        vn1 = op.getIn(1);
                        if (!vn1.isConstant()) return (Varnode)null;
                        sa = (int)vn1.getOffset();
                        vn = op.getIn(0);
                        bitPos += sa;
                        if (bitPos >= vn.getSize() * 8) return (Varnode)null;
                        mask <<= sa;
                        break;
                    default:
                        return (Varnode)null;
                }
            }
        }
    }
}
