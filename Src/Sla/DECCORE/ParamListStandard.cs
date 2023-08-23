using Sla.CORE;
using Sla.DECCORE;

using ParamEntryResolver = Sla.EXTRA.rangemap<Sla.DECCORE.ParamEntryRange>;

namespace Sla.DECCORE
{
    /// \brief A standard model for parameters as an ordered list of storage resources
    ///
    /// This is a configurable model for passing (input) parameters as a list to a function.
    /// The model allows 1 or more resource lists based on data-type, either type_metatype.TYPE_UNKNOWN for
    /// general purpose or type_metatype.TYPE_FLOAT for floating-point registers. Within a resource list,
    /// any number of parameters can be used but they must come starting at the beginning of
    /// the list with no \e holes (skipped resources). A resource list can include (at the end)
    /// \e stack parameters that are allocated based on an alignment.  Optionally, the model supports
    /// converting data-types larger than a specified size to pointers within the parameter list.
    internal class ParamListStandard : ParamList
    {
        /// Number of \e groups in this parameter convention
        protected int numgroup;
        /// Maximum heritage delay across all parameters
        protected int maxdelay;
        /// If non-zero, maximum size of a data-type before converting to a pointer
        protected int pointermax;
        /// Does a \b this parameter come before a hidden return parameter
        protected bool thisbeforeret;
        /// The starting group for each resource section
        protected List<int> resourceStart;
        /// The ordered list of parameter entries
        protected List<ParamEntry> entry;
        /// Map from space id to resolver
        protected List<ParamEntryResolver> resolverMap = new List<ParamEntryResolver>();
        /// Address space containing relative offset parameters
        protected AddrSpace spacebase;

        /// Given storage location find matching ParamEntry
        /// Find the (first) entry containing the given memory range
        /// \param loc is the starting address of the range
        /// \param size is the number of bytes in the range
        /// \return the pointer to the matching ParamEntry or null if no match exists
        protected ParamEntry? findEntry(Address loc, int size)
        {
            int index = loc.getSpace().getIndex();
            if (index >= resolverMap.size())
                return (ParamEntry)null;
            ParamEntryResolver resolver = resolverMap[index];
            if (resolver == (ParamEntryResolver)null)
                return (ParamEntry)null;
            Tuple<ParamEntryResolver.Enumerator, ParamEntryResolver.Enumerator> res;
            res = resolver.find(loc.getOffset());
            while (res.first != res.second) {
                ParamEntry testEntry = (res.Current.first).getParamEntry();
                ++res.first;
                if (testEntry.getMinSize() > size) continue;
                if (testEntry.justifiedContain(loc, size) == 0)
                    // Make sure the range is properly justified in entry
                    return testEntry;
            }
            return (ParamEntry)null;
        }

        /// Assign storage for given parameter data-type
        /// Given the next data-type and the status of previously allocated slots,
        /// select the storage location for the parameter.  The status array is
        /// indexed by \e group: a positive value indicates how many \e slots have been allocated
        /// from that group, and a -1 indicates the group/resource is fully consumed.
        /// \param tp is the data-type of the next parameter
        /// \param status is an array marking how many \e slots have already been consumed in a group
        /// \return the newly assigned address for the parameter
        protected Address assignAddress(Datatype tp, List<int> status)
        {
            IEnumerator<ParamEntry> iter = entry.GetEnumerator();
            while (iter.MoveNext()) {
                ParamEntry curEntry = iter.Current;
                int grp = curEntry.getGroup();
                if (status[grp] < 0) continue;
                if ((curEntry.getType() != type_metatype.TYPE_UNKNOWN) && tp.getMetatype() != curEntry.getType())
                    // Wrong type
                    continue;

                Address res = curEntry.getAddrBySlot(status[grp], tp.getSize());
                // If -tp- doesn't fit an invalid address is returned
                if (res.isInvalid()) continue;
                if (curEntry.isExclusion()) {
                    List<int> groupSet = curEntry.getAllGroups();
                    for (int j = 0; j < groupSet.size(); ++j)  // For an exclusion entry
                        status[groupSet[j]] = -1;       // some number of groups are taken up
                }
                return res;
            }
            // Return invalid address to indicated we could not assign anything
            return new Address();
        }

        /// Select entry to fill an unreferenced param
        /// From among the ParamEntrys matching the given \e group, return the one that best matches
        /// the given \e metatype attribute. If there are no ParamEntrys in the group, null is returned.
        /// \param grp is the given \e group number
        /// \param prefType is the preferred \e metatype attribute to match
        protected ParamEntry? selectUnreferenceEntry(int grp, type_metatype prefType)
        {
            int bestScore = -1;
            ParamEntry? bestEntry = (ParamEntry)null;
            IEnumerator<ParamEntry> iter = entry.GetEnumerator();
            while (iter.MoveNext()) {
                ParamEntry curEntry = iter.Current;
                if (curEntry.getGroup() != grp) continue;
                int curScore;
                if (curEntry.getType() == prefType)
                    curScore = 2;
                else if (prefType == type_metatype.TYPE_UNKNOWN)
                    curScore = 1;
                else
                    curScore = 0;
                if (curScore > bestScore) {
                    bestScore = curScore;
                    bestEntry = curEntry;
                }
            }
            return bestEntry;
        }

        /// Build map from parameter trials to model ParamEntrys
        /// Given a set of \b trials (putative Varnode parameters) as ParamTrial objects,
        /// associate each trial with a model ParamEntry within \b this list. Trials for
        /// for which there are no matching entries are marked as unused. Any holes
        /// in the resource list are filled with \e unreferenced trials. The trial list is sorted.
        /// \param active is the set of \b trials to map and organize
        protected void buildTrialMap(ParamActive active)
        {
            // List of groups for which we have a representative
            List<ParamEntry> hitlist = new List<ParamEntry>();
            int floatCount = 0;
            int intCount = 0;

            for (int i = 0; i < active.getNumTrials(); ++i) {
                ParamTrial paramtrial = active.getTrial(i);
                ParamEntry? entrySlot = findEntry(paramtrial.getAddress(), paramtrial.getSize());
                // Note: if a trial is "definitely not used" but there is a matching entry,
                // we still include it in the map
                if (entrySlot == (ParamEntry)null)
                    paramtrial.markNoUse();
                else {
                    // Keep track of entry recovered for this trial
                    paramtrial.setEntry(entrySlot, 0);
                    if (paramtrial.isActive()) {
                        if (entrySlot.getType() == type_metatype.TYPE_FLOAT)
                            floatCount += 1;
                        else
                            intCount += 1;
                    }

                    // Make sure we list that the entries group is marked
                    int grp = entrySlot.getGroup();
                    while (hitlist.size() <= grp)
                        hitlist.Add((ParamEntry)null);
                    ParamEntry lastentry = hitlist[grp];
                    if (lastentry == (ParamEntry)null)
                        // This is the first hit for this group
                        hitlist[grp] = entrySlot;
                }
            }

            // Created unreferenced (unref) ParamTrial for any group that we don't have a representative for
            // if that group occurs before one where we do have a representative
            for (int i = 0; i < hitlist.size(); ++i) {
                ParamEntry curentry = hitlist[i];

                if (curentry == (ParamEntry)null) {
                    curentry = selectUnreferenceEntry(i,
                        (floatCount > intCount) ? type_metatype.TYPE_FLOAT : type_metatype.TYPE_UNKNOWN);
                    if (curentry == (ParamEntry)null)
                        continue;
                    int sz = curentry.isExclusion() ? curentry.getSize() : curentry.getAlign();
                    int nextslot = 0;
                    Address addr = curentry.getAddrBySlot(nextslot, sz);
                    int trialpos = active.getNumTrials();
                    active.registerTrial(addr, sz);
                    ParamTrial paramtrial = active.getTrial(trialpos);
                    paramtrial.markUnref();
                    paramtrial.setEntry(curentry, 0);
                }
                else if (!curentry.isExclusion()) {
                    // For non-exclusion groups, we need to create a secondary hitlist to find holes
                    // within the group
                    List<int> slotlist = new List<int>();
                    for (int j = 0; j < active.getNumTrials(); ++j) {
                        ParamTrial paramtrial = active.getTrial(j);
                        if (paramtrial.getEntry() != curentry) continue;
                        int slot = curentry.getSlot(paramtrial.getAddress(), 0) - curentry.getGroup();
                        int endslot = curentry.getSlot(paramtrial.getAddress(), paramtrial.getSize() - 1) - curentry.getGroup();
                        if (endslot < slot) {
                            // With reverse stacks, the ending address may be in an earlier slot
                            int tmp = slot;
                            slot = endslot;
                            endslot = tmp;
                        }
                        while (slotlist.size() <= endslot)
                            slotlist.Add(0);
                        while (slot <= endslot) {
                            slotlist[slot] = 1;
                            slot += 1;
                        }
                    }
                    for (int j = 0; j < slotlist.size(); ++j) {
                        if (slotlist[j] == 0) {
                            // Make copy of j, so that getAddrBySlot can change it
                            int nextslot = j;
                            Address addr = curentry.getAddrBySlot(nextslot, curentry.getAlign());
                            int trialpos = active.getNumTrials();
                            active.registerTrial(addr, curentry.getAlign());
                            ParamTrial paramtrial = active.getTrial(trialpos);
                            paramtrial.markUnref();
                            paramtrial.setEntry(curentry, 0);
                        }
                    }
                }
            }
            active.sortTrials();
        }

        /// \brief Calculate the range of trials in each resource sections
        /// The trials must already be mapped, which should put them in group order.  The sections
        /// split at the groups given by \b resourceStart.  We pass back the starting index for
        /// each range of trials.
        /// \param active is the given set of parameter trials
        /// \param trialStart will hold the starting index for each range of trials
        protected void separateSections(ParamActive active, List<int> trialStart)
        {
            int numtrials = active.getNumTrials();
            int currentTrial = 0;
            int nextGroup = resourceStart[1];
            int nextSection = 2;
            trialStart.Add(currentTrial);
            for (; currentTrial < numtrials; ++currentTrial) {
                ParamTrial curtrial = active.getTrial(currentTrial);
                if (curtrial.getEntry() == (ParamEntry)null) continue;
                if (curtrial.getEntry().getGroup() >= nextGroup) {
                    if (nextSection > resourceStart.size())
                        throw new LowlevelError("Missing next resource start");
                    nextGroup = resourceStart[nextSection];
                    nextSection += 1;
                    trialStart.Add(currentTrial);
                }
            }
            trialStart.Add(numtrials);
        }

        /// \brief Mark all the trials within the indicated groups as \e not \e used, except for one
        /// specified index. Only one trial within an exclusion group can have active use, mark all others as unused.
        /// \param active is the set of trials, which must be sorted on group
        /// \param activeTrial is the index of the trial whose groups are to be considered active
        /// \param trialStart is the index of the first trial to mark
        protected static void markGroupNoUse(ParamActive active, int activeTrial, int trialStart)
        {
            int numTrials = active.getNumTrials();
            ParamEntry activeEntry = active.getTrial(activeTrial).getEntry();
            for (int i = trialStart; i < numTrials; ++i) {
                // Mark entries intersecting the group set as definitely not used
                // The trial NOT to mark
                if (i == activeTrial) continue;
                ParamTrial othertrial = active.getTrial(i);
                if (othertrial.isDefinitelyNotUsed()) continue;
                if (!othertrial.getEntry().groupOverlap(activeEntry)) break;
                othertrial.markNoUse();
            }
        }

        /// \brief From among multiple \e inactive trials, select the most likely to be active and mark others as not used
        ///
        /// There can be at most one \e inactive trial in an exclusion group for the fill algorithms to work.
        /// Score all the trials and pick the one that is the most likely to actually be an active param.
        /// Mark all the others as definitely not used.
        /// \param active is the sorted set of trials
        /// \param group is the group number
        /// \param groupStart is the index of the first trial in the group
        /// \param prefType is a preferred entry to type to use in scoring
        protected static void markBestInactive(ParamActive active, int group, int groupStart,
            type_metatype prefType)
        {
            int numTrials = active.getNumTrials();
            int bestTrial = -1;
            int bestScore = -1;
            for (int i = groupStart; i < numTrials; ++i) {
                ParamTrial trial = active.getTrial(i);
                if (trial.isDefinitelyNotUsed()) continue;
                ParamEntry entry = trial.getEntry();
                int grp = entry.getGroup();
                if (grp != group) break;
                // Covering multiple slots automatically give low score
                if (entry.getAllGroups().size() > 1) continue;
                int score = 0;
                if (trial.hasAncestorRealistic()) {
                    score += 5;
                    if (trial.hasAncestorSolid())
                        score += 5;
                }
                if (entry.getType() == prefType)
                    score += 1;
                if (score > bestScore) {
                    bestScore = score;
                    bestTrial = i;
                }
            }
            if (bestTrial >= 0)
                markGroupNoUse(active, bestTrial, groupStart);
        }

        /// \brief Enforce exclusion rules for the given set of parameter trials
        ///
        /// If there are more than one active trials in a single group,
        /// and if that group is an exclusion group, mark all but the first trial to \e defnouse.
        /// \param active is the set of trials
        protected static void forceExclusionGroup(ParamActive active)
        {
            int numTrials = active.getNumTrials();
            int curGroup = -1;
            int groupStart = -1;
            int inactiveCount = 0;
            for (int i = 0; i < numTrials; ++i) {
                ParamTrial curtrial = active.getTrial(i);
                if (curtrial.isDefinitelyNotUsed() || !curtrial.getEntry().isExclusion())
                    continue;
                int grp = curtrial.getEntry().getGroup();
                if (grp != curGroup) {
                    if (inactiveCount > 1)
                        markBestInactive(active, curGroup, groupStart, type_metatype.TYPE_UNKNOWN);
                    curGroup = grp;
                    groupStart = i;
                    inactiveCount = 0;
                }
                if (curtrial.isActive()) {
                    markGroupNoUse(active, i, groupStart);
                }
                else {
                    inactiveCount += 1;
                }
            }
            if (inactiveCount > 1)
                markBestInactive(active, curGroup, groupStart, type_metatype.TYPE_UNKNOWN);
        }

        /// \brief Mark every trial above the first "definitely not used" as \e inactive.
        ///
        /// Inspection and marking only occurs within an indicated range of trials,
        /// allowing floating-point and general purpose resources to be treated separately.
        /// \param active is the set of trials, which must already be ordered
        /// \param start is the index of the first trial in the range to consider
        /// \param stop is the index (+1) of the last trial in the range to consider
        protected static void forceNoUse(ParamActive active, int start, int stop)
        {
            bool seendefnouse = false;
            int curgroup = -1;
            bool exclusion = false;
            bool alldefnouse = false;
            for (int i = start; i < stop; ++i) {
                ParamTrial curtrial = active.getTrial(i);
                if (curtrial.getEntry() == (ParamEntry)null)
                    // Already marked as not used
                    continue;
                int grp = curtrial.getEntry().getGroup();
                exclusion = curtrial.getEntry().isExclusion();
                if ((grp <= curgroup) && exclusion) {
                    // If in the same exclusion group
                    if (!curtrial.isDefinitelyNotUsed())
                        // A single element that might be used
                        // means that the whole group might be used
                        alldefnouse = false;
                }
                else {
                    // First trial in a new group (or next element in same non-exclusion group)
                    if (alldefnouse)
                        // If all in the last group were defnotused
                        // then force everything afterward to be defnotused
                        seendefnouse = true;
                    alldefnouse = curtrial.isDefinitelyNotUsed();
                    curgroup = grp;
                }
                if (seendefnouse)
                    curtrial.markInactive();
            }
        }

        /// \brief Enforce rules about chains of inactive slots.
        ///
        /// If there is a chain of slots whose length is greater than \b maxchain,
        /// where all trials are \e inactive, mark trials in any later slot as \e inactive.
        /// Mark any \e inactive trials before this (that aren't in a maximal chain) as active.
        /// The parameter entries in the model may be split up into different resource sections,
        /// as in floating-point vs general purpose.  This method must be called on a single
        /// section at a time. The \b start and \b stop indices describe the range of trials
        /// in the particular section.
        /// \param active is the set of trials, which must be sorted
        /// \param maxchain is the maximum number of \e inactive trials to allow in a chain
        /// \param start is the first index in the range of trials to consider
        /// \param stop is the last index (+1) in the range of trials to consider
        /// \param groupstart is the smallest group id in the particular section
        protected static void forceInactiveChain(ParamActive active, int maxchain, int start,
            int stop, int groupstart)
        {
            bool seenchain = false;
            int chainlength = 0;
            int max = -1;
            for (int i = start; i < stop; ++i) {
                ParamTrial trial = active.getTrial(i);
                if (trial.isDefinitelyNotUsed())
                    // Already know not used
                    continue;
                if (!trial.isActive()) {
                    if (trial.isUnref() && active.isRecoverSubcall()) {
                        // If there is no reference to the trial within the function, the only real possibility
                        // is that a register is an input to the calling function and it is being reused (immediately)
                        // to pass the input into the called function.  This really can't happen on the stack because
                        // the stack relative caller offset and callee offset are different
                        if (trial.getAddress().getSpace().getType() == spacetype.IPTR_SPACEBASE)
                            // So if the parameter is on the stack
                            // Mark that we have already seen an inactive chain
                            seenchain = true;
                    }
                    if (i == start) {
                        chainlength += (trial.slotGroup() - groupstart + 1);
                    }
                    else
                        chainlength += trial.slotGroup() - active.getTrial(i - 1).slotGroup();
                    if (chainlength > maxchain)
                        seenchain = true;
                }
                else {
                    chainlength = 0;
                    if (!seenchain)
                        max = i;
                }
                if (seenchain)
                    trial.markInactive();
            }
            for (int i = start; i <= max; ++i) {
                // Across the range of active trials, fill in "holes" of inactive trials
                ParamTrial trial = active.getTrial(i);
                if (trial.isDefinitelyNotUsed()) continue;
                if (!trial.isActive())
                    trial.markActive();
            }
        }

        /// Calculate the maximum heritage delay for any potential parameter in this list
        protected void calcDelay()
        {
            maxdelay = 0;
            IEnumerator<ParamEntry> iter = entry.GetEnumerator();
            while (iter.MoveNext()) {
                int delay = iter.Current.getSpace().getDelay();
                if (delay > maxdelay)
                    maxdelay = delay;
            }
        }

        /// \brief Internal method for adding a single address range to the ParamEntryResolvers
        ///
        /// Specify the contiguous address range, the ParamEntry to map to it, and a position recording
        /// the order in which ranges are added.
        /// \param spc is address space of the memory range
        /// \param first is the starting offset of the memory range
        /// \param last is the ending offset of the memory range
        /// \param paramEntry is the ParamEntry to associate with the memory range
        /// \param position is the ordering position
        protected void addResolverRange(AddrSpace spc, ulong first, ulong last, ParamEntry paramEntry, int position)
        {
            int index = spc.getIndex();
            while (resolverMap.Count <= index) {
                resolverMap.Add((ParamEntryResolver)null);
            }
            ParamEntryResolver? resolver = resolverMap[index];
            if (resolver == (ParamEntryResolver)null) {
                resolver = new ParamEntryResolver();
                resolverMap[spc.getIndex()] = resolver;
            }
            ParamEntryResolver::inittype initData = ParamEntryResolver::inittype(position, paramEntry);
            resolver.insert(initData, first, last);
        }

        /// Build the ParamEntry resolver maps
        /// Enter all the ParamEntry objects into an interval map (based on address space)
        protected void populateResolver()
        {
            IEnumerator<ParamEntry> iter = entry.begin();
            int position = 0;
            while (iter.MoveNext()) {
                ParamEntry paramEntry = iter.Current;
                AddrSpace spc = paramEntry.getSpace();
                if (spc.getType() == spacetype.IPTR_JOIN) {
                    JoinRecord joinRec = paramEntry.getJoinRecord();
                    for (int i = 0; i < joinRec.numPieces(); ++i) {
                        // Individual pieces making up the join are mapped to the ParamEntry
                        VarnodeData vData = joinRec.getPiece((uint)i);
                        ulong last = vData.offset + (vData.size - 1);
                        addResolverRange(vData.space, vData.offset, last, paramEntry, position);
                        position += 1;
                    }
                }
                else {
                    ulong first = paramEntry.getBase();
                    ulong last = first + (ulong)(paramEntry.getSize() - 1);
                    addResolverRange(spc, first, last, paramEntry, position);
                    position += 1;
                }
            }
        }

        /// \brief Parse a \<pentry> element and add it to \b this list
        ///
        /// \param decoder is the stream decoder
        /// \param effectlist holds any passed back effect records
        /// \param groupid is the group to which the new ParamEntry is assigned
        /// \param normalstack is \b true if the parameters should be allocated from the front of the range
        /// \param autokill is \b true if parameters are automatically added to the killedbycall list
        /// \param splitFloat is \b true if floating-point parameters are in their own resource section
        /// \param grouped is \b true if the new ParamEntry is grouped with other entries
        protected void parsePentry(Decoder decoder, List<EffectRecord> effectlist, int groupid, bool normalstack,
            bool autokill, bool splitFloat, bool grouped)
        {
            type_metatype lastMeta = type_metatype.TYPE_UNION;
            if (!entry.empty()) {
                lastMeta = entry.Last().isGrouped() ? type_metatype.TYPE_UNKNOWN : entry.Last().getType();
            }
            entry.Add(new ParamEntry(groupid));
            entry.Last().decode(decoder, normalstack, grouped, entry);
            if (splitFloat) {
                type_metatype currentMeta = grouped
                    ? type_metatype.TYPE_UNKNOWN
                    : entry.Last().getType();
                if (lastMeta != currentMeta) {
                    if (lastMeta > currentMeta)
                        throw new LowlevelError("parameter list entries must be ordered by metatype");
                    resourceStart.Add(groupid);
                }
            }
            AddrSpace spc = entry.Last().getSpace();
            if (spc.getType() == spacetype.IPTR_SPACEBASE)
                spacebase = spc;
            else if (autokill)  // If a register parameter AND we automatically generate killedbycall
                effectlist.Add(new EffectRecord(entry.Last(), EffectRecord.EffectType.killedbycall));

            int maxgroup = entry.Last().getAllGroups().Last() + 1;
            if (maxgroup > numgroup)
                numgroup = maxgroup;
        }

        /// \brief Parse a sequence of \<pentry> elements that are allocated as a group
        ///
        /// All ParamEntry objects will share the same \b group id.
        /// \param decoder is the stream decoder
        /// \param effectlist holds any passed back effect records
        /// \param groupid is the group to which all ParamEntry elements are assigned
        /// \param normalstack is \b true if the parameters should be allocated from the front of the range
        /// \param autokill is \b true if parameters are automatically added to the killedbycall list
        /// \param splitFloat is \b true if floating-point parameters are in their own resource section
        protected void parseGroup(Decoder decoder, List<EffectRecord> effectlist,
                        int groupid, bool normalstack, bool autokill, bool splitFloat)
        {
            int basegroup = numgroup;
            ParamEntry? previous1 = (ParamEntry)null;
            ParamEntry? previous2 = (ParamEntry)null;
            ElementId elemId = decoder.openElement(ElementId.ELEM_GROUP);
            while (decoder.peekElement() != 0) {
                parsePentry(decoder, effectlist, basegroup, normalstack, autokill, splitFloat, true);
                ParamEntry pentry = entry.Last();
                if (pentry.getSpace().getType() == spacetype.IPTR_JOIN)
                    throw new LowlevelError("<pentry> in the join space not allowed in <group> tag");
                if (previous1 != (ParamEntry)null) {
                    ParamEntry.orderWithinGroup(previous1, pentry);
                    if (previous2 != (ParamEntry)null)
                        ParamEntry.orderWithinGroup(previous2, pentry);
                }
                previous2 = previous1;
                previous1 = pentry;
            }
            decoder.closeElement(elemId);
        }

        /// Construct for use with decode()
        public ParamListStandard()
        {
        }

        /// Copy constructor
        public ParamListStandard(ParamListStandard op2)
        {
            numgroup = op2.numgroup;
            entry = op2.entry;
            spacebase = op2.spacebase;
            maxdelay = op2.maxdelay;
            pointermax = op2.pointermax;
            thisbeforeret = op2.thisbeforeret;
            resourceStart = op2.resourceStart;
            populateResolver();
        }

        ~ParamListStandard()
        {
            for (int i = 0; i < resolverMap.Count; ++i) {
                ParamEntryResolver resolver = resolverMap[i];
                //if (resolver != (ParamEntryResolver)null)
                //    delete resolver;
            }
        }

        /// Get the list of parameter entries
        public List<ParamEntry> getEntry() => entry;

        public override uint getType() => p_standard;

        public override void assignMap(List<Datatype> proto, TypeFactory typefactory, List<ParameterPieces> res)
        {
            List<int> status = new List<int>((int)numgroup);

            if (res.size() == 2) {
                // Check for hidden parameters defined by the output list
                // Reserve first param for hidden ret value
                res.Last().addr = assignAddress(res.Last().type, status);
                res.Last().flags |= ParameterPieces.Flags.hiddenretparm;
                if (res.Last().addr.isInvalid())
                    throw new ParamUnassignedError(
                        $"Cannot assign parameter address for {res.Last().type.getName()}");
            }
            for (int i = 1; i < proto.size(); ++i) {
                res.Add(new ParameterPieces());
                if ((pointermax != 0) && (proto[i].getSize() > pointermax)) {
                    // Datatype is too big
                    // Assume datatype is stored elsewhere and only the pointer is passed
                    AddrSpace? spc = spacebase;
                    if (spc == (AddrSpace)null) spc = typefactory.getArch().getDefaultDataSpace();
                    int pointersize = (int)spc.getAddrSize();
                    int wordsize = (int)spc.getWordSize();
                    Datatype pointertp = typefactory.getTypePointer(pointersize, proto[i], (uint)wordsize);
                    res.Last().addr = assignAddress(pointertp, status);
                    res.Last().type = pointertp;
                    res.Last().flags = ParameterPieces.Flags.indirectstorage;
                }
                else {
                    res.Last().addr = assignAddress(proto[i], status);
                    res.Last().type = proto[i];
                    res.Last().flags = 0;
                }
                if (res.Last().addr.isInvalid())
                    throw new ParamUnassignedError("Cannot assign parameter address for " + proto[i].getName());
            }
        }

        public override void fillinMap(ParamActive active)
        {
            if (active.getNumTrials() == 0) return; // No trials to check
            if (entry.empty())
                throw new LowlevelError(
                    "Cannot derive parameter storage for prototype model without parameter entries");

            // Associate varnodes with sorted list of parameter locations
            buildTrialMap(active);

            forceExclusionGroup(active);
            List<int> trialStart = new List<int>();
            separateSections(active, trialStart);
            int numSection = trialStart.size() - 1;
            for (int i = 0; i < numSection; ++i) {
                // Definitely not used -- overrides active
                forceNoUse(active, trialStart[i], trialStart[i + 1]);
            }
            for (int i = 0; i < numSection; ++i) {
                // Chains of inactivity override later actives
                forceInactiveChain(active, 2, trialStart[i], trialStart[i + 1], resourceStart[i]);
            }

            // Mark every active trial as used
            for (int i = 0; i < active.getNumTrials(); ++i) {
                ParamTrial paramtrial = active.getTrial(i);
                if (paramtrial.isActive())
                    paramtrial.markUsed();
            }
        }

        public override bool checkJoin(Address hiaddr, int hisize, Address loaddr, int losize)
        {
            ParamEntry? entryHi = findEntry(hiaddr, hisize);
            if (entryHi == (ParamEntry)null) return false;
            ParamEntry? entryLo = findEntry(loaddr, losize);
            if (entryLo == (ParamEntry)null) return false;
            if (entryHi.getGroup() == entryLo.getGroup()) {
                if (entryHi.isExclusion() || entryLo.isExclusion()) return false;
                if (!hiaddr.isContiguous(hisize, loaddr, losize)) return false;
                if (((hiaddr.getOffset() - entryHi.getBase()) % (uint)entryHi.getAlign()) != 0) return false;
                if (((loaddr.getOffset() - entryLo.getBase()) % (uint)entryLo.getAlign()) != 0) return false;
                return true;
            }
            else {
                int sizesum = hisize + losize;
                IEnumerator<ParamEntry> iter = entry.GetEnumerator();
                while (iter.MoveNext()) {
                    if (iter.Current.getSize() < sizesum) continue;
                    if (iter.Current.justifiedContain(loaddr, losize) != 0) continue;
                    if (iter.Current.justifiedContain(hiaddr, hisize) != losize) continue;
                    return true;
                }
            }
            return false;
        }

        public override bool checkSplit(Address loc, int size, int splitpoint)
        {
            Address loc2 = loc + splitpoint;
            int size2 = size - splitpoint;
            ParamEntry? entryNum = findEntry(loc, splitpoint);
            if (entryNum == (ParamEntry)null) return false;
            entryNum = findEntry(loc2, size2);
            if (entryNum == (ParamEntry)null) return false;
            return true;
        }

        public override ParamEntry.Containment characterizeAsParam(Address loc, int size)
        {
            int index = loc.getSpace().getIndex();
            if (index >= resolverMap.Count)
                return ParamEntry.Containment.no_containment;
            ParamEntryResolver? resolver = resolverMap[index];
            if (resolver == (ParamEntryResolver)null)
                return ParamEntry.Containment.no_containment;
            Tuple<ParamEntryResolver::const_iterator, ParamEntryResolver::const_iterator> iterpair;
            iterpair = resolver.find(loc.getOffset());
            bool resContains = false;
            bool resContainedBy = false;
            while (iterpair.Item1 != iterpair.Item2) {
                ParamEntry testEntry = (iterpair.Item1).getParamEntry();
                int off = testEntry.justifiedContain(loc, size);
                if (off == 0)
                    return ParamEntry.Containment.contains_justified;
                else if (off > 0)
                    resContains = true;
                if (testEntry.isExclusion() && testEntry.containedBy(loc, size))
                    resContainedBy = true;
                ++iterpair.Item1;
            }
            if (resContains) return ParamEntry.Containment.contains_unjustified;
            if (resContainedBy) return ParamEntry.Containment.contained_by;
            if (iterpair.Item1 != resolver.end()) {
                iterpair.Item2 = resolver.find_end(loc.getOffset() + (uint)(size - 1));
                while (iterpair.Item1 != iterpair.Item2) {
                    ParamEntry testEntry = (iterpair.Item1).getParamEntry();
                    if (testEntry.isExclusion() && testEntry.containedBy(loc, size)) {
                        return ParamEntry.Containment.contained_by;
                    }
                    ++iterpair.Item1;
                }
            }
            return ParamEntry.Containment.no_containment;
        }

        public override bool possibleParam(Address loc, int size)
        {
            return ((ParamEntry)null != findEntry(loc, size));
        }

        public override bool possibleParamWithSlot(Address loc, int size, out int slot, out int slotsize)
        {
            slot = 0;
            slotsize = 0;
            ParamEntry? entryNum = findEntry(loc, size);
            if (entryNum == (ParamEntry)null) return false;
            slot = entryNum.getSlot(loc, 0);
            if (entryNum.isExclusion()) {
                slotsize = entryNum.getAllGroups().size();
            }
            else {
                slotsize = ((size - 1) / entryNum.getAlign()) + 1;
            }
            return true;
        }

        public override bool getBiggestContainedParam(Address loc, int size, VarnodeData res)
        {
            int index = loc.getSpace().getIndex();
            if (index >= resolverMap.Count)
                return false;
            ParamEntryResolver? resolver = resolverMap[index];
            if (resolver == (ParamEntryResolver)null)
                return false;
            Address endLoc = loc + (size - 1);
            if (endLoc.getOffset() < loc.getOffset())
                return false;   // Assume there is no parameter if we see wrapping
            ParamEntry maxEntry = (ParamEntry)null;
            ParamEntryResolver::const_iterator iter = resolver.find_begin(loc.getOffset());
            ParamEntryResolver::const_iterator enditer = resolver.find_end(endLoc.getOffset());
            while (iter != enditer) {
                ParamEntry testEntry = (*iter).getParamEntry();
                ++iter;
                if (testEntry.containedBy(loc, size)) {
                    if (maxEntry == (ParamEntry)null)
                        maxEntry = testEntry;
                    else if (testEntry.getSize() > maxEntry.getSize())
                        maxEntry = testEntry;
                }
            }
            if (maxEntry != (ParamEntry)null) {
                if (!maxEntry.isExclusion())
                    return false;
                res.space = maxEntry.getSpace();
                res.offset = maxEntry.getBase();
                res.size = (uint)maxEntry.getSize();
                return true;
            }
            return false;
        }

        public override bool unjustifiedContainer(Address loc, int size, VarnodeData res)
        {
            IEnumerator<ParamEntry> iter = entry.GetEnumerator();
            while (iter.MoveNext()) {
                if (iter.Current.getMinSize() > size) continue;
                int just = iter.Current.justifiedContain(loc, size);
                if (just < 0) continue;
                if (just == 0) return false;
                iter.Current.getContainer(loc, size, res);
                return true;
            }
            return false;
        }

        public override OpCode assumedExtension(Address addr, int size, VarnodeData res)
        {
            IEnumerator<ParamEntry> iter = entry.GetEnumerator();
            while (iter.MoveNext()) {
                if (iter.Current.getMinSize() > size) continue;
                OpCode ext = iter.Current.assumedExtension(addr, size, res);
                if (ext != OpCode.CPUI_COPY)
                    return ext;
            }
            return OpCode.CPUI_COPY;
        }

        public override AddrSpace getSpacebase() => spacebase;

        public override void getRangeList(AddrSpace spc, RangeList res)
        {
            IEnumerator<ParamEntry> iter = entry.GetEnumerator();
            while (iter.MoveNext()) {
                if (iter.Current.getSpace() != spc) continue;
                ulong baseoff = iter.Current.getBase();
                ulong endoff = baseoff + (uint)iter.Current.getSize() - 1;
                res.insertRange(spc, baseoff, endoff);
            }
        }

        public override int getMaxDelay() => maxdelay;

        public override void decode(Sla.CORE.Decoder decoder, List<EffectRecord> effectlist, bool normalstack)
        {
            numgroup = 0;
            spacebase = (AddrSpace)null;
            pointermax = 0;
            thisbeforeret = false;
            // True if we should split FLOAT entries into their own resource section
            bool splitFloat = true;
            bool autokilledbycall = false;
            ElementId elemId = decoder.openElement();
            for (; ; ) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_POINTERMAX) {
                    pointermax = (int)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_THISBEFORERETPOINTER) {
                    thisbeforeret = decoder.readBool();
                }
                else if (attribId == AttributeId.ATTRIB_KILLEDBYCALL) {
                    autokilledbycall = decoder.readBool();
                }
                else if (attribId == AttributeId.ATTRIB_SEPARATEFLOAT) {
                    splitFloat = decoder.readBool();
                }
            }
            for (; ; ) {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_PENTRY) {
                    parsePentry(decoder, effectlist, numgroup, normalstack, autokilledbycall, splitFloat, false);
                }
                else if (subId == ElementId.ELEM_GROUP) {
                    parseGroup(decoder, effectlist, numgroup, normalstack, autokilledbycall, splitFloat);
                }
            }
            decoder.closeElement(elemId);
            resourceStart.Add(numgroup);
            calcDelay();
            populateResolver();
        }

        public override ParamList clone()
        {
            ParamList res = new ParamListStandard(this);
            return res;
        }
    }
}
