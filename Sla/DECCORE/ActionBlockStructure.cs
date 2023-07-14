using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Structure control-flow using standard high-level code constructs.
    internal class ActionBlockStructure
    {
        /// Constructor
        public ActionBlockStructure(string g)
            : base(0,"blockstructure", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) 
                ? null
                : new ActionBlockStructure(getGroup());
        }

        public override int apply(Funcdata data)
        {
            BlockGraph graph = data.getStructure();

            // Check if already structured
            if (graph.getSize() != 0) {
                return 0;
            }
            data.installSwitchDefaults();
            graph.buildCopy(data.getBasicBlocks());

            CollapseStructure collapse = new CollapseStructure(graph);
            collapse.collapseAll();
            count += collapse.getChangeCount();
            return 0;
        }
    }
}
