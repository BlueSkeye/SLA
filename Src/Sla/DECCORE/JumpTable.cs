using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A map from values to control-flow targets within a function
    ///
    /// A JumpTable is attached to a specific OpCode.CPUI_BRANCHIND and encapsulates all
    /// the information necessary to model the indirect jump as a \e switch statement.
    /// It knows how to map from specific switch variable values to the destination
    /// \e case block and how to label the value.
    internal class JumpTable
    {
        // \brief An address table index and its corresponding out-edge
        internal struct IndexPair
        {
            // Out-edge index for the basic-block
            internal int blockPosition;
            // Index of address targeting the basic-block
            internal int addressIndex;
            
            internal IndexPair(int pos, int index)
            {
                blockPosition = pos;
                addressIndex = index;
            }

            /// Compare by position then by index
            /// \param op2 is the other IndexPair to compare with \b this
            /// \return \b true if \b this is ordered before the other IndexPair
            internal static bool operator <(IndexPair op1, IndexPair op2)
            {
                return (op1.blockPosition != op2.blockPosition) 
                    ? (op1.blockPosition < op2.blockPosition)
                    : (op1.addressIndex < op2.addressIndex);
            }

            /// Compare just by position
            /// \param op1 is the first IndexPair to compare
            /// \param op2 is the second IndexPair to compare
            /// \return \b true if op1 is ordered before op2
            internal static bool compareByPosition(IndexPair op1, IndexPair op2)
            {
                return (op1.blockPosition < op2.blockPosition);
            }
        }

        /// Architecture under which this jump-table operates
        private Architecture glb;
        /// Current model of how the jump table is implemented in code
        private JumpModel jmodel;
        /// Initial jump table model, which may be incomplete
        private JumpModel origmodel;
        /// Raw addresses in the jump-table
        private List<Address> addresstable;
        /// Map from basic-blocks to address table index
        private List<IndexPair> block2addr;
        /// The case label for each explicit target
        private List<ulong> label;
        /// Any recovered in-memory data for the jump-table
        private List<LoadTable> loadpoints;
        /// Absolute address of the BRANCHIND jump
        private Address opaddress;
        /// OpCode.CPUI_BRANCHIND linked to \b this jump-table
        private PcodeOp indirect;
        /// Bits of the switch variable being consumed
        private ulong switchVarConsume;
        /// The out-edge corresponding to the \e default switch destination (-1 = undefined)
        private int defaultBlock;
        /// Block out-edge corresponding to last entry in the address table
        private int lastBlock;
        /// Maximum ADDs or SUBs to normalize
        private uint maxaddsub;
        /// Maximum shifts to normalize
        private uint maxleftright;
        /// Maximum extensions to normalize
        private uint maxext;
        /// 0=no stages recovered, 1=additional stage needed, 2=complete
        private int recoverystage;
        /// Set to \b true if information about in-memory model data is/should be collected
        private bool collectloads;

        /// Attempt recovery of the jump-table model
        /// Try to recover each model in turn, until we find one that matches the specific BRANCHIND.
        /// \param fd is the function containing the switch
        private void recoverModel(Funcdata fd)
        {
            if (jmodel != (JumpModel)null) {
                if (jmodel.isOverride()) {
                    // If preexisting model is override
                    jmodel.recoverModel(fd, indirect, 0, glb.max_jumptable_size);
                    return;
                }
                // Otherwise this is an old attempt we should remove
                // delete jmodel;
            }
            Varnode vn = indirect.getIn(0);
            if (vn.isWritten()) {
                PcodeOp op = vn.getDef() ?? throw new BugException();
                if (op.code() == OpCode.CPUI_CALLOTHER) {
                    JumpAssisted jassisted = new JumpAssisted(this);
                    jmodel = jassisted;
                    if (jmodel.recoverModel(fd, indirect, (uint)addresstable.size(), glb.max_jumptable_size))
                        return;
                }
            }
            JumpBasic jbasic = new JumpBasic(this);
            jmodel = jbasic;
            if (jmodel.recoverModel(fd, indirect, (uint)addresstable.size(), glb.max_jumptable_size))
                return;
            jmodel = new JumpBasic2(this);
            ((JumpBasic2)jmodel).initializeStart(jbasic.getPathMeld());
            // delete jbasic;
            if (jmodel.recoverModel(fd, indirect, (uint)addresstable.size(), glb.max_jumptable_size))
                return;
            // delete jmodel;
            jmodel = (JumpModel)null;
        }

        /// Switch \b this table over to a trivial model
        /// Make exactly one case for each output edge of the switch block.
        private void trivialSwitchOver()
        {
            FlowBlock parent;

            block2addr.Clear();
            block2addr.reserve(addresstable.size());
            parent = indirect.getParent();

            if (parent.sizeOut() != addresstable.size())
                throw new LowlevelError("Trivial addresstable and switch block size do not match");
            for (int i = 0; i < parent.sizeOut(); ++i)
                block2addr.Add(new IndexPair(i, i));  // Addresses corresponds exactly to out-edges of switch block
            lastBlock = parent.sizeOut() - 1;
            defaultBlock = -1;      // Trivial case does not have default case
        }

        /// Perform sanity check on recovered address targets
        /// Check that the BRANCHIND is still reachable, if not throw new JumptableNotReachableError.
        /// Check pathological cases when there is only one address in the table, if we find
        /// this, throw the JumptableThunkError. Let the model run its sanity check.
        /// Print a warning if the sanity check truncates the original address table.
        /// \param fd is the function containing the switch
        private void sanityCheck(Funcdata fd)
        {
            uint sz = (uint)addresstable.size();

            if (!isReachable(indirect))
                throw new JumptableNotReachableError("No legal flow");
            if (addresstable.size() == 1)
            { // One entry is likely some kind of thunk
                bool isthunk = false;
                ulong diff;
                Address addr = addresstable[0];
                if (addr.getOffset() == 0)
                    isthunk = true;
                else {
                    Address addr2 = indirect.getAddr();
                    diff = (addr.getOffset() < addr2.getOffset()) ?
                        (addr2.getOffset() - addr.getOffset()) :
                        (addr.getOffset() - addr2.getOffset());
                    if (diff > 0xffff)
                        isthunk = true;
                }
                if (isthunk) {
                    throw new JumptableThunkError("Likely thunk");
                }
            }
            if (!jmodel.sanityCheck(fd, indirect, addresstable)) {
                throw new LowlevelError($"Jumptable at {opaddress} did not pass sanity check.");
            }
            if (sz != addresstable.size()) // If address table was resized
                fd.warning("Sanity check requires truncation of jumptable", opaddress);
        }

        /// Convert a basic-block to an out-edge index from the switch.
        /// Given a specific basic-block, figure out which edge out of the switch block
        /// hits it.  This \e position is different from the index into the address table,
        /// the out edges are deduped and may include additional guard destinations.
        /// If no edge hits it, throw an exception.
        /// \param bl is the specific basic-block
        /// \return the position of the basic-block
        private int block2Position(FlowBlock bl)
        {
            FlowBlock parent;
            int position;

            parent = indirect.getParent();
            for (position = 0; position < bl.sizeIn(); ++position)
                if (bl.getIn(position) == parent) break;
            if (position == bl.sizeIn())
                throw new LowlevelError("Requested block, not in jumptable");
            return bl.getInRevIndex(position);
        }

        /// Check if the given PcodeOp still seems reachable in its function
        /// We are not doing a complete check, we are looking for a guard that has collapsed to "if (false)"
        /// \param op is the given PcodeOp to check
        /// \return \b true is the PcodeOp is reachable
        private static bool isReachable(PcodeOp op)
        {
            BlockBasic parent = op.getParent();

            for (int i = 0; i < 2; ++i)
            {   // Only check two levels
                if (parent.sizeIn() != 1) return true;
                BlockBasic bl = (BlockBasic)parent.getIn(0);
                if (bl.sizeOut() != 2) continue; // Check if -bl- looks like it contains a guard
                PcodeOp cbranch = bl.lastOp();
                if ((cbranch == (PcodeOp)null) || (cbranch.code() != OpCode.CPUI_CBRANCH))
                    continue;
                Varnode vn = cbranch.getIn(1); // Get the boolean variable
                if (!vn.isConstant()) continue; // Has the guard collapsed
                int trueslot = cbranch.isBooleanFlip() ? 0 : 1;
                if (vn.getOffset() == 0)
                    trueslot = 1 - trueslot;
                if (bl.getOut(trueslot) != parent) // If the remaining path does not lead to -op-
                    return false;       // return that op is not reachable
                parent = bl;
            }
            return true;
        }

        /// \param g is the Architecture the table exists within
        /// \param ad is the Address of the BRANCHIND \b this models
        public JumpTable(Architecture g, Address? ad = null)
        {
            opaddress = ad ?? new Address();
            glb = g;
            jmodel = (JumpModel)null;
            origmodel = (JumpModel)null;
            indirect = (PcodeOp)null;
            switchVarConsume = ulong.MaxValue;
            defaultBlock = -1;
            lastBlock = -1;
            maxaddsub = 1;
            maxleftright = 1;
            maxext = 1;
            recoverystage = 0;
            collectloads = false;
        }

        /// This is a partial clone of another jump-table. Objects that are specific
        /// to the particular Funcdata instance must be recalculated.
        /// \param op2 is the jump-table to clone
        internal JumpTable(JumpTable op2)
        {
            glb = op2.glb;
            jmodel = (JumpModel)null;
            origmodel = (JumpModel)null;
            indirect = (PcodeOp)null;
            switchVarConsume = ulong.MaxValue;
            defaultBlock = -1;
            lastBlock = op2.lastBlock;
            maxaddsub = op2.maxaddsub;
            maxleftright = op2.maxleftright;
            maxext = op2.maxext;
            recoverystage = op2.recoverystage;
            collectloads = op2.collectloads;
            // We just clone the addresses themselves
            addresstable = op2.addresstable;
            loadpoints = op2.loadpoints;
            opaddress = op2.opaddress;
            if (op2.jmodel != (JumpModel)null)
                jmodel = op2.jmodel.clone(this);
        }

        ~JumpTable()
        {
            //if (jmodel != (JumpModel)null)
            //    delete jmodel;
            //if (origmodel != (JumpModel)null)
            //    delete origmodel;
        }

        /// Return \b true if a model has been recovered
        internal bool isRecovered() => !addresstable.empty();

        /// Return \b true if \e case labels are computed
        internal bool isLabelled() => !label.empty();

        /// Return \b true if \b this table was manually overridden
        internal bool isOverride() => (jmodel != (JumpModel)null) && jmodel.isOverride();

        /// Return \b true if this could be multi-staged
        internal bool isPossibleMultistage() => (addresstable.Count == 1);

        /// Return what stage of recovery this jump-table is @in.
        internal int getStage() => recoverystage;

        /// Return the size of the address table for \b this jump-table
        internal int numEntries() => addresstable.Count;

        /// Get bits of switch variable consumed by \b this table
        internal ulong getSwitchVarConsume() => switchVarConsume;

        /// Get the out-edge corresponding to the \e default switch destination
        internal int getDefaultBlock() => defaultBlock;

        /// Get the address of the BRANCHIND for the switch
        internal Address getOpAddress() => opaddress;

        /// Get the BRANCHIND PcodeOp
        internal PcodeOp getIndirectOp() => indirect;

        /// Set the BRANCHIND PcodeOp
        internal void setIndirectOp(PcodeOp ind)
        {
            ind.AssertIsIndirectBranching();
            opaddress = ind.getAddr();
            indirect = ind;
        }

        /// Set the switch variable normalization model restrictions
        private void setNormMax(uint maddsub, uint mleftright, uint mext)
        {
            maxaddsub = maddsub; maxleftright = mleftright; maxext = mext;
        }

        /// \brief Force manual override information on \b this jump-table.
        ///
        /// The model is switched over to JumpBasicOverride, which is initialized with an externally
        /// provided list of addresses.  The addresses are forced as the output addresses the BRANCHIND
        /// for \b this jump-table.  If a non-zero hash and an address is provided, this identifies a
        /// specific Varnode to use as the normalized switch variable. A potential starting value for
        /// normalized switch variable range is provided.
        /// \param addrtable is the manually provided list of addresses to put in the address table
        /// \param naddr is the address where the normalized switch variable is defined
        /// \param h is a hash identifying the normalized switch variable (or 0)
        /// \param sv is the starting value for the range of possible normalized switch variable values (usually 0)
        private void setOverride(List<Address> addrtable, Address naddr, ulong h, ulong sv)
        {
            //if (jmodel != (JumpModel)null)
            //    delete jmodel;
            JumpBasicOverride @override;
            jmodel = @override = new JumpBasicOverride(this);
            @override.setAddresses(addrtable);
            @override.setNorm(naddr, h);
            @override.setStartingValue(sv);
        }

        /// \brief Return the number of address table entries that target the given basic-block
        ///
        /// \param bl is the given basic-block
        /// \return the count of entries
        internal int numIndicesByBlock(FlowBlock bl)
        {
            IndexPair val = new IndexPair(block2Position(bl),0);
            Tuple<IEnumerator<IndexPair>, IEnumerator<IndexPair>> range;
            range = equal_range(block2addr.begin(), block2addr.end(), val, IndexPair.compareByPosition);
            return range.second - range.first;
        }

        /// \brief Get the index of the i-th address table entry that corresponds to the given basic-block
        ///
        /// An exception is thrown if no address table entry targets the block.
        /// \param bl is the given basic-block
        /// \param i requests a specific position within the duplicate entries
        /// \return the address table index
        internal int getIndexByBlock(FlowBlock bl, int i)
        {
            IndexPair val = new IndexPair(block2Position(bl),0);
            int count = 0;
            IEnumerator<IndexPair> iter = lower_bound(block2addr.begin(), block2addr.end(), val, IndexPair.compareByPosition);
            while (iter.MoveNext()) {
                if (iter.Current.blockPosition == val.blockPosition) {
                    if (count == i)
                        return iter.Current.addressIndex;
                    count += 1;
                }
            }
            throw new LowlevelError("Could not get jumptable index for block");
        }

        // Get the i-th address table entry
        internal Address getAddressByIndex(int i) => addresstable[i];

        // Set the most common jump-table target to be the last address in the table
        internal void setLastAsMostCommon()
        {
            defaultBlock = lastBlock;
        }

        // Set out-edge of the switch destination considered to be \e default
        internal void setDefaultBlock(int bl)
        {
            defaultBlock = bl;
        }

        // Set whether LOAD records should be collected
        internal void setLoadCollect(bool val)
        {
            collectloads = val;
        }

        // Force a given basic-block to be a switch destination
        // This is used to add address targets from guard branches if they are
        // not already in the address table. A specific case label for the block
        // can also be provided. The new target is appended directly to the end of the table.
        // \param bl is the given basic-block
        // \param lab is the case label for the block
        internal void addBlockToSwitch(BlockBasic bl, ulong lab)
        {
            addresstable.Add(bl.getStart());
            // The block WILL be added to the end of the out-edges
            lastBlock = indirect.getParent().sizeOut();
            block2addr.Add(new IndexPair(lastBlock, addresstable.size() - 1));
            label.Add(lab);
        }

        /// Convert absolute addresses to block indices
        /// Convert addresses in \b this table to actual targeted basic-blocks.
        /// This constructs a map from each out-edge from the basic-block containing the BRANCHIND
        /// to addresses in the table targetting that out-block. The most common
        /// address table entry is also calculated here.
        /// \param flow is used to resolve address targets
        internal void switchOver(FlowInfo flow)
        {
            FlowBlock parent;
            FlowBlock tmpbl;
            int pos;
            PcodeOp op;

            block2addr.Clear();
            block2addr.reserve(addresstable.Count);
            parent = indirect.getParent();

            for (int i = 0; i < addresstable.Count; ++i) {
                Address addr = addresstable[i];
                op = flow.target(addr);
                tmpbl = op.getParent();
                for (pos = 0; pos < parent.sizeOut(); ++pos)
                    if (parent.getOut(pos) == tmpbl) break;
                if (pos == parent.sizeOut())
                    throw new LowlevelError("Jumptable destination not linked");
                block2addr.Add(new IndexPair(pos, i));
            }
            // Out-edge of last address in table
            lastBlock = block2addr.GetLastItem().blockPosition;
            block2addr.Sort();

            // There is no default case initially
            defaultBlock = -1;
            // If the maxcount is less than 2
            int maxcount = 1;
            IEnumerator<IndexPair> iter = block2addr.GetEnumerator();
            while (iter.MoveNext()) {
                int curPos = iter.Current.blockPosition;
                IEnumerator<IndexPair> nextiter = iter;
                int count = 0;
                while (iter.Current.blockPosition == curPos) {
                    count += 1;
                    if (!iter.MoveNext()) break;
                }
                iter = nextiter;
                if (count > maxcount) {
                    maxcount = count;
                    defaultBlock = curPos;
                }
            }
        }

        // Given a \e case index, get its label
        internal ulong getLabelByIndex(int index) => label[index];

        /// Hide the normalization code for the switch
        /// Eliminate any code involved in actually computing the destination address so
        /// it looks like the OpCode.CPUI_BRANCHIND operation does it all internally.
        /// \param fd is the function containing \b this switch
        private void foldInNormalization(Funcdata fd)
        {
            Varnode? switchvn = jmodel.foldInNormalization(fd, indirect);
            if (switchvn != (Varnode)null) {
                // If possible, mark up the switch variable as not fully consumed so that
                // subvariable flow can truncate it.
                switchVarConsume = Globals.minimalmask(switchvn.getNZMask());
                if (switchVarConsume >= Globals.calc_mask(switchvn.getSize()))
                {   // If mask covers everything
                    if (switchvn.isWritten()) {
                        PcodeOp op = switchvn.getDef();
                        if (op.code() == OpCode.CPUI_INT_SEXT) {
                            // Check for a signed extension
                            // Assume the extension is not consumed
                            switchVarConsume = Globals.calc_mask((uint)op.getIn(0).getSize());
                        }
                    }
                }
            }
        }

        // Hide any guard code for \b this switch
        private bool foldInGuards(Funcdata fd) => jmodel.foldInGuards(fd, this);

        // Recover the raw jump-table addresses (the address table)
        // The addresses that the raw BRANCHIND op might branch to itself are recovered,
        // not including other targets of the final model, like guard addresses.  The normalized switch
        // variable and the guards are identified in the process however.
        //
        // Generally this method is run during flow analysis when we only have partial information about
        // the function (and possibly the switch itself).  The Funcdata instance is a partial clone of the
        // function and is different from the final instance that will hold the fully recovered jump-table.
        // The final instance inherits the addresses recovered here, but recoverModel() will need to be
        // run on it separately.
        //
        // A sanity check is also run, which might truncate the original set of addresses.
        // \param fd is the function containing the switch
        internal void recoverAddresses(Funcdata fd)
        {
            recoverModel(fd);
            if (jmodel == (JumpModel)null) {
                throw new LowlevelError(
                    $"Could not recover jumptable at {opaddress}. Too many branches");
            }
            if (jmodel.getTableSize() == 0) {
                throw new JumptableNotReachableError(
                    $"Impossible to reach jumptable at {opaddress}");
            }
            //  if (sz < 2)
            //    fd.warning("Jumptable has only one branch",opaddress);
            if (collectloads)
                jmodel.buildAddresses(fd, indirect, addresstable, loadpoints);
            else
                jmodel.buildAddresses(fd, indirect, addresstable, (List<LoadTable>)null);
            sanityCheck(fd);
        }

        // Recover jump-table addresses keeping track of a possible previous stage
        // Do a normal recoverAddresses, but save off the old JumpModel, and if we fail recovery, put back the old model.
        //\param fd is the function containing the switch
        internal void recoverMultistage(Funcdata fd)
        {
            //if (origmodel != (JumpModel)null)
            //    delete origmodel;
            origmodel = jmodel;
            jmodel = (JumpModel)null;

            List<Address> oldaddresstable = addresstable;
            addresstable.Clear();
            loadpoints.Clear();
            try {
                recoverAddresses(fd);
            }
            catch (JumptableThunkError) {
                //if (jmodel != (JumpModel)null)
                //    delete jmodel;
                jmodel = origmodel;
                origmodel = (JumpModel)null;
                addresstable = oldaddresstable;
                fd.warning("Second-stage recovery error", indirect.getAddr());
            }
            catch (LowlevelError) {
                //if (jmodel != (JumpModel)null)
                //    delete jmodel;
                jmodel = origmodel;
                origmodel = (JumpModel)null;
                addresstable = oldaddresstable;
                fd.warning("Second-stage recovery error", indirect.getAddr());
            }
            recoverystage = 2;
            if (origmodel != (JumpModel)null) {
                // Keep the new model if it was created successfully
                // delete origmodel;
                origmodel = (JumpModel)null;
            }
        }

        /// Recover the case labels for \b this jump-table
        /// This is run assuming the address table has already been recovered, via recoverAddresses() in another
        /// Funcdata instance. So recoverModel() needs to be rerun on the instance passed in here.
        ///
        /// The unnormalized switch variable is recovered, and for each possible address table entry, the variable
        /// value that produces it is calculated and stored as the formal \e case label for the associated code block.
        /// \param fd is the (final instance of the) function containing the switch
        /// \return \b true if it looks like a multi-stage restart is needed.
        private bool recoverLabels(Funcdata fd)
        {
            if (!isRecovered())
                throw new LowlevelError("Trying to recover jumptable labels without addresses");

            // Unless the model is an override, move model (created on a flow copy) so we can create a current instance
            if (jmodel != (JumpModel)null) {
                //if (origmodel != (JumpModel)null)
                //    delete origmodel;
                if (!jmodel.isOverride()) {
                    origmodel = jmodel;
                    jmodel = (JumpModel)null;
                }
                else
                    fd.warning("Switch is manually overridden", opaddress);
            }

            bool multistagerestart = false;
            recoverModel(fd);       // Create a current instance of the model
            if (jmodel != (JumpModel)null) {
                if (jmodel.getTableSize() != addresstable.size()) {
                    fd.warning("Could not find normalized switch variable to match jumptable", opaddress);
                    if ((addresstable.size() == 1) && (jmodel.getTableSize() > 1))
                        multistagerestart = true;
                }
                if ((origmodel == (JumpModel)null) || (origmodel.getTableSize() == 0)) {
                    jmodel.findUnnormalized(maxaddsub, maxleftright, maxext);
                    jmodel.buildLabels(fd, addresstable, label, jmodel);
                }
                else {
                    jmodel.findUnnormalized(maxaddsub, maxleftright, maxext);
                    jmodel.buildLabels(fd, addresstable, label, origmodel);
                }
            }
            else {
                jmodel = new JumpModelTrivial(this);
                jmodel.recoverModel(fd, indirect, (uint)addresstable.size(), glb.max_jumptable_size);
                jmodel.buildAddresses(fd, indirect, addresstable, (List<LoadTable>)null);
                trivialSwitchOver();
                jmodel.buildLabels(fd, addresstable, label, origmodel);
            }
            if (origmodel != (JumpModel)null) {
                // delete origmodel;
                origmodel = (JumpModel)null;
            }
            return multistagerestart;
        }

        /// Check if this jump-table requires an additional recovery stage
        /// Look for the override directive that indicates we need an additional recovery stage for
        /// \b this jump-table.
        /// \param fd is the function containing the switch
        /// \return \b true if an additional recovery stage is required.
        internal bool checkForMultistage(Funcdata fd)
        {
            if (addresstable.size() != 1) return false;
            if (recoverystage != 0) return false;
            if (indirect == (PcodeOp)null) return false;

            if (fd.getOverride().queryMultistageJumptable(indirect.getAddr())) {
                // Mark that we need additional recovery
                recoverystage = 1;
                return true;
            }
            return false;
        }

        /// Clear instance specific data for \b this jump-table
        /// Clear out any data that is specific to a Funcdata instance.
        /// Right now this is only getting called, when the jumptable is an override in order to clear out derived data.
        internal void clear()
        {
            if (origmodel != (JumpModel)null) {
                //delete origmodel;
                origmodel = (JumpModel)null;
            }
            if (jmodel.isOverride())
                jmodel.clear();
            else {
                //delete jmodel;
                jmodel = (JumpModel)null;
            }
            addresstable.Clear();
            block2addr.Clear();
            lastBlock = -1;
            label.Clear();
            loadpoints.Clear();
            indirect = (PcodeOp)null;
            switchVarConsume = ulong.MaxValue;
            defaultBlock = -1;
            recoverystage = 0;
            // -opaddress- -maxtablesize- -maxaddsub- -maxleftright- -maxext- -collectloads- are permanent
        }

        /// Encode \b this jump-table as a \<jumptable> element
        /// The recovered addresses and case labels are encode to the stream.
        /// If override information is present, this is also incorporated into the element.
        /// \param encoder is the stream encoder
        internal void encode(Sla.CORE.Encoder encoder)
        {
            if (!isRecovered())
                throw new LowlevelError("Trying to save unrecovered jumptable");

            encoder.openElement(ElementId.ELEM_JUMPTABLE);
            opaddress.encode(encoder);
            for (int i = 0; i < addresstable.size(); ++i) {
                encoder.openElement(ElementId.ELEM_DEST);
                AddrSpace spc = addresstable[i].getSpace();
                ulong off = addresstable[i].getOffset();
                if (spc != (AddrSpace)null)
                    spc.encodeAttributes(encoder, off);
                if (i < label.size()) {
                    if (label[i] != 0xBAD1ABE1)
                        encoder.writeUnsignedInteger(AttributeId.ATTRIB_LABEL, label[i]);
                }
                encoder.closeElement(ElementId.ELEM_DEST);
            }
            if (!loadpoints.empty()) {
                for (int i = 0; i < loadpoints.size(); ++i)
                    loadpoints[i].encode(encoder);
            }
            if ((jmodel != (JumpModel)null) && (jmodel.isOverride()))
                jmodel.encode(encoder);
            encoder.closeElement(ElementId.ELEM_JUMPTABLE);
        }

        /// Decode \b this jump-table from a \<jumptable> element
        /// Parse addresses, \e case labels, and any override information from a \<jumptable> element.
        /// Other parts of the model and jump-table will still need to be recovered.
        /// \param decoder is the stream decoder
        internal void decode(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_JUMPTABLE);
            opaddress = Address.decode(decoder);
            bool missedlabel = false;
            while(true) {
                ElementId subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_DEST) {
                    decoder.openElement();
                    bool foundlabel = false;
                    while(true) {
                        AttributeId attribId = decoder.getNextAttributeId();
                        if (attribId == 0) break;
                        if (attribId == AttributeId.ATTRIB_LABEL) {
                            if (missedlabel)
                                throw new LowlevelError("Jumptable entries are missing labels");
                            ulong lab = decoder.readUnsignedInteger();
                            label.Add(lab);
                            foundlabel = true;
                            break;
                        }
                    }
                    if (!foundlabel)        // No label attribute
                        missedlabel = true; // No following entries are allowed to have a label attribute
                    addresstable.Add(Address.decode(decoder));
                }
                else if (subId == ElementId.ELEM_LOADTABLE) {
                    LoadTable newTable = new LoadTable();
                    newTable.decode(decoder);
                    loadpoints.Add(newTable);
                }
                else if (subId == ElementId.ELEM_BASICOVERRIDE) {
                    if (jmodel != (JumpModel)null)
                        throw new LowlevelError("Duplicate jumptable override specs");
                    jmodel = new JumpBasicOverride(this);
                    jmodel.decode(decoder);
                }
            }
            decoder.closeElement(elemId);
            if (label.size() != 0) {
                while (label.size() < addresstable.size())
                    label.Add(0xBAD1ABE1);
            }
        }
    }
}
