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
        private PatternBlock? maskvalue;

        protected override PatternBlock? getBlock(bool context)
            => context ? (PatternBlock)null : maskvalue;
    
        public InstructionPattern()
        {
            maskvalue = (PatternBlock)null;
        }

        public InstructionPattern(PatternBlock mv)
        {
            maskvalue = mv;
        }

        public InstructionPattern(bool tf)
        {
            maskvalue = new PatternBlock(tf);
        }

        public PatternBlock? getBlock() => maskvalue;

        ~InstructionPattern()
        {
            // if (maskvalue != (PatternBlock)null) delete maskvalue;
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

            CombinePattern b2 = b as CombinePattern;
            if (b2 != (CombinePattern)null)
                return b.doOr(this, -sa);

            DisjointPattern res1 = (DisjointPattern)simplifyClone();
            DisjointPattern res2 = (DisjointPattern)b.simplifyClone();
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

            CombinePattern? b2 = b as CombinePattern;
            if (b2 != (CombinePattern)null)
                return b.doAnd(this, -sa);

            ContextPattern? b3 = b as ContextPattern;
            if (b3 != (ContextPattern)null) {
                InstructionPattern newpat = (InstructionPattern)simplifyClone();
                if (sa < 0)
                    newpat.shiftInstruction(-sa);
                return new CombinePattern((ContextPattern)b3.simplifyClone(), newpat);
            }
            InstructionPattern b4 = (InstructionPattern)b;

            PatternBlock respattern;
            if (sa < 0) {
                PatternBlock a = maskvalue.clone();
                a.shift(-sa);
                respattern = a.intersect(b4.maskvalue);
                // delete a;
            }
            else {
                PatternBlock c = b4.maskvalue.clone();
                c.shift(sa);
                respattern = maskvalue.intersect(c);
                // delete c;
            }
            return new InstructionPattern(respattern);
        }

        public override Pattern commonSubPattern(Pattern b, int sa)
        {
            if (b.numDisjoint() > 0)
                return b.commonSubPattern(this, -sa);

            CombinePattern? b2 = b as CombinePattern;
            if (b2 != (CombinePattern)null)
                return b.commonSubPattern(this, -sa);

            ContextPattern? b3 = b as ContextPattern;
            if (b3 != (ContextPattern)null) {
                InstructionPattern res = new InstructionPattern(true);
                return res;
            }
            InstructionPattern b4 = (InstructionPattern)b;

            PatternBlock respattern;
            if (sa < 0) {
                PatternBlock a = maskvalue.clone();
                a.shift(-sa);
                respattern = a.commonSubPattern(b4.maskvalue);
                // delete a;
            }
            else {
                PatternBlock c = b4.maskvalue.clone();
                c.shift(sa);
                respattern = maskvalue.commonSubPattern(c);
                // delete c;
            }
            return new InstructionPattern(respattern);
        }

        public override bool isMatch(ParserWalker walker) => maskvalue.isInstructionMatch(walker);

        public override bool alwaysTrue() => maskvalue.alwaysTrue();

        public override bool alwaysFalse() => maskvalue.alwaysFalse();

        public override bool alwaysInstructionTrue() => maskvalue.alwaysTrue();

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<instruct_pat>");
            maskvalue.saveXml(s);
            s.WriteLine("</instruct_pat>");
        }

        public override void restoreXml(Element el)
        {
            maskvalue = new PatternBlock(true);
            maskvalue.restoreXml(el.getChildren().First());
        }
    }
}
