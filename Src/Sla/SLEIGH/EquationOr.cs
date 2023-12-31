﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class EquationOr : PatternEquation
    {
        // Pattern Equations ORed together
        private PatternEquation left;
        private PatternEquation right;

        ~EquationOr()
        {
            PatternEquation.release(left);
            PatternEquation.release(right);
        }

        public EquationOr(PatternEquation l, PatternEquation r)
        {
            (left = l).layClaim();
            (right = r).layClaim();
        }

        public override void genPattern(List<TokenPattern> ops)
        {
            left.genPattern(ops);
            right.genPattern(ops);
            resultpattern = left.getTokenPattern().doOr(right.getTokenPattern());
        }

        public override bool resolveOperandLeft(OperandResolve state)
        {
            int cur_rightmost = -1;    // Initially we don't know our rightmost
            int cur_size = -1;     //   or size traversed since rightmost
            bool res = right.resolveOperandLeft(state);
            if (!res) return false;
            if ((state.cur_rightmost != -1) && (state.size != -1))
            {
                cur_rightmost = state.cur_rightmost;
                cur_size = state.size;
            }
            res = left.resolveOperandLeft(state);
            if (!res) return false;
            if ((state.cur_rightmost == -1) || (state.size == -1))
            {
                state.cur_rightmost = cur_rightmost;
                state.size = cur_size;
            }
            return true;
        }

        public override void operandOrder(Constructor ct, List<OperandSymbol> order)
        {
            left.operandOrder(ct, order);  // List operands left
            right.operandOrder(ct, order); //  to right
        }
    }
}
