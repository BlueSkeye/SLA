﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleDivChain : Rule
    {
        public RuleDivChain(string g)
            : base(g, 0, "divchain")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleDivChain(getGroup());
        }

        /// \class RuleDivChain
        /// \brief Collapse two consecutive divisions:  `(x / c1) / c2  =>  x / (c1*c2)`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_DIV);
            oplist.push_back(CPUI_INT_SDIV);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            OpCode opc2 = op->code();
            Varnode* constVn2 = op->getIn(1);
            if (!constVn2->isConstant()) return 0;
            Varnode* vn = op->getIn(0);
            if (!vn->isWritten()) return 0;
            PcodeOp* divOp = vn->getDef();
            OpCode opc1 = divOp->code();
            if (opc1 != opc2 && (opc2 != CPUI_INT_DIV || opc1 != CPUI_INT_RIGHT))
                return 0;
            Varnode* constVn1 = divOp->getIn(1);
            if (!constVn1->isConstant()) return 0;
            // If the intermediate result is being used elsewhere, don't apply
            // Its likely collapsing the divisions will interfere with the modulo rules
            if (vn->loneDescend() == (PcodeOp*)0) return 0;
            uintb val1;
            if (opc1 == opc2)
            {
                val1 = constVn1->getOffset();
            }
            else
            {   // Unsigned case with INT_RIGHT
                int4 sa = constVn1->getOffset();
                val1 = 1;
                val1 <<= sa;
            }
            Varnode* baseVn = divOp->getIn(0);
            if (baseVn->isFree()) return 0;
            int4 sz = vn->getSize();
            uintb val2 = constVn2->getOffset();
            uintb resval = (val1 * val2) & calc_mask(sz);
            if (resval == 0) return 0;
            if (signbit_negative(val1, sz))
                val1 = (~val1 + 1) & calc_mask(sz);
            if (signbit_negative(val2, sz))
                val2 = (~val2 + 1) & calc_mask(sz);
            int4 bitcount = mostsigbit_set(val1) + mostsigbit_set(val2) + 2;
            if (opc2 == CPUI_INT_DIV && bitcount > sz * 8) return 0;    // Unsigned overflow
            if (opc2 == CPUI_INT_SDIV && bitcount > sz * 8 - 2) return 0;   // Signed overflow
            data.opSetInput(op, baseVn, 0);
            data.opSetInput(op, data.newConstant(sz, resval), 1);
            return 1;
        }
    }
}