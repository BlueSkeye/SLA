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
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleTermOrder(getGroup());
        }

        /// \class RuleTermOrder
        /// \brief Order the inputs to commutative operations
        ///
        /// Constants always come last in particular which eliminates
        /// some of the combinatorial explosion of expression variations.
        public override void getOpList(List<OpCode> oplist)
        {
            // FIXME:  All the commutative ops
            // Use the TypeOp::commutative function
            uint list[] ={ OpCode.CPUI_INT_EQUAL, OpCode.CPUI_INT_NOTEQUAL, OpCode.CPUI_INT_ADD, OpCode.CPUI_INT_CARRY,
         OpCode.CPUI_INT_SCARRY, OpCode.CPUI_INT_XOR, OpCode.CPUI_INT_AND, OpCode.CPUI_INT_OR,
         OpCode.CPUI_INT_MULT, OpCode.CPUI_BOOL_XOR, OpCode.CPUI_BOOL_AND, OpCode.CPUI_BOOL_OR,
         OpCode.CPUI_FLOAT_EQUAL, OpCode.CPUI_FLOAT_NOTEQUAL, OpCode.CPUI_FLOAT_ADD,
         OpCode.CPUI_FLOAT_MULT };
            oplist.insert(oplist.end(), list, list + 16);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
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
