using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class SubtableSymbol : TripleSymbol
    {
        private TokenPattern pattern;
        private bool beingbuilt, errors;
        private List<Constructor> construct; // All the Constructors in this table
        private DecisionNode decisiontree;

        public SubtableSymbol()
        {
            pattern = (TokenPattern)null;
            decisiontree = (DecisionNode)null;
        }

        public SubtableSymbol(string nm)
            : base(nm)
        {
            beingbuilt = false;
            pattern = (TokenPattern)null;
            decisiontree = (DecisionNode)null;
            errors = 0;
        }

        ~SubtableSymbol()
        {
            //if (pattern != (TokenPattern)null)
            //    delete pattern;
            //if (decisiontree != (DecisionNode)null)
            //    delete decisiontree;
            IEnumerator<Constructor> iter = construct.begin();
            //while (iter.MoveNext())
            //    delete* iter;
        }

        public bool isBeingBuilt() => beingbuilt;

        public bool isError() => errors;

        public void addConstructor(Constructor ct)
        {
            ct.setId((uint)construct.size());
            construct.Add(ct);
        }

        public void buildDecisionTree(DecisionProperties props)
        {
            // Associate pattern disjoints to constructors
            if (pattern == (TokenPattern)null) return; // Pattern not fully formed
            Pattern pat;
            decisiontree = new DecisionNode((DecisionNode)null);
            for (int i = 0; i < construct.size(); ++i) {
                pat = construct[i].getPattern().getPattern();
                if (pat.numDisjoint() == 0)
                    decisiontree.addConstructorPair((DisjointPattern)pat, construct[i]);
                else
                    for (int j = 0; j < pat.numDisjoint(); ++j)
                        decisiontree.addConstructorPair(pat.getDisjoint(j), construct[i]);
            }
            decisiontree.split(props); // Create the decision strategy
        }

        public TokenPattern buildPattern(TextWriter s)
        {
            if (pattern != (TokenPattern)null) return pattern; // Already built

            errors = false;
            beingbuilt = true;
            pattern = new TokenPattern();
            if (construct.empty()) {
                s.WriteLine($"Error: There are no constructors in table: {getName()}");
                errors = true;
                return pattern;
            }
            try {
                construct.First().buildPattern(s);
            }
            catch (SleighError err) {
                s.Write($"Error: {err.ToString()}: for ");
                construct.First().printInfo(s);
                s.WriteLine();
                errors = true;
            }
            pattern = construct.First().getPattern();
            for (int i = 1; i < construct.size(); ++i) {
                try {
                    construct[i].buildPattern(s);
                }
                catch (SleighError err) {
                    s.Write($"Error: {err.ToString()}: for ");
                    construct[i].printInfo(s);
                    s.WriteLine();
                    errors = true;
                }
                pattern = construct[i].getPattern().commonSubPattern(pattern);
            }
            beingbuilt = false;
            return pattern;
        }

        public TokenPattern getPattern() => pattern;

        public int getNumConstructors() => construct.size();

        public Constructor getConstructor(int id) => construct[id];

        public override Constructor resolve(ParserWalker walker) => decisiontree.resolve(walker);

        public override PatternExpression getPatternExpression()
        {
            throw new SleighError("Cannot use subtable in expression");
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            throw new SleighError("Cannot use subtable in expression");
        }

        public override int getSize() => -1;

        public override void print(TextWriter s, ParserWalker walker)
        {
            throw new SleighError("Cannot use subtable in expression");
        }

        public override void collectLocalValues(List<ulong> results)
        {
            for (int i = 0; i < construct.size(); ++i)
                construct[i].collectLocalExports(results);
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.subtable_symbol;

        public override void saveXml(TextWriter s)
        {
            if (decisiontree == (DecisionNode)null) return; // Not fully formed
            s.Write("<subtable_sym");
            base.saveXmlHeader(s);
            s.WriteLine($" numct=\"{construct.size()}\">");
            for (int i = 0; i < construct.size(); ++i)
                construct[i].saveXml(s);
            decisiontree.saveXml(s);
            s.WriteLine("</subtable_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<subtable_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            int numct = int.Parse(el.getAttributeValue("numct"));
            construct.reserve(numct);
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            while (iter.MoveNext()) {
                if (iter.Current.getName() == "constructor") {
                    Constructor ct = new Constructor();
                    addConstructor(ct);
                    ct.restoreXml(iter.Current, trans);
                }
                else if (iter.Current.getName() == "decision") {
                    decisiontree = new DecisionNode();
                    decisiontree.restoreXml(iter.Current, (DecisionNode)null, this);
                }
            }
            pattern = (TokenPattern)null;
            beingbuilt = false;
            errors = false;
        }
    }
}
