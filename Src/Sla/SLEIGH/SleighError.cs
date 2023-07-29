using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class SleighError : LowlevelError
    {
        internal SleighError(string s)
            : base(s)
        {
        }
    }
}
