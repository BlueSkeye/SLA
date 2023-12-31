﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Find Varnodes with a vectorized lane scheme and attempt to split the lanes
    /// The Architecture lists (List) registers that may be used to perform parallelized operations
    /// on \b lanes within the register. This action looks for these registers as Varnodes, determines
    /// if a particular lane scheme makes sense in terms of the function's data-flow, and then
    /// rewrites the data-flow so that the lanes become explicit Varnodes.
    internal class ActionLaneDivide : Action
    {
        /// \brief Examine the PcodeOps using the given Varnode to determine possible lane sizes
        /// Run through the defining op and any descendant ops of the given Varnode, looking for
        /// OpCode.CPUI_PIECE and OpCode.CPUI_SUBPIECE. Use these to determine possible lane sizes and
        /// register them with the given LanedRegister object.
        /// \param vn is the given Varnode
        /// \param allowedLanes is used to determine if a putative lane size is allowed
        /// \param checkLanes collects the possible lane sizes
        private void collectLaneSizes(Varnode vn, LanedRegister allowedLanes,
            LanedRegister checkLanes)
        {
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            bool iterationCompleted = !iter.MoveNext();
            int step = 0;      // 0 = descendants, 1 = def, 2 = done
            if (iterationCompleted) {
                step = 1;
            }
            while (step < 2) {
                int curSize;       // Putative lane size
                if (step == 0) {
                    PcodeOp op = iter.Current;
                    iterationCompleted = !iter.MoveNext();
                    if (iterationCompleted)
                        step = 1;
                    if (op.code() != OpCode.CPUI_SUBPIECE) continue;  // Is the big register split into pieces
                    curSize = op.getOut().getSize();
                }
                else {
                    step = 2;
                    if (!vn.isWritten()) continue;
                    PcodeOp op = vn.getDef();
                    if (op.code() != OpCode.CPUI_PIECE) continue;     // Is the big register formed from smaller pieces
                    curSize = op.getIn(0).getSize();
                    int tmpSize = op.getIn(1).getSize();
                    if (tmpSize < curSize)
                        curSize = tmpSize;
                }
                if (allowedLanes.allowedLane(curSize))
                    checkLanes.addLaneSize(curSize);            // Register this possible size
            }
        }

        /// \brief Search for a likely lane size and try to divide a single Varnode into these lanes
        ///
        /// There are different ways to search for a lane size:
        ///
        /// Mode 0: Collect putative lane sizes based on the local ops using the Varnode. Attempt
        /// to divide based on each of those lane sizes in turn.
        ///
        /// Mode 1: Similar to mode 0, except we allow for SUBPIECE operations that truncate to
        /// variables that are smaller than the lane size.
        ///
        /// Mode 2: Attempt to divide based on a default lane size.
        /// \param data is the function being transformed
        /// \param vn is the given single Varnode
        /// \param lanedRegister is acceptable set of lane sizes for the Varnode
        /// \param mode is the lane size search mode (0, 1, or 2)
        /// \return \b true if the Varnode (and its data-flow) was successfully split
        private bool processVarnode(Funcdata data, Varnode vn, LanedRegister lanedRegister, int mode)
        {
            // Lanes we are going to try, initialized to no lanes
            LanedRegister checkLanes = new LanedRegister();
            bool allowDowncast = (mode > 0);
            if (mode < 2) {
                collectLaneSizes(vn, lanedRegister, checkLanes);
            }
            else {
                checkLanes.addLaneSize(4);      // Default lane size
            }
            IEnumerator<int> laneIterator = checkLanes.GetEnumerator();
            while (laneIterator.MoveNext()) {
                int curSize = laneIterator.Current;
                // Lane scheme dictated by curSize
                LaneDescription description = new LaneDescription(lanedRegister.getWholeSize(),
                    curSize);
                LaneDivide laneDivide = new LaneDivide(data,vn,description,allowDowncast);
                if (laneDivide.doTrace()) {
                    laneDivide.apply();
                    // Indicate a change was made
                    count += 1;
                    return true;
                }
            }
            return false;
        }

        /// Constructor
        public ActionLaneDivide(string g)
            : base(ruleflags.rule_onceperfunc,"lanedivide", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionLaneDivide(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            for (int mode = 0; mode < 3; ++mode) {
                bool allStorageProcessed = true;
                IEnumerator<KeyValuePair<VarnodeData, LanedRegister>> iter = data.beginLaneAccess();
                while (iter.MoveNext()) {
                    LanedRegister lanedReg = iter.Current.Value;
                    Address addr = iter.Current.Key.getAddr();
                    uint sz = iter.Current.Key.size;
                    IEnumerator<Varnode> viter = data.beginLoc((int)sz, addr);
                    // IEnumerator<Varnode> venditer = data.endLoc(sz, addr);
                    bool allVarnodesProcessed = true;
                    while (viter.MoveNext()) {
                        Varnode vn = viter.Current;
                        if (processVarnode(data, vn, lanedReg, mode)) {
                            // Recalculate bounds
                            viter = data.beginLoc((int)sz, addr);
                            // venditer = data.endLoc((int)sz, addr);
                            allVarnodesProcessed = true;
                        }
                        else {
                            allVarnodesProcessed = false;
                        }
                    }
                    if (!allVarnodesProcessed) {
                        allStorageProcessed = false;
                    }
                }
                if (allStorageProcessed) {
                    break;
                }
            }
            data.clearLanedAccessMap();
            data.setLanedRegGenerated();
            return 0;
        }
    }
}
