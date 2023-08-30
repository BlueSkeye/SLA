using Sla.CORE;
using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class ScopeResolve :
        rangemap<ScopeMapper, Address, ScopeMapper.NullSubsort, Scope>
    {
    }
}
