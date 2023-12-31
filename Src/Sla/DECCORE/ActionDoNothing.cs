﻿
namespace Sla.DECCORE
{
    /// \brief Remove blocks that do nothing
    internal class ActionDoNothing : Action
    {
        public ActionDoNothing(string g)
            : base(ruleflags.rule_repeatapply,"donothing", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDoNothing(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            // Remove blocks that do nothing
            BlockGraph graph = data.getBasicBlocks();

            for (int i = 0; i < graph.getSize(); ++i) {
                BlockBasic bb = (BlockBasic)graph.getBlock(i);
                if (bb.isDoNothing()) {
                    if ((bb.sizeOut() == 1) && (bb.getOut(0) == bb)) {
                        // Infinite loop
                        if (!bb.isDonothingLoop()) {
                            bb.setDonothingLoop();
                            data.warning("Do nothing block with infinite loop", bb.getStart());
                        }
                    }
                    else if (bb.unblockedMulti(0)) {
                        data.removeDoNothingBlock(bb);
                        count += 1;
                        return 0;
                    }
                }
            }
            return 0;
        }
    }
}
