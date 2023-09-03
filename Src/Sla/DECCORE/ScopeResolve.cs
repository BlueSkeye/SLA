using Sla.CORE;
using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class ScopeResolve :
        rangemap<ScopeMapper, Address, ScopeMapper.NullSubsort, Scope>
    {
        internal ScopeResolve()
            : base(ScopeMapper.Instanciator.Instance, LinetypeComparer.Instance)
        {
        }

        private class LinetypeComparer : IComparer<Address>
        {
            internal static LinetypeComparer Instance = new LinetypeComparer();

            private LinetypeComparer()
            {
            }

            public int Compare(Address? x, Address? y)
            {
                if (x == null) throw new ApplicationException();
                return x.CompareTo(y);
            }
        }
    }
}
