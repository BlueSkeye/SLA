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
        private int4 state;
        private int4 endstate;
        
        public TraverseCountState(int4 i)
            : base(i)
        {
        }
        
        public int4 getState() => state;

        public void initialize(int4 end)
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
