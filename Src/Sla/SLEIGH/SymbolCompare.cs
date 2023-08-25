
namespace Sla.SLEIGH
{
    internal class SymbolCompare : IComparer<SleighSymbol>
    {
        internal static readonly SymbolCompare Instance = new SymbolCompare();

        private SymbolCompare() { }

        public int Compare(SleighSymbol? a, SleighSymbol? b)
        {
            if (null == a) throw new ArgumentNullException();
            if (null == b) throw new ArgumentNullException();
            return string.Compare(a.getName(), b.getName());
        }
    }
}
