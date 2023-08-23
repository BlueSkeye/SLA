using Sla.CORE;

namespace Sla.SLEIGH
{
    // A pattern with a context piece and an instruction piece
    internal class CombinePattern : DisjointPattern
    {
        private ContextPattern context;    // Context piece
        private InstructionPattern instr;  // Instruction piece
        
        protected virtual PatternBlock getBlock(bool cont)
            => cont ? context.getBlock() : instr.getBlock();

        public CombinePattern()
        {
            context = (ContextPattern)null;
            instr = (InstructionPattern)null;
        }

        public CombinePattern(ContextPattern con, InstructionPattern @in)
        {
            context = con;
            instr = @in;
        }
    
        ~CombinePattern()
        {
            //if (context != (ContextPattern)null)
            //    delete context;
            //if (instr != (InstructionPattern*)0)
            //    delete instr;
        }

        public override Pattern simplifyClone()
        {
            // We should only have to think at "our" level
            if (context.alwaysTrue())
                return instr.simplifyClone();
            if (instr.alwaysTrue())
                return context.simplifyClone();
            if (context.alwaysFalse() || instr.alwaysFalse())
                return new InstructionPattern(false);
            return new CombinePattern((ContextPattern)context.simplifyClone(),
                (InstructionPattern)instr.simplifyClone());
        }

        public override void shiftInstruction(int sa)
        {
            instr.shiftInstruction(sa);
        }

        public override bool isMatch(ParserWalker walker)
        {
            if (!instr.isMatch(walker)) return false;
            if (!context.isMatch(walker)) return false;
            return true;
        }

        public override bool alwaysTrue() => (context.alwaysTrue() && instr.alwaysTrue());

        public override bool alwaysFalse() => (context.alwaysFalse() || instr.alwaysFalse());

        public override bool alwaysInstructionTrue() => instr.alwaysInstructionTrue();

        public override Pattern doOr(Pattern b, int sa)
        {
            if (b.numDisjoint() != 0)
                return b.doOr(this, -sa);

            DisjointPattern res1 = (DisjointPattern)simplifyClone();
            DisjointPattern res2 = (DisjointPattern)b.simplifyClone();
            if (sa < 0)
                res1.shiftInstruction(-sa);
            else
                res2.shiftInstruction(sa);
            OrPattern tmp = new OrPattern(res1, res2);
            return tmp;
        }

        public override Pattern doAnd(Pattern b, int sa)
        {
            CombinePattern tmp;

            if (b.numDisjoint() != 0)
                return b.doAnd(this, -sa);

            CombinePattern? b2 = b as CombinePattern;
            if (b2 != (CombinePattern)null) {
                ContextPattern c = (ContextPattern)context.doAnd(b2.context, 0);
                InstructionPattern i = (InstructionPattern)instr.doAnd(b2.instr, sa);
                tmp = new CombinePattern(c, i);
            }
            else {
                InstructionPattern b3 = b as InstructionPattern;
                if (b3 != (InstructionPattern)null) {
                    InstructionPattern i = (InstructionPattern)instr.doAnd(b3, sa);
                    tmp = new CombinePattern((ContextPattern)context.simplifyClone(), i);
                }
                else {
                    // Must be a ContextPattern
                    ContextPattern c = (ContextPattern)context.doAnd(b, 0);
                    InstructionPattern newpat = (InstructionPattern)instr.simplifyClone();
                    if (sa < 0)
                        newpat.shiftInstruction(-sa);
                    tmp = new CombinePattern(c, newpat);
                }
            }
            return tmp;
        }

        public override Pattern commonSubPattern(Pattern b, int sa)
        {
            Pattern tmp;

            if (b.numDisjoint() != 0)
                return b.commonSubPattern(this, -sa);

            CombinePattern? b2 = b as CombinePattern;
            if (b2 != (CombinePattern)null) {
                ContextPattern c = (ContextPattern)context.commonSubPattern(b2.context, 0);
                InstructionPattern i = (InstructionPattern)instr.commonSubPattern(b2.instr, sa);
                tmp = new CombinePattern(c, i);
            }
            else {
                InstructionPattern? b3 = b as InstructionPattern;
                if (b3 != (InstructionPattern)null)
                    tmp = instr.commonSubPattern(b3, sa);
                else            // Must be a ContextPattern
                    tmp = context.commonSubPattern(b, 0);
            }
            return tmp;
        }

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<combine_pat>");
            context.saveXml(s);
            instr.saveXml(s);
            s.WriteLine("</combine_pat>");
        }

        public override void restoreXml(Element el)
        {
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            context = new ContextPattern();
            if (!iter.MoveNext()) throw new ApplicationException();
            context.restoreXml(iter.Current);
            if (!iter.MoveNext()) throw new ApplicationException();
            instr = new InstructionPattern();
            instr.restoreXml(iter.Current);
        }
    }
}
