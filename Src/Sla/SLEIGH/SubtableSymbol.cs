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
            pattern = (TokenPattern*)0;
            decisiontree = (DecisionNode*)0;
        }

        public SubtableSymbol(string nm)
            : base(nm)
        {
            beingbuilt = false;
            pattern = (TokenPattern*)0;
            decisiontree = (DecisionNode*)0;
            errors = 0;
        }

        ~SubtableSymbol()
        {
            if (pattern != (TokenPattern*)0)
                delete pattern;
            if (decisiontree != (DecisionNode*)0)
                delete decisiontree;
            vector<Constructor*>::iterator iter;
            for (iter = construct.begin(); iter != construct.end(); ++iter)
                delete* iter;
        }

        public bool isBeingBuilt() => beingbuilt;

        public bool isError() => errors;

        public void addConstructor(Constructor ct)
        {
            ct->setId(construct.size());
            construct.push_back(ct);
        }

        public void buildDecisionTree(DecisionProperties props)
        {               // Associate pattern disjoints to constructors
            if (pattern == (TokenPattern*)0) return; // Pattern not fully formed
            Pattern* pat;
            decisiontree = new DecisionNode((DecisionNode*)0);
            for (int4 i = 0; i < construct.size(); ++i)
            {
                pat = construct[i]->getPattern()->getPattern();
                if (pat->numDisjoint() == 0)
                    decisiontree->addConstructorPair((DisjointPattern*)pat, construct[i]);
                else
                    for (int4 j = 0; j < pat->numDisjoint(); ++j)
                        decisiontree->addConstructorPair(pat->getDisjoint(j), construct[i]);
            }
            decisiontree->split(props); // Create the decision strategy
        }

        public TokenPattern buildPattern(TextWriter s)
        {
            if (pattern != (TokenPattern*)0) return pattern; // Already built

            errors = false;
            beingbuilt = true;
            pattern = new TokenPattern();
            if (construct.empty())
            {
                s << "Error: There are no constructors in table: " + getName() << endl;
                errors = true;
                return pattern;
            }
            try
            {
                construct.front()->buildPattern(s);
            }
            catch (SleighError err) {
                s << "Error: " << err.ToString() << ": for ";
                construct.front()->printInfo(s);
                s << endl;
                errors = true;
            }
            *pattern = *construct.front()->getPattern();
            for (int4 i = 1; i < construct.size(); ++i)
            {
                try
                {
                    construct[i]->buildPattern(s);
                }
                catch (SleighError err) {
                    s << "Error: " << err.ToString() << ": for ";
                    construct[i]->printInfo(s);
                    s << endl;
                    errors = true;
                }
                *pattern = construct[i]->getPattern()->commonSubPattern(*pattern);
            }
            beingbuilt = false;
            return pattern;
        }

        public TokenPattern getPattern() => pattern;

        public int4 getNumConstructors() => construct.size();

        public Constructor getConstructor(uintm id) => construct[id];

        public override Constructor resolve(ParserWalker walker) => decisiontree->resolve(walker);

        public override PatternExpression getPatternExpression()
        {
            throw SleighError("Cannot use subtable in expression");
        }

        public override void getFixedHandle(ref FixedHandle hand, ParserWalker walker)
        {
            throw SleighError("Cannot use subtable in expression");
        }

        public override int4 getSize() => -1;

        public override void print(TextWriter s, ParserWalker walker)
        {
            throw SleighError("Cannot use subtable in expression");
        }

        public override void collectLocalValues(List<uintb> results)
        {
            for (int4 i = 0; i < construct.size(); ++i)
                construct[i]->collectLocalExports(results);
        }

        public override symbol_type getType() => subtable_symbol;

        public override void saveXml(TextWriter s)
        {
            if (decisiontree == (DecisionNode*)0) return; // Not fully formed
            s << "<subtable_sym";
            SleighSymbol::saveXmlHeader(s);
            s << " numct=\"" << dec << construct.size() << "\">\n";
            for (int4 i = 0; i < construct.size(); ++i)
                construct[i]->saveXml(s);
            decisiontree->saveXml(s);
            s << "</subtable_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<subtable_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            {
                int4 numct;
                istringstream s(el->getAttributeValue("numct"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> numct;
                construct.reserve(numct);
            }
            List list = el->getChildren();
            List::const_iterator iter;
            iter = list.begin();
            while (iter != list.end())
            {
                if ((*iter)->getName() == "constructor")
                {
                    Constructor* ct = new Constructor();
                    addConstructor(ct);
                    ct->restoreXml(*iter, trans);
                }
                else if ((*iter)->getName() == "decision")
                {
                    decisiontree = new DecisionNode();
                    decisiontree->restoreXml(*iter, (DecisionNode*)0, this);
                }
                ++iter;
            }
            pattern = (TokenPattern*)0;
            beingbuilt = false;
            errors = 0;
        }
    }
}
