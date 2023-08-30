using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class EntryMap :
        rangemap<SymbolEntry, System.UInt64, SymbolEntry.EntrySubsort, SymbolEntry.EntryInitData>
    {
        internal EntryMap()
            : base(SymbolEntry.EntrySubsort.Instanciator.Instance)
        {
        }
    }
}
