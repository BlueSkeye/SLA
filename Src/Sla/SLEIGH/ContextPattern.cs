using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ContextPattern : DisjointPattern
    {
        // Matches the context bitstream
        private PatternBlock maskvalue;
        
        protected override PatternBlock getBlock(bool context)
            => context? maskvalue : (PatternBlock*)0;

        public ContextPattern()
        {
            maskvalue = (PatternBlock*)0;
        }

        public ContextPattern(PatternBlock mv)
        {
            maskvalue = mv;
        }

        public PatternBlock getBlock() => maskvalue;

        ~ContextPattern()
        {
            if (maskvalue != (PatternBlock*)0) delete maskvalue;
        }

        public override Pattern simplifyClone() => new ContextPattern(maskvalue->clone());

        public override void shiftInstruction(int4 sa)
        {
        }

        public override Pattern doOr(Pattern b, int4 sa)
        {
            ContextPattern* b2 = dynamic_cast <ContextPattern*> (b);
            if (b2 == (ContextPattern*)0)
                return b->doOr(this, -sa);

            return new OrPattern((DisjointPattern*)simplifyClone(), (DisjointPattern*)b2->simplifyClone());
        }

        public override Pattern doAnd(Pattern b, int4 sa)
        {
            ContextPattern* b2 = dynamic_cast <ContextPattern*> (b);
            if (b2 == (ContextPattern*)0)
                return b->doAnd(this, -sa);

            PatternBlock* resblock = maskvalue->intersect(b2->maskvalue);
            return new ContextPattern(resblock);
        }

        public override Pattern commonSubPattern(Pattern b, int4 sa)
        {
            ContextPattern* b2 = dynamic_cast <ContextPattern*> (b);
            if (b2 == (ContextPattern*)0)
                return b->commonSubPattern(this, -sa);

            PatternBlock* resblock = maskvalue->commonSubPattern(b2->maskvalue);
            return new ContextPattern(resblock);
        }

        public override bool isMatch(ParserWalker walker) => maskvalue->isContextMatch(walker);

        public override bool alwaysTrue() => maskvalue->alwaysTrue();

        public override bool alwaysFalse() => maskvalue->alwaysFalse();

        public override bool alwaysInstructionTrue() => true;

        public override void saveXml(TextWriter s)
        {
            s << "<context_pat>\n";
            maskvalue->saveXml(s);
            s << "</context_pat>\n";
        }

        public override void restoreXml(Element el)
        {
            List list = el->getChildren();
            List::const_iterator iter;
            iter = list.begin();
            maskvalue = new PatternBlock(true);
            maskvalue->restoreXml(*iter);
        }
    }
}
