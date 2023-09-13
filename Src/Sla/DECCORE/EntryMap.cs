using Sla.EXTRA;
using static Sla.DECCORE.SymbolEntry;

namespace Sla.DECCORE
{
    internal class EntryMap :
        rangemap<SymbolEntry, System.UInt64, EntrySubsort, EntryInitData>
    {
        internal EntryMap()
            : base(SymbolEntryInstanciator.Instance,
                  SymbolEntry.EntrySubsort.Instanciator.Instance,
                  Sla.UInt64LinetypeComparer.Instance, UInt64LinetypeAdder.Instance)
        {
        }

        private class SymbolEntryInstanciator
            : IRecordTypeInstanciator<SymbolEntry, SymbolEntry.EntryInitData, System.UInt64>
        {
            internal static readonly SymbolEntryInstanciator Instance =
                new SymbolEntryInstanciator();

            private SymbolEntryInstanciator()
            {
            }

            public SymbolEntry CreateRecord(EntryInitData initdata, ulong a, ulong b)
            {
                return new SymbolEntry(initdata, a, b);
            }
        }
    }
}
