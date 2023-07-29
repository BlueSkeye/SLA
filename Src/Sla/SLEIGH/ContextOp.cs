using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ContextOp : ContextChange
    {
        private PatternExpression patexp;  // Expression determining value
        private int num;           // index of word containing context variable to set
        private uint mask;         // Mask off size of variable
        private int shift;         // Number of bits to shift value into place

        public ContextOp(int startbit, int endbit, PatternExpression pe)
        {
            calc_maskword(startbit, endbit, out num, out shift, out mask);
            patexp = pe;
            patexp.layClaim();
        }

        public ContextOp()
        {
        }

        ~ContextOp()
        {
            PatternExpression::release(patexp);
        }

        public override void validate()
        { // Throw an exception if the PatternExpression is not valid
            List<PatternValue*> values;

            patexp.listValues(values); // Get all the expression tokens
            for (int i = 0; i < values.size(); ++i)
            {
                OperandValue* val = dynamic_cast<OperandValue*>(values[i]);
                if (val == (OperandValue*)0) continue;
                // Certain operands cannot be used in context expressions
                // because these are evaluated BEFORE the operand offset
                // has been recovered. If the offset is not relative to
                // the base constructor, then we throw an error
                if (!val.isConstructorRelative())
                    throw new SleighError(val.getName() + ": cannot be used in context expression");
            }
        }

        public override void saveXml(TextWriter s)
        {
            s << "<context_op";
            s << " i=\"" << dec << num << "\"";
            s << " shift=\"" << shift << "\"";
            s << " mask=\"0x" << hex << mask << "\" >\n";
            patexp.saveXml(s);
            s << "</context_op>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            {
                istringstream s = new istringstream(el.getAttributeValue("i"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> num;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("shift"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> shift;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("mask"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> mask;
            }
            const List &list(el.getChildren());
            List::const_iterator iter;
            iter = list.begin();
            patexp = (PatternValue*)PatternExpression::restoreExpression(*iter, trans);
            patexp.layClaim();
        }

        public override void apply(ParserWalkerChange walker)
        {
            uint val = patexp.getValue(walker); // Get our value based on context
            val <<= shift;
            walker.getParserContext().setContextWord(num, val, mask);
        }

        public override ContextChange clone()
        {
            ContextOp* res = new ContextOp();
            (res.patexp = patexp).layClaim();
            res.mask = mask;
            res.num = num;
            res.shift = shift;
            return res;
        }
    }
}
