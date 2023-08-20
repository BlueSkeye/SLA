using Sla.CORE;

namespace Sla.SLEIGH
{
    internal abstract class PcodeBuilder
    {
        // SLEIGH specific pcode generator
        private uint labelbase;
        private uint labelcount;
        
        protected ParserWalker walker;
        
        protected abstract void dump(OpTpl op);

        public PcodeBuilder(uint lbcnt)
        {
            labelbase = labelcount = lbcnt;
        }
        
        ~PcodeBuilder()
        {
        }

        public uint getLabelBase() => labelbase;

        public ParserWalker getCurrentWalker() => walker;

        public void build(ConstructTpl construct, int secnum)
        {
            if (construct == (ConstructTpl)null)
                throw new UnimplError("", 0);   // Pcode is not implemented for this constructor

            uint oldbase = labelbase;  // Recursively store old labelbase
            labelbase = labelcount; // Set the newbase
            labelcount += construct.numLabels();   // Add labels from this template

            OpTpl op;
            List<OpTpl> ops = construct.getOpvec();
            IEnumerator<OpTpl> iter = ops.GetEnumerator();

            while (iter.MoveNext()) {
                op = iter.Current;
                switch (op.getOpcode()) {
                    case OpCode.BUILD:
                        appendBuild(op, secnum);
                        break;
                    case OpCode.DELAY_SLOT:
                        delaySlot(op);
                        break;
                    case OpCode.LABELBUILD:
                        setLabel(op);
                        break;
                    case OpCode.CROSSBUILD:
                        appendCrossBuild(op, secnum);
                        break;
                    default:
                        dump(op);
                        break;
                }
            }
            // Restore old labelbase
            labelbase = oldbase;
        }

        public abstract void appendBuild(OpTpl bld, int secnum);

        public abstract void delaySlot(OpTpl op);

        public abstract void setLabel(OpTpl op);

        public abstract void appendCrossBuild(OpTpl bld, int secnum);
    }
}
