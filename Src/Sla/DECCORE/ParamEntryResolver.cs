using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class ParamEntryResolver :
        rangemap<ParamEntryRange, System.UInt64, ParamEntryRange.SubsortPosition, ParamEntryRange.InitData>
    {
        internal ParamEntryResolver()
            : base(ParamEntryRangeInstanciator.Instance, SubsorttypeInstanciator.Instance,
                  Sla.UInt64LinetypeComparer.Instance, UInt64LinetypeAdder.Instance)
        {
        }

        private class ParamEntryRangeInstanciator :
            IRecordTypeInstanciator<ParamEntryRange, ParamEntryRange.InitData, System.UInt64>
        {
            internal static readonly ParamEntryRangeInstanciator Instance =
                new ParamEntryRangeInstanciator();

            private ParamEntryRangeInstanciator()
            {
            }

            public ParamEntryRange CreateRecord(ParamEntryRange.InitData initdata, ulong a,
                ulong b)
            {
                return new ParamEntryRange(initdata, a, b);
            }
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
