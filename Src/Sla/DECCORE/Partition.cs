using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A range of nodes (within the weak topological ordering) that are iterated together
    internal class Partition
    {
        // friend class ValueSetSolver;
        /// Starting node of component
        internal ValueSet startNode;
        /// Ending node of component
        internal ValueSet stopNode;
        /// Set to \b true if a node in \b this component has changed this iteration
        internal bool isDirty;

        /// Construct empty partition
        public Partition()
        {
            startNode = (ValueSet)null;
            stopNode = (ValueSet)null;
            isDirty = false;
        }
    }
}
