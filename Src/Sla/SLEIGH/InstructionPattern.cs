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
    internal class InstructionPattern : DisjointPattern
    {
        // Matches the instruction bitstream
        private PatternBlock maskvalue;

        protected virtual PatternBlock getBlock(bool context)
            => context? (PatternBlock*)0 : maskvalue;
    
        public InstructionPattern()
        {
            maskvalue = (PatternBlock*)0;
        }

        public InstructionPattern(PatternBlock mv)
        {
            maskvalue = mv;
        }

        public InstructionPattern(bool tf)
        {
            maskvalue = new PatternBlock(tf);
        }

        public PatternBlock getBlock() => maskvalue;

        ~InstructionPattern()
        {
            if (maskvalue != (PatternBlock*)0) delete maskvalue;
        }

        public override Pattern simplifyClone() => new InstructionPattern(maskvalue.clone());

        public override void shiftInstruction(int sa)
        {
            maskvalue.shift(sa);
        }

        public override Pattern doOr(Pattern b, int sa)
        {
            if (b.numDisjoint() > 0)
                return b.doOr(this, -sa);

            CombinePattern* b2 = dynamic_cast < CombinePattern*> (b);
            if (b2 != (CombinePattern*)0)
                return b.doOr(this, -sa);

            DisjointPattern* res1,*res2;
            res1 = (DisjointPattern*)simplifyClone();
            res2 = (DisjointPattern*)b.simplifyClone();
            if (sa < 0)
                res1.shiftInstruction(-sa);
            else
                res2.shiftInstruction(sa);
            return new OrPattern(res1, res2);
        }

        public override Pattern doAnd(Pattern b, int sa)
        {
            if (b.numDisjoint() > 0)
                return b.doAnd(this, -sa);

            CombinePattern* b2 = dynamic_cast <CombinePattern*> (b);
            if (b2 != (CombinePattern*)0)
                return b.doAnd(this, -sa);

            ContextPattern* b3 = dynamic_cast <ContextPattern*> (b);
            if (b3 != (ContextPattern*)0) {
                InstructionPattern* newpat = (InstructionPattern*)simplifyClone();
                if (sa < 0)
                    newpat.shiftInstruction(-sa);
                return new CombinePattern((ContextPattern*)b3.simplifyClone(), newpat);
            }
            InstructionPattern* b4 = (InstructionPattern*)b;

            PatternBlock* respattern;
            if (sa < 0)
            {
                PatternBlock* a = maskvalue.clone();
                a.shift(-sa);
                respattern = a.intersect(b4.maskvalue);
                delete a;
            }
            else
            {
                PatternBlock* c = b4.maskvalue.clone();
                c.shift(sa);
                respattern = maskvalue.intersect(c);
                delete c;
            }
            return new InstructionPattern(respattern);
        }

        public override Pattern commonSubPattern(Pattern b, int sa)
        {
            if (b.numDisjoint() > 0)
                return b.commonSubPattern(this, -sa);

            CombinePattern* b2 = dynamic_cast <CombinePattern*> (b);
            if (b2 != (CombinePattern*)0)
                return b.commonSubPattern(this, -sa);

            ContextPattern* b3 = dynamic_cast <ContextPattern*> (b);
            if (b3 != (ContextPattern*)0) {
                InstructionPattern* res = new InstructionPattern(true);
                return res;
            }
            InstructionPattern* b4 = (InstructionPattern*)b;

            PatternBlock* respattern;
            if (sa < 0)
            {
                PatternBlock* a = maskvalue.clone();
                a.shift(-sa);
                respattern = a.commonSubPattern(b4.maskvalue);
                delete a;
            }
            else
            {
                PatternBlock* c = b4.maskvalue.clone();
                c.shift(sa);
                respattern = maskvalue.commonSubPattern(c);
                delete c;
            }
            return new InstructionPattern(respattern);
        }

        public override bool isMatch(ParserWalker walker) => maskvalue.isInstructionMatch(walker);

        public override bool alwaysTrue() => maskvalue.alwaysTrue();

        public override bool alwaysFalse() => maskvalue.alwaysFalse();

        public override bool alwaysInstructionTrue) => maskvalue.alwaysTrue();

        public override void saveXml(TextWriter s)
        {
            s << "<instruct_pat>\n";
            maskvalue.saveXml(s);
            s << "</instruct_pat>\n";
        }

        public override void restoreXml(Element el)
        {
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            maskvalue = new PatternBlock(true);
            maskvalue.restoreXml(*iter);
        }
    }
}
