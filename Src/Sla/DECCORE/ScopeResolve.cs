using Sla.CORE;
using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class ScopeResolve :
        rangemap<ScopeMapper, Address, ScopeMapper.NullSubsort, Scope>
    {
        internal ScopeResolve()
            : base(ScopeMapperInstanciator.Instance,
                  ScopeMapper.SubsorttypeInstanciator.Instance, LinetypeComparer.Instance,
                  AddressLinetypeAdder.Instance)
        {
        }

        private class ScopeMapperInstanciator :
            IRecordTypeInstanciator<ScopeMapper, Scope, Address>
        {
            internal static readonly ScopeMapperInstanciator Instance =
                new ScopeMapperInstanciator();

            private ScopeMapperInstanciator()
            {
            }

            public ScopeMapper CreateRecord(Scope initdata, Address a, Address b)
            {
                return new ScopeMapper(initdata, a, b);
            }
        }

        private class LinetypeComparer : IComparer<Address>
        {
            internal static LinetypeComparer Instance = new LinetypeComparer();

            private LinetypeComparer()
            {
            }

            public int Compare(Address? x, Address? y)
            {
                if (null == x) throw new ApplicationException();
                return x.CompareTo(y);
            }
        }
    }
}
