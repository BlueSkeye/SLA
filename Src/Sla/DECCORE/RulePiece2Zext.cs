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
    internal class RulePiece2Zext : Rule
    {
        public RulePiece2Zext(string g)
            : base(g, 0, "piece2zext")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePiece2Zext(getGroup());
        }

        /// \class RulePiece2Zext
        /// \brief Concatenation with 0 becomes an extension:  `V = concat(#0,W)  =>  V = zext(W)`
        public override void getOpList(vector<uint4> oplist)
        {
            oplist.push_back(CPUI_PIECE);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* constvn;

            constvn = op->getIn(0); // Constant must be most significant bits
            if (!constvn->isConstant()) return 0;   // Must append with constant
            if (constvn->getOffset() != 0) return 0; // of value 0
            data.opRemoveInput(op, 0);  // Remove the constant
            data.opSetOpcode(op, CPUI_INT_ZEXT);
            return 1;
        }
    }
}