using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class TraverseGroupState : TraverseConstraint
    {
        private List<TraverseConstraint> traverselist = new List<TraverseConstraint>();
        private int4 currentconstraint;
        private int4 state;
        
        public TraverseGroupState(int4 i)
            : base(i)
        {
        }
        
        public void addTraverse(TraverseConstraint tc)
        {
            traverselist.push_back(tc);
        }

        public TraverseConstraint getSubTraverse(int4 slot) => traverselist[slot];

        public int4 getCurrentIndex() => currentconstraint;

        public void setCurrentIndex(int4 val)
        {
            currentconstraint = val;
        }

        public int4 getState() => state;

        public void setState(int4 val)
        {
            state = val;
        }
    }
}
