using Sla.CORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class PatternExpression
    {
        // Number of objects referencing this
        private int refcount;

        // for deletion
        ~PatternExpression()
        {
        }

        public PatternExpression()
        {
            refcount = 0;
        }
        
        public abstract long getValue(ParserWalker walker);

        public abstract TokenPattern genMinPattern(List<TokenPattern> ops);

        public abstract void listValues(List<PatternValue> list);

        public abstract void getMinMax(List<long> minlist, List<long> maxlist);

        public abstract long getSubValue(List<long> replace,int listpos);

        public abstract void saveXml(TextWriter s);

        public abstract void restoreXml(Element el, Translate trans);

        public abstract long getSubValue(List<long> replace) {
            int listpos = 0;
            return getSubValue(replace, listpos);
        }

        public abstract void layClaim()
        {
            refcount += 1;
        }
    
        public static void release(PatternExpression p)
        {
            p.refcount -= 1;
            if (p.refcount <= 0)
                delete p;
        }

        public static PatternExpression restoreExpression(Element el, Translate trans)
        {
            PatternExpression* res;
            string nm = el.getName();

            if (nm == "tokenfield")
                res = new TokenField();
            else if (nm == "contextfield")
                res = new ContextField();
            else if (nm == "long")
                res = new ConstantValue();
            else if (nm == "operand_exp")
                res = new OperandValue();
            else if (nm == "start_exp")
                res = new StartInstructionValue();
            else if (nm == "end_exp")
                res = new EndInstructionValue();
            else if (nm == "plus_exp")
                res = new PlusExpression();
            else if (nm == "sub_exp")
                res = new SubExpression();
            else if (nm == "mult_exp")
                res = new MultExpression();
            else if (nm == "lshift_exp")
                res = new LeftShiftExpression();
            else if (nm == "rshift_exp")
                res = new RightShiftExpression();
            else if (nm == "and_exp")
                res = new AndExpression();
            else if (nm == "or_exp")
                res = new OrExpression();
            else if (nm == "xor_exp")
                res = new XorExpression();
            else if (nm == "div_exp")
                res = new DivExpression();
            else if (nm == "minus_exp")
                res = new MinusExpression();
            else if (nm == "not_exp")
                res = new NotExpression();
            else
                return (PatternExpression*)0;

            res.restoreXml(el, trans);
            return res;
        }
    }
}
