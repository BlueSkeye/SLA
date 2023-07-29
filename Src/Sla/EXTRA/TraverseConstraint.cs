using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class TraverseConstraint
    {
        protected int uniqid;
        
        public TraverseConstraint(int i)
        {
            uniqid = i;
        }
        
        ~TraverseConstraint()
        {
        }
  
        //  int getId(void) { return uniqid; }
    }
}
