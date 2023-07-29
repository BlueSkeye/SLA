using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Perform final organization of the control-flow structure
    /// Label unstructured edges, order switch cases, and order disjoint components of the control-flow
    internal class ActionFinalStructure : Action
    {
        /// Constructor
        public ActionFinalStructure(string g)
            : base(0,"finalstructure", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) 
                ? null 
                : new ActionFinalStructure(getGroup());
        }

        public override int apply(Funcdata data)
        {
            BlockGraph graph = data.getStructure();

            graph.orderBlocks();
            graph.finalizePrinting(data);
            graph.scopeBreak(-1, -1);   // Put in \e break statements
            graph.markUnstructured();   // Put in \e gotos
            graph.markLabelBumpUp(false); // Fix up labeling
            return 0;
        }
    }
}
