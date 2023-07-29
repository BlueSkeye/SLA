using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class EquationCat : PatternEquation
    {
        // Pattern Equations concatenated
        private PatternEquation left;
        private PatternEquation right;

        ~EquationCat()
        {
            PatternEquation::release(left);
            PatternEquation::release(right);
        }

        public EquationCat(PatternEquation l, PatternEquation r)
        {
            (left = l)->layClaim();
            (right = r)->layClaim();
        }

        public override void genPattern(List<TokenPattern> ops)
        {
            left->genPattern(ops);
            right->genPattern(ops);
            resultpattern = left->getTokenPattern().doCat(right->getTokenPattern());
        }

        public override bool resolveOperandLeft(OperandResolve state)
        {
            bool res = left->resolveOperandLeft(state);
            if (!res) return false;
            int4 cur_base = state.base;
            int4 cur_offset = state.offset;
            if ((!left->getTokenPattern().getLeftEllipsis()) && (!left->getTokenPattern().getRightEllipsis()))
            {
                // Keep the same base
                state.offset += left->getTokenPattern().getMinimumLength(); // But add to its size
            }
            else if (state.cur_rightmost != -1)
            {
                state.base = state.cur_rightmost;
                state.offset = state.size;
            }
            else if (state.size != -1)
            {
                state.offset += state.size;
            }
            else
            {
                state.base = -2;        // We have no anchor
            }
            int4 cur_rightmost = state.cur_rightmost;
            int4 cur_size = state.size;
            res = right->resolveOperandLeft(state);
            if (!res) return false;
            state.base = cur_base;  // Restore base and offset
            state.offset = cur_offset;
            if (state.cur_rightmost == -1)
            {
                if ((state.size != -1) && (cur_rightmost != -1) && (cur_size != -1))
                {
                    state.cur_rightmost = cur_rightmost;
                    state.size += cur_size;
                }
            }
            return true;
        }

        public override void operandOrder(Constructor ct, List<OperandSymbol> order)
        {
            left->operandOrder(ct, order);  // List operands left
            right->operandOrder(ct, order); //  to right
        }
    }
}
