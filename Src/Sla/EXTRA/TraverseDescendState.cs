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
        // Different forward branches we could traverse
        private IEnumerator<PcodeOp>? iter = null;
        private bool iterationComplete = false;

        public TraverseDescendState(int i)
            : base(i)
        {
        }

        public PcodeOp getCurrentOp()
        {
            if (null == iter) {
                throw new InvalidOperationException();
            }
            if (iterationComplete) {
                throw new BugException();
            }
            return iter.Current;
        }

        public void initialize(Varnode vn)
        {
            iter = vn.beginDescend();
        }

        public bool step()
        {
            if (null == iter) {
                throw new InvalidOperationException();
            }
            return (iterationComplete = !iter.MoveNext());
        }
    }
}
