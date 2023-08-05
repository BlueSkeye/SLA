using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class OperandSymbol : SpecificSymbol
    {
        //friend class Constructor;
        //friend class OperandEquation;
        [Flags()]
        public enum Flags
        {
            code_address = 1,
            offset_irrel = 2,
            variable_len = 4,
            marked = 8
        }
        
        private uint reloffset;      // Relative offset
        private int offsetbase;        // Base operand to which offset is relative (-1=constructor start)
        internal int minimumlength;     // Minimum size of operand (within instruction tokens)
        private int hand;          // Handle index
        private OperandValue localexp;
        private TripleSymbol? triple;       // Defining symbol
        private PatternExpression? defexp;  // OR defining expression
        private Flags flags;

        internal void setVariableLength()
        {
            flags |= Flags.variable_len;
        }

        private bool isVariableLength() => ((flags& Flags.variable_len)!=0);

        public OperandSymbol()
        {
        }

        public OperandSymbol(string nm, int index, Constructor ct)
            : base(nm)
        {
            flags = 0;
            hand = index;
            localexp = new OperandValue(index, ct);
            localexp.layClaim();
            defexp = (PatternExpression)null;
            triple = (TripleSymbol)null;
        }

        public uint getRelativeOffset() => reloffset;

        public int getOffsetBase() => offsetbase;

        public int getMinimumLength() => minimumlength;

        public PatternExpression getDefiningExpression() => defexp;

        public TripleSymbol getDefiningSymbol() => triple;

        public int getIndex() => hand;

        public void defineOperand(PatternExpression pe)
        {
            if ((defexp != (PatternExpression)null) || (triple != (TripleSymbol)null))
                throw new SleighError("Redefining operand");
            defexp = pe;
            defexp.layClaim();
        }

        public void defineOperand(TripleSymbol tri)
        {
            if ((defexp != (PatternExpression)null) || (triple != (TripleSymbol)null))
                throw new SleighError("Redefining operand");
            triple = tri;
        }

        public void setCodeAddress()
        {
            flags |= Flags.code_address;
        }

        public bool isCodeAddress() => ((flags& Flags.code_address)!= 0);

        public void setOffsetIrrelevant()
        {
            flags |= Flags.offset_irrel;
        }

        public bool isOffsetIrrelevant() => ((flags& Flags.offset_irrel)!= 0);

        public void setMark()
        {
            flags |= Flags.marked;
        }

        public void clearMark()
        {
            flags &= ~Flags.marked;
        }

        public bool isMarked() => ((flags& Flags.marked)!= 0);

        ~OperandSymbol()
        {
            PatternExpression.release(localexp);
            if (defexp != (PatternExpression)null)
                PatternExpression.release(defexp);
        }

        public override VarnodeTpl getVarnode()
        {
            VarnodeTpl res;
            if (defexp != (PatternExpression)null)
                res = new VarnodeTpl(hand, true); // Definite constant handle
            else {
                SpecificSymbol? specsym = triple as SpecificSymbol;
                if (specsym != (SpecificSymbol)null)
                    res = specsym.getVarnode();
                else if ((triple != (TripleSymbol)null) &&
                     ((triple.getType() == SleighSymbol.symbol_type.valuemap_symbol) || (triple.getType() == SleighSymbol.symbol_type.name_symbol)))
                    res = new VarnodeTpl(hand, true); // Zero-size symbols
                else
                    res = new VarnodeTpl(hand, false); // Possible dynamic handle
            }
            return res;
        }

        public override PatternExpression getPatternExpression() => localexp;

        public override void getFixedHandle(out FixedHandle hnd, ParserWalker walker)
        {
            hnd = walker.getFixedHandle(hand);
        }

        public override int getSize() => (triple == (TripleSymbol)null) ? 0 : triple.getSize();

        public override void print(TextWriter s, ParserWalker walker)
        {
            walker.pushOperand(getIndex());
            if (triple != (TripleSymbol)null) {
                if (triple.getType() ==  SleighSymbol.symbol_type.subtable_symbol)
                    walker.getConstructor().print(s, walker);
                else
                    triple.print(s, walker);
            }
            else {
                long val = defexp.getValue(walker);
                if (val >= 0)
                    s.Write($"0x{val:X}");
                else
                    s.Write($"-0x{val:X}");
            }
            walker.popOperand();
        }

        public override void collectLocalValues(List<ulong> results)
        {
            if (triple != (TripleSymbol)null)
                triple.collectLocalValues(results);
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.operand_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<operand_sym");
            base.saveXmlHeader(s);
            if (triple != (TripleSymbol)null)
                s.Write($" subsym=\"0x{triple.getId():X}\"");
            s.Write($" off=\"{reloffset}\" base=\"{offsetbase}\" minlen=\"{minimumlength}\"");
            if (isCodeAddress())
                s.Write(" code=\"true\"");
            s.WriteLine(" index=\"{hand}\">";
            localexp.saveXml(s);
            if (defexp != (PatternExpression)null)
                defexp.saveXml(s);
            s.WriteLine("</operand_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<operand_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            defexp = (PatternExpression)null;
            triple = (TripleSymbol)null;
            flags = 0;
            hand = int.Parse(el.getAttributeValue("index"));
            reloffset = uint.Parse(el.getAttributeValue("off"));
            offsetbase = int.Parse(el.getAttributeValue("base"));
            minimumlength = int.Parse(el.getAttributeValue("minlen"));
            for (int i = 0; i < el.getNumAttributes(); ++i) {
                if (el.getAttributeName(i) == "subsym") {
                    uint id = uint.Parse(el.getAttributeValue(i));
                    triple = (TripleSymbol)trans.findSymbol(id);
                }
                else if (el.getAttributeName(i) == "code") {
                    if (Xml.xml_readbool(el.getAttributeValue(i)))
                        flags |= Flags.code_address;
                }
            }
            List<Element> list = el.getChildren();
            localexp = (OperandValue)PatternExpression.restoreExpression(list[0], trans);
            localexp.layClaim();
            if (1 < list.Count) {
                defexp = PatternExpression.restoreExpression(list[1], trans);
                defexp.layClaim();
            }
        }
    }
}
