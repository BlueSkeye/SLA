
using SymbolTree = System.Collections.Generic.HashSet<Sla.SLEIGH.SleighSymbol>; // SymbolCompare

namespace Sla.SLEIGH
{
    internal class SymbolScope
    {
        // friend class SymbolTable;
        internal SymbolScope? parent;
        internal SymbolTree tree = new SymbolTree();
        internal uint id;
        
        public SymbolScope(SymbolScope p, uint i)
        {
            parent = p;
            id = i;
        }

        public SymbolScope getParent() => parent;

        public SleighSymbol addSymbol(SleighSymbol a)
        {
            SleighSymbol result;

            if (tree.TryGetValue(a, out result)) {
                return result;
            }
            tree.Add(a);
            return a;
        }

        public SleighSymbol? findSymbol(string nm)
        {
            SleighSymbol dummy = new SleighSymbol(nm);
            SleighSymbol? result;

            return tree.TryGetValue(dummy, out result) ? result : (SleighSymbol)null;
        }

        public IEnumerator<SleighSymbol> begin() => tree.GetEnumerator();

        // public IEnumerator<SleighSymbol> end() => tree.end();

        public uint getId() => id;

        public void removeSymbol(SleighSymbol a)
        {
            tree.Remove(a);
        }
    }
}
