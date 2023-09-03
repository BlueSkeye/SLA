using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class ParamEntryResolver :
        rangemap<ParamEntryRange, System.UInt64, ParamEntryRange.SubsortPosition, ParamEntryRange.InitData>
    {
        internal ParamEntryResolver()
            : base(SubsorttypeInstanciator.Instance, Sla.UInt64LinetypeComparer.Instance)
        {
        }

        private class SubsorttypeInstanciator :
            IRangemapSubsortTypeInstantiator<ParamEntryRange.SubsortPosition>
        {
            internal static readonly SubsorttypeInstanciator Instance =
                new SubsorttypeInstanciator(); 

            private SubsorttypeInstanciator()
            {
            }

            public ParamEntryRange.SubsortPosition Create(bool value)
            {
                return new ParamEntryRange.SubsortPosition(value);
            }

            public ParamEntryRange.SubsortPosition Create(
                ParamEntryRange.SubsortPosition cloned)
            {
                throw new NotSupportedException();
            }
        }
    }
}
