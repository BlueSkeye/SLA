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
        private int currentconstraint;
        private int state;
        
        public TraverseGroupState(int i)
            : base(i)
        {
        }
        
        public void addTraverse(TraverseConstraint tc)
        {
            traverselist.Add(tc);
        }

        public TraverseConstraint getSubTraverse(int slot) => traverselist[slot];

        public int getCurrentIndex() => currentconstraint;

        public void setCurrentIndex(int val)
        {
            currentconstraint = val;
        }

        public int getState() => state;

        public void setState(int val)
        {
            state = val;
        }
    }
}
