using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief The basic switch model
    ///
    /// This is the most common model:
    ///   - A straight-line calculation from switch variable to BRANCHIND
    ///   - The switch variable is bounded by one or more \e guards that branch around the BRANCHIND
    ///   - The unnormalized switch variable is recovered from the normalized variable through some basic transforms
    internal class JumpBasic : JumpModel
    {
        protected JumpValuesRange jrange;      ///< Range of values for the (normalized) switch variable
        protected PathMeld pathMeld;          ///< Set of PcodeOps and Varnodes producing the final target addresses
        protected List<GuardRecord> selectguards;   ///< Any guards associated with \b model
        protected int varnodeIndex;          ///< Position of the normalized switch Varnode within PathMeld
        protected Varnode normalvn;          ///< Normalized switch Varnode
        protected Varnode switchvn;          ///< Unnormalized switch Varnode

        /// Do we prune in here in our depth-first search for the normalized switch variable
        /// \param vn is the Varnode we are testing for pruning
        /// \return \b true if the search should be pruned here
        protected static bool isprune(Varnode vn)
        {
            if (!vn.isWritten()) return true;
            PcodeOp* op = vn.getDef();
            if (op.isCall() || op.isMarker()) return true;
            if (op.numInput() == 0) return true;
            return false;
        }

        /// Is it possible for the given Varnode to be a switch variable?
        /// \param vn is the given Varnode to test
        /// \return \b false if it is impossible for the Varnode to be the switch variable
        protected static bool ispoint(Varnode vn)
        {
            if (vn.isConstant()) return false;
            if (vn.isAnnotation()) return false;
            if (vn.isReadOnly()) return false;
            return true;
        }

        /// Get the step/stride associated with the Varnode
        /// If the some of the least significant bits of the given Varnode are known to
        /// be zero, translate this into a stride for the jumptable range.
        /// \param vn is the given Varnode
        /// \return the calculated stride = 1,2,4,...
        protected static int getStride(Varnode vn)
        {
            ulong mask = vn.getNZMask();
            if ((mask & 0x3f) == 0)     // Limit the maximum stride we can return
                return 32;
            int stride = 1;
            while ((mask & 1) == 0)
            {
                mask >>= 1;
                stride <<= 1;
            }
            return stride;
        }

        /// \brief Back up the constant value in the output Varnode to the value in the input Varnode
        /// This does the work of going from a normalized switch value to the unnormalized value.
        /// PcodeOps between the output and input Varnodes must be reversible or an exception is thrown.
        /// \param fd is the function containing the switch
        /// \param output is the constant value to back up
        /// \param outvn is the output Varnode of the data-flow
        /// \param invn is the input Varnode to back up to
        /// \return the recovered value associated with the input Varnode
        protected static ulong backup2Switch(Funcdata fd, ulong output, Varnode outvn, Varnode invn)
        {
            Varnode* curvn = outvn;
            PcodeOp* op;
            TypeOp* top;
            int slot;

            while (curvn != invn)
            {
                op = curvn.getDef();
                top = op.getOpcode();
                for (slot = 0; slot < op.numInput(); ++slot) // Find first non-constant input
                    if (!op.getIn(slot).isConstant()) break;
                if (op.getEvalType() == PcodeOp::binary)
                {
                    Address addr = op.getIn(1 - slot).getAddr();
                    ulong otherval;
                    if (!addr.isConstant())
                    {
                        MemoryImage mem = new MemoryImage(addr.getSpace(),4,1024,fd.getArch().loader);
                        otherval = mem.getValue(addr.getOffset(), op.getIn(1 - slot).getSize());
                    }
                    else
                        otherval = addr.getOffset();
                    output = top.recoverInputBinary(slot, op.getOut().getSize(), output,
                                     op.getIn(slot).getSize(), otherval);
                    curvn = op.getIn(slot);
                }
                else if (op.getEvalType() == PcodeOp::unary)
                {
                    output = top.recoverInputUnary(op.getOut().getSize(), output, op.getIn(slot).getSize());
                    curvn = op.getIn(slot);
                }
                else
                    throw new LowlevelError("Bad switch normalization op");
            }
            return output;
        }

        /// Get maximum value associated with the given Varnode
        /// If the Varnode has a restricted range due to masking via INT_AND, the maximum value of this range is returned.
        /// Otherwise, 0 is returned, indicating that the Varnode can take all possible values.
        /// \param vn is the given Varnode
        /// \return the maximum value or 0
        protected static ulong getMaxValue(Varnode vn)
        {
            ulong maxValue = 0;     // 0 indicates maximum possible value
            if (!vn.isWritten())
                return maxValue;
            PcodeOp* op = vn.getDef();
            if (op.code() == OpCode.CPUI_INT_AND)
            {
                Varnode* constvn = op.getIn(1);
                if (constvn.isConstant())
                {
                    maxValue = Globals.coveringmask(constvn.getOffset());
                    maxValue = (maxValue + 1) & Globals.calc_mask(vn.getSize());
                }
            }
            else if (op.code() == OpCode.CPUI_MULTIEQUAL)
            {   // Its possible the AND is duplicated across multiple blocks
                int i;
                for (i = 0; i < op.numInput(); ++i)
                {
                    Varnode* subvn = op.getIn(i);
                    if (!subvn.isWritten()) break;
                    PcodeOp* andOp = subvn.getDef();
                    if (andOp.code() != OpCode.CPUI_INT_AND) break;
                    Varnode* constvn = andOp.getIn(1);
                    if (!constvn.isConstant()) break;
                    if (maxValue < constvn.getOffset())
                        maxValue = constvn.getOffset();
                }
                if (i == op.numInput())
                {
                    maxValue = Globals.coveringmask(maxValue);
                    maxValue = (maxValue + 1) & Globals.calc_mask(vn.getSize());
                }
                else
                    maxValue = 0;
            }
            return maxValue;
        }

        /// \brief Calculate the initial set of Varnodes that might be switch variables
        ///
        /// Paths that terminate at the given PcodeOp are calculated and organized
        /// in a PathMeld object that determines Varnodes that are common to all the paths.
        /// \param op is the given PcodeOp
        /// \param slot is input slot to the PcodeOp all paths must terminate at
        protected void findDeterminingVarnodes(PcodeOp op, int slot)
        {
            List<PcodeOpNode> path;
            bool firstpoint = false;    // Have not seen likely switch variable yet

            path.Add(PcodeOpNode(op, slot));

            do
            {   // Traverse through tree of inputs to final address
                PcodeOpNode & node(path.GetLastItem());
                Varnode* curvn = node.op.getIn(node.slot);
                if (isprune(curvn))
                {   // Here is a node of the tree
                    if (ispoint(curvn))
                    {   // Is it a possible switch variable
                        if (!firstpoint)
                        {   // If it is the first possible
                            pathMeld.set(path); // Take the current path as the result
                            firstpoint = true;
                        }
                        else            // If we have already seen at least one possible
                            pathMeld.meld(path);
                    }

                    path.GetLastItem().slot += 1;
                    while (path.GetLastItem().slot >= path.GetLastItem().op.numInput())
                    {
                        path.RemoveLastItem();
                        if (path.empty()) break;
                        path.GetLastItem().slot += 1;
                    }
                }
                else
                {           // This varnode is not pruned
                    path.Add(PcodeOpNode(curvn.getDef(), 0));
                }
            } while (path.size() > 1);
            if (pathMeld.empty())
            {   // Never found a likely point, which means that
                // it looks like the address is uniquely determined
                // but the constants/readonlys haven't been collapsed
                pathMeld.set(op, op.getIn(slot));
            }
        }

        /// \brief Analyze CBRANCHs leading up to the given basic-block as a potential switch \e guard.
        ///
        /// In general there is only one path to the switch, and the given basic-block will
        /// hold the BRANCHIND.  In some models, there is more than one path to the switch block,
        /// and a path must be specified.  In this case, the given basic-block will be a block that
        /// flows into the switch block, and the \e pathout parameter describes which path leads
        /// to the switch block.
        ///
        /// For each CBRANCH, range restrictions on the various variables which allow
        /// control flow to pass through the CBRANCH to the switch are analyzed.
        /// A GuardRecord is created for each of these restrictions.
        /// \param bl is the given basic-block
        /// \param pathout is an optional path from the basic-block to the switch or -1
        protected void analyzeGuards(BlockBasic bl, int pathout)
        {
            int i, j, indpath;
            int maxbranch = 2;     // Maximum number of CBRANCHs to consider
            int maxpullback = 2;
            bool usenzmask = (jumptable.getStage() == 0);

            selectguards.clear();
            BlockBasic* prevbl;
            Varnode* vn;

            for (i = 0; i < maxbranch; ++i)
            {
                if ((pathout >= 0) && (bl.sizeOut() == 2))
                {
                    prevbl = bl;
                    bl = (BlockBasic*)prevbl.getOut(pathout);
                    indpath = pathout;
                    pathout = -1;
                }
                else
                {
                    pathout = -1;       // Make sure not to use pathout next time around
                    for (; ; )
                    {
                        if (bl.sizeIn() != 1)
                        {
                            if (bl.sizeIn() > 1)
                                checkUnrolledGuard(bl, maxpullback, usenzmask);
                            return;
                        }
                        // Only 1 flow path to the switch
                        prevbl = (BlockBasic*)bl.getIn(0);
                        if (prevbl.sizeOut() != 1) break; // Is it possible to deviate from switch path in this block
                        bl = prevbl;        // If not, back up to next block
                    }
                    indpath = bl.getInRevIndex(0);
                }
                PcodeOp* cbranch = prevbl.lastOp();
                if ((cbranch == (PcodeOp)null) || (cbranch.code() != OpCode.CPUI_CBRANCH))
                    break;
                if (i != 0)
                {
                    // Check that this CBRANCH isn't protecting some other switch
                    BlockBasic* otherbl = (BlockBasic*)prevbl.getOut(1 - indpath);
                    PcodeOp* otherop = otherbl.lastOp();
                    if (otherop != (PcodeOp)null && otherop.code() == OpCode.CPUI_BRANCHIND)
                    {
                        if (otherop != jumptable.getIndirectOp())
                            break;
                    }
                }
                bool toswitchval = (indpath == 1);
                if (cbranch.isBooleanFlip())
                    toswitchval = !toswitchval;
                bl = prevbl;
                vn = cbranch.getIn(1);
                CircleRange rng(toswitchval);

                // The boolean variable could conceivably be the switch variable
                int indpathstore = prevbl.getFlipPath() ? 1 - indpath : indpath;
                selectguards.Add(GuardRecord(cbranch, cbranch, indpathstore, rng, vn));
                for (j = 0; j < maxpullback; ++j)
                {
                    Varnode* markup;        // Throw away markup information
                    if (!vn.isWritten()) break;
                    PcodeOp* readOp = vn.getDef();
                    vn = rng.pullBack(readOp, &markup, usenzmask);
                    if (vn == (Varnode)null) break;
                    if (rng.isEmpty()) break;
                    selectguards.Add(GuardRecord(cbranch, readOp, indpathstore, rng, vn));
                }
            }
        }

        /// \brief Calculate the range of values in the given Varnode that direct control-flow to the switch
        ///
        /// The Varnode is evaluated against each GuardRecord to determine if its range of values
        /// can be restricted. Multiple guards may provide different restrictions.
        /// \param vn is the given Varnode
        /// \param rng will hold resulting range of values the Varnode can hold at the switch
        protected void calcRange(Varnode vn, CircleRange rng)
        {
            // Get an initial range, based on the size/type of -vn-
            int stride = 1;
            if (vn.isConstant())
                rng = CircleRange(vn.getOffset(), vn.getSize());
            else if (vn.isWritten() && vn.getDef().isBoolOutput())
                rng = CircleRange(0, 2, 1, 1);  // Only 0 or 1 possible
            else
            {           // Should we go ahead and use nzmask in all cases?
                ulong maxValue = getMaxValue(vn);
                stride = getStride(vn);
                rng = CircleRange(0, maxValue, vn.getSize(), stride);
            }

            // Intersect any guard ranges which apply to -vn-
            int bitsPreserved;
            Varnode* baseVn = GuardRecord::quasiCopy(vn, bitsPreserved);
            List<GuardRecord>::const_iterator iter;
            for (iter = selectguards.begin(); iter != selectguards.end(); ++iter)
            {
                GuardRecord guard = *iter;
                int matchval = guard.valueMatch(vn, baseVn, bitsPreserved);
                // if (matchval == 2)   TODO: we need to check for aliases
                if (matchval == 0) continue;
                if (rng.intersect(guard.getRange()) != 0) continue;
            }

            // It may be an assumption that the switch value is positive
            // in which case the guard might not check for it. If the
            // size is too big, we try only positive values
            if (rng.getSize() > 0x10000)
            {
                CircleRange positive = new CircleRange(0,(rng.getMask() >> 1) + 1,vn.getSize(),stride);
                positive.intersect(rng);
                if (!positive.isEmpty())
                    rng = positive;
            }
        }

        /// \brief Find the putative switch variable with the smallest range of values reaching the switch
        ///
        /// The Varnode with the smallest range and closest to the BRANCHIND is assumed to be the normalized
        /// switch variable. If an expected range size is provided, it is used to \e prefer a particular
        /// Varnode as the switch variable.  Whatever Varnode is selected,
        /// the JumpValue object is set up to iterator over its range.
        /// \param matchsize optionally gives an expected size of the range, or it can be 0
        protected void findSmallestNormal(uint matchsize)
        {
            CircleRange rng;
            ulong sz, maxsize;

            varnodeIndex = 0;
            calcRange(pathMeld.getVarnode(0), rng);
            jrange.setRange(rng);
            jrange.setStartVn(pathMeld.getVarnode(0));
            jrange.setStartOp(pathMeld.getOp(0));
            maxsize = rng.getSize();
            for (uint i = 1; i < pathMeld.numCommonVarnode(); ++i)
            {
                if (maxsize == matchsize)   // Found variable that gives (already recovered) size
                    return;
                calcRange(pathMeld.getVarnode(i), rng);
                sz = rng.getSize();
                if (sz < maxsize)
                {
                    // Don't let a 1-byte switch variable get thru without a guard
                    if ((sz != 256) || (pathMeld.getVarnode(i).getSize() != 1))
                    {
                        varnodeIndex = i;
                        maxsize = sz;
                        jrange.setRange(rng);
                        jrange.setStartVn(pathMeld.getVarnode(i));
                        jrange.setStartOp(pathMeld.getEarliestOp(i));
                    }
                }
            }
        }

        /// \brief Do all the work necessary to recover the normalized switch variable
        ///
        /// The switch can be specified as the basic-block containing the BRANCHIND, or
        /// as a block that flows to the BRANCHIND block by following the specified path out.
        /// \param fd is the function containing the switch
        /// \param rootbl is the basic-block
        /// \param pathout is the (optional) path to the BRANCHIND or -1
        /// \param matchsize is an (optional) size to expect for the normalized switch variable range
        /// \param maxtablesize is the maximum size expected for the normalized switch variable range
        protected void findNormalized(Funcdata fd, BlockBasic rootbl, int pathout, uint matchsize,
            uint maxtablesize)
        {
            ulong sz;

            analyzeGuards(rootbl, pathout);
            findSmallestNormal(matchsize);
            sz = jrange.getSize();
            if ((sz > maxtablesize) && (pathMeld.numCommonVarnode() == 1))
            {
                // Check for jump through readonly variable
                // Note the normal jumptable algorithms are cavalier about
                // the jumptable being in readonly memory or not because
                // a jumptable construction almost always implies that the
                // entries are readonly even if they aren't labelled properly
                // The exception is if the jumptable has only one branch
                // as it very common to have semi-dynamic vectors that are
                // set up by the system. But the original LoadImage values
                // are likely incorrect. So for 1 branch, we insist on readonly
                Architecture glb = fd.getArch();
                Varnode vn = pathMeld.getVarnode(0);
                if (vn.isReadOnly())
                {
                    MemoryImage mem = new MemoryImage(vn.getSpace(),4,16,glb.loader);
                    ulong val = mem.getValue(vn.getOffset(), vn.getSize());
                    varnodeIndex = 0;
                    jrange.setRange(new CircleRange(val, vn.getSize()));
                    jrange.setStartVn(vn);
                    jrange.setStartOp(pathMeld.getOp(0));
                }
            }
        }

        /// \brief Mark the guard CBRANCHs that are truly part of the model.
        ///
        /// These CBRANCHs will be removed from the active control-flow graph, their
        /// function \e folded into the action of the model, as represented by BRANCHIND.
        protected void markFoldableGuards()
        {
            Varnode* vn = pathMeld.getVarnode(varnodeIndex);
            int bitsPreserved;
            Varnode* baseVn = GuardRecord::quasiCopy(vn, bitsPreserved);
            for (int i = 0; i < selectguards.size(); ++i)
            {
                GuardRecord & guardRecord(selectguards[i]);
                if (guardRecord.valueMatch(vn, baseVn, bitsPreserved) == 0 || guardRecord.isUnrolled())
                {
                    guardRecord.clear();        // Indicate this guard was not used or should not be folded
                }
            }
        }

        /// Mark (or unmark) all PcodeOps involved in the model
        /// \param val is \b true to set marks, \b false to clear marks
        protected void markModel(bool val)
        {
            pathMeld.markPaths(val, varnodeIndex);
            for (int i = 0; i < selectguards.size(); ++i)
            {
                PcodeOp* op = selectguards[i].getBranch();
                if (op == (PcodeOp)null) continue;
                PcodeOp* readOp = selectguards[i].getReadOp();
                if (val)
                    readOp.setMark();
                else
                    readOp.clearMark();
            }
        }

        /// Check if the given Varnode flows to anything other than \b this model
        /// The PcodeOps in \b this model must have been previously marked with markModel().
        /// Run through the descendants of the given Varnode and look for this mark.
        /// \param vn is the given Varnode
        /// \param trailOp is an optional known PcodeOp that leads to the model
        /// \return \b true if the only flow is into \b this model
        protected bool flowsOnlyToModel(Varnode vn, PcodeOp trailOp)
        {
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op == trailOp) continue;
                if (!op.isMark())
                    return false;
            }
            return true;
        }

        /// Check that all incoming blocks end with a CBRANCH
        /// All CBRANCHs in addition to flowing to the given block, must also flow to another common block,
        /// and each boolean value must select between the given block and the common block in the same way.
        /// If this flow exists, \b true is returned and the boolean Varnode inputs to each CBRANCH are passed back.
        /// \param varArray will hold the input Varnodes being passed back
        /// \param bl is the given block
        /// \return \b true if the common CBRANCH flow exists across all incoming blocks
        protected bool checkCommonCbranch(List<Varnode> varArray, BlockBasic bl)
        {
            BlockBasic* curBlock = (BlockBasic*)bl.getIn(0);
            PcodeOp* op = curBlock.lastOp();
            if (op == (PcodeOp)null || op.code() != OpCode.CPUI_CBRANCH)
                return false;
            int outslot = bl.getInRevIndex(0);
            bool isOpFlip = op.isBooleanFlip();
            varArray.Add(op.getIn(1));   // Pass back boolean input to CBRANCH
            for (int i = 1; i < bl.sizeIn(); ++i)
            {
                curBlock = (BlockBasic*)bl.getIn(i);
                op = curBlock.lastOp();
                if (op == (PcodeOp)null || op.code() != OpCode.CPUI_CBRANCH)
                    return false;               // All blocks must end with CBRANCH
                if (op.isBooleanFlip() != isOpFlip)
                    return false;
                if (outslot != bl.getInRevIndex(i))
                    return false;               // Boolean value must have some meaning
                varArray.Add(op.getIn(1));       // Pass back boolean input to CBRANCH
            }
            return true;
        }

        /// \brief Check for a guard that has been unrolled across multiple blocks
        ///
        /// A guard calculation can be duplicated across multiple blocks that all branch to the basic block
        /// performing the final BRANCHIND.  In this case, the switch variable is also duplicated across multiple Varnodes
        /// that are all inputs to a MULTIEQUAL whose output is used for the final BRANCHIND calculation.  This method
        /// looks for this situation and creates a GuardRecord associated with this MULTIEQUAL output.
        /// \param bl is the basic block on the path to the switch with multiple incoming flows
        /// \param maxpullback is the maximum number of times to pull back from the guard CBRANCH to the putative switch variable
        /// \param usenzmask is \b true if the NZMASK should be used as part of the pull-back operation
        protected void checkUnrolledGuard(BlockBasic bl, int maxpullback, bool usenzmask)
        {
            List<Varnode*> varArray;
            if (!checkCommonCbranch(varArray, bl))
                return;
            int indpath = bl.getInRevIndex(0);
            bool toswitchval = (indpath == 1);
            PcodeOp* cbranch = ((BlockBasic*)bl.getIn(0)).lastOp();
            if (cbranch.isBooleanFlip())
                toswitchval = !toswitchval;
            CircleRange rng(toswitchval);
            int indpathstore = bl.getIn(0).getFlipPath() ? 1 - indpath : indpath;
            PcodeOp* readOp = cbranch;
            for (int j = 0; j < maxpullback; ++j)
            {
                PcodeOp* multiOp = bl.findMultiequal(varArray);
                if (multiOp != (PcodeOp)null)
                {
                    selectguards.Add(GuardRecord(cbranch, readOp, indpathstore, rng, multiOp.getOut(), true));
                }
                Varnode* markup;        // Throw away markup information
                Varnode* vn = varArray[0];
                if (!vn.isWritten()) break;
                PcodeOp* readOp = vn.getDef();
                vn = rng.pullBack(readOp, &markup, usenzmask);
                if (vn == (Varnode)null) break;
                if (rng.isEmpty()) break;
                if (!BlockBasic::liftVerifyUnroll(varArray, readOp.getSlot(vn))) break;
            }
        }

        /// \brief Eliminate the given guard to \b this switch
        /// We \e disarm the guard instructions by making the guard condition
        /// always \b false.  If the simplification removes the unusable branches,
        /// we are left with only one path through the switch.
        /// \param fd is the function containing the switch
        /// \param guard is a description of the particular guard mechanism
        /// \param jump is the JumpTable owning \b this model
        /// \return \b true if a change was made to data-flow
        protected virtual bool foldInOneGuard(Funcdata fd, GuardRecord guard, JumpTable jump)
        {
            PcodeOp* cbranch = guard.getBranch();
            int indpath = guard.getPath(); // Get stored path to indirect block
            BlockBasic* cbranchblock = cbranch.getParent();
            if (cbranchblock.getFlipPath()) // Based on whether out branches have been flipped
                indpath = 1 - indpath;  // get actual path to indirect block
            BlockBasic* guardtarget = (BlockBasic*)cbranchblock.getOut(1 - indpath);
            bool change = false;
            int pos;

            // Its possible the guard branch has been converted between the switch recovery and now
            if (cbranchblock.sizeOut() != 2) return false; // In which case, we can't fold it in
            BlockBasic* switchbl = jump.getIndirectOp().getParent();
            for (pos = 0; pos < switchbl.sizeOut(); ++pos)
                if (switchbl.getOut(pos) == guardtarget) break;
            if (pos == switchbl.sizeOut())
            {
                if (BlockBasic::noInterveningStatement(cbranch, indpath, switchbl.lastOp()))
                {
                    // Adjust tables and control flow graph
                    // for new jumptable destination
                    jump.addBlockToSwitch(guardtarget, 0xBAD1ABE1);
                    jump.setLastAsMostCommon();
                    fd.pushBranch(cbranchblock, 1 - indpath, switchbl);
                    guard.clear();
                    change = true;
                }
            }
            else
            {
                // We should probably check that there are no intervening
                // statements between the guard and the switch. But the
                // fact that the guard target is also a switch target
                // is a good indicator that there are none
                ulong val = ((indpath == 0) != (cbranch.isBooleanFlip())) ? 0 : 1;
                fd.opSetInput(cbranch, fd.newConstant(cbranch.getIn(0).getSize(), val), 1);
                jump.setDefaultBlock(pos); // A guard branch generally targets the default case
                guard.clear();
                change = true;
            }
            return change;
        }

        /// Construct given a parent JumpTable
        public JumpBasic(JumpTable jt)
            : base(jt)
        {
            jrange = (JumpValuesRange*)0;
        }

        /// Get the possible of paths to the switch
        public PathMeld getPathMeld() => pathMeld;

        /// Get the normalized value iterator
        public JumpValuesRange getValueRange() => jrange;

        ~JumpBasic()
        {
            if (jrange != (JumpValuesRange*)0)
                delete jrange;
        }

        public override bool isOverride() => false;

        public override int getTableSize() => jrange.getSize();

        public override bool recoverModel(Funcdata fd, PcodeOp indop, uint matchsize,
            uint maxtablesize)
        {
            // Basically there needs to be a straight line calculation from a switch variable to the final
            // address used for the BRANCHIND.  The switch variable is restricted to a small range by one
            // or more "guard" instructions that, if the switch variable is not in range, branch to a default
            // location.
            jrange = new JumpValuesRange();
            findDeterminingVarnodes(indop, 0);
            findNormalized(fd, indop.getParent(), -1, matchsize, maxtablesize);
            if (jrange.getSize() > maxtablesize)
                return false;
            markFoldableGuards();
            return true;
        }

        public override virtual void buildAddresses(Funcdata fd, PcodeOp indop,
            List<Address> addresstable, List<LoadTable> loadpoints)
        {
            ulong val, addr;
            addresstable.clear();       // Clear out any partial recoveries
                                        // Build the emulation engine
            EmulateFunction emul(fd);
            if (loadpoints != (List<LoadTable>*)0)
                emul.setLoadCollect(true);

            ulong mask = ~((ulong)0);
            int bit = fd.getArch().funcptr_align;
            if (bit != 0)
            {
                mask = (mask >> bit) << bit;
            }
            AddrSpace* spc = indop.getAddr().getSpace();
            bool notdone = jrange.initializeForReading();
            while (notdone)
            {
                val = jrange.getValue();
                addr = emul.emulatePath(val, pathMeld, jrange.getStartOp(), jrange.getStartVarnode());
                addr = AddrSpace::addressToByte(addr, spc.getWordSize());
                addr &= mask;
                addresstable.Add(Address(spc, addr));
                notdone = jrange.next();
            }
            if (loadpoints != (List<LoadTable>*)0)
                emul.collectLoadPoints(*loadpoints);
        }

        public override void findUnnormalized(uint maxaddsub, uint maxleftright, uint maxext)
        {
            int i, j;

            i = varnodeIndex;
            normalvn = pathMeld.getVarnode(i++);
            switchvn = normalvn;
            markModel(true);

            int countaddsub = 0;
            int countext = 0;
            PcodeOp* normop = (PcodeOp)null;
            while (i < pathMeld.numCommonVarnode())
            {
                if (!flowsOnlyToModel(switchvn, normop)) break; // Switch variable should only flow into model
                Varnode* testvn = pathMeld.getVarnode(i);
                if (!switchvn.isWritten()) break;
                normop = switchvn.getDef();
                for (j = 0; j < normop.numInput(); ++j)
                    if (normop.getIn(j) == testvn) break;
                if (j == normop.numInput()) break;
                switch (normop.code())
                {
                    case OpCode.CPUI_INT_ADD:
                    case OpCode.CPUI_INT_SUB:
                        countaddsub += 1;
                        if (countaddsub > maxaddsub) break;
                        if (!normop.getIn(1 - j).isConstant()) break;
                        switchvn = testvn;
                        break;
                    case OpCode.CPUI_INT_ZEXT:
                    case OpCode.CPUI_INT_SEXT:
                        countext += 1;
                        if (countext > maxext) break;
                        switchvn = testvn;
                        break;
                    default:
                        break;
                }
                if (switchvn != testvn) break;
                i += 1;
            }
            markModel(false);
        }

        public override void buildLabels(Funcdata fd, List<Address> addresstable,
            List<ulong> label, JumpModel orig)
        {
            ulong val, switchval;
            JumpValuesRange origrange = ((JumpBasic*)orig).getValueRange();

            bool notdone = origrange.initializeForReading();
            while (notdone)
            {
                val = origrange.getValue();
                int needswarning = 0;  // 0=nowarning, 1=this code block may not be properly labeled, 2=calculation failed
                if (origrange.isReversible())
                {   // If the current value is reversible
                    if (!jrange.contains(val))
                        needswarning = 1;
                    try
                    {
                        switchval = backup2Switch(fd, val, normalvn, switchvn);     // Do reverse emulation to get original switch value
                    }
                    catch (EvaluationError err) {
                        switchval = 0xBAD1ABE1;
                        needswarning = 2;
                    }
                }
                else
                    switchval = 0xBAD1ABE1; // If can't reverse, hopefully this is the default or exit, otherwise give "badlabel"
                if (needswarning == 1)
                    fd.warning("This code block may not be properly labeled as switch case", addresstable[label.size()]);
                else if (needswarning == 2)
                    fd.warning("Calculation of case label failed", addresstable[label.size()]);
                label.Add(switchval);

                // Take into account the fact that the address table may have
                // been truncated (via the sanity check)
                if (label.size() >= addresstable.size()) break;
                notdone = origrange.next();
            }

            while (label.size() < addresstable.size())
            {
                fd.warning("Bad switch case", addresstable[label.size()]);
                label.Add(0xBAD1ABE1);
            }
        }

        public override Varnode foldInNormalization(Funcdata fd, PcodeOp indop)
        {
            // Set the BRANCHIND input to be the unnormalized switch variable, so
            // all the intervening code to calculate the final address is eliminated as dead.
            fd.opSetInput(indop, switchvn, 0);
            return switchvn;
        }

        public override bool foldInGuards(Funcdata fd, JumpTable jump)
        {
            bool change = false;
            for (int i = 0; i < selectguards.size(); ++i)
            {
                PcodeOp* cbranch = selectguards[i].getBranch();
                if (cbranch == (PcodeOp)null) continue; // Already normalized
                if (cbranch.isDead())
                {
                    selectguards[i].clear();
                    continue;
                }
                if (foldInOneGuard(fd, selectguards[i], jump))
                    change = true;
            }
            return change;
        }

        public override bool sanityCheck(Funcdata fd, PcodeOp indop, List<Address> addresstable)
        {
            // Test all the addresses in \b this address table checking
            // that they are reasonable. We cut off at the first unreasonable address.
            int i;
            ulong diff;
            if (addresstable.empty()) return true;
            Address addr = addresstable[0];
            i = 0;
            if (addr.getOffset() != 0)
            {
                for (i = 1; i < addresstable.size(); ++i)
                {
                    if (addresstable[i].getOffset() == 0) break;
                    diff = (addr.getOffset() < addresstable[i].getOffset()) ?
                        (addresstable[i].getOffset() - addr.getOffset()) :
                        (addr.getOffset() - addresstable[i].getOffset());
                    if (diff > 0xffff)
                    {
                        byte buffer[8];
                        LoadImage* loadimage = fd.getArch().loader;
                        bool dataavail = true;
                        try
                        {
                            loadimage.loadFill(buffer, 4, addresstable[i]);
                        }
                        catch (DataUnavailError err) {
                            dataavail = false;
                        }
                        if (!dataavail) break;
                    }
                }
            }
            if (i == 0)
                return false;
            if (i != addresstable.size()) {
                addresstable.resize(i);
                jrange.truncate(i);
            }
            return true;
        }

        public override JumpModel clone(JumpTable jt)
        {
            JumpBasic* res = new JumpBasic(jt);
            res.jrange = (JumpValuesRange*)jrange.clone();    // We only need to clone the JumpValues
            return res;
        }

        public override void clear()
        {
            if (jrange != (JumpValuesRange*)0)
            {
                delete jrange;
                jrange = (JumpValuesRange*)0;
            }
            pathMeld.clear();
            selectguards.clear();
            normalvn = (Varnode)null;
            switchvn = (Varnode)null;
        }
    }
}
