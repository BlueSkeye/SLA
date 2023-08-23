using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class OperandValue : PatternValue
    {
        // This is the defining field of expression
        private int index;
        // cached pointer to constructor
        private Constructor ct;
        
        public OperandValue()
        {
        }

        public OperandValue(int ind, Constructor c)
        {
            index = ind;
            ct = c;
        }

        public void changeIndex(int newind)
        {
            index = newind;
        }

        public bool isConstructorRelative()
        {
            OperandSymbol sym = ct.getOperand(index);
            return (sym.getOffsetBase() == -1);
        }

        public string getName()
        {
            OperandSymbol sym = ct.getOperand(index);
            return sym.getName();
        }

        public override TokenPattern genPattern(long val)
        {
            // In general an operand cannot be interpreted as any sort
            // of static constraint in an equation, and if it is being
            // defined by the equation, it should be on the left hand side.
            // If the operand has a defining expression already, use
            // of the operand in the equation makes sense, its defining
            // expression would become a subexpression in the full
            // expression. However, since this can be accomplished
            // by explicitly copying the subexpression into the full
            // expression, we don't support operands as placeholders.
            throw new SleighError("Operand used in pattern expression");
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => ops[index];

        public override long getValue(ParserWalker walker)
        {
            // Get the value of an operand when it is used in an expression. 
            OperandSymbol sym = ct.getOperand(index);
            PatternExpression? patexp = sym.getDefiningExpression();
            if (patexp == (PatternExpression)null) {
                TripleSymbol? defsym = sym.getDefiningSymbol();
                if (defsym != (TripleSymbol)null)
                    patexp = defsym.getPatternExpression();
                if (patexp == (PatternExpression)null)
                    return 0;
            }
            ConstructState tempstate = new ConstructState();
            ParserWalker newwalker = new ParserWalker(walker.getParserContext());
            newwalker.setOutOfBandState(ct, index, tempstate, walker);
            long res = patexp.getValue(newwalker);
            return res;
        }

        public override long getSubValue(List<long> replace, int listpos)
        {
            OperandSymbol sym = ct.getOperand(index);
            return sym.getDefiningExpression().getSubValue(replace, listpos);
        }

        public override long minValue()
        {
            throw new SleighError("Operand used in pattern expression");
        }

        public override long maxValue()
        {
            throw new SleighError("Operand used in pattern expression");
        }

        public override void saveXml(TextWriter s)
        {
            s.Write("<operand_exp");
            s.Write($" index=\"{index}\"");
            s.Write(" table=\"0x{ct.getParent().getId():X}\"");
            // Save id of our constructor
            s.WriteLine($" ct=\"0x{ct.getId():X}\"/>");
        }

        public override void restoreXml(Element el, Translate trans)
        {
            uint ctid, tabid;
            index = int.Parse(el.getAttributeValue("index"));
            tabid = uint.Parse(el.getAttributeValue("table"));
            ctid = uint.Parse(el.getAttributeValue("ct"));
            SleighBase sleigh = (SleighBase)trans;
            SubtableSymbol? tab = (sleigh.findSymbol(tabid) as SubtableSymbol) ?? throw new BugException();
            ct = tab.getConstructor((int)ctid);
        }
    }
}
