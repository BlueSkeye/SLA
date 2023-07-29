using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class CodeUnit
    {
        [Flags()]
        public enum Flags
        {
            fallthru = 1,
            jump = 2,
            call = 4,
            notcode = 8,
            hit_by_fallthru = 16,
            hit_by_jump = 32,
            hit_by_call = 64,
            errantstart = 128,
            targethit = 256,
            thunkhit = 512
        };
        
        internal Flags flags;
        internal int4 size;
    }
}
