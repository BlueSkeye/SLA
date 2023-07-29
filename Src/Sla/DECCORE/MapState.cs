using ghidra;
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
            int step = guard.getStep();
            if (step == 0) return;      // No definitive sign of array access
            Datatype* ct = guard.getOp().getIn(1).getTypeReadFacing(guard.getOp());
            if (ct.getMetatype() == TYPE_PTR)
            {
                ct = ((TypePointer*)ct).getPtrTo();
                while (ct.getMetatype() == TYPE_ARRAY)
                    ct = ((TypeArray*)ct).getBase();
            }
            int outSize;
            if (opc == CPUI_STORE)
                outSize = guard.getOp().getIn(2).getSize();   // The Varnode being stored
            else
                outSize = guard.getOp().getOut().getSize();   // The Varnode being loaded
            if (outSize != step)
            {
                // LOAD size doesn't match step:  field in array of structures or something more unusual
                if (outSize > step || (step % outSize) != 0)
                    return;
                // Since the LOAD size divides the step and we want to preserve the arrayness
                // we pretend we have an array of LOAD's size
                step = outSize;
            }
            if (ct.getSize() != step)
            {   // Make sure data-type matches our step size
                if (step > 8)
                    return;     // Don't manufacture primitives bigger than 8-bytes
                ct = typeFactory.getBase(step, TYPE_UNKNOWN);
            }
            if (guard.isRangeLocked())
            {
                int minItems = ((guard.getMaximum() - guard.getMinimum()) + 1) / step;
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
        private void addRange(ulong st, Datatype ct, uint fl, RangeHint::RangeType rt, int hi)
        {
            if ((ct == (Datatype)null) || (ct.getSize() == 0)) // Must have a real type
                ct = defaultType;
            int sz = ct.getSize();
            if (!range.inRange(Address(spaceid, st), sz))
                return;
            long sst = (long)AddrSpace::byteToAddress(st, spaceid.getWordSize());
            sign_extend(sst, spaceid.getAddrSize() * 8 - 1);
            sst = (long)AddrSpace::addressToByte(sst, spaceid.getWordSize());
            RangeHint* newRange = new RangeHint(st, sz, sst, ct, fl, rt, hi);
            maplist.Add(newRange);
#if OPACTION_DEBUG
            if (debugon)
            {
                ostringstream s;
                s << "Add Range: " << hex << st << ":" << dec << sz;
                s << " ";
                ct.printRaw(s);
                s << endl;
                glb.printDebug(s.str());
            }
#endif
        }

        /// Decide on data-type for RangeHints at the same address
        /// Assuming a sorted list, from among a sequence of RangeHints with the same start and size, select
        /// the most specific data-type.  Set all elements to use this data-type, and eliminate duplicates.
        private void reconcileDatatypes()
        {
            List<RangeHint*> newList;
            newList.reserve(maplist.size());
            int startPos = 0;
            RangeHint* startHint = maplist[0];
            Datatype* startDatatype = startHint.type;
            newList.Add(startHint);
            int curPos = 1;
            while (curPos < maplist.size())
            {
                RangeHint* curHint = maplist[curPos++];
                if (curHint.start == startHint.start && curHint.size == startHint.size)
                {
                    Datatype* curDatatype = curHint.type;
                    if (curDatatype.typeOrder(*startDatatype) < 0) // Take the most specific variant of data-type
                        startDatatype = curDatatype;
                    if (curHint.compare(*newList.GetLastItem()) != 0)
                        newList.Add(curHint);     // Keep the current hint if it is otherwise different
                    else
                        delete curHint;     // RangeHint is on the heap, so delete if we are not keeping it
                }
                else
                {
                    while (startPos < newList.size())
                    {
                        newList[startPos].type = startDatatype;
                        startPos += 1;
                    }
                    startHint = curHint;
                    startDatatype = startHint.type;
                    newList.Add(startHint);
                }
            }
            while (startPos < newList.size())
            {
                newList[startPos].type = startDatatype;
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
                ulong first = (*pmiter).getFirst();
                ulong last = (*pmiter).getLast();
                range.removeRange(pmSpc, first, last); // Clear possible input symbols
            }
#if OPACTION_DEBUG
            debugon = false;
#endif
        }

        ~MapState()
        {
            List<RangeHint*>::iterator riter;
            for (riter = maplist.begin(); riter != maplist.end(); ++riter)
                delete* riter;
        }

        /// Initialize the hint collection for iteration
        /// Sort the collection and add a special terminating RangeHint
        /// \return \b true if the collection isn't empty (and iteration can begin)
        public bool initialize()
        {
            // Enforce boundaries of local variables
            Range lastrange = range.getLastSignedRange(spaceid);
            if (lastrange == (Range*)0) return false;
            if (maplist.empty()) return false;
            ulong high = spaceid.wrapOffset(lastrange.getLast() + 1);
            long sst = (long)AddrSpace::byteToAddress(high, spaceid.getWordSize());
            sign_extend(sst, spaceid.getAddrSize() * 8 - 1);
            sst = (long)AddrSpace::addressToByte(sst, spaceid.getWordSize());
            // Add extra range to bound any final open entry
            RangeHint* termRange = new RangeHint(high, 1, sst, defaultType, 0, RangeHint::endpoint, -2);
            maplist.Add(termRange);

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
        public List<ulong> getAlias() => checker.getAlias();

        /// Add Symbol information as hints to the collection
        /// Run through all Symbols in the given map and create a corresponding RangeHint
        /// to \b this collection for each Symbol.
        /// \param rangemap is the given map of Symbols
        public void gatherSymbols(EntryMap rangemap)
        {
            list<SymbolEntry>::const_iterator riter;
            Symbol* sym;
            if (rangemap == (EntryMap)null) return;
            for (riter = rangemap.begin_list(); riter != rangemap.end_list(); ++riter)
            {
                sym = (*riter).getSymbol();
                if (sym == (Symbol)null) continue;
                //    if ((*iter).isPiece()) continue;     // This should probably never happen
                ulong start = (*riter).getAddr().getOffset();
                Datatype* ct = sym.getType();
                addRange(start, ct, sym.getFlags(), RangeHint::@fixed, -1);
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
                if (vn.isFree()) continue;
                ulong start = vn.getOffset();
                Datatype* ct = vn.getType();
                // Assume parents are present so partials aren't needed
                if (ct.getMetatype() == TYPE_PARTIALSTRUCT) continue;
                if (ct.getMetatype() == TYPE_PARTIALUNION) continue;
                // Do not force Varnode flags on the entry
                // as the flags were inherited from the previous
                // (now obsolete) entry
                addRange(start, ct, 0, RangeHint::@fixed,-1);
            }
        }

        /// Add HighVariables as hints to the collection
        /// Add a RangeHint corresponding to each HighVariable that is mapped to our
        /// address space for the given function.
        /// \param fd is the given function
        public void gatherHighs(Funcdata fd)
        {
            List<HighVariable*> varvec;
            VarnodeLocSet::const_iterator riter, iterend;
            Varnode* vn;
            HighVariable* high;
            riter = fd.beginLoc(spaceid);
            iterend = fd.endLoc(spaceid);
            while (riter != iterend)
            {
                vn = *riter++;
                high = vn.getHigh();
                if (high == (HighVariable)null) continue;
                if (high.isMark()) continue;
                if (!high.isAddrTied()) continue;
                vn = high.getTiedVarnode();    // Original vn may not be good representative
                high.setMark();
                varvec.Add(high);
                ulong start = vn.getOffset();
                Datatype* ct = high.getType(); // Get type from high
                if (ct.getMetatype() == TYPE_PARTIALUNION) continue;
                addRange(start, ct, 0, RangeHint::@fixed,-1);
            }
            for (int i = 0; i < varvec.size(); ++i)
                varvec[i].clearMark();
        }

        /// Add pointer references as hints to the collection
        /// For any Varnode that looks like a pointer into our address space, create an
        /// \e open RangeHint. The size of the object may not be known.
        /// \param fd is the given function
        public void gatherOpen(Funcdata fd)
        {
            checker.gather(&fd, spaceid, false);

            List<AliasChecker::AddBase> addbase = checker.getAddBase();
            List<ulong> alias = checker.getAlias();
            ulong offset;
            Datatype* ct;

            for (int i = 0; i < addbase.size(); ++i)
            {
                offset = alias[i];
                ct = addbase[i].@base.getType();
                if (ct.getMetatype() == TYPE_PTR)
                {
                    ct = ((TypePointer*)ct).getPtrTo();
                    while (ct.getMetatype() == TYPE_ARRAY)
                        ct = ((TypeArray*)ct).getBase();
                }
                else
                    ct = (Datatype)null;  // Do unknown array
                int minItems;
                if (addbase[i].index != (Varnode)null)
                {
                    minItems = 3;           // If there is an index, assume it takes on at least the 4 values [0,3]
                }
                else
                {
                    minItems = -1;
                }
                addRange(offset, ct, 0, RangeHint::open, minItems);
            }

            TypeFactory* typeFactory = fd.getArch().types;
            List<LoadGuard> loadGuard = fd.getLoadGuards();
            for (list<LoadGuard>::const_iterator giter = loadGuard.begin(); giter != loadGuard.end(); ++giter)
                addGuard(*giter, CPUI_LOAD, typeFactory);

            List<LoadGuard> storeGuard = fd.getStoreGuards();
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
