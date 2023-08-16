using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class OperandValue : PatternValue
    {
        private int index;         // This is the defining field of expression
        private Constructor ct;        // cached pointer to constructor
        
        public OperandValue()
        {
        } // For use with restoreXml

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
            OperandSymbol* sym = ct.getOperand(index);
            return (sym.getOffsetBase() == -1);
        }

        public string getName()
        {
            OperandSymbol* sym = ct.getOperand(index);
            return sym.getName();
        }

        public override TokenPattern genPattern(long val);

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
            s.Write("<operand_exp";
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
            SubtableSymbol? tab = sleigh.findSymbol(tabid) as SubtableSymbol;
            ct = tab.getConstructor(ctid);
        }
    }
}
