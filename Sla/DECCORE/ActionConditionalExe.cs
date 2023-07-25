using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Search for and remove various forms of redundant CBRANCH operations
    /// This action wraps the analysis performed by ConditionalExecution to simplify control-flow
    /// that repeatedly branches on the same (or slightly modified) boolean expression.
    internal class ActionConditionalExe : Action
    {
        ///< Constructor
        public ActionConditionalExe(string g)
            : base(0,"conditionalexe", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup()))
                ? null
                : new ActionConditionalExe(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            bool changethisround;
            int numhits = 0;
            int i;

            if (data.hasUnreachableBlocks()) {
                // Conditional execution elimination logic may not work with unreachable blocks
                return 0;
            }
            ConditionalExecution condexe = new ConditionalExecution(data);
            BlockGraph bblocks = data.getBasicBlocks();
            do {
                changethisround = false;
                for (i = 0; i < bblocks.getSize(); ++i) {
                    BlockBasic bb = (BlockBasic)bblocks.getBlock(i);
                    if (condexe.trial(bb)) {
                        // Adjust dataflow
                        condexe.execute();
                        numhits += 1;
                        changethisround = true;
                    }
                }
            } while (changethisround);
            // Number of changes
            count += numhits;
            return 0;
        }
    }
}
