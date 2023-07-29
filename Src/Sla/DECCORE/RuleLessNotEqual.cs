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
    internal class RuleLessNotEqual : Rule
    {
        public RuleLessNotEqual(string g)
            : base(g, 0, "lessnotequal")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleLessNotEqual(getGroup());
        }

        /// \class RuleLessNotEqual
        /// \brief Simplify INT_LESSEQUAL && INT_NOTEQUAL:  `V <= W && V != W  =>  V < W`
        ///
        /// Handle INT_SLESSEQUAL variant.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_BOOL_AND);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {               // Convert [(s)lessequal AND notequal] to (s)less
            Varnode* compvn1,*compvn2,*vnout1,*vnout2;
            PcodeOp* op_less,*op_equal;
            OpCode opc;

            vnout1 = op->getIn(0);
            if (!vnout1->isWritten()) return 0;
            vnout2 = op->getIn(1);
            if (!vnout2->isWritten()) return 0;
            op_less = vnout1->getDef();
            opc = op_less->code();
            if ((opc != CPUI_INT_LESSEQUAL) && (opc != CPUI_INT_SLESSEQUAL))
            {
                op_equal = op_less;
                op_less = vnout2->getDef();
                opc = op_less->code();
                if ((opc != CPUI_INT_LESSEQUAL) && (opc != CPUI_INT_SLESSEQUAL))
                    return 0;
            }
            else
                op_equal = vnout2->getDef();
            if (op_equal->code() != CPUI_INT_NOTEQUAL) return 0;

            compvn1 = op_less->getIn(0);
            compvn2 = op_less->getIn(1);
            if (!compvn1->isHeritageKnown()) return 0;
            if (!compvn2->isHeritageKnown()) return 0;
            if (((*compvn1 != *op_equal->getIn(0)) || (*compvn2 != *op_equal->getIn(1))) &&
                ((*compvn1 != *op_equal->getIn(1)) || (*compvn2 != *op_equal->getIn(0))))
                return 0;

            data.opSetInput(op, compvn1, 0);
            data.opSetInput(op, compvn2, 1);
            data.opSetOpcode(op, (opc == CPUI_INT_SLESSEQUAL) ? CPUI_INT_SLESS : CPUI_INT_LESS);

            return 1;
        }
    }
}