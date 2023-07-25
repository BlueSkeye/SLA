using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Split the epilog code of the function
    /// Introduce RETURN operations corresponding to individual branches flowing to the epilog.
    internal class ActionReturnSplit : Action
    {
        /// \brief Gather all blocks that have \e goto edge to a RETURN
        /// Collect all BlockGoto or BlockIf nodes, where there is a \e goto
        /// edge to a RETURN block.
        /// \param parent is a FlowBlock that ends in a RETURN operation
        /// \param vec will hold the \e goto blocks
        private static void gatherReturnGotos(FlowBlock parent, List<FlowBlock> vec)
        {
            FlowBlock bl;
            FlowBlock? ret;

            for (int i = 0; i < parent.sizeIn(); ++i) {
                bl = parent.getIn(i).getCopyMap();
                while (bl != null) {
                    if (!bl.isMark()) {
                        ret = null;
                        if (bl.getType() == FlowBlock.block_type.t_goto) {
                            if (((BlockGoto)bl).gotoPrints()) {
                                ret = ((BlockGoto)bl).getGotoTarget();
                            }
                        }
                        else if (bl.getType() == FlowBlock.block_type.t_if) {
                            // if this is an ifgoto block, get target, otherwise null
                            ret = ((BlockIf)bl).getGotoTarget();
                        }
                        if (ret != null) {
                            while (ret.getType() != FlowBlock.block_type.t_basic) {
                                ret = ret.subBlock(0);
                            }
                            if (ret == parent) {
                                bl.setMark();
                                vec.Add(bl);
                            }
                        }
                    }
                    bl = bl.getParent();
                }
            }
        }

        /// Determine if a RETURN block can be split
        /// Given a BasicBlock ending in a RETURN operation, determine
        /// if there is any other substantive operation going on in the block. If there
        /// is, the block is deemed too complicated to split.
        /// \param b is the given BasicBlock
        /// \return \b true if the block can be split
        private static bool isSplittable(BlockBasic b)
        {
            foreach (PcodeOp iter in b) {
                PcodeOp op = iter;
                OpCode opc = op.code();
                if (opc == CPUI_MULTIEQUAL) {
                    continue;
                }
                if ((opc == CPUI_COPY) || (opc == CPUI_RETURN)) {
                    for (int i = 0; i < op->numInput(); ++i) {
                        if (op.getIn(i).isConstant()) {
                            continue;
                        }
                        if (op.getIn(i).isAnnotation()) {
                            continue;
                        }
                        if (op.getIn(i).isFree()) {
                            return false;
                        }
                    }
                    continue;
                }
                return false;
            }
            return true;
        }

        /// Constructor
        public ActionReturnSplit(string g)
            : base(0,"returnsplit", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup()))
                ? null
                : new ActionReturnSplit(getGroup());
        }
        
        public override int apply(Funcdata data)
        {
            PcodeOp op;
            BlockBasic parent;
            FlowBlock? bl;
            list<PcodeOp*>::const_iterator iter, iterend;
            List<int> splitedge;
            List<BlockBasic> retnode;

            if (data.getStructure().getSize() == 0) {
                // Some other restructuring happened first
                return 0;
            }
            iterend = data.endOp(CPUI_RETURN);
            for (iter = data.beginOp(CPUI_RETURN); iter != iterend; ++iter) {
                op = *iter;
                if (op.isDead()) {
                    continue;
                }
                parent = op->getParent();
                if (parent.sizeIn() <= 1) {
                    continue;
                }
                if (!isSplittable(parent)) {
                    continue;
                }
                List<FlowBlock> gotoblocks = new List<FlowBlock>();
                gatherReturnGotos(parent, gotoblocks);
                if (0 == gotoblocks.Count) {
                    continue;
                }

                int splitcount = 0;
                // splitedge will contain edges to be split, IN THE ORDER
                // they will be split.  So we start from the biggest index
                // So that edge removal won't change the index of remaining edges
                for (int i = parent.sizeIn() - 1; i >= 0; --i) {
                    bl = parent.getIn(i).getCopyMap();
                    while (bl != null) {
                        if (bl.isMark()) {
                            splitedge.Add(i);
                            retnode.Add(parent);
                            bl = null;
                            splitcount += 1;
                        }
                        else {
                            bl = bl.getParent();
                        }
                    }
                }

                for (int i = 0; i < gotoblocks.Count; ++i) {
                    // Clear our marks
                    gotoblocks[i].clearMark();
                }

                // Can't split ALL in edges
                if (parent.sizeIn() == splitcount) {
                    splitedge.RemoveAt(splitedge.Count - 1);
                    retnode.RemoveAt(retnode.Count - 1);
                }
            }

            for (int i = 0; i < splitedge.Count; ++i) {
                data.nodeSplit(retnode[i], splitedge[i]);
                count += 1;
#if BLOCKCONSISTENT_DEBUG
                if (!data.getBasicBlocks().isConsistent()) {
                    data.getArch().printMessage("Block structure is not consistent");
                }
#endif
            }
            return 0;
        }
    }
}
