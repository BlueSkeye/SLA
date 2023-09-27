using Sla;
using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class CodeDataAnalysis : IfaceData
    {
        // Alignment of instructions
        public int alignment;
        public Architecture glb;
        public DisassemblyEngine disengine;
        public RangeList modelhits;
        public SortedList<Address, CodeUnit> codeunit = new SortedList<Address, CodeUnit>();
        public SortedList<AddrLink, CodeUnit.Flags> fromto_crossref =
            new SortedList<AddrLink, CodeUnit.Flags>();
        public SortedList<AddrLink, CodeUnit.Flags> tofrom_crossref =
            new SortedList<AddrLink, CodeUnit.Flags>();
        public SortedList<Address, CodeUnit> taintlist = new SortedList<Address, CodeUnit>();
        public List<Address> unlinkedstarts = new List<Address>();
        public List<TargetHit> targethits = new List<TargetHit>();
        public Dictionary<Address, TargetFeature> targets =
            new Dictionary<Address, TargetFeature>();
        
        ~CodeDataAnalysis()
        {
        }

        public void init(Architecture g)
        {
            glb = g;
            disengine.init(glb.translate);
            alignment = glb.translate.getAlignment();
            modelhits.clear();
            codeunit.Clear();
            fromto_crossref.Clear();
            tofrom_crossref.Clear();
            taintlist.Clear();
            unlinkedstarts.Clear();
            targethits.Clear();
            targets.Clear();
        }

        public void pushTaintAddress(Address addr)
        {
            // First after
            int /*Dictionary<Address, CodeUnit>.Enumerator*/ iter = codeunit.upper_bound(addr);
            if (iter == 0 /*codeunit.begin()*/) {
                return;
            }
            // Last before or equal
            KeyValuePair<Address, CodeUnit> scannedPair = codeunit.ElementAt(--iter);
            CodeUnit cu = scannedPair.Value;
            if (scannedPair.Key.getOffset() + (uint)cu.size - 1 < addr.getOffset()) {
                return;
            }
            if ((cu.flags & CodeUnit.Flags.notcode) != 0) {
                // Already visited
                return;
            }
            taintlist.Add(scannedPair.Key, scannedPair.Value);
        }

        public void processTaint()
        {
            KeyValuePair<Address, CodeUnit> iter = taintlist.Last();
            taintlist.Remove(iter.Key);

            CodeUnit cu = iter.Value;
            cu.flags |= CodeUnit.Flags.notcode;
            Address startaddr = iter.Key;
            Address endaddr = startaddr + cu.size;
            if (0 != taintlist.Count) {
                iter = taintlist.Last();
                CodeUnit cu2 = iter.Value;
                if ((cu2.flags & (CodeUnit.Flags.fallthru & CodeUnit.Flags.notcode)) == CodeUnit.Flags.fallthru)
                {
                    // not "notcode" and fallthru
                    Address addr2 = iter.Key + cu.size;
                    if (addr2 == startaddr) {
                        taintlist.Add(iter.Key, iter.Value);
                    }
                }
            }
            //int /*Dictionary<AddrLink, uint>.Enumerator*/ ftiter =
            //    fromto_crossref.lower_bound(new AddrLink(startaddr));
            //int /*Dictionary<AddrLink, uint>.Enumerator*/ enditer =
            //    fromto_crossref.lower_bound(new AddrLink(endaddr));
            // Erase all cross-references coming out of this block
            fromto_crossref.RemoveRange(new AddrLink(startaddr), new AddrLink(endaddr));

            int copiedIndex = tofrom_crossref.lower_bound(new AddrLink(startaddr));
            int firstNonCopiedIndex = tofrom_crossref.lower_bound(new AddrLink(endaddr));
            while (copiedIndex < firstNonCopiedIndex) {
                AddrLink taintedLink = tofrom_crossref.ElementAt(copiedIndex).Key;
                pushTaintAddress(taintedLink.b);
                ++copiedIndex;
                tofrom_crossref.Remove(taintedLink);
            }
        }

        public Address commitCodeVec(Address addr, List<CodeUnit> codevec,
            Dictionary<AddrLink, CodeUnit.Flags> fromto_vec)
        {
            // Commit all the code units in the List, build all the crossrefs
            Address curaddr = addr;
            for (int i = 0; i < codevec.size(); ++i) {
                codeunit[curaddr] = codevec[i];
                curaddr = curaddr + codevec[i].size;
            }
            Dictionary<AddrLink, CodeUnit.Flags>.Enumerator citer = fromto_vec.GetEnumerator();
            while (citer.MoveNext()) {
                AddrLink fromto = citer.Current.Key;
                fromto_crossref[fromto] = citer.Current.Value;
                AddrLink tofrom = new AddrLink(fromto.b, fromto.a );
                tofrom_crossref[tofrom] = citer.Current.Value;
            }
            return curaddr;
        }

        public void clearHitBy()
        {
            // Clear all the "hit_by" flags from all code units
            IEnumerator<KeyValuePair<Address, CodeUnit>> iter = codeunit.GetEnumerator();

            while (iter.MoveNext()) {
                CodeUnit cu = iter.Current.Value;
                cu.flags &= ~(CodeUnit.Flags.hit_by_fallthru | CodeUnit.Flags.hit_by_jump | CodeUnit.Flags.hit_by_call);
            }
        }

        public void clearCrossRefs(Address addr, Address endaddr)
        {
            // Clear all crossrefs originating from [addr,endaddr)
            int /*Dictionary<AddrLink, uint>.Enumerator*/ startiter =
                fromto_crossref.lower_bound(new AddrLink(addr));
            int /*Dictionary<AddrLink, uint>.Enumerator*/ enditer =
                fromto_crossref.lower_bound(new AddrLink(endaddr));
            for (int iter = startiter; iter < enditer; ++iter) {
                AddrLink fromto = fromto_crossref.ElementAt(iter).Key;
                AddrLink searchedLink = new AddrLink(fromto.b, fromto.a);
                if (tofrom_crossref.ContainsKey(searchedLink)) {
                    tofrom_crossref.Remove(searchedLink);
                }
            }
            fromto_crossref.RemoveRange(startiter, enditer);
        }

        public void clearCodeUnits(Address addr, Address endaddr)
        {
            // Clear all the code units in [addr,endaddr)
            int /*Dictionary<Address, CodeUnit>.Enumerator*/ iter = codeunit.lower_bound(addr);
            int /*Dictionary<Address, CodeUnit>.Enumerator*/ enditer = codeunit.lower_bound(endaddr);
            codeunit.RemoveRange(iter, enditer);
            clearCrossRefs(addr, endaddr);
        }

        public void addTarget(string nm, Address addr,uint mask)
        {
            // Add a target thunk to be searched for
            TargetFeature targfeat = targets[addr];
            targfeat.name = nm;
            targfeat.featuremask = mask;
            // Tell the disassembler to search for address
            disengine.addTarget(addr);
        }

        public int getNumTargets() => targets.Count;

        public Address disassembleBlock(Address addr, Address endaddr)
        {
            List<CodeUnit> codevec = new List<CodeUnit>();
            Dictionary<AddrLink, CodeUnit.Flags> fromto_vec = new Dictionary<AddrLink, CodeUnit.Flags>();
            bool flowin = false;
            bool hardend = false;

            Address curaddr = addr;
            int /*Dictionary<Address, CodeUnit>.Enumerator*/ iter = codeunit.lower_bound(addr);
            Address lastaddr;
            if (iter < codeunit.Count) {
                lastaddr = codeunit.ElementAt(iter).Key;
                if (endaddr < lastaddr) {
                    lastaddr = endaddr;
                    hardend = true;
                }
            }
            else {
                lastaddr = endaddr;
                hardend = true;
            }
            DisassemblyResult disresult = new DisassemblyResult();
            while (true) {
                disengine.disassemble(curaddr, disresult);
                CodeUnit addedUnit = new CodeUnit();
                codevec.Add(addedUnit);
                if (!disresult.success) {
                    addedUnit.flags = CodeUnit.Flags.notcode;
                    addedUnit.size = 1;
                    curaddr = curaddr + 1;
                    break;
                }
                if ((disresult.flags & CodeUnit.Flags.jump) != 0) {
                    fromto_vec[new AddrLink(curaddr, disresult.jumpaddress)] = disresult.flags;
                }
                addedUnit.flags = disresult.flags;
                addedUnit.size = disresult.length;
                curaddr = curaddr + disresult.length;
                while (lastaddr < curaddr) {
                    KeyValuePair<Address, CodeUnit> scannedUnit = codeunit.ElementAt(iter);
                    if (!hardend && (scannedUnit.Value.flags & CodeUnit.Flags.notcode) != 0) {
                        if (scannedUnit.Value.size == 1) {
                            int /*Dictionary<Address, CodeUnit>.Enumerator*/ iter2 = iter;
                            // We delete the bad disassembly, as it looks like it is unaligned
                            ++iter;
                            codeunit.RemoveAt(iter2);
                            if (iter != codeunit.Count) {
                                scannedUnit = codeunit.ElementAt(iter);
                                lastaddr = scannedUnit.Key;
                                if (endaddr < lastaddr) {
                                    lastaddr = endaddr;
                                    hardend = true;
                                }
                            }
                            else {
                                lastaddr = endaddr;
                                hardend = true;
                            }
                        }
                        else {
                            disresult.success = false;
                            flowin = true;
                            break;
                        }
                    }
                    else {
                        disresult.success = false;
                        break;
                    }
                }
                if (!disresult.success)
                    break;
                if (curaddr == lastaddr) {
                    if ((codeunit.ElementAt(iter).Value.flags & CodeUnit.Flags.notcode) != 0) {
                        flowin = true;
                        break;
                    }
                }
                if (((disresult.flags & CodeUnit.Flags.fallthru) == 0) || (curaddr == lastaddr)) {
                    // found the end of a block
                    return commitCodeVec(addr, codevec, fromto_vec);
                }
            }
            // If we reach here, we have bad disassembly of some sort
            CodeUnit cu = codeunit[addr];
            cu.flags = CodeUnit.Flags.notcode;
            if (hardend && (lastaddr < curaddr))
                curaddr = lastaddr;
            int wholesize = (int)(curaddr.getOffset() - addr.getOffset());
            if ((!flowin) && (wholesize < 10)) {
                wholesize = 1;
            }
            cu.size = wholesize;
            curaddr = addr + cu.size;
            return curaddr;
        }

        public void disassembleRange(Sla.CORE.Range range)
        {
            Address addr = range.getFirstAddr();
            Address lastaddr = range.getLastAddr();
            while (addr <= lastaddr) {
                addr = disassembleBlock(addr, lastaddr);
            }
        }

        public void disassembleRangeList(RangeList rangelist)
        {
            IEnumerator<Sla.CORE.Range> iter = rangelist.begin();

            while (iter.MoveNext()) {
                disassembleRange(iter.Current);
            }
        }

        public void findNotCodeUnits()
        {
            // Mark any code units that have flow into "notcode" units as "notcode"
            // Remove any references to or from these units
            IEnumerator<KeyValuePair<Address, CodeUnit>> iter = codeunit.GetEnumerator();

            // We spread the "notcode" attribute as a taint
            // We build the initial work list with known "notcode"
            while (iter.MoveNext()) {
                if ((iter.Current.Value.flags & CodeUnit.Flags.notcode) != 0)
                    taintlist.Add(iter.Current.Key, iter.Current.Value);
            }
            while (!taintlist.empty())
                // Propagate taint along fallthru and crossref edges
                processTaint();
        }

        public void markFallthruHits()
        {
            // Mark every code unit that has another code unit fall into it

            Address fallthruaddr = new Address((AddrSpace)null, 0);
            IEnumerator<KeyValuePair<Address, CodeUnit>> iter = codeunit.GetEnumerator();
            while (iter.MoveNext()) {
                CodeUnit cu = iter.Current.Value;
                if ((cu.flags & CodeUnit.Flags.notcode) != 0) continue;
                if (fallthruaddr == iter.Current.Key)
                    cu.flags |= CodeUnit.Flags.hit_by_fallthru;
                if ((cu.flags & CodeUnit.Flags.fallthru) != 0)
                    fallthruaddr = iter.Current.Key + cu.size;
            }
        }

        public void markCrossHits()
        {
            // Mark every codeunit hit by a call or jump
            IEnumerator<KeyValuePair<AddrLink, CodeUnit.Flags>> iter =
                tofrom_crossref.GetEnumerator();

            while (iter.MoveNext()) {
                CodeUnit? to;
                if (!codeunit.TryGetValue(iter.Current.Key.a, out to))
                    continue;
                CodeUnit.Flags fromflags = iter.Current.Value;
                if ((fromflags & CodeUnit.Flags.call) != 0)
                    to.flags |= CodeUnit.Flags.hit_by_call;
                else if ((fromflags & CodeUnit.Flags.jump) != 0)
                    to.flags |= CodeUnit.Flags.hit_by_jump;
            }
        }

        public void addTargetHit(Address codeaddr, ulong targethit)
        {
            Address funcstart = findFunctionStart(codeaddr);
            Address thunkaddr = new Address(glb.translate.getDefaultCodeSpace(), targethit);
            uint mask;
            Dictionary<Address, TargetFeature>.Enumerator titer;
            TargetFeature feature;
            if (!targets.TryGetValue(thunkaddr, out feature))
                throw new CORE.LowlevelError("Found thunk without a feature mask");
            mask = feature.featuremask;
            targethits.Add(new TargetHit(funcstart, codeaddr, thunkaddr, mask));
        }

        public void resolveThunkHit(Address codeaddr, ulong targethit)
        {
            // Code unit make indirect jump to target
            // Assume the address of the jump is another level of thunk
            // Look for direct calls to it and include those as TargetHits
            int /*Dictionary<AddrLink, CodeUnit.Flags>.Enumerator*/ iter =
                tofrom_crossref.lower_bound(new AddrLink(codeaddr));
            Address endaddr = codeaddr + 1;
            int /*Dictionary<AddrLink, CodeUnit.Flags>.Enumerator*/ enditer =
                tofrom_crossref.lower_bound(new AddrLink(endaddr));
            while (iter < enditer) {
                KeyValuePair<AddrLink, CodeUnit.Flags> scannedElement = tofrom_crossref.ElementAt(iter);
                CodeUnit.Flags flags = scannedElement.Value;
                if ((flags & CodeUnit.Flags.call) != 0) {
                    addTargetHit(scannedElement.Key.b, targethit);
                }
                ++iter;
            }
        }

        public void findUnlinked()
        {
            // Find all code units that have no jump/call/fallthru to them
            IEnumerator<KeyValuePair<Address, CodeUnit>> iter = codeunit.GetEnumerator();

            while (iter.MoveNext()) {
                CodeUnit cu = iter.Current.Value;
                if ((cu.flags & (CodeUnit.Flags.hit_by_fallthru | CodeUnit.Flags.hit_by_jump |
                        CodeUnit.Flags.hit_by_call | CodeUnit.Flags.notcode |
                        CodeUnit.Flags.errantstart)) == 0)
                    unlinkedstarts.Add(iter.Current.Key);
                if ((cu.flags & (CodeUnit.Flags.targethit | CodeUnit.Flags.notcode)) == CodeUnit.Flags.targethit) {
                    Address codeaddr = iter.Current.Key;
                    DisassemblyResult res = new DisassemblyResult();
                    disengine.disassemble(codeaddr, res);
                    if ((cu.flags & CodeUnit.Flags.thunkhit) != 0)
                        resolveThunkHit(codeaddr, res.targethit);
                    else
                        addTargetHit(codeaddr, res.targethit);
                }
            }
        }

        public bool checkErrantStart(int /*Dictionary<Address, CodeUnit>.Enumerator*/ iter)
        {
            int count = 0;

            while (count < 1000) {
                CodeUnit cu = codeunit.ElementAt(iter).Value;
                if ((cu.flags & (CodeUnit.Flags.hit_by_jump | CodeUnit.Flags.hit_by_call)) != 0) {
                    // Something else jumped in
                    return false;
                }
                if ((cu.flags & CodeUnit.Flags.hit_by_fallthru) == 0) {
                    cu.flags |= CodeUnit.Flags.errantstart;
                    return true;
                }
                if (iter == 0) {
                    return false;
                }
                --iter;
                count += 1;
            }
            return false;
        }

        public bool repairJump(Address addr, int max)
        {
            // Assume -addr- is a correct instruction start. Try to repair
            // disassembly for up to -max- instructions following it,
            // trying to get back on cut
            DisassemblyResult disresult = new DisassemblyResult();
            List<CodeUnit> codevec = new List<CodeUnit>();
            Dictionary<AddrLink, CodeUnit.Flags> fromto_vec = new Dictionary<AddrLink, CodeUnit.Flags>();
            Address curaddr = addr;
            int count = 0;

            int /*Dictionary<Address, CodeUnit>.Enumerator*/ iter = codeunit.lower_bound(addr);
            while(true) {
                count += 1;
                if (count >= max) {
                    return false;
                }
                while (codeunit.ElementAt(iter).Key < curaddr) {
                    ++iter;
                    if (iter == codeunit.Count) {
                        return false;
                    }
                }
                if (curaddr == codeunit.ElementAt(iter).Key) {
                    // Back on cut
                    break;
                }
                disengine.disassemble(curaddr, disresult);
                if (!disresult.success) return false;
                CodeUnit addedUnit = new CodeUnit();
                codevec.Add(addedUnit);
                if ((disresult.flags & CodeUnit.Flags.jump) != 0) {
                    fromto_vec[new AddrLink(curaddr, disresult.jumpaddress)] = disresult.flags;
                }
                addedUnit.flags = disresult.flags;
                addedUnit.size = disresult.length;
                curaddr = curaddr + disresult.length;
            }
            clearCodeUnits(addr, curaddr);
            commitCodeVec(addr, codevec, fromto_vec);
            return true;
        }

        public void findOffCut()
        {
            int /*Dictionary<AddrLink, CodeUnit.Flags>.Enumerator*/ iter = 0;
            bool iterationCompleted = (tofrom_crossref.Count <= iter);

            while (!iterationCompleted) {
                // Destination of a jump
                Address addr = tofrom_crossref.ElementAt(iter).Key.a;
                int /*Dictionary<Address, CodeUnit>.Enumerator*/ citer = codeunit.lower_bound(addr);
                if (citer < codeunit.Count) {
                    if (codeunit.ElementAt(citer).Key == addr) {
                        // Not off cut
                        CodeUnit cu = codeunit.ElementAt(citer).Value;
                        if ((cu.flags & (CodeUnit.Flags.hit_by_fallthru | CodeUnit.Flags.hit_by_call)) ==
                            (CodeUnit.Flags.hit_by_fallthru | CodeUnit.Flags.hit_by_call))
                        {
                            // Somebody falls through into the call
                            --citer;
                            checkErrantStart(citer);
                        }
                        iterationCompleted = (tofrom_crossref.Count <= ++iter);
                        continue;
                    }
                }
                if (citer == 0 /*codeunit.begin()*/) {
                    iterationCompleted = (tofrom_crossref.Count <= ++iter);
                    continue;
                }
                // Last lessthan or equal
                --citer;
                if (codeunit.ElementAt(citer).Key == addr) {
                    iterationCompleted = (tofrom_crossref.Count <= ++iter);
                    // on cut
                    continue;
                }
                KeyValuePair<Address, CodeUnit> scannedElement = codeunit.ElementAt(citer);
                Address endaddr = scannedElement.Key + scannedElement.Value.size;
                if (endaddr <= addr) {
                    iterationCompleted = (tofrom_crossref.Count <= ++iter);
                    continue;
                }
                if (!checkErrantStart(citer)) {
                    iterationCompleted = (tofrom_crossref.Count <= ++iter);
                    continue;
                }
                AddrLink addrlink = tofrom_crossref.ElementAt(iter).Key;
                // This may delete tofrom_crossref nodes
                repairJump(addr, 10);
                iter = tofrom_crossref.upper_bound(addrlink);
            }
        }

        public Address findFunctionStart(Address addr)
        {
            // Find the starting address of a function containing the address addr
            int /*Dictionary<AddrLink, CodeUnit.Flags>.Enumerator*/ iter =
                tofrom_crossref.lower_bound(new AddrLink(addr));
            
            while (iter > 0) {
                KeyValuePair<AddrLink, CodeUnit.Flags> scannedElement = tofrom_crossref.ElementAt(--iter);
                if ((scannedElement.Value & CodeUnit.Flags.call) != 0) {
                    return scannedElement.Key.a;
                }
            }
            // Return invalid address
            return new Address();
        }

        public List<TargetHit> getTargetHits() => targethits;

        public void dumpModelHits(TextWriter s)
        {
            IEnumerator<Sla.CORE.Range> iter = modelhits.begin();
            bool completed = !iter.MoveNext();
            while (!completed) {
                ulong off = iter.Current.getFirst();
                s.Write($"0x{off:X} ");
                ulong endoff = iter.Current.getLast();
                s.Write($"0x{endoff:X}");
                completed = !iter.MoveNext();
                if (!completed) {
                    off = iter.Current.getFirst();
                    s.Write($" {(int)(off - endoff)}");
                }
                s.WriteLine();
            }
        }

        public void dumpCrossRefs(TextWriter s)
        {
            IEnumerator<KeyValuePair<AddrLink, CodeUnit.Flags>> iter =
                fromto_crossref.GetEnumerator();

            while (iter.MoveNext()) {
                AddrLink addrlink = iter.Current.Key;
                CodeUnit.Flags flags = iter.Current.Value;

                s.Write($"0x{addrlink.a.getOffset():X} . 0x{addrlink.b.getOffset():X}");
                if ((flags & CodeUnit.Flags.call) != 0)
                    s.Write(" call");
                s.WriteLine();
            }
        }

        public void dumpFunctionStarts(TextWriter s)
        {
            IEnumerator<KeyValuePair<AddrLink, CodeUnit.Flags>> iter =
                tofrom_crossref.GetEnumerator();

            while (iter.MoveNext()) {
                AddrLink addrlink = iter.Current.Key;
                CodeUnit.Flags flags = iter.Current.Value;

                if ((flags & CodeUnit.Flags.call) != 0)
                    s.WriteLine($"0x{addrlink.a.getOffset():X}");
            }
        }

        public void dumpUnlinked(TextWriter s)
        {
            IEnumerator<Address> iter = unlinkedstarts.GetEnumerator();

            while (iter.MoveNext()) {
                s.WriteLine($"0x{iter.Current.getOffset():X}");
            }
        }

        public void dumpTargetHits(TextWriter s)
        {
            // Dump every code unit that refers to a target
            foreach (TargetHit target in targethits) {
                Address funcaddr = target.funcstart;
                Address addr = target.codeaddr;
                TargetFeature feature;
                if (!targets.TryGetValue(target.thunkaddr, out feature))
                    throw new ApplicationException();
                string nm = feature.name;
                if (!funcaddr.isInvalid())
                    s.Write($"{funcaddr.getOffset():X} ");
                else
                    s.Write("nostart ");
                s.WriteLine($"{addr.getOffset():X} {nm}");
            }
        }

        public void runModel()
        {
            LoadImage loadimage = glb.loader ?? throw new ApplicationException();
            LoadImageSection secinfo = new LoadImageSection();
            bool moresections;
            loadimage.openSectionInfo();
            Address lastaddr = new Address();
            do {
                moresections = loadimage.getNextSection(secinfo);
                Address endaddr = secinfo.address + (int)secinfo.size;
                if (secinfo.size == 0) continue;
                if (lastaddr.isInvalid())
                    lastaddr = endaddr;
                else if (lastaddr < endaddr)
                    lastaddr = endaddr;

                if ((secinfo.flags & (LoadImageSection.Properties.unalloc | LoadImageSection.Properties.noload)) == 0) {
                    modelhits.insertRange(secinfo.address.getSpace(),
                              secinfo.address.getOffset(), endaddr.getOffset());
                }
            } while (moresections);
            loadimage.closeSectionInfo();
            CodeUnit cu = codeunit[lastaddr];
            cu.size = 100;
            cu.flags = CodeUnit.Flags.notcode;
            disassembleRangeList(modelhits);
            findNotCodeUnits();
            markFallthruHits();
            markCrossHits();
            findOffCut();
            clearHitBy();
            markFallthruHits();
            markCrossHits();
            findUnlinked();
            // Sort the list of hits by function containing hit
            targethits.Sort();
        }
    }
}
