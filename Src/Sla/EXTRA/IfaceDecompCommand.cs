using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    /// \brief Root class for all decompiler specific commands
    /// Commands share the data object IfaceDecompData and are capable of
    /// iterating over all functions in the program/architecture.
    internal abstract class IfaceDecompCommand : IfaceCommand
    {
        // The console owning \b this command
        protected IfaceStatus status;
        // Data common to decompiler commands
        protected IfaceDecompData dcp;

        // Iterate recursively over all functions in given scope
        // Runs over every function in the scope, or any sub-scope , calling iterationCallback()
        // \param scope is the given scope
        protected void iterateScopesRecursive(Scope scope)
        {
            if (!scope.isGlobal()) return;
            iterateFunctionsAddrOrder(scope);
            ScopeMap.Enumerator iter = scope.childrenBegin();
            while(iter.MoveNext()) { 
                iterateScopesRecursive(iter.Current.Value);
            }
        }

        // Iterate over all functions in a given scope
        // Runs over every function in the scope calling iterationCallback().
        // \param scope is the given scope
        protected void iterateFunctionsAddrOrder(Scope scope)
        {
            IEnumerator<SymbolEntry> miter = scope.begin();
            while (miter.MoveNext()) {
                Symbol sym = miter.Current.getSymbol();
                FunctionSymbol? fsym = sym as FunctionSymbol;
                if (fsym != (FunctionSymbol)null)
                    iterationCallback(fsym.getFunction());
            }
        }

        public override void setData(IfaceStatus root, IfaceData data)
        {
            status = root;
            dcp = (IfaceDecompData)data;
        }

        public override string getModule() => "decompile";

        public override IfaceData createData() => new IfaceDecompData();

        /// \brief Perform the per-function aspect of \b this command.
        ///
        /// \param fd is the particular function to operate on
        public virtual void iterationCallback(Funcdata fd)
        {
        }

        ///< Iterate command over all functions in all scopes
        /// Scopes are traversed depth-first, then within a scope, functions are
        /// traversed in address order.
        public void iterateFunctionsAddrOrder()
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No architecture loaded");
            iterateScopesRecursive(dcp.conf.symboltab.getGlobalScope());
        }

        ///< Iterate command over all functions in a call-graph traversal
        /// Traversal is based on the current CallGraph for the program.
        /// Child functions are traversed before their parents.
        public void iterateFunctionsLeafOrder()
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No architecture loaded");

            if (dcp.cgraph == (CallGraph)null)
                throw new IfaceExecutionError("No callgraph present");

            CallGraphNode? node = dcp.cgraph.initLeafWalk();
            while (node != (CallGraphNode)null) {
                if (node.getName().Length == 0)
                    // Skip if has no name
                    continue;
                Funcdata? fd = node.getFuncdata();
                if (fd != (Funcdata)null)
                    iterationCallback(fd);
                node = dcp.cgraph.nextLeaf(node);
            }
        }
    }
}
