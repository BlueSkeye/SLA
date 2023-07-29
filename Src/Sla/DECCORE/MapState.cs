﻿using ghidra;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.AliasChecker;

namespace Sla.DECCORE
{
    /// \brief A container for hints about the data-type layout of an address space
    ///
    /// A collection of data-type hints for the address space (as RangeHint objects) can
    /// be collected from Varnodes, HighVariables or other sources, using the
    /// gatherVarnodes(), gatherHighs(), and gatherOpen() methods. This class can then sort
    /// and iterate through the RangeHint objects.
    internal class MapState
    {
        /// The address space being analyzed
        private AddrSpace spaceid;
        /// The subset of ranges, within the whole address space to analyze
        private RangeList range;
        /// The list of collected RangeHints
        private List<RangeHint> maplist;
        /// The current iterator into the RangeHints
        private IEnumerator<RangeHint> iter;
        /// The default data-type to use for RangeHints
        private Datatype defaultType;
        /// A collection of pointer Varnodes into our address space
        private AliasChecker checker;

        /// Add LoadGuard record as a hint to the collection
        /// The given LoadGuard, which may be a LOAD or STORE, is converted into an appropriate
        /// RangeHint, attempting to make use of any data-type or index information.
        /// \param guard is the given LoadGuard
        /// \param opc is the expected op-code (CPUI_LOAD or CPUI_STORE)
        /// \param typeFactory is used to manufacture a data-type for the hint if necessary
        private void addGuard(LoadGuard guard,OpCode opc, TypeFactory typeFactory)
        {
            if (!guard.isValid(opc)) return;
            int4 step = guard.getStep();
            if (step == 0) return;      // No definitive sign of array access
            Datatype* ct = guard.getOp()->getIn(1)->getTypeReadFacing(guard.getOp());
            if (ct->getMetatype() == TYPE_PTR)
            {
                ct = ((TypePointer*)ct)->getPtrTo();
                while (ct->getMetatype() == TYPE_ARRAY)
                    ct = ((TypeArray*)ct)->getBase();
            }
            int4 outSize;
            if (opc == CPUI_STORE)
                outSize = guard.getOp()->getIn(2)->getSize();   // The Varnode being stored
            else
                outSize = guard.getOp()->getOut()->getSize();   // The Varnode being loaded
            if (outSize != step)
            {
                // LOAD size doesn't match step:  field in array of structures or something more unusual
                if (outSize > step || (step % outSize) != 0)
                    return;
                // Since the LOAD size divides the step and we want to preserve the arrayness
                // we pretend we have an array of LOAD's size
                step = outSize;
            }
            if (ct->getSize() != step)
            {   // Make sure data-type matches our step size
                if (step > 8)
                    return;     // Don't manufacture primitives bigger than 8-bytes
                ct = typeFactory->getBase(step, TYPE_UNKNOWN);
            }
            if (guard.isRangeLocked())
            {
                int4 minItems = ((guard.getMaximum() - guard.getMinimum()) + 1) / step;
                addRange(guard.getMinimum(), ct, 0, RangeHint::open, minItems - 1);
            }
            else
                addRange(guard.getMinimum(), ct, 0, RangeHint::open, 3);
        }

        /// Add a hint to the collection
        /// A specific range of bytes is described for the hint, given a starting offset and other information.
        /// The size of range can be fixed or open-ended. A putative data-type can be provided.
        /// \param st is the starting offset of the range
        /// \param ct is the (optional) data-type information, which may be NULL
        /// \param fl is additional boolean properties
        /// \param rt is the type of the hint
        /// \param hi is the biggest guaranteed index for \e open range hints
        private void addRange(uintb st, Datatype ct, uint4 fl, RangeHint::RangeType rt, int4 hi)
        {
            if ((ct == (Datatype*)0) || (ct->getSize() == 0)) // Must have a real type
                ct = defaultType;
            int4 sz = ct->getSize();
            if (!range.inRange(Address(spaceid, st), sz))
                return;
            intb sst = (intb)AddrSpace::byteToAddress(st, spaceid->getWordSize());
            sign_extend(sst, spaceid->getAddrSize() * 8 - 1);
            sst = (intb)AddrSpace::addressToByte(sst, spaceid->getWordSize());
            RangeHint* newRange = new RangeHint(st, sz, sst, ct, fl, rt, hi);
            maplist.push_back(newRange);
#if OPACTION_DEBUG
            if (debugon)
            {
                ostringstream s;
                s << "Add Range: " << hex << st << ":" << dec << sz;
                s << " ";
                ct->printRaw(s);
                s << endl;
                glb->printDebug(s.str());
            }
#endif
        }

        /// Decide on data-type for RangeHints at the same address
        /// Assuming a sorted list, from among a sequence of RangeHints with the same start and size, select
        /// the most specific data-type.  Set all elements to use this data-type, and eliminate duplicates.
        private void reconcileDatatypes()
        {
            vector<RangeHint*> newList;
            newList.reserve(maplist.size());
            int4 startPos = 0;
            RangeHint* startHint = maplist[0];
            Datatype* startDatatype = startHint->type;
            newList.push_back(startHint);
            int4 curPos = 1;
            while (curPos < maplist.size())
            {
                RangeHint* curHint = maplist[curPos++];
                if (curHint->start == startHint->start && curHint->size == startHint->size)
                {
                    Datatype* curDatatype = curHint->type;
                    if (curDatatype->typeOrder(*startDatatype) < 0) // Take the most specific variant of data-type
                        startDatatype = curDatatype;
                    if (curHint->compare(*newList.back()) != 0)
                        newList.push_back(curHint);     // Keep the current hint if it is otherwise different
                    else
                        delete curHint;     // RangeHint is on the heap, so delete if we are not keeping it
                }
                else
                {
                    while (startPos < newList.size())
                    {
                        newList[startPos]->type = startDatatype;
                        startPos += 1;
                    }
                    startHint = curHint;
                    startDatatype = startHint->type;
                    newList.push_back(startHint);
                }
            }
            while (startPos < newList.size())
            {
                newList[startPos]->type = startDatatype;
                startPos += 1;
            }
            maplist.swap(newList);
        }

#if OPACTION_DEBUG
        public /*mutable*/ bool debugon;
        public /*mutable*/ Architecture *glb;
  
        public void turnOnDebug(Architecture g)
        {
            debugon = true;
            glb=g;
        }
        
        public void turnOffDebug() 
        {
            debugon = false;
        }
#endif
        /// \param spc is the address space being analyzed
        /// \param rn is the subset of addresses within the address space to analyze
        /// \param pm is subset of ranges within the address space considered to be parameters
        /// \param dt is the default data-type
        public MapState(AddrSpace spc, RangeList rn, RangeList pm, Datatype dt)
        {
            spaceid = spc;
            defaultType = dt;
            set<Range>::const_iterator pmiter;
            for (pmiter = pm.begin(); pmiter != pm.end(); ++pmiter)
            {
                AddrSpace* pmSpc = (*pmiter).getSpace();
                uintb first = (*pmiter).getFirst();
                uintb last = (*pmiter).getLast();
                range.removeRange(pmSpc, first, last); // Clear possible input symbols
            }
#if OPACTION_DEBUG
            debugon = false;
#endif
        }

        ~MapState()
        {
            vector<RangeHint*>::iterator riter;
            for (riter = maplist.begin(); riter != maplist.end(); ++riter)
                delete* riter;
        }

        /// Initialize the hint collection for iteration
        /// Sort the collection and add a special terminating RangeHint
        /// \return \b true if the collection isn't empty (and iteration can begin)
        public bool initialize()
        {
            // Enforce boundaries of local variables
            const Range* lastrange = range.getLastSignedRange(spaceid);
            if (lastrange == (Range*)0) return false;
            if (maplist.empty()) return false;
            uintb high = spaceid->wrapOffset(lastrange->getLast() + 1);
            intb sst = (intb)AddrSpace::byteToAddress(high, spaceid->getWordSize());
            sign_extend(sst, spaceid->getAddrSize() * 8 - 1);
            sst = (intb)AddrSpace::addressToByte(sst, spaceid->getWordSize());
            // Add extra range to bound any final open entry
            RangeHint* termRange = new RangeHint(high, 1, sst, defaultType, 0, RangeHint::endpoint, -2);
            maplist.push_back(termRange);

            stable_sort(maplist.begin(), maplist.end(), RangeHint::compareRanges);
            reconcileDatatypes();
            iter = maplist.begin();
            return true;
        }

        /// Sort the alias starting offsets
        public void sortAlias()
        {
            checker.sortAlias();
        }

        /// Get the list of alias starting offsets
        public List<uintb> getAlias() => checker.getAlias();

        /// Add Symbol information as hints to the collection
        /// Run through all Symbols in the given map and create a corresponding RangeHint
        /// to \b this collection for each Symbol.
        /// \param rangemap is the given map of Symbols
        public void gatherSymbols(EntryMap rangemap)
        {
            list<SymbolEntry>::const_iterator riter;
            Symbol* sym;
            if (rangemap == (EntryMap*)0) return;
            for (riter = rangemap->begin_list(); riter != rangemap->end_list(); ++riter)
            {
                sym = (*riter).getSymbol();
                if (sym == (Symbol*)0) continue;
                //    if ((*iter).isPiece()) continue;     // This should probably never happen
                uintb start = (*riter).getAddr().getOffset();
                Datatype* ct = sym->getType();
                addRange(start, ct, sym->getFlags(), RangeHint::@fixed, -1);
            }
        }

        /// Add stack Varnodes as hints to the collection
        /// Add a RangeHint corresponding to each Varnode stored in the address space
        /// for the given function.  The current knowledge of the Varnode's data-type
        /// is included as part of the hint.
        /// \param fd is the given function
        public void gatherVarnodes(Funcdata fd)
        {
            VarnodeLocSet::const_iterator riter, iterend;
            Varnode* vn;
            riter = fd.beginLoc(spaceid);
            iterend = fd.endLoc(spaceid);
            while (riter != iterend)
            {
                vn = *riter++;
                if (vn->isFree()) continue;
                uintb start = vn->getOffset();
                Datatype* ct = vn->getType();
                // Assume parents are present so partials aren't needed
                if (ct->getMetatype() == TYPE_PARTIALSTRUCT) continue;
                if (ct->getMetatype() == TYPE_PARTIALUNION) continue;
                // Do not force Varnode flags on the entry
                // as the flags were inherited from the previous
                // (now obsolete) entry
                addRange(start, ct, 0, RangeHint::fixed,-1);
            }
        }

        /// Add HighVariables as hints to the collection
        /// Add a RangeHint corresponding to each HighVariable that is mapped to our
        /// address space for the given function.
        /// \param fd is the given function
        public void gatherHighs(Funcdata fd)
        {
            vector<HighVariable*> varvec;
            VarnodeLocSet::const_iterator riter, iterend;
            Varnode* vn;
            HighVariable* high;
            riter = fd.beginLoc(spaceid);
            iterend = fd.endLoc(spaceid);
            while (riter != iterend)
            {
                vn = *riter++;
                high = vn->getHigh();
                if (high == (HighVariable*)0) continue;
                if (high->isMark()) continue;
                if (!high->isAddrTied()) continue;
                vn = high->getTiedVarnode();    // Original vn may not be good representative
                high->setMark();
                varvec.push_back(high);
                uintb start = vn->getOffset();
                Datatype* ct = high->getType(); // Get type from high
                if (ct->getMetatype() == TYPE_PARTIALUNION) continue;
                addRange(start, ct, 0, RangeHint::fixed,-1);
            }
            for (int4 i = 0; i < varvec.size(); ++i)
                varvec[i]->clearMark();
        }

        /// Add pointer references as hints to the collection
        /// For any Varnode that looks like a pointer into our address space, create an
        /// \e open RangeHint. The size of the object may not be known.
        /// \param fd is the given function
        public void gatherOpen(Funcdata fd)
        {
            checker.gather(&fd, spaceid, false);

            const vector<AliasChecker::AddBase> &addbase(checker.getAddBase());
            const vector<uintb> &alias(checker.getAlias());
            uintb offset;
            Datatype* ct;

            for (int4 i = 0; i < addbase.size(); ++i)
            {
                offset = alias[i];
                ct = addbase[i].base->getType();
                if (ct->getMetatype() == TYPE_PTR)
                {
                    ct = ((TypePointer*)ct)->getPtrTo();
                    while (ct->getMetatype() == TYPE_ARRAY)
                        ct = ((TypeArray*)ct)->getBase();
                }
                else
                    ct = (Datatype*)0;  // Do unknown array
                int4 minItems;
                if (addbase[i].index != (Varnode*)0)
                {
                    minItems = 3;           // If there is an index, assume it takes on at least the 4 values [0,3]
                }
                else
                {
                    minItems = -1;
                }
                addRange(offset, ct, 0, RangeHint::open, minItems);
            }

            TypeFactory* typeFactory = fd.getArch()->types;
            const list<LoadGuard> &loadGuard(fd.getLoadGuards());
            for (list<LoadGuard>::const_iterator giter = loadGuard.begin(); giter != loadGuard.end(); ++giter)
                addGuard(*giter, CPUI_LOAD, typeFactory);

            const list<LoadGuard> &storeGuard(fd.getStoreGuards());
            for (list<LoadGuard>::const_iterator siter = storeGuard.begin(); siter != storeGuard.end(); ++siter)
                addGuard(*siter, CPUI_STORE, typeFactory);
        }

        /// Get the current RangeHint in the collection
        public RangeHint next() => *iter;

        /// Advance the iterator, return \b true if another hint is available
        public bool getNext()
        {
            ++iter;
            if (iter == maplist.end())
                return false;
            return true;
        }
    }
}