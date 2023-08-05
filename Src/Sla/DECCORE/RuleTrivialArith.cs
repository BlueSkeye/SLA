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
            if (!grouplist.contains(getGroup())) return (Rule)null;
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
        public override void getOpList(List<OpCode> oplist)
        {
            uint list[] ={ OpCode.CPUI_INT_NOTEQUAL, OpCode.CPUI_INT_SLESS, OpCode.CPUI_INT_LESS, OpCode.CPUI_BOOL_XOR, OpCode.CPUI_BOOL_AND, OpCode.CPUI_BOOL_OR,
         OpCode.CPUI_INT_EQUAL, OpCode.CPUI_INT_SLESSEQUAL, OpCode.CPUI_INT_LESSEQUAL,
         OpCode.CPUI_INT_XOR, OpCode.CPUI_INT_AND, OpCode.CPUI_INT_OR,
                 OpCode.CPUI_FLOAT_EQUAL, OpCode.CPUI_FLOAT_NOTEQUAL, OpCode.CPUI_FLOAT_LESS, OpCode.CPUI_FLOAT_LESSEQUAL };
            oplist.insert(oplist.end(), list, list + 16);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn;
            Varnode* in0,*in1;

            if (op.numInput() != 2) return 0;
            in0 = op.getIn(0);
            in1 = op.getIn(1);
            if (in0 != in1)
            {       // Inputs must be identical
                if (!in0.isWritten()) return 0;
                if (!in1.isWritten()) return 0;
                if (!in0.getDef().isCseMatch(in1.getDef())) return 0; // or constructed identically
            }
            switch (op.code())
            {

                case OpCode.CPUI_INT_NOTEQUAL: // Boolean 0
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_BOOL_XOR:
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                case OpCode.CPUI_FLOAT_LESS:
                    vn = data.newConstant(1, 0);
                    break;
                case OpCode.CPUI_INT_EQUAL:        // Boolean 1
                case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_FLOAT_EQUAL:
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                    vn = data.newConstant(1, 1);
                    break;
                case OpCode.CPUI_INT_XOR:      // Same size 0
                                        //  case OpCode.CPUI_INT_SUB:
                    vn = data.newConstant(op.getOut().getSize(), 0);
                    break;
                case OpCode.CPUI_BOOL_AND:     // Identity
                case OpCode.CPUI_BOOL_OR:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                    vn = (Varnode)null;
                    break;
                default:
                    return 0;
            }

            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            if (vn != (Varnode)null)
                data.opSetInput(op, vn, 0);

            return 1;
        }
    }
}
