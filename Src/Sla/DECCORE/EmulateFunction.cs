using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A light-weight emulator to calculate switch targets from switch variables
    ///
    /// We assume we only have to store memory state for individual Varnodes and that dynamic
    /// LOADs are resolved from the LoadImage. BRANCH and CBRANCH emulation will fail, there can
    /// only be one execution path, although there can be multiple data-flow paths.
    internal class EmulateFunction : EmulatePcodeOp
    {
        /// The function being emulated
        private Funcdata fd;
        /// Light-weight memory state based on Varnodes
        private Dictionary<Varnode, ulong> varnodeMap;
        /// Set to \b true if the emulator collects individual LOAD addresses
        private bool collectloads;
        /// The set of collected LOAD records
        private List<LoadTable> loadpoints;

        protected override void executeLoad()
        {
            if (collectloads)
            {
                ulong off = getVarnodeValue(currentOp.getIn(1));
                AddrSpace* spc = currentOp.getIn(0).getSpaceFromConst();
                off = AddrSpace.addressToByte(off, spc.getWordSize());
                int sz = currentOp.getOut().getSize();
                loadpoints.Add(LoadTable(Address(spc, off), sz));
            }
            EmulatePcodeOp::executeLoad();
        }

        protected override void executeBranch()
        {
            throw new LowlevelError("Branch encountered emulating jumptable calculation");
        }

        protected override void executeBranchind()
        {
            throw new LowlevelError("Indirect branch encountered emulating jumptable calculation");
        }

        protected override void executeCall()
        {
            // Ignore calls, as presumably they have nothing to do with final address
            fallthruOp();
        }

        protected override void executeCallind()
        {
            // Ignore calls, as presumably they have nothing to do with final address
            fallthruOp();
        }

        protected override void executeCallother()
        {
            // Ignore callothers
            fallthruOp();
        }

        protected override void fallthruOp()
        {
            lastOp = currentOp;     // Keep track of lastOp for MULTIEQUAL
                                    // Otherwise do nothing: outer loop is controlling execution flow
        }

        /// \param f is the function to emulate within
        public EmulateFunction(Funcdata f)
            : base(f.getArch())
        {
            fd = f;
            collectloads = false;
        }

        public void setLoadCollect(bool val)
        {
            collectloads = val;
        }   ///< Set whether we collect LOAD information

        public override void setExecuteAddress(Address addr)
        {
            if (!addr.getSpace().hasPhysical())
                throw new LowlevelError("Bad execute address");
            currentOp = fd.target(addr);
            if (currentOp == (PcodeOp)null)
                throw new LowlevelError("Could not set execute address");
            currentBehave = currentOp.getOpcode().getBehavior();
        }

        public override ulong getVarnodeValue(Varnode vn)
        {
            // Get the value of a Varnode which is in a syntax tree
            // We can't just use the memory location as, within the tree,
            // this is just part of the label
            if (vn.isConstant())
                return vn.getOffset();
            ulong value;
            if (varnodeMap.TryGetValue(vn, out value))
                return value;  // We have seen this varnode before
            return getLoadImageValue(vn.getSpace(), vn.getOffset(), vn.getSize());
        }

        public override void setVarnodeValue(Varnode vn, ulong val)
        {
            varnodeMap[vn] = val;
        }

        /// \brief Execute from a given starting point and value to the common end-point of the path set
        ///
        /// Flow the given value through all paths in the path container to produce the
        /// single output value.
        /// \param val is the starting value
        /// \param pathMeld is the set of paths to execute
        /// \param startop is the starting PcodeOp within the path set
        /// \param startvn is the Varnode holding the starting value
        /// \return the calculated value at the common end-point
        public ulong emulatePath(ulong val, PathMeld pathMeld, PcodeOp startop, Varnode startvn)
        {
            uint i;
            for (i = 0; i < pathMeld.numOps(); ++i)
                if (pathMeld.getOp(i) == startop) break;
            if (startop.code() == OpCode.CPUI_MULTIEQUAL)
            { // If we start on a MULTIEQUAL
                int j;
                for (j = 0; j < startop.numInput(); ++j)
                { // Is our startvn one of the branches
                    if (startop.getIn(j) == startvn)
                        break;
                }
                if ((j == startop.numInput()) || (i == 0)) // If not, we can't continue;
                    throw new LowlevelError("Cannot start jumptable emulation with unresolved MULTIEQUAL");
                // If the startvn was a branch of the MULTIEQUAL, emulate as if we just came from that branch
                startvn = startop.getOut(); // So the output of the MULTIEQUAL is the new startvn (as if a COPY from old startvn)
                i -= 1;         // Move to the next instruction to be executed
                startop = pathMeld.getOp(i);
            }
            if (i == pathMeld.numOps())
                throw new LowlevelError("Bad jumptable emulation");
            if (!startvn.isConstant())
                setVarnodeValue(startvn, val);
            while (i > 0)
            {
                PcodeOp* curop = pathMeld.getOp(i);
                --i;
                setCurrentOp(curop);
                try
                {
                    executeCurrentOp();
                }
                catch (DataUnavailError err) {
                    ostringstream msg;
                    msg << "Could not emulate address calculation at " << curop.getAddr();
                    throw new LowlevelError(msg.str());
                }
            }
            Varnode* invn = pathMeld.getOp(0).getIn(0);
            return getVarnodeValue(invn);
        }

        /// Recover any LOAD table descriptions
        /// Pass back any LOAD records collected during emulation.  The individual records
        /// are sorted and collapsed into concise \e table descriptions.
        /// \param res will hold any resulting table descriptions
        public void collectLoadPoints(List<LoadTable> res)
        {
            if (loadpoints.empty()) return;
            bool issorted = true;

            IEnumerator<LoadTable> iter = loadpoints.GetEnumerator();
            if (!iter.MoveNext()) throw new BugException();
            res.Add(iter.Current);   // Copy the first entry
            IEnumerator<LoadTable> lastiter = res.GetEnumerator();
            if (!lastiter.MoveNext()) throw new BugException();
            Address nextaddr = lastiter.Current.addr + lastiter.Current.size;
            while (iter.MoveNext()) {
                if (issorted && ((iter.Current.addr == nextaddr) && (iter.Current.size == lastiter.Current.size))) {
                    lastiter.Current.num += iter.Current.num;
                    nextaddr = iter.Current.addr + iter.Current.size;
                }
                else {
                    issorted = false;
                    res.Add(iter.Current);
                }
            }
            if (!issorted) {
                res.Sort();
                LoadTable.collapseTable(res);
            }
        }
    }
}
