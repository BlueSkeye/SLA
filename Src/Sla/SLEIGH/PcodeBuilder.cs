using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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
                throw UnimplError("", 0);   // Pcode is not implemented for this constructor

            uint oldbase = labelbase;  // Recursively store old labelbase
            labelbase = labelcount; // Set the newbase
            labelcount += construct.numLabels();   // Add labels from this template

            List<OpTpl*>::const_iterator iter;
            OpTpl* op;
            List<OpTpl> ops = construct.getOpvec();

            for (iter = ops.begin(); iter != ops.end(); ++iter)
            {
                op = *iter;
                switch (op.getOpcode())
                {
                    case BUILD:
                        appendBuild(op, secnum);
                        break;
                    case DELAY_SLOT:
                        delaySlot(op);
                        break;
                    case LABELBUILD:
                        setLabel(op);
                        break;
                    case CROSSBUILD:
                        appendCrossBuild(op, secnum);
                        break;
                    default:
                        dump(op);
                        break;
                }
            }
            labelbase = oldbase;        // Restore old labelbase
        }

        public abstract void appendBuild(OpTpl bld, int secnum);

        public abstract void delaySlot(OpTpl op);

        public abstract void setLabel(OpTpl op);

        public abstract void appendCrossBuild(OpTpl bld, int secnum);
    }
}
