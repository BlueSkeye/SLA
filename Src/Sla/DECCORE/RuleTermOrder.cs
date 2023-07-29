using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleTermOrder : Rule
    {
        public RuleTermOrder(string g)
            : base(g, 0, "termorder")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleTermOrder(getGroup());
        }

        /// \class RuleTermOrder
        /// \brief Order the inputs to commutative operations
        ///
        /// Constants always come last in particular which eliminates
        /// some of the combinatorial explosion of expression variations.
        public override void getOpList(List<uint4> oplist)
        {
            // FIXME:  All the commutative ops
            // Use the TypeOp::commutative function
            uint4 list[] ={ CPUI_INT_EQUAL, CPUI_INT_NOTEQUAL, CPUI_INT_ADD, CPUI_INT_CARRY,
         CPUI_INT_SCARRY, CPUI_INT_XOR, CPUI_INT_AND, CPUI_INT_OR,
         CPUI_INT_MULT, CPUI_BOOL_XOR, CPUI_BOOL_AND, CPUI_BOOL_OR,
         CPUI_FLOAT_EQUAL, CPUI_FLOAT_NOTEQUAL, CPUI_FLOAT_ADD,
         CPUI_FLOAT_MULT };
            oplist.insert(oplist.end(), list, list + 16);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn1 = op.getIn(0);
            Varnode* vn2 = op.getIn(1);

            if (vn1.isConstant() && (!vn2.isConstant()))
            {
                data.opSwapInput(op, 0, 1); // Reverse the order of the terms
                return 1;
            }
            return 0;
        }
    }
}
