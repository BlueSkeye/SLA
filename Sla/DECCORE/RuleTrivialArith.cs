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
    internal class RuleTrivialArith : Rule
    {
        public RuleTrivialArith(string g)
            : base(g, 0, "trivialarith")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleTrivialArith(getGroup());
        }

        /// \class RuleTrivialArith
        /// \brief Simplify trivial arithmetic expressions
        ///
        /// All forms are binary operations where both inputs hold the same value.
        ///   - `V == V  =>  true`
        ///   - `V != V  =>  false`
        ///   - `V < V   => false`
        ///   - `V <= V  => true`
        ///   - `V & V   => V`
        ///   - `V | V  => V`
        ///   - `V ^ V   => #0`
        ///
        /// Handles other signed, boolean, and floating-point variants.
        public override void getOpList(List<uint4> oplist)
        {
            uint4 list[] ={ CPUI_INT_NOTEQUAL, CPUI_INT_SLESS, CPUI_INT_LESS, CPUI_BOOL_XOR, CPUI_BOOL_AND, CPUI_BOOL_OR,
         CPUI_INT_EQUAL, CPUI_INT_SLESSEQUAL, CPUI_INT_LESSEQUAL,
         CPUI_INT_XOR, CPUI_INT_AND, CPUI_INT_OR,
                 CPUI_FLOAT_EQUAL, CPUI_FLOAT_NOTEQUAL, CPUI_FLOAT_LESS, CPUI_FLOAT_LESSEQUAL };
            oplist.insert(oplist.end(), list, list + 16);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn;
            Varnode* in0,*in1;

            if (op->numInput() != 2) return 0;
            in0 = op->getIn(0);
            in1 = op->getIn(1);
            if (in0 != in1)
            {       // Inputs must be identical
                if (!in0->isWritten()) return 0;
                if (!in1->isWritten()) return 0;
                if (!in0->getDef()->isCseMatch(in1->getDef())) return 0; // or constructed identically
            }
            switch (op->code())
            {

                case CPUI_INT_NOTEQUAL: // Boolean 0
                case CPUI_INT_SLESS:
                case CPUI_INT_LESS:
                case CPUI_BOOL_XOR:
                case CPUI_FLOAT_NOTEQUAL:
                case CPUI_FLOAT_LESS:
                    vn = data.newConstant(1, 0);
                    break;
                case CPUI_INT_EQUAL:        // Boolean 1
                case CPUI_INT_SLESSEQUAL:
                case CPUI_INT_LESSEQUAL:
                case CPUI_FLOAT_EQUAL:
                case CPUI_FLOAT_LESSEQUAL:
                    vn = data.newConstant(1, 1);
                    break;
                case CPUI_INT_XOR:      // Same size 0
                                        //  case CPUI_INT_SUB:
                    vn = data.newConstant(op->getOut()->getSize(), 0);
                    break;
                case CPUI_BOOL_AND:     // Identity
                case CPUI_BOOL_OR:
                case CPUI_INT_AND:
                case CPUI_INT_OR:
                    vn = (Varnode*)0;
                    break;
                default:
                    return 0;
            }

            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, CPUI_COPY);
            if (vn != (Varnode*)0)
                data.opSetInput(op, vn, 0);

            return 1;
        }
    }
}
