using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class CodeDataAnalysis : IfaceData
    {
        public int alignment;       // Alignment of instructions
        public Architecture glb;
        public DisassemblyEngine disengine;
        public RangeList modelhits;
        public Dictionary<Address, CodeUnit> codeunit;
        public Dictionary<AddrLink, uint> fromto_crossref;
        public Dictionary<AddrLink, uint> tofrom_crossref;
        public List<Dictionary<Address, CodeUnit>::iterator> taintlist;
        public List<Address> unlinkedstarts;
        public List<TargetHit> targethits;
        public Dictionary<Address, TargetFeature> targets;
        
        ~CodeDataAnalysis()
        {
        }

        public void init(Architecture g)
        {
            glb = g;
            disengine.init(glb.translate);
            alignment = glb.translate.getAlignment();
            modelhits.clear();
            codeunit.clear();
            fromto_crossref.clear();
            tofrom_crossref.clear();
            taintlist.clear();
            unlinkedstarts.clear();
            targethits.clear();
            targets.clear();
        }

        public void pushTaintAddress(Address addr)
        {
            map<Address, CodeUnit>::iterator iter;

            iter = codeunit.upper_bound(addr); // First after
            if (iter == codeunit.begin()) return;
            --iter;         // Last before or equal
            CodeUnit & cu((*iter).second);
            if ((*iter).first.getOffset() + cu.size - 1 < addr.getOffset()) return;
            if ((cu.flags & CodeUnit::notcode) != 0) return; // Already visited
            taintlist.push_back(iter);
        }

        public void processTaint()
        {
            map<Address, CodeUnit>::iterator iter = taintlist.back();
            taintlist.pop_back();

            CodeUnit & cu((*iter).second);
            cu.flags |= CodeUnit::notcode;
            Address startaddr = (*iter).first;
            Address endaddr = startaddr + cu.size;
            if (iter != codeunit.begin())
            {
                --iter;
                CodeUnit & cu2((*iter).second);
                if ((cu2.flags & (CodeUnit::fallthru & CodeUnit::notcode)) == CodeUnit::fallthru)
                { // not "notcode" and fallthru
                    Address addr2 = (*iter).first + cu.size;
                    if (addr2 == startaddr)
                        taintlist.push_back(iter);
                }
            }
            map<AddrLink, uint>::iterator ftiter, diter, enditer;
            ftiter = fromto_crossref.lower_bound(AddrLink(startaddr));
            enditer = fromto_crossref.lower_bound(AddrLink(endaddr));
            fromto_crossref.erase(ftiter, enditer); // Erase all cross-references coming out of this block

            ftiter = tofrom_crossref.lower_bound(AddrLink(startaddr));
            enditer = tofrom_crossref.lower_bound(AddrLink(endaddr));
            while (ftiter != enditer)
            {
                pushTaintAddress((*ftiter).first.b);
                diter = ftiter;
                ++ftiter;
                tofrom_crossref.erase(diter);
            }
        }

        public Address commitCodeVec(Address addr, List<CodeUnit> codevec,
            Dictionary<AddrLink, uint> fromto_vec)
        { // Commit all the code units in the List, build all the crossrefs
            Address curaddr = addr;
            for (int i = 0; i < codevec.size(); ++i)
            {
                codeunit[curaddr] = codevec[i];
                curaddr = curaddr + codevec[i].size;
            }
            map<AddrLink, uint>::iterator citer;
            for (citer = fromto_vec.begin(); citer != fromto_vec.end(); ++citer)
            {
                AddrLink fromto = (*citer).first;
                fromto_crossref[fromto] = (*citer).second;
                AddrLink tofrom(fromto.b, fromto.a );
                tofrom_crossref[tofrom] = (*citer).second;
            }
            return curaddr;
        }

        public void clearHitBy()
        { // Clear all the "hit_by" flags from all code units
            map<Address, CodeUnit>::iterator iter;

            for (iter = codeunit.begin(); iter != codeunit.end(); ++iter)
            {
                CodeUnit & cu((*iter).second);
                cu.flags &= ~(CodeUnit::hit_by_fallthru | CodeUnit::hit_by_jump | CodeUnit::hit_by_call);
            }
        }

        public void clearCrossRefs(Address addr, Address endaddr)
        { // Clear all crossrefs originating from [addr,endaddr)
            map<AddrLink, uint>::iterator startiter, iter, enditer, tfiter;

            startiter = fromto_crossref.lower_bound(AddrLink(addr));
            enditer = fromto_crossref.lower_bound(AddrLink(endaddr));
            for (iter = startiter; iter != enditer; ++iter)
            {
                AddrLink fromto = (*iter).first;
                tfiter = tofrom_crossref.find(AddrLink(fromto.b, fromto.a));
                if (tfiter != tofrom_crossref.end())
                    tofrom_crossref.erase(tfiter);
            }
            fromto_crossref.erase(startiter, enditer);
        }

        public void clearCodeUnits(Address addr, Address endaddr)
        { // Clear all the code units in [addr,endaddr)
            map<Address, CodeUnit>::iterator iter, enditer;

            iter = codeunit.lower_bound(addr);
            enditer = codeunit.lower_bound(endaddr);
            codeunit.erase(iter, enditer);
            clearCrossRefs(addr, endaddr);
        }

        public void addTarget(string nm, Address addr,uint mask)
        { // Add a target thunk to be searched for
            TargetFeature & targfeat(targets[addr]);
            targfeat.name = nm;
            targfeat.featuremask = mask;
            disengine.addTarget(addr);  // Tell the disassembler to search for address
        }

        public int getNumTargets() => targets.size();

        public Address disassembleBlock(Address addr, Address endaddr)
        {
            DisassemblyResult disresult;
            List<CodeUnit> codevec;
            map<AddrLink, uint> fromto_vec;
            bool flowin = false;
            bool hardend = false;

            Address curaddr = addr;
            map<Address, CodeUnit>::iterator iter;
            iter = codeunit.lower_bound(addr);
            Address lastaddr;
            if (iter != codeunit.end())
            {
                lastaddr = (*iter).first;
                if (endaddr < lastaddr)
                {
                    lastaddr = endaddr;
                    hardend = true;
                }
            }
            else
            {
                lastaddr = endaddr;
                hardend = true;
            }
            for (; ; )
            {
                disengine.disassemble(curaddr, disresult);
                codevec.emplace_back();
                if (!disresult.success)
                {
                    codevec.back().flags = CodeUnit::notcode;
                    codevec.back().size = 1;
                    curaddr = curaddr + 1;
                    break;
                }
                if ((disresult.flags & CodeUnit::jump) != 0)
                {
                    fromto_vec[AddrLink(curaddr, disresult.jumpaddress)] = disresult.flags;
                }
                codevec.back().flags = disresult.flags;
                codevec.back().size = disresult.length;
                curaddr = curaddr + disresult.length;
                while (lastaddr < curaddr)
                {
                    if ((!hardend) && ((*iter).second.flags & CodeUnit::notcode) != 0)
                    {
                        if ((*iter).second.size == 1)
                        {
                            map<Address, CodeUnit>::iterator iter2 = iter;
                            ++iter;     // We delete the bad disassembly, as it looks like it is unaligned
                            codeunit.erase(iter2);
                            if (iter != codeunit.end())
                            {
                                lastaddr = (*iter).first;
                                if (endaddr < lastaddr)
                                {
                                    lastaddr = endaddr;
                                    hardend = true;
                                }
                            }
                            else
                            {
                                lastaddr = endaddr;
                                hardend = true;
                            }
                        }
                        else
                        {
                            disresult.success = false;
                            flowin = true;
                            break;
                        }
                    }
                    else
                    {
                        disresult.success = false;
                        break;
                    }
                }
                if (!disresult.success)
                    break;
                if (curaddr == lastaddr)
                {
                    if (((*iter).second.flags & CodeUnit::notcode) != 0)
                    {
                        flowin = true;
                        break;
                    }
                }
                if (((disresult.flags & CodeUnit::fallthru) == 0) || (curaddr == lastaddr))
                {  // found the end of a block
                    return commitCodeVec(addr, codevec, fromto_vec);
                }
            }
            // If we reach here, we have bad disassembly of some sort
            CodeUnit & cu(codeunit[addr]);
            cu.flags = CodeUnit::notcode;
            if (hardend && (lastaddr < curaddr))
                curaddr = lastaddr;
            int wholesize = curaddr.getOffset() - addr.getOffset();
            if ((!flowin) && (wholesize < 10))
            {
                wholesize = 1;
            }
            cu.size = wholesize;
            curaddr = addr + cu.size;
            return curaddr;
        }

        public void disassembleRange(Range range)
        {
            Address addr = range.getFirstAddr();
            Address lastaddr = range.getLastAddr();
            while (addr <= lastaddr)
            {
                addr = disassembleBlock(addr, lastaddr);
            }
        }

        public void disassembleRangeList(RangeList rangelist)
        {
            set<Range>::const_iterator iter, enditer;
            iter = rangelist.begin();
            enditer = rangelist.end();

            while (iter != enditer)
            {
                disassembleRange(*iter);
                ++iter;
            }
        }

        public void findNotCodeUnits()
        { // Mark any code units that have flow into "notcode" units as "notcode"
          // Remove any references to or from these units
            map<Address, CodeUnit>::iterator iter;

            // We spread the "notcode" attribute as a taint
            // We build the initial work list with known "notcode"
            for (iter = codeunit.begin(); iter != codeunit.end(); ++iter)
            {
                if (((*iter).second.flags & CodeUnit::notcode) != 0)
                    taintlist.push_back(iter);
            }

            while (!taintlist.empty())  // Propagate taint along fallthru and crossref edges
                processTaint();
        }

        public void markFallthruHits()
        { // Mark every code unit that has another code unit fall into it
            map<Address, CodeUnit>::iterator iter;

            Address fallthruaddr((AddrSpace*)0,0);
            iter = codeunit.begin();
            for (iter = codeunit.begin(); iter != codeunit.end(); ++iter)
            {
                CodeUnit & cu((*iter).second);
                if ((cu.flags & CodeUnit::notcode) != 0) continue;
                if (fallthruaddr == (*iter).first)
                    cu.flags |= CodeUnit::hit_by_fallthru;
                if ((cu.flags & CodeUnit::fallthru) != 0)
                    fallthruaddr = (*iter).first + cu.size;
            }
        }

        public void markCrossHits()
        { // Mark every codeunit hit by a call or jump
            map<AddrLink, uint>::iterator iter;
            map<Address, CodeUnit>::iterator fiter;

            for (iter = tofrom_crossref.begin(); iter != tofrom_crossref.end(); ++iter)
            {
                fiter = codeunit.find((*iter).first.a);
                if (fiter == codeunit.end()) continue;
                uint fromflags = (*iter).second;
                CodeUnit & to((*fiter).second);
                if ((fromflags & CodeUnit::call) != 0)
                    to.flags |= CodeUnit::hit_by_call;
                else if ((fromflags & CodeUnit::jump) != 0)
                    to.flags |= CodeUnit::hit_by_jump;
            }
        }

        public void addTargetHit(Address codeaddr, ulong targethit)
        {
            Address funcstart = findFunctionStart(codeaddr);
            Address thunkaddr = Address(glb.translate.getDefaultCodeSpace(), targethit);
            uint mask;
            map<Address, TargetFeature>::const_iterator titer;
            titer = targets.find(thunkaddr);
            if (titer != targets.end())
                mask = (*titer).second.featuremask;
            else
                throw new LowlevelError("Found thunk without a feature mask");
            targethits.emplace_back(funcstart, codeaddr, thunkaddr, mask);
        }

        public void resolveThunkHit(Address codeaddr, ulong targethit)
        { // Code unit make indirect jump to target
          // Assume the address of the jump is another level of thunk
          // Look for direct calls to it and include those as TargetHits
            map<AddrLink, uint>::iterator iter, enditer;
            iter = tofrom_crossref.lower_bound(AddrLink(codeaddr));
            Address endaddr = codeaddr + 1;
            enditer = tofrom_crossref.lower_bound(AddrLink(endaddr));
            while (iter != enditer)
            {
                uint flags = (*iter).second;
                if ((flags & CodeUnit::call) != 0)
                    addTargetHit((*iter).first.b, targethit);
                ++iter;
            }
        }

        public void findUnlinked()
        { // Find all code units that have no jump/call/fallthru to them
            map<Address, CodeUnit>::iterator iter;

            for (iter = codeunit.begin(); iter != codeunit.end(); ++iter)
            {
                CodeUnit & cu((*iter).second);
                if ((cu.flags & (CodeUnit::hit_by_fallthru | CodeUnit::hit_by_jump |
                         CodeUnit::hit_by_call | CodeUnit::notcode |
                         CodeUnit::errantstart)) == 0)
                    unlinkedstarts.push_back((*iter).first);
                if ((cu.flags & (CodeUnit::targethit | CodeUnit::notcode)) == CodeUnit::targethit)
                {
                    Address codeaddr = (*iter).first;
                    DisassemblyResult res;
                    disengine.disassemble(codeaddr, res);
                    if ((cu.flags & CodeUnit::thunkhit) != 0)
                        resolveThunkHit(codeaddr, res.targethit);
                    else
                        addTargetHit(codeaddr, res.targethit);
                }
            }
        }

        public bool checkErrantStart(Dictionary<Address, CodeUnit>::iterator iter)
        {
            int count = 0;

            while (count < 1000)
            {
                CodeUnit & cu((*iter).second);
                if ((cu.flags & (CodeUnit::hit_by_jump | CodeUnit::hit_by_call)) != 0)
                    return false;       // Something else jumped in
                if ((cu.flags & CodeUnit::hit_by_fallthru) == 0)
                {
                    cu.flags |= CodeUnit::errantstart;
                    return true;
                }
                if (iter == codeunit.begin()) return false;
                --iter;
                count += 1;
            }
            return false;
        }

        public bool repairJump(Address addr, int max)
        { // Assume -addr- is a correct instruction start. Try to repair
          // disassembly for up to -max- instructions following it,
          // trying to get back on cut
            DisassemblyResult disresult;
            List<CodeUnit> codevec;
            map<AddrLink, uint> fromto_vec;
            Address curaddr = addr;
            map<Address, CodeUnit>::iterator iter;
            int count = 0;

            iter = codeunit.lower_bound(addr);
            if (iter == codeunit.end()) return false;
            for (; ; )
            {
                count += 1;
                if (count >= max) return false;
                while ((*iter).first < curaddr)
                {
                    ++iter;
                    if (iter == codeunit.end()) return false;
                }
                if (curaddr == (*iter).first) break; // Back on cut
                disengine.disassemble(curaddr, disresult);
                if (!disresult.success) return false;
                codevec.emplace_back();
                if ((disresult.flags & CodeUnit::jump) != 0)
                {
                    fromto_vec[AddrLink(curaddr, disresult.jumpaddress)] = disresult.flags;
                }
                codevec.back().flags = disresult.flags;
                codevec.back().size = disresult.length;
                curaddr = curaddr + disresult.length;
            }
            clearCodeUnits(addr, curaddr);
            commitCodeVec(addr, codevec, fromto_vec);
            return true;
        }

        public void findOffCut()
        {
            map<AddrLink, uint>::iterator iter;
            map<Address, CodeUnit>::iterator citer;

            iter = tofrom_crossref.begin();
            while (iter != tofrom_crossref.end())
            {
                Address addr = (*iter).first.a; // Destination of a jump
                citer = codeunit.lower_bound(addr);
                if (citer != codeunit.end())
                {
                    if ((*citer).first == addr)
                    { // Not off cut
                        CodeUnit & cu((*citer).second);
                        if ((cu.flags & (CodeUnit::hit_by_fallthru | CodeUnit::hit_by_call)) ==
                            (CodeUnit::hit_by_fallthru | CodeUnit::hit_by_call))
                        {
                            // Somebody falls through into the call
                            --citer;
                            checkErrantStart(citer);
                        }
                        ++iter;
                        continue;
                    }
                }
                if (citer == codeunit.begin())
                {
                    ++iter;
                    continue;
                }
                --citer;            // Last lessthan or equal
                if ((*citer).first == addr)
                {
                    ++iter;
                    continue; // on cut
                }
                Address endaddr = (*citer).first + (*citer).second.size;
                if (endaddr <= addr)
                {
                    ++iter;
                    continue;
                }
                if (!checkErrantStart(citer))
                {
                    ++iter;
                    continue;
                }
                AddrLink addrlink = (*iter).first;
                repairJump(addr, 10);   // This may delete tofrom_crossref nodes
                iter = tofrom_crossref.upper_bound(addrlink);
            }
        }

        public Address findFunctionStart(Address addr)
        { // Find the starting address of a function containing the address addr
            map<AddrLink, uint>::const_iterator iter;

            iter = tofrom_crossref.lower_bound(AddrLink(addr));
            while (iter != tofrom_crossref.begin())
            {
                --iter;
                if (((*iter).second & CodeUnit::call) != 0)
                    return (*iter).first.a;
            }
            return Address();       // Return invalid address
        }

        public List<TargetHit> getTargetHits() => targethits;

        public void dumpModelHits(TextWriter s)
        {
            set<Range>::const_iterator iter, enditer;
            iter = modelhits.begin();
            enditer = modelhits.end();
            while (iter != enditer)
            {
                ulong off = (*iter).getFirst();
                s << hex << "0x" << off << ' ';
                ulong endoff = (*iter).getLast();
                s << hex << "0x" << endoff;
                ++iter;
                if (iter != enditer)
                {
                    off = (*iter).getFirst();
                    s << ' ' << dec << (int)(off - endoff);
                }
                s << endl;
            }
        }

        public void dumpCrossRefs(TextWriter s)
        {
            map<AddrLink, uint>::const_iterator iter;

            for (iter = fromto_crossref.begin(); iter != fromto_crossref.end(); ++iter)
            {
                AddrLink addrlink = (*iter).first;
                uint flags = (*iter).second;

                s << hex << "0x" << addrlink.a.getOffset() << " . 0x" << addrlink.b.getOffset();
                if ((flags & CodeUnit::call) != 0)
                    s << " call";
                s << endl;
            }
        }

        public void dumpFunctionStarts(TextWriter s)
        {
            map<AddrLink, uint>::const_iterator iter;

            for (iter = tofrom_crossref.begin(); iter != tofrom_crossref.end(); ++iter)
            {
                AddrLink addrlink = (*iter).first;
                uint flags = (*iter).second;

                if ((flags & CodeUnit::call) != 0)
                    s << hex << "0x" << addrlink.a.getOffset() << endl;
            }
        }

        public void dumpUnlinked(TextWriter s)
        {
            list<Address>::const_iterator iter;

            for (iter = unlinkedstarts.begin(); iter != unlinkedstarts.end(); ++iter)
            {
                s << hex << "0x" << (*iter).getOffset() << endl;
            }
        }

        public void dumpTargetHits(TextWriter s)
        { // Dump every code unit that refers to a target
            list<TargetHit>::const_iterator iter;

            for (iter = targethits.begin(); iter != targethits.end(); ++iter)
            {
                Address funcaddr = (*iter).funcstart;
                Address addr = (*iter).codeaddr;
                string nm = (*targets.find((*iter).thunkaddr)).second.name;
                if (!funcaddr.isInvalid())
                    s << hex << funcaddr.getOffset() << ' ';
                else
                    s << "nostart ";
                s << hex << addr.getOffset() << ' ' << nm << endl;
            }
        }

        public void runModel()
        {
            LoadImage* loadimage = glb.loader;
            LoadImageSection secinfo;
            bool moresections;
            loadimage.openSectionInfo();
            Address lastaddr;
            do
            {
                moresections = loadimage.getNextSection(secinfo);
                Address endaddr = secinfo.address + secinfo.size;
                if (secinfo.size == 0) continue;
                if (lastaddr.isInvalid())
                    lastaddr = endaddr;
                else if (lastaddr < endaddr)
                    lastaddr = endaddr;

                if ((secinfo.flags & (LoadImageSection::unalloc | LoadImageSection::noload)) == 0)
                {
                    modelhits.insertRange(secinfo.address.getSpace(),
                              secinfo.address.getOffset(), endaddr.getOffset());
                }
            } while (moresections);
            loadimage.closeSectionInfo();
            CodeUnit & cu(codeunit[lastaddr]);
            cu.size = 100;
            cu.flags = CodeUnit::notcode;
            disassembleRangeList(modelhits);
            findNotCodeUnits();
            markFallthruHits();
            markCrossHits();
            findOffCut();
            clearHitBy();
            markFallthruHits();
            markCrossHits();
            findUnlinked();
            targethits.sort();      // Sort the list of hits by function containing hit
        }
    }
}
