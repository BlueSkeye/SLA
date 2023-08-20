using Sla.CORE;
using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleAndCompare : Rule
    {
        public RuleAndCompare(string g)
            : base(g, 0, "andcompare")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleAndCompare(getGroup());
        }

        /// \class RuleAndCompare
        /// \brief Simplify INT_ZEXT and SUBPIECE in masked comparison: `zext(V) & c == 0  =>  V & (c & mask) == 0`
        ///
        /// Similarly:  `sub(V,c) & d == 0  =>  V & (d & mask) == 0`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_EQUAL);
            oplist.Add(OpCode.CPUI_INT_NOTEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant()) return 0;
            if (op.getIn(1).getOffset() != 0) return 0;

            Varnode andvn;
            Varnode subvn;
            Varnode basevn;
            Varnode constvn;
            PcodeOp andop;
            PcodeOp subop;
            ulong andconst, baseconst;

            andvn = op.getIn(0);
            if (!andvn.isWritten()) return 0;
            andop = andvn.getDef();
            if (andop.code() != OpCode.CPUI_INT_AND) return 0;
            if (!andop.getIn(1).isConstant()) return 0;
            subvn = andop.getIn(0);
            if (!subvn.isWritten()) return 0;
            subop = subvn.getDef();
            switch (subop.code())
            {
                case OpCode.CPUI_SUBPIECE:
                    basevn = subop.getIn(0);
                    baseconst = andop.getIn(1).getOffset();
                    andconst = baseconst << subop.getIn(1).getOffset() * 8;
                    break;
                case OpCode.CPUI_INT_ZEXT:
                    basevn = subop.getIn(0);
                    baseconst = andop.getIn(1).getOffset();
                    andconst = baseconst & Globals.calc_mask(basevn.getSize());
                    break;
                default:
                    return 0;
            }

            if (baseconst == Globals.calc_mask(andvn.getSize())) return 0; // Degenerate AND
            if (basevn.isFree()) return 0;

            constvn = data.newConstant(basevn.getSize(), andconst);
            if (baseconst == andconst)          // If no effective change in constant (except varnode size)
                constvn.copySymbol(andop.getIn(1));   // Keep any old symbol
                                                        // New version of and with bigger inputs
            PcodeOp newop = data.newOp(2, andop.getAddr());
            data.opSetOpcode(newop, OpCode.CPUI_INT_AND);
            Varnode newout = data.newUniqueOut(basevn.getSize(), newop);
            data.opSetInput(newop, basevn, 0);
            data.opSetInput(newop, constvn, 1);
            data.opInsertBefore(newop, andop);

            data.opSetInput(op, newout, 0);
            data.opSetInput(op, data.newConstant(basevn.getSize(), 0), 1);
            return 1;
        }
    }
}
