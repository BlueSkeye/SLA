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
        private uint4 labelbase;
        private uint4 labelcount;
        
        protected ParserWalker walker;
        
        protected abstract void dump(OpTpl op);

        public PcodeBuilder(uint4 lbcnt)
        {
            labelbase = labelcount = lbcnt;
        }
        
        ~PcodeBuilder()
        {
        }

        public uint4 getLabelBase() => labelbase;

        public ParserWalker getCurrentWalker() => walker;

        public void build(ConstructTpl construct, int4 secnum)
        {
            if (construct == (ConstructTpl*)0)
                throw UnimplError("", 0);   // Pcode is not implemented for this constructor

            uint4 oldbase = labelbase;  // Recursively store old labelbase
            labelbase = labelcount; // Set the newbase
            labelcount += construct->numLabels();   // Add labels from this template

            vector<OpTpl*>::const_iterator iter;
            OpTpl* op;
            List<OpTpl> ops = construct->getOpvec();

            for (iter = ops.begin(); iter != ops.end(); ++iter)
            {
                op = *iter;
                switch (op->getOpcode())
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

        public abstract void appendBuild(OpTpl bld, int4 secnum);

        public abstract void delaySlot(OpTpl op);

        public abstract void setLabel(OpTpl op);

        public abstract void appendCrossBuild(OpTpl bld, int4 secnum);
    }
}
