using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal struct TargetFeature
    {
        internal string name;            // Name of the target function
        internal uint featuremask;		// id of this target for ORing into a mask
    }
}
