using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief  Attempt to normalize symmetric block structures.
    /// This is used in conjunction with the action ActionBlockStructure
    /// to make the most natural choice, when there is a choice in how code is structured.
    /// This uses the preferComplement() method on structured FlowBlocks to choose between symmetric
    /// structurings, such as an if/else where the \b true and \b false blocks can be swapped.
    internal class ActionPreferComplement : Action
    {
        /// Constructor
        public ActionPreferComplement(string g)
            : base(0,"prefercomplement", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) {
                return null;
            }
            return new ActionPreferComplement(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            BlockGraph graph = new BlockGraph(data.getStructure());

            if (graph.getSize() == 0) {
                return 0;
            }
            List<BlockGraph> vec = new List<BlockGraph>();
            vec.Add(graph);
            int pos = 0;

            while (pos < vec.Count) {
                BlockGraph curbl = vec[pos];
                FlowBlock.block_type bt;
                pos += 1;
                int sz = curbl.getSize();
                for (int i = 0; i < sz; ++i) {
                    FlowBlock childbl = curbl.getBlock(i);
                    bt = childbl.getType();
                    if ((bt == FlowBlock.block_type.t_copy) || (bt == FlowBlock.block_type.t_basic)) {
                        continue;
                    }
                    vec.Add((BlockGraph)childbl);
                }
                if (curbl.preferComplement(data)) {
                    count += 1;
                }
            }
            // Clear any ops deleted during this action
            data.clearDeadOps();
            return 0;
        }
    }
}
