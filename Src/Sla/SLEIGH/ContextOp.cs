using Sla.CORE;
using Sla.DECCORE;

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
            PatternExpression.release(patexp);
        }

        public override void validate()
        {
            // Throw an exception if the PatternExpression is not valid
            List<PatternValue> values = new List<PatternValue>();

            patexp.listValues(values); // Get all the expression tokens
            for (int i = 0; i < values.size(); ++i) {
                OperandValue? val = values[i] as OperandValue;
                if (val == (OperandValue)null) continue;
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
            s.WriteLine($"<context_op i=\"{num}\" shift=\"{shift}\" mask=\"0x{mask:X}\" >");
            patexp.saveXml(s);
            s.WriteLine("</context_op>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            num = int.Parse(el.getAttributeValue("i"));
            shift = int.Parse(el.getAttributeValue("shift"));
            mask = uint.Parse(el.getAttributeValue("mask"));
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            if (!iter.MoveNext()) throw new BugException();
            patexp = (PatternValue)PatternExpression.restoreExpression(iter.Current, trans);
            patexp.layClaim();
        }

        public override void apply(ParserWalkerChange walker)
        {
            uint val = (uint)patexp.getValue(walker); // Get our value based on context
            val <<= shift;
            walker.getParserContext().setContextWord(num, val, mask);
        }

        public override ContextChange clone()
        {
            ContextOp res = new ContextOp();
            (res.patexp = patexp).layClaim();
            res.mask = mask;
            res.num = num;
            res.shift = shift;
            return res;
        }
    }
}
