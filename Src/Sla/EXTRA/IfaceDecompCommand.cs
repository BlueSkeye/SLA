using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief Root class for all decompiler specific commands
    ///
    /// Commands share the data object IfaceDecompData and are capable of
    /// iterating over all functions in the program/architecture.
    internal class IfaceDecompCommand : IfaceCommand
    {
        protected IfaceStatus status;          ///< The console owning \b this command
        protected IfaceDecompData dcp;           ///< Data common to decompiler commands

        ///< Iterate recursively over all functions in given scope
        /// Runs over every function in the scope, or any sub-scope , calling
        /// iterationCallback()
        /// \param scope is the given scope
        protected void iterateScopesRecursive(Scope scope)
        {
            if (!scope.isGlobal()) return;
            iterateFunctionsAddrOrder(scope);
            ScopeMap::const_iterator iter, enditer;
            iter = scope.childrenBegin();
            enditer = scope.childrenEnd();
            for (; iter != enditer; ++iter)
            {
                iterateScopesRecursive((*iter).second);
            }
        }

        ///< Iterate over all functions in a given scope
        /// Runs over every function in the scope calling iterationCallback().
        /// \param scope is the given scope
        protected void iterateFunctionsAddrOrder(Scope scope)
        {
            MapIterator miter, menditer;
            miter = scope.begin();
            menditer = scope.end();
            while (miter != menditer)
            {
                Symbol* sym = (*miter).getSymbol();
                FunctionSymbol* fsym = dynamic_cast<FunctionSymbol*>(sym);
                ++miter;
                if (fsym != (FunctionSymbol*)0)
                    iterationCallback(fsym.getFunction());
            }
        }

        public override void setData(IfaceStatus root, IfaceData data)
        {
            status = root;
            dcp = (IfaceDecompData*)data;
        }

        public override string getModule() => "decompile";

        public override IfaceData createData() => new IfaceDecompData();

        /// \brief Perform the per-function aspect of \b this command.
        ///
        /// \param fd is the particular function to operate on
        public override void iterationCallback(Funcdata fd)
        {
        }

        ///< Iterate command over all functions in all scopes
        /// Scopes are traversed depth-first, then within a scope, functions are
        /// traversed in address order.
        public void iterateFunctionsAddrOrder()
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No architecture loaded");
            iterateScopesRecursive(dcp.conf.symboltab.getGlobalScope());
        }

        ///< Iterate command over all functions in a call-graph traversal
        /// Traversal is based on the current CallGraph for the program.
        /// Child functions are traversed before their parents.
        public void iterateFunctionsLeafOrder()
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No architecture loaded");

            if (dcp.cgraph == (CallGraph*)0)
                throw IfaceExecutionError("No callgraph present");

            CallGraphNode* node;
            node = dcp.cgraph.initLeafWalk();
            while (node != (CallGraphNode*)0)
            {
                if (node.getName().size() == 0) continue; // Skip if has no name
                Funcdata* fd = node.getFuncdata();
                if (fd != (Funcdata*)0)
                    iterationCallback(fd);
                node = dcp.cgraph.nextLeaf(node);
            }
        }
    }
}
