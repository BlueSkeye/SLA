using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class JumptableNotReachableError : LowlevelError
    {
        internal JumptableNotReachableError(string s)
            : base(s)
        {
        }
    }
}
