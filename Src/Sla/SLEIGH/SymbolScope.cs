using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class SymbolScope
    {
        // friend class SymbolTable;
        private SymbolScope parent;
        private SymbolTree tree;
        private uintm id;
        
        public SymbolScope(SymbolScope p, uintm i)
        {
            parent = p;
            id = i;
        }

        public SymbolScope getParent() => parent;

        public SleighSymbol addSymbol(SleighSymbol a)
        {
            pair<SymbolTree::iterator, bool> res;

            res = tree.insert(a);
            if (!res.second)
                return *res.first;      // Symbol already exists in this table
            return a;
        }

        public SleighSymbol findSymbol(string nm)
        {
            SleighSymbol dummy(nm);
            SymbolTree::const_iterator iter;

            iter = tree.find(&dummy);
            if (iter != tree.end())
                return *iter;
            return (SleighSymbol*)0;
        }

        public SymbolTree::const_iterator begin() => tree.begin();

        public SymbolTree::const_iterator end() => tree.end();

        public uintm getId() => id;

        public void removeSymbol(SleighSymbol a)
        {
            tree.erase(a);
        }
    }
}
