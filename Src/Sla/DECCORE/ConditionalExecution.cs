using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A class for simplifying a series of conditionally executed statements.
    ///
    /// This class tries to perform transformations like the following:
    /// \code
    ///    if (a) {           if (a) {
    ///       BODY1
    ///    }          ==>       BODY1
    ///    if (a) {             BODY2
    ///       BODY2
    ///    }                  }
    /// \endcode
    /// Other similar configurations where two CBRANCHs are based on
    /// the same condition are handled.  The main variation, referred to as a
    /// \b directsplit in the code looks like:
    /// \code
    ///  if (a) {                      if (a && new_boolean()) {
    ///     a = new_boolean();
    ///  }                      ==>      BODY1
    ///  if (a) {
    ///     BODY1
    ///  }                             }
    /// \endcode
    /// The value of 'a' doesn't need to be reevaluated if it is false.
    ///
    /// In the first scenario, there is a block where two flows come
    /// together but don't need to, as the evaluation of the boolean
    /// is redundant.  This block is the \b iblock.  The original
    /// evaluation of the boolean occurs in \b initblock.  There are
    /// two paths from here to \b iblock, called the \b prea path and \b preb path,
    /// either of which may contain additional 1in/1out blocks.
    /// There are also two paths out of \b iblock, \b posta, and \b postb.
    /// The ConditionalExecution class determines if the CBRANCH in
    /// \b iblock is redundant by determining if the boolean value is
    /// either the same as, or the complement of, the boolean value
    /// in \b initblock.  If the CBRANCH is redundant, \b iblock is
    /// removed, linking \b prea to \b posta and \b preb to \b postb (or vice versa
    /// depending on whether the booleans are complements of each other).
    /// If \b iblock is to be removed, modifications to data-flow made
    /// by \b iblock must be preserved.  For MULTIEQUALs in \b iblock,
    /// reads are examined to see if they came from the \b posta path,
    /// or the \b postb path, then the are replaced by the MULTIEQUAL
    /// slot corresponding to the matching \b prea or \b preb branch. If
    /// \b posta and \b postb merge at an \b exitblock, the MULTIEQUAL must
    /// be pushed into the \b exitblock and reads which can't be
    /// attributed to the \b posta or \b postb path are replaced by the
    /// \b exitblock MULTIEQUAL.
    ///
    /// In theory, other operations performed in \b iblock could be
    /// pushed into \b exitblock if they are not read in the \b posta
    /// or \b postb paths, but currently
    /// non MULTIEQUAL operations in \b iblock terminate the action.
    ///
    /// In the second scenario, the boolean evaluated in \b initblock
    /// remains unmodified along only one of the two paths out, \b prea
    /// or \b reb.  The boolean in \b iblock (modulo complementing) will
    /// evaluate in the same way. We define \b posta as the path out of
    /// \b iblock that will be followed by this unmodified path. The
    /// transform that needs to be made is to have the unmodified path
    /// out of \b initblock flow immediately into the \b posta path without
    /// having to reevalute the condition in \b iblock.  \b iblock is not
    /// removed because flow from the "modified" path may also flow
    /// into \b posta, depending on how the boolean was modified.
    /// Adjustments to data-flow are similar to the first scenario but
    /// slightly more complicated.  The first block along the \b posta
    /// path is referred to as the \b posta_block, this block will
    /// have a new block flowing into it.
    internal class ConditionalExecution
    {
        /// Function being analyzed
        private Funcdata fd;
        /// CBRANCH in iblock
        private PcodeOp cbranch;
        /// The initial block computing the boolean value
        private BlockBasic initblock;
        /// The block where flow is (unnecessarily) coming together
        private BlockBasic iblock;
        /// iblock.In(prea_inslot) = pre a path
        private int prea_inslot;
        /// Does \b true branch (in terms of iblock) go to path pre a
        private bool init2a_true;
        /// Does \b true branch go to path post a
        private bool iblock2posta_true;
        /// init or pre slot to use, for data-flow thru post
        private int camethruposta_slot;
        /// The \b out edge from iblock to posta
        private int posta_outslot;
        /// First block in posta path
        private BlockBasic posta_block;
        /// First block in postb path
        private BlockBasic postb_block;
        /// True if this the \e direct \e split variation
        private bool directsplit;
        /// Map from block to replacement Varnode for (current) Varnode
        private Dictionary<int, Varnode> replacement;
        /// RETURN ops that have flow coming out of the iblock
        private List<PcodeOp> returnop;
        /// Boolean array indexed by address space indicating whether the space is heritaged
        private List<bool> heritageyes;

        /// \brief Calculate boolean array of all address spaces that have had a heritage pass run.
        /// Used to test if all the links out of the iblock have been calculated.
        private void buildHeritageArray()
        {
            heritageyes.clear();
            Architecture glb = fd.getArch();
            heritageyes.resize(glb.numSpaces(), false);
            for (int i = 0; i < glb.numSpaces(); ++i) {
                AddrSpace spc = glb.getSpace(i);
                if (spc == null) {
                    continue;
                }
                int index = spc.getIndex();
                if (!spc.isHeritaged()) {
                    continue;
                }
                if (fd.numHeritagePasses(spc) > 0) {
                    // At least one pass has been performed on the space
                    heritageyes[index] = true;
                }
            }
        }

        /// \brief Test the most basic requirements on \b iblock
        /// The block must have 2 \b in edges and 2 \b out edges and a final CBRANCH op.
        /// \return \b true if \b iblock matches basic requirements
        private bool testIBlock()
        {
            if (iblock.sizeIn() != 2) {
                return false;
            }
            if (iblock.sizeOut() != 2) {
                return false;
            }
            cbranch = iblock.lastOp();
            if (cbranch == null) {
                return false;
            }
            if (cbranch.code() != OpCode.CPUI_CBRANCH) {
                return false;
            }
            return true;
        }

        /// Find \b initblock, based on \b iblock
        /// \return \b true if configuration between \b initblock and \b iblock is correct
        private bool findInitPre()
        {
            FlowBlock tmp = iblock.getIn(prea_inslot);
            FlowBlock last = iblock;
            while ((tmp.sizeOut() == 1) && (tmp.sizeIn() == 1)) {
                last = tmp;
                tmp = tmp.getIn(0);
            }
            if (tmp.sizeOut() != 2) {
                return false;
            }
            initblock = (BlockBasic)tmp;
            tmp = iblock.getIn(1 - prea_inslot);
            while ((tmp.sizeOut() == 1) && (tmp.sizeIn() == 1)) {
                tmp = tmp.getIn(0);
            }
            if (tmp != initblock) {
                return false;
            }
            if (initblock == iblock) {
                return false;
            }
            init2a_true = (initblock.getTrueOut() == last);
            return true;
        }

        /// Verify that \b initblock and \b iblock branch on the same condition
        /// The conditions must always have the same value or always have
        /// complementary values.
        /// \return \b true if the conditions are correlated
        private bool verifySameCondition()
        {
            PcodeOp init_cbranch = initblock.lastOp();
            if (init_cbranch == null) {
                return false;
            }
            if (init_cbranch.code() != OpCode.CPUI_CBRANCH) {
                return false;
            }
            ConditionMarker tester;
            if (!tester.verifyCondition(cbranch, init_cbranch)){
                return false;
            }
            if (tester.getFlip()) {
                init2a_true = !init2a_true;
            }
            int multislot = tester.getMultiSlot();
            if (multislot != -1) {
                // This is a direct split
                directsplit = true;
                posta_outslot = (multislot == prea_inslot) ? 0 : 1;
                if (init2a_true) {
                    posta_outslot = 1 - posta_outslot;
                }
            }
            return true;
        }

        /// Can we move the (non MULTIEQUAL) defining p-code of the given Varnode
        /// The given Varnode is defined by an operation in \b iblock which must be removed.
        /// Test if this is possible/advisable given a specific p-code op that reads the Varnode
        /// \param vn is the given Varnode
        /// \param op is the given PcodeOp reading the Varnode
        /// \return \b false if it is not possible to move the defining op (because of the given op)
        private bool testOpRead(Varnode vn, PcodeOp op)
        {
            if (op.getParent() == iblock) {
                return true;
            }
            if ((op.code() == OpCode.CPUI_RETURN) && !directsplit) {
                if ((op.numInput() < 2) || (op.getIn(1) != vn)) {
                    // Only test for flow thru to return value
                    return false;
                }
                PcodeOp copyop = vn.getDef();
                if (copyop.code() == OpCode.CPUI_COPY) {
                    // Ordinarily, if -vn- is produced by a COPY we want to return false here because the propagation
                    // hasn't had time to happen here.  But if the flow is into a RETURN this can't propagate, so
                    // we allow this as a read that can be altered.  (We have to move the COPY)
                    Varnode invn = copyop.getIn(0);
                    if (!invn.isWritten()) {
                        return false;
                    }
                    PcodeOp upop = invn.getDef();
                    if ((upop.getParent() == iblock) && (upop.code() != OpCode.CPUI_MULTIEQUAL)) {
                        return false;
                    }
                    returnop.Add(op);
                    return true;
                }
            }
            return false;
        }

        /// Can we mave the MULTIEQUAL defining p-code of the given Varnode
        /// The given Varnode is defined by a MULTIEQUAL in \b iblock which must be removed.
        /// Test if this is possible/advisable given a specific p-code op that reads the Varnode
        /// \param vn is the given Varnode
        /// \param op is the given PcodeOp reading the Varnode
        /// \return \b false if it is not possible to move the defining op (because of the given op)
        private bool testMultiRead(Varnode vn, PcodeOp op)
        {
            if (op.getParent() == iblock) {
                if (!directsplit) {
                    // The COPY is tested separately
                    // If the COPY's output reads can be altered, then -vn- can be altered
                    return (op.code() == OpCode.CPUI_COPY);
                }
            }
            if (op.code() == OpCode.CPUI_RETURN) {
                if ((op.numInput() < 2) || (op.getIn(1) != vn)) {
                    // Only test for flow thru to return value
                    return false;
                }
                // mark that OpCode.CPUI_RETURN needs special handling
                returnop.Add(op);
            }
            return true;
        }

        /// Test if the given PcodeOp can be removed from \b iblock
        /// \param op is the PcodeOp within \b iblock to test
        /// \return \b true if it is removable
        private bool testRemovability(PcodeOp op)
        {
            PcodeOp readop;
            Varnode? vn;

            if (op.code() == OpCode.CPUI_MULTIEQUAL) {
                vn = op.getOut();
                IEnumerator<PcodeOp> iter = vn.beginDescend();
                while (iter.MoveNext()) {
                    readop = iter.Current;
                    if (!testMultiRead(vn, readop)) {
                        return false;
                    }
                }
            }
            else {
                if (op.isFlowBreak() || op.isCall()) {
                    return false;
                }
                if ((op.code() == OpCode.CPUI_LOAD) || (op.code() == OpCode.CPUI_STORE)){
                    return false;
                }
                if (op.code() == OpCode.CPUI_INDIRECT) {
                    return false;
                }
                vn = op.getOut();
                if (vn != null) {
                    bool hasnodescend = true;
                    IEnumerator<PcodeOp> iter = vn.beginDescend();
                    while (iter.MoveNext()) {
                        readop = iter.Current;
                        if (!testOpRead(vn, readop)) {
                            return false;
                        }
                        hasnodescend = false;
                    }
                    if (hasnodescend && (!heritageyes[vn.getSpace().getIndex()])){
                        // Check if heritage is performed for this varnode's space
                        return false;
                    }
                }
            }
            return true;
        }

        /// \brief Prebuild a replacement MULTIEQUAL for output Varnode of the given PcodeOp in \b posta_block
        /// The new op will hold the same data-flow as the original Varnode once a new
        /// edge into \b posta_block is created.
        /// \param op is the given PcodeOp
        private void predefineDirectMulti(PcodeOp op)
        {
            PcodeOp newop = fd.newOp(posta_block.sizeIn() + 1, posta_block.getStart());
            Varnode outvn = op.getOut();
            Varnode newoutvn;
            newoutvn = fd.newVarnodeOut(outvn.getSize(), outvn.getAddr(), newop);
            fd.opSetOpcode(newop, OpCode.CPUI_MULTIEQUAL);
            Varnode vn;
            int inslot = iblock.getOutRevIndex(posta_outslot);
            for (int i = 0; i < posta_block.sizeIn(); ++i) {
                vn = (i == inslot)
                    ? op.getIn(1 - camethruposta_slot)
                    : newoutvn;
                fd.opSetInput(newop, vn, i);
            }
            fd.opSetInput(newop, op.getIn(camethruposta_slot), posta_block.sizeIn());
            fd.opInsertBegin(newop, posta_block);

            // Cache this new data flow holder
            replacement[posta_block.getIndex()] = newoutvn;
        }

        /// Update inputs to any MULTIEQUAL in the direct block
        /// In the \e direct \e split case, MULTIEQUALs in the body block (\b posta_block)
        /// must update their flow to account for \b iblock being removed and a new
        /// block flowing into the body block.
        private void adjustDirectMulti()
        {
            int inslot = iblock.getOutRevIndex(posta_outslot);
            foreach (PcodeOp iter in posta_block) {
                PcodeOp op;
                if (op.code() != OpCode.CPUI_MULTIEQUAL) {
                    continue;
                }
                Varnode vn = op.getIn(inslot);
                if (vn.isWritten() && (vn.getDef().getParent() == iblock)) {
                    if (vn.getDef().code() != OpCode.CPUI_MULTIEQUAL)
                        throw new LowlevelError("Cannot push non-trivial operation");
                    // Flow that stays in iblock, comes from modified side
                    fd.opSetInput(op, vn.getDef().getIn(1 - camethruposta_slot), inslot);
                    // Flow from unmodified side, forms new branch
                    vn = vn.getDef().getIn(camethruposta_slot);
                }
                fd.opInsertInput(op, vn, op.numInput());
            }
        }

        /// \brief Create a MULTIEQUAL in the given block that will hold data-flow from the given PcodeOp
        /// A new MULTIEQUAL is created whose inputs are the output of the given PcodeOp
        /// \param op is the PcodeOp whose output will get held
        /// \param bl is the block that will contain the new MULTIEQUAL
        /// \return the output Varnode of the new MULTIEQUAL
        private Varnode getNewMulti(PcodeOp op, BlockBasic bl)
        {
            PcodeOp newop = fd.newOp(bl.sizeIn(), bl.getStart());
            Varnode outvn = op.getOut();
            Varnode newoutvn;
            // Using the original outvn address may cause merge conflicts
            //  newoutvn = fd.newVarnodeOut(outvn.getSize(),outvn.getAddr(),newop);
            newoutvn = fd.newUniqueOut(outvn.getSize(), newop);
            fd.opSetOpcode(newop, OpCode.CPUI_MULTIEQUAL);

            // We create NEW references to outvn, these refs will get put
            // at the end of the dependency list and will get handled in
            // due course
            for (int i = 0; i < bl.sizeIn(); ++i) {
                fd.opSetInput(newop, outvn, i);
            }
            fd.opInsertBegin(newop, bl);
            return newoutvn;
        }

        /// \brief Find a replacement Varnode for the output of the given PcodeOp that is read in the given block
        /// The replacement Varnode must be valid for everything below (dominated) by the block.
        /// If we can't find a replacement, create one (as a MULTIEQUAL) in the given
        /// block (creating recursion through input blocks).  Any new Varnode created is
        /// cached in the \b replacement array so it can get picked up by other calls to this function
        /// for different blocks.
        /// \param op is the given PcodeOp whose output we must replace
        /// \param bl is the given basic block (containing a read of the Varnode)
        /// \return the replacement Varnode
        private Varnode getReplacementRead(PcodeOp op, BlockBasic bl)
        {
            Varnode iter;
            if (replacement.TryGetValue(bl.getIndex(), out iter)) {
                return iter;
            }
            BlockBasic curbl = bl;
            // Flow must eventually come through iblock
            while (curbl.getImmedDom() != iblock) {
                // Get immediate dominator
                curbl = (BlockBasic)curbl.getImmedDom();
                if (curbl == null) {
                    throw new LowlevelError("Conditional execution: Could not find dominator");
                }
            }
            if (replacement.TryGetValue(curbl.getIndex(), out iter)) {
                replacement[bl.getIndex()] = iter;
                return iter;
            }
            Varnode res;
            if (curbl.sizeIn() == 1) {
                // Since dominator is iblock, In(0) must be iblock
                // Figure what side of -iblock- we came through
                int slot = (curbl.getInRevIndex(0) == posta_outslot)
                    ? camethruposta_slot
                    : 1 - camethruposta_slot;
                res = op.getIn(slot);
            }
            else {
                res = getNewMulti(op, curbl);
            }
            replacement[curbl.getIndex()] = res;
            if (curbl != bl) {
                replacement[bl.getIndex()] = res;
            }
            return res;
        }

        /// Replace the data-flow for the given PcodeOp in \b iblock
        /// The data-flow for the given op is reproduced in the new control-flow configuration.
        /// After completion of this method, the op can be removed.
        /// \param op is the given PcodeOp
        private void doReplacement(PcodeOp op)
        {
            if (op.code() == OpCode.CPUI_COPY) {
                // Verify that this has been dealt with by fixReturnOp
                if (op.getOut().hasNoDescend()) {
                    return;
                }
                // It could be a COPY internal to iblock, we need to remove it like any other op
            }
            replacement.Clear();
            if (directsplit) {
                predefineDirectMulti(op);
            }
            Varnode vn = op.getOut();
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp readop = iter.Current;
                int slot = readop.getSlot(vn);
                BlockBasic bl = readop.getParent();
                Varnode rvn;
                if (bl == iblock) {
                    if (directsplit) {
                        // We know op is MULTIEQUAL
                        fd.opSetInput(readop, op.getIn(1 - camethruposta_slot), slot);
                    }
                    else {
                        fd.opUnsetInput(readop, slot);
                    }
                }
                else {
                    if (readop.code() == OpCode.CPUI_MULTIEQUAL) {
                        BlockBasic inbl = (BlockBasic)bl.getIn(slot);
                        if (inbl == iblock) {
                            int s = (bl.getInRevIndex(slot) == posta_outslot)
                                ? camethruposta_slot
                                : 1 - camethruposta_slot;
                            rvn = op.getIn(s);
                        }
                        else {
                            rvn = getReplacementRead(op, inbl);
                        }
                    }
                    else {
                        rvn = getReplacementRead(op, bl);
                    }
                    fd.opSetInput(readop, rvn, slot);
                }
                // The last descendant is now gone
                iter = vn.beginDescend();
            }
        }

        /// \brief Reproduce COPY data-flow into RETURN ops affected by the removal of \b iblock
        private void fixReturnOp()
        {
            for (int i = 0; i < returnop.Count; ++i) {
                PcodeOp retop = returnop[i];
                Varnode retvn = retop.getIn(1);
                PcodeOp iblockop = retvn.getDef();
                Varnode invn;
                invn = (iblockop.code() == OpCode.CPUI_COPY)
                    // This must either be from MULTIEQUAL or something written outside of iblock
                    ? iblockop.getIn(0)
                    : retvn;
                PcodeOp newcopyop = fd.newOp(1, retop.getAddr());
                fd.opSetOpcode(newcopyop, OpCode.CPUI_COPY);
                // Preserve the OpCode.CPUI_RETURN storage address
                Varnode outvn = fd.newVarnodeOut(retvn.getSize(), retvn.getAddr(), newcopyop);
                fd.opSetInput(newcopyop, invn, 0);
                fd.opSetInput(retop, outvn, 1);
                fd.opInsertBefore(newcopyop, retop);
            }
        }

        /// Verify that we have a removable \b iblock
        /// The \b iblock has been fixed. Test all control-flow conditions, and test removability
        /// of all ops in the \b iblock.
        /// \return \b true if the configuration can be modified
        private bool verify()
        {
            prea_inslot = 0;
            posta_outslot = 0;
            directsplit = false;

            if (!testIBlock()) {
                return false;
            }
            if (!findInitPre()) {
                return false;
            }
            if (!verifySameCondition()) {
                return false;
            }

            // Cache some useful values
            iblock2posta_true = (posta_outslot == 1);
            camethruposta_slot = (init2a_true == iblock2posta_true) ? prea_inslot : 1 - prea_inslot;
            posta_block = (BlockBasic)iblock.getOut(posta_outslot);
            postb_block = (BlockBasic)iblock.getOut(1 - posta_outslot);

            returnop.Clear();
            IEnumerator<PcodeOp> iter = iblock.reverseEnumerator();

            // Skip branch
            iter.MoveNext();
            while (iter.MoveNext()) {
                if (!testRemovability(iter.Current)) {
                    return false;
                }
            }
            return true;
        }

        /// Constructor
        /// Set up for testing ConditionalExecution on multiple iblocks
        /// \param f is the function to do testing on
        public ConditionalExecution(Funcdata f)
        {
            fd = f;
            // Cache an array depending on the particular heritage pass
            buildHeritageArray();
        }

        /// Test for a modifiable configuration around the given block
        /// The given block is tested as a possible \b iblock. If this configuration
        /// works and is not a \b directsplit, \b true is returned.
        /// If the configuration works as a \b directsplit, then recursively check that
        /// its \b posta_block works as an \b iblock. If it does work, keep this
        /// \b iblock, otherwise revert to the \b directsplit configuration. In either
        /// case return \b true.  Processing the \b directsplit first may prevent
        /// posta_block from being an \b iblock.
        /// \param ib is the trial \b iblock
        /// \return \b true if (some) configuration is recognized and can be modified
        public bool trial(BlockBasic ib)
        {
            iblock = ib;
            if (!verify()) {
                return false;
            }

            PcodeOp cbranch_copy;
            BlockBasic initblock_copy;
            BlockBasic iblock_copy;
            int prea_inslot_copy;
            bool init2a_true_copy;
            bool iblock2posta_true_copy;
            int camethruposta_slot_copy;
            int posta_outslot_copy;
            BlockBasic posta_block_copy;
            BlockBasic postb_block_copy;
            bool directsplit_copy;

            while(true) {
                if (!directsplit) {
                    return true;
                }
                // Save off the data for current iblock
                cbranch_copy = cbranch;
                initblock_copy = initblock;
                iblock_copy = iblock;
                prea_inslot_copy = prea_inslot;
                init2a_true_copy = init2a_true;
                iblock2posta_true_copy = iblock2posta_true;
                camethruposta_slot_copy = camethruposta_slot;
                posta_outslot_copy = posta_outslot;
                posta_block_copy = posta_block;
                postb_block_copy = postb_block;
                directsplit_copy = directsplit;

                iblock = posta_block;
                if (!verify()) {
                    cbranch = cbranch_copy;
                    initblock = initblock_copy;
                    iblock = iblock_copy;
                    prea_inslot = prea_inslot_copy;
                    init2a_true = init2a_true_copy;
                    iblock2posta_true = iblock2posta_true_copy;
                    camethruposta_slot = camethruposta_slot_copy;
                    posta_outslot = posta_outslot_copy;
                    posta_block = posta_block_copy;
                    postb_block = postb_block_copy;
                    directsplit = directsplit_copy;
                    return true;
                }
            }
        }

        /// Eliminate the unnecessary path join at \b iblock
        /// We assume the last call to verify() returned \b true
        public void execute()
        {
            PcodeOp op;

            // Patch any data-flow thru to OpCode.CPUI_RETURN
            fixReturnOp();
            if (!directsplit) {
                IEnumerator<PcodeOp> iter = iblock.beginOp();
                while (iter.MoveNext()) {
                    op = iter.Current;
                    if (!op.isBranch()) {
                        // Remove all read refs of op
                        doReplacement(op);
                    }
                    // Then destroy op
                    fd.opDestroy(op);
                }
                fd.removeFromFlowSplit(iblock, (posta_outslot != camethruposta_slot));
            }
            else {
                adjustDirectMulti();
                IEnumerator<PcodeOp> iter = iblock.beginOp();
                while (iter.MoveNext()) {
                    op = iter.Current;
                    if (op.code() == OpCode.CPUI_MULTIEQUAL) {
                        // Only adjust MULTIEQUALs
                        doReplacement(op);
                        fd.opDestroy(op);
                    }
                    // Branch stays, other operations stay
                }
                fd.switchEdge(iblock.getIn(camethruposta_slot), iblock, posta_block);
            }
        }
    }
}
