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
    internal class RuleSubvarAnd : Rule
    {
        public RuleSubvarAnd(string g)
            : base(g, 0, "subvar_and")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSubvarAnd(getGroup());
        }

        /// \class RuleSubvarAnd
        /// \brief Perform SubVariableFlow analysis triggered by INT_AND
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_AND);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            if (!op->getIn(1)->isConstant()) return 0;
            Varnode* vn = op->getIn(0);
            Varnode* outvn = op->getOut();
            //  if (vn->getSize() != 1) return 0; // Only for bitsize variables
            if (outvn->getConsume() != op->getIn(1)->getOffset()) return 0;
            if ((outvn->getConsume() & 1) == 0) return 0;
            uintb cmask;
            if (outvn->getConsume() == (uintb)1)
                cmask = (uintb)1;
            else
            {
                cmask = calc_mask(vn->getSize());
                cmask >>= 8;
                while (cmask != 0)
                {
                    if (cmask == outvn->getConsume()) break;
                    cmask >>= 8;
                }
            }
            if (cmask == 0) return 0;
            //  if (vn->getConsume() == 0) return 0;
            //  if ((vn->getConsume() & 0xff)==0xff) return 0;
            //  if (op->getIn(1)->getOffset() != (uintb)1) return 0;
            if (op->getOut()->hasNoDescend()) return 0;
            SubvariableFlow subflow(&data,vn,cmask,false,false,false);
            if (!subflow.doTrace()) return 0;
            subflow.doReplacement();
            return 1;
        }
    }
}