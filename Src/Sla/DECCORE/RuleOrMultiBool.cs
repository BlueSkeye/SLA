﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleOrMultiBool : Rule
    {
        public RuleOrMultiBool(string g)
            : base(g, 0, "ormultibool")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null :  new RuleOrMultiBool(getGroup());
        }

        /// \class RuleOrMultiBool
        /// \brief Simplify boolean expressions that are combined through INT_OR
        /// Convert expressions involving boolean values b1 and b2:
        ///  - `(b1 << 6) | (b2 << 2)  != 0  =>  b1 || b2
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_OR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode outVn = op.getOut() ?? throw new ApplicationException();
            IEnumerator<PcodeOp> iter = outVn.beginDescend();

            if (Globals.popcount(outVn.getNZMask()) != 2) return 0;
            while (iter.MoveNext()) {
                PcodeOp baseOp = iter.Current;
                OpCode opc = baseOp.code();
                // Result of INT_OR must be compared with zero
                if (opc != OpCode.CPUI_INT_EQUAL && opc != OpCode.CPUI_INT_NOTEQUAL) continue;
                Varnode zerovn = baseOp.getIn(1);
                if (!zerovn.isConstant()) continue;
                if (zerovn.getOffset() != 0) continue;
                int pos0 = Globals.leastsigbit_set(outVn.getNZMask());
                int pos1 = Globals.mostsigbit_set(outVn.getNZMask());
                int constRes0;
                int constRes1;
                Varnode? b1 = RulePopcountBoolXor.getBooleanResult(outVn, pos0, out constRes0);
                if (b1 == (Varnode)null && constRes0 != 1) continue;
                Varnode? b2 = RulePopcountBoolXor.getBooleanResult(outVn, pos1, out constRes1);
                if (b2 == (Varnode)null && constRes1 != 1) continue;
                if (b1 == (Varnode)null && b2 == (Varnode)null) continue;

                if (b1 == (Varnode)null)
                    b1 = data.newConstant(1, 1);
                if (b2 == (Varnode)null)
                    b2 = data.newConstant(1, 1);
                if (opc == OpCode.CPUI_INT_EQUAL) {
                    PcodeOp newOp = data.newOp(2, baseOp.getAddr());
                    Varnode notIn = data.newUniqueOut(1, newOp);
                    data.opSetOpcode(newOp, OpCode.CPUI_BOOL_OR);
                    data.opSetInput(newOp, b1, 0);
                    data.opSetInput(newOp, b2, 1);
                    data.opInsertBefore(newOp, baseOp);
                    data.opRemoveInput(baseOp, 1);
                    data.opSetInput(baseOp, notIn, 0);
                    data.opSetOpcode(baseOp, OpCode.CPUI_BOOL_NEGATE);
                }
                else {
                    data.opSetOpcode(baseOp, OpCode.CPUI_BOOL_OR);
                    data.opSetInput(baseOp, b1, 0);
                    data.opSetInput(baseOp, b2, 1);
                }
                return 1;
            }
            return 0;
        }
    }
}
