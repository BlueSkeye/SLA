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
        public Dictionary<Address, CodeUnit> codeunit = new Dictionary<Address, CodeUnit>();
        public Dictionary<AddrLink, CodeUnit.Flags> fromto_crossref =
            new Dictionary<AddrLink, CodeUnit.Flags>();
        public Dictionary<AddrLink, CodeUnit.Flags> tofrom_crossref =
            new Dictionary<AddrLink, CodeUnit.Flags>();
        public Dictionary<Address, CodeUnit> taintlist = new Dictionary<Address, CodeUnit>();
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
            Dictionary<Address, CodeUnit>.Enumerator iter = codeunit.upper_bound(addr);
            if (iter == codeunit.begin())
                return;
            // Last before or equal
            --iter;
            CodeUnit cu = iter.Current.Value;
            if (iter.Current.Key.getOffset() + cu.size - 1 < addr.getOffset())
                return;
            if ((cu.flags & CodeUnit.Flags.notcode) != 0) {
                // Already visited
                return;
            }
            taintlist.Add(iter.Current.Key, iter.Current.Value);
        }

        public void processTaint()
        {
            Dictionary<Address, CodeUnit>.Enumerator iter = taintlist.GetLastItem();
            taintlist.RemoveLastItem();

            CodeUnit cu = iter.Current.Value;
            cu.flags |= CodeUnit.Flags.notcode;
            Address startaddr = iter.Current.Key;
            Address endaddr = startaddr + cu.size;
            if (iter != codeunit.begin()) {
                --iter;
                CodeUnit cu2 = iter.Current.Value;
                if ((cu2.flags & (CodeUnit.Flags.fallthru & CodeUnit.Flags.notcode)) == CodeUnit.Flags.fallthru)
                {
                    // not "notcode" and fallthru
                    Address addr2 = iter.Current.Key + cu.size;
                    if (addr2 == startaddr)
                        taintlist.Add(iter);
                }
            }
            Dictionary<AddrLink, uint>.Enumerator ftiter =
                fromto_crossref.lower_bound(new AddrLink(startaddr));
            Dictionary<AddrLink, uint>.Enumerator enditer =
                fromto_crossref.lower_bound(new AddrLink(endaddr));
            // Erase all cross-references coming out of this block
            fromto_crossref.erase(ftiter, enditer);

            ftiter = tofrom_crossref.lower_bound(new AddrLink(startaddr));
            enditer = tofrom_crossref.lower_bound(new AddrLink(endaddr));
            while (ftiter != enditer) {
                pushTaintAddress((*ftiter).first.b);
                Dictionary<AddrLink, uint>.Enumerator diter = ftiter;
                ++ftiter;
                tofrom_crossref.erase(diter);
            }
        }

        public Address commitCodeVec(Address addr, List<CodeUnit> codevec,
            Dictionary<AddrLink, uint> fromto_vec)
        {
            // Commit all the code units in the List, build all the crossrefs
            Address curaddr = addr;
            for (int i = 0; i < codevec.size(); ++i) {
                codeunit[curaddr] = codevec[i];
                curaddr = curaddr + codevec[i].size;
            }
            Dictionary<AddrLink, uint>.Enumerator citer = fromto_vec.GetEnumerator();
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
            Dictionary<Address, CodeUnit>.Enumerator iter = codeunit.GetEnumerator();

            while (iter.MoveNext()) {
                CodeUnit cu = iter.Current.Value;
                cu.flags &= ~(CodeUnit.Flags.hit_by_fallthru | CodeUnit.Flags.hit_by_jump | CodeUnit.Flags.hit_by_call);
            }
        }

        public void clearCrossRefs(Address addr, Address endaddr)
        {
            // Clear all crossrefs originating from [addr,endaddr)

            Dictionary<AddrLink, uint>.Enumerator startiter =
                fromto_crossref.lower_bound(new AddrLink(addr));
            Dictionary<AddrLink, uint>.Enumerator enditer =
                fromto_crossref.lower_bound(new AddrLink(endaddr));
            Dictionary<AddrLink, uint>.Enumerator iter;
            for (iter = startiter; iter != enditer; ++iter) {
                AddrLink fromto = iter.Current.Key;
                Dictionary<AddrLink, uint>.Enumerator tfiter =
                    tofrom_crossref.find(new AddrLink(fromto.b, fromto.a));
                if (tfiter != tofrom_crossref.end())
                    tofrom_crossref.erase(tfiter);
            }
            fromto_crossref.erase(startiter, enditer);
        }

        public void clearCodeUnits(Address addr, Address endaddr)
        {
            // Clear all the code units in [addr,endaddr)
            Dictionary<Address, CodeUnit>.Enumerator iter =
                codeunit.lower_bound(addr);
            Dictionary<Address, CodeUnit>.Enumerator enditer =
                codeunit.lower_bound(endaddr);
            codeunit.erase(iter, enditer);
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
            Dictionary<AddrLink, uint> fromto_vec = new Dictionary<AddrLink, uint>();
            bool flowin = false;
            bool hardend = false;

            Address curaddr = addr;
            Dictionary<Address, CodeUnit>.Enumerator iter = codeunit.lower_bound(addr);
            Address lastaddr;
            if (iter != codeunit.end()) {
                lastaddr = iter.Current.Key;
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
                    if ((!hardend) && (iter.Current.Value.flags & CodeUnit.Flags.notcode) != 0) {
                        if (iter.Current.Value.size == 1) {
                            Dictionary<Address, CodeUnit>.Enumerator iter2 = iter;
                            // We delete the bad disassembly, as it looks like it is unaligned
                            ++iter;
                            codeunit.erase(iter2);
                            if (iter != codeunit.end()) {
                                lastaddr = iter.Current.Key;
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
                    if ((iter.Current.Value.flags & CodeUnit.Flags.notcode) != 0) {
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
            int wholesize = curaddr.getOffset() - addr.getOffset();
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
            Dictionary<Address, CodeUnit>.Enumerator iter = codeunit.GetEnumerator();

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
            Dictionary<Address, CodeUnit>.Enumerator iter = codeunit.GetEnumerator();
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
            Dictionary<AddrLink, CodeUnit.Flags>.Enumerator iter =
                tofrom_crossref.GetEnumerator();

            while (iter.MoveNext()) {
                Dictionary<Address, CodeUnit>.Enumerator fiter = codeunit.find(iter.Current.Key.a);
                if (fiter == codeunit.end()) continue;
                CodeUnit.Flags fromflags = iter.Current.Value;
                CodeUnit to = fiter.Current.Value;
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
            titer = targets.find(thunkaddr);
            if (titer != targets.end())
                mask = titer.Current.Value.featuremask;
            else
                throw new CORE.LowlevelError("Found thunk without a feature mask");
            targethits.Add(new TargetHit(funcstart, codeaddr, thunkaddr, mask));
        }

        public void resolveThunkHit(Address codeaddr, ulong targethit)
        {
            // Code unit make indirect jump to target
            // Assume the address of the jump is another level of thunk
            // Look for direct calls to it and include those as TargetHits
            Dictionary<AddrLink, uint>.Enumerator iter =
                tofrom_crossref.lower_bound(new AddrLink(codeaddr));
            Address endaddr = codeaddr + 1;
            Dictionary<AddrLink, uint>.Enumerator enditer =
                tofrom_crossref.lower_bound(new AddrLink(endaddr));
            while (iter != enditer) {
                CodeUnit.Flags flags = iter.Current.Value;
                if ((flags & CodeUnit.Flags.call) != 0)
                    addTargetHit(iter.Current.Key.b, targethit);
                ++iter;
            }
        }

        public void findUnlinked()
        {
            // Find all code units that have no jump/call/fallthru to them
            Dictionary<Address, CodeUnit>.Enumerator iter = codeunit.GetEnumerator();

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

        public bool checkErrantStart(Dictionary<Address, CodeUnit>.Enumerator iter)
        {
            int count = 0;

            while (count < 1000)
            {
                CodeUnit cu = iter.Current.Value;
                if ((cu.flags & (CodeUnit.Flags.hit_by_jump | CodeUnit.Flags.hit_by_call)) != 0)
                    return false;       // Something else jumped in
                if ((cu.flags & CodeUnit.Flags.hit_by_fallthru) == 0) {
                    cu.flags |= CodeUnit.Flags.errantstart;
                    return true;
                }
                if (iter == codeunit.begin()) return false;
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
            DisassemblyResult disresult;
            List<CodeUnit> codevec = new List<CodeUnit>();
            Dictionary<AddrLink, uint> fromto_vec = new Dictionary<AddrLink, uint>();
            Address curaddr = addr;
            int count = 0;

            Dictionary<Address, CodeUnit>.Enumerator iter = codeunit.lower_bound(addr);
            if (!iter.MoveNext()) return false;
            while(true) {
                count += 1;
                if (count >= max) return false;
                while (iter.Current.Key < curaddr) {
                    ++iter;
                    if (iter == codeunit.end()) return false;
                }
                if (curaddr == iter.Current.Key)
                    // Back on cut
                    break;
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
            Dictionary<AddrLink, uint>.Enumerator iter = tofrom_crossref.begin();
            bool iterationCompleted = !iter.MoveNext();

            while (!iterationCompleted) {
                // Destination of a jump
                Address addr = iter.Current.Key.a;
                Dictionary<Address, CodeUnit>.Enumerator citer = codeunit.lower_bound(addr);
                if (citer != codeunit.end()) {
                    if (citer.Current.Key == addr) {
                        // Not off cut
                        CodeUnit cu = citer.Current.Value;
                        if ((cu.flags & (CodeUnit.Flags.hit_by_fallthru | CodeUnit.Flags.hit_by_call)) ==
                            (CodeUnit.Flags.hit_by_fallthru | CodeUnit.Flags.hit_by_call))
                        {
                            // Somebody falls through into the call
                            --citer;
                            checkErrantStart(citer);
                        }
                        iterationCompleted = !iter.MoveNext();
                        continue;
                    }
                }
                if (citer == codeunit.begin()) {
                    iterationCompleted = !iter.MoveNext();
                    continue;
                }
                // Last lessthan or equal
                --citer;
                if (citer.Current.Key == addr) {
                    iterationCompleted = !iter.MoveNext();
                    continue; // on cut
                }
                Address endaddr = citer.Current.Key + citer.Current.Value.size;
                if (endaddr <= addr) {
                    iterationCompleted = !iter.MoveNext();
                    continue;
                }
                if (!checkErrantStart(citer)) {
                    iterationCompleted = !iter.MoveNext();
                    continue;
                }
                AddrLink addrlink = iter.Current.Key;
                // This may delete tofrom_crossref nodes
                repairJump(addr, 10);
                iter = tofrom_crossref.upper_bound(addrlink);
            }
        }

        public Address findFunctionStart(Address addr)
        {
            // Find the starting address of a function containing the address addr
            Dictionary<AddrLink, uint>.Enumerator iter;

            iter = tofrom_crossref.lower_bound(AddrLink(addr));
            while (iter != tofrom_crossref.begin()) {
                --iter;
                if ((iter.Current.Value & CodeUnit.Flags.call) != 0)
                    return iter.Current.Key.a;
            }
            return new Address();       // Return invalid address
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
            Dictionary<AddrLink, CodeUnit.Flags>.Enumerator iter =
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
            Dictionary<AddrLink, CodeUnit.Flags>.Enumerator iter =
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
                string nm = targets.find(target.thunkaddr).second.name;
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
            LoadImageSection secinfo;
            bool moresections;
            loadimage.openSectionInfo();
            Address lastaddr;
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
