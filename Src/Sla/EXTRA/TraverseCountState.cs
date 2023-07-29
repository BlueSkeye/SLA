using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Sla.EXTRA
{
    internal class TraverseCountState : TraverseConstraint
    {
        private int state;
        private int endstate;
        
        public TraverseCountState(int i)
            : base(i)
        {
        }
        
        public int getState() => state;

        public void initialize(int end)
        {
            state = -1;
            endstate = end;
        }

        public bool step()
        {
            ++state;
            return (state != endstate);
        }
    }
}
