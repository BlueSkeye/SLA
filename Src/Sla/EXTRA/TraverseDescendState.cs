using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class TraverseDescendState : TraverseConstraint
    {
        private bool onestep;           // true if first step has occurred
        private List<PcodeOp>::const_iterator iter;    // Different forward branches we could traverse
        private List<PcodeOp>::const_iterator enditer;

        public TraverseDescendState(int4 i)
            : base(i)
        {
        }

        public PcodeOp getCurrentOp() => *iter;

        public void initialize(Varnode vn)
        {
            onestep = false;
            iter = vn->beginDescend();
            enditer = vn->endDescend();
        }

        public bool step()
        {
            if (onestep)
                ++iter;
            else
                onestep = true;
            return (iter != enditer);
        }
    }
}
