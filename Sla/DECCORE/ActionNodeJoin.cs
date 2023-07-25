using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief  Look for conditional branch expressions that have been split and rejoin them
    internal class ActionNodeJoin : Action
    {
        /// Constructor
        public ActionNodeJoin(string g)
            : base(0,"nodejoin", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) 
                ? null
                : new ActionNodeJoin(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            BlockGraph graph = data.getBasicBlocks();
            if (graph.getSize() == 0) {
                return 0;
            }

            ConditionalJoin condjoin = new ConditionalJoin(data);
            for (int i = 0; i < graph.getSize(); ++i) {
                BlockBasic bb = (BlockBasic)graph.getBlock(i);
                if (bb.sizeOut() != 2) {
                    continue;
                }
                BlockBasic out1 = (BlockBasic)bb.getOut(0);
                BlockBasic out2 = (BlockBasic)bb.getOut(1);
                int inslot;
                BlockBasic leastout;
                if (out1.sizeIn() < out2.sizeIn()) {
                    leastout = out1;
                    inslot = bb.getOutRevIndex(0);
                }
                else {
                    leastout = out2;
                    inslot = bb.getOutRevIndex(1);
                }
                if (leastout.sizeIn() == 1) {
                    continue;
                }

                for (int j = 0; j < leastout.sizeIn(); ++j) {
                    if (j == inslot) {
                        continue;
                    }
                    BlockBasic bb2 = (BlockBasic)leastout.getIn(j);
                    if (condjoin.match(bb, bb2)) {
                        // Indicate change has been made
                        count += 1;
                        condjoin.execute();
                        condjoin.clear();
                        break;
                    }
                }

            }
            return 0;
        }
    }
}
