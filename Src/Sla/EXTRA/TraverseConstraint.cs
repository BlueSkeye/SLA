using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class TraverseConstraint
    {
        protected int4 uniqid;
        
        public TraverseConstraint(int4 i)
        {
            uniqid = i;
        }
        
        ~TraverseConstraint()
        {
        }
  
        //  int4 getId(void) { return uniqid; }
    }
}
