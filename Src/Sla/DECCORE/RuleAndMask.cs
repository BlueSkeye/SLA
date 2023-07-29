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
    internal class RuleAndMask : Rule
    {
        public RuleAndMask(string g)
            : base(g, 0, "andmask")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleAndMask(getGroup());
        }

        /// \class RuleAndMask
        /// \brief Collapse unnecessary INT_AND
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_AND);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            uintb mask1, mask2, andmask;
            int4 size = op->getOut()->getSize();
            Varnode* vn;

            if (size > sizeof(uintb)) return 0; // FIXME: uintb should be arbitrary precision
            mask1 = op->getIn(0)->getNZMask();
            if (mask1 == 0)
                andmask = 0;
            else
            {
                mask2 = op->getIn(1)->getNZMask();
                andmask = mask1 & mask2;
            }

            if (andmask == 0)       // Result of AND is always zero
                vn = data.newConstant(size, 0);
            else if ((andmask & op->getOut()->getConsume()) == 0)
                vn = data.newConstant(size, 0);
            else if (andmask == mask1)
            {
                if (!op->getIn(1)->isConstant()) return 0;
                vn = op->getIn(0);      // Result of AND is equal to input(0)
            }
            else
                return 0;
            if (!vn->isHeritageKnown()) return 0;

            data.opSetOpcode(op, CPUI_COPY);
            data.opRemoveInput(op, 1);
            data.opSetInput(op, vn, 0);
            return 1;
        }
    }
}