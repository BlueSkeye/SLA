﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A register or memory register that may be used to pass a parameter or return value
    ///
    /// The parameter recovery utilities (see ParamActive) use this to denote a putative
    /// parameter passing storage location. It is made up of the address and size of the memory range,
    /// a set of properties about the use of the range (as a parameter) in context, and a link to
    /// the matching part of the PrototypeModel.
    ///
    /// Data-flow for the putative parameter is held directly by a Varnode.  To quickly map to the
    /// Varnode (which may or may not exist at points during the ParamTrial lifetime), the concept
    /// of \b slot is used.  ParamTrials are assigned a \e slot, starting at 1.  For sub-function parameters,
    /// this represents the actual input index of the Varnode in the corresponding CALL or CALLIND op.
    /// For parameters, this gives the position within the list of possible input Varnodes in address order.
    /// The \e slot ordering varies over the course of analysis and is unlikely to match
    /// the final parameter ordering.  The ParamTrial comparator sorts the trials in final parameter ordering.
    internal class ParamTrial
    {
        [Flags()]
        public enum ParamFlags
        {
            /// Trial has been checked
            @checked = 1,
            /// Trial is definitely used  (final verdict)
            used = 2,
            /// Trial is definitely not used
            defnouse = 4,
            /// Trial looks active (hint that it is used)
            active = 8,
            /// There is no direct reference to this parameter trial
            unref = 0x10,
            /// Data in this location is unlikely to flow thru a func and still be a param
            killedbycall = 0x20,
            /// The trial is built out of a remainder operation
            rem_formed = 0x40,
            /// The trial is built out of an indirect creation
            indcreate_formed = 0x80,
            /// This trial may be affected by conditional execution
            condexe_effect = 0x100,
            /// Trial has a realistic ancestor
            ancestor_realistic = 0x200,
            /// Solid movement into the Varnode
            ancestor_solid = 0x400
        }

        /// Boolean properties of the trial
        private ParamFlags flags;
        /// Starting address of the memory range
        private Address addr;
        /// Number of bytes in the memory range
        private int size;
        /// Slot assigned to this trial
        private int slot;
        /// PrototypeModel entry matching this trial
        private ParamEntry entry;
        /// "justified" offset into entry
        private int offset;
        /// argument position if a fixed arg of a varargs function, else -1
        private int fixedPosition;
        
        /// \brief Construct from components
        public ParamTrial(Address ad, int sz, int sl)
        {
            addr = ad;
            size = sz;
            slot = sl;
            flags=0;
            entry=(ParamEntry)null;
            offset=-1;
            fixedPosition = -1;
        }

        /// Get the starting address of \b this trial
        public Address getAddress() => addr;

        /// Get the number of bytes in \b this trial
        public int getSize() => size;

        /// Get the \e slot associated with \b this trial
        public int getSlot() => slot;

        /// Set the \e slot associated with \b this trial
        public void setSlot(int val)
        {
            slot = val;
        }

        /// Get the model entry associated with \b this trial
        public ParamEntry getEntry() => entry;

        /// Get the offset associated with \b this trial
        public int getOffset() => offset;

        /// Set the model entry for this trial
        public void setEntry(ParamEntry ent, int off)
        {
            entry = ent;
            offset = off;
        }

        /// Mark the trial as a formal parameter
        public void markUsed()
        {
            flags |= ParamFlags.used;
        }

        /// Mark that trial is actively used (in data-flow)
        public void markActive()
        {
            flags |= (ParamFlags.active | ParamFlags.@checked);
        }

        /// Mark that trial is not actively used
        public void markInactive()
        {
            flags &= ~ParamFlags.active;
            flags |= ParamFlags.@checked;
        }

        /// Mark trial as definitely \e not a parameter
        public void markNoUse()
        {
            flags &= ~(ParamFlags.active | ParamFlags.used);
            flags |= (ParamFlags.@checked | ParamFlags.defnouse);
        }

        /// Mark that \b this trial has no Varnode representative
        public void markUnref()
        {
            flags |= (ParamFlags.unref | ParamFlags.@checked);
            slot = -1;
        }

        /// Mark that \b this storage is \e killed-by-call
        public void markKilledByCall()
        {
            flags |= ParamFlags.killedbycall;
        }

        /// Has \b this trial been checked
        public bool isChecked() => ((flags & ParamFlags.@checked)!= 0);

        /// Is \b this trial actively used in data-flow
        public bool isActive() => ((flags & ParamFlags.active)!= 0);

        /// Is \b this trial as definitely not a parameter
        public bool isDefinitelyNotUsed() => ((flags & ParamFlags.defnouse)!= 0);

        /// Is \b this trial as a formal parameter
        public bool isUsed() => ((flags & ParamFlags.used)!= 0);

        /// Does \b this trial not have a Varnode representative
        public bool isUnref() => ((flags & ParamFlags.unref)!= 0);

        /// Is \b this storage \e killed-by-call
        public bool isKilledByCall() => ((flags & ParamFlags.killedbycall)!= 0);

        /// Mark that \b this is formed by a INT_REM operation
        public void setRemFormed()
        {
            flags |= ParamFlags.rem_formed;
        }

        /// Is \b this formed by a INT_REM operation
        public bool isRemFormed() => ((flags & ParamFlags.rem_formed)!= 0);

        /// Mark \b this trial as formed by \e indirect \e creation
        public void setIndCreateFormed()
        {
            flags |= ParamFlags.indcreate_formed;
        }

        /// Is \b this trial formed by \e indirect \e creation
        public bool isIndCreateFormed() => ((flags & ParamFlags.indcreate_formed)!= 0);

        /// Mark \b this trial as possibly affected by conditional execution
        public void setCondExeEffect()
        {
            flags |= ParamFlags.condexe_effect;
        }

        /// Is \b this trial possibly affected by conditional execution
        public bool hasCondExeEffect() => ((flags & ParamFlags.condexe_effect)!= 0);

        /// Mark \b this as having a realistic ancestor
        public void setAncestorRealistic()
        {
            flags |= ParamFlags.ancestor_realistic;
        }

        /// Does \b this have a realistic ancestor
        public bool hasAncestorRealistic() => ((flags & ParamFlags.ancestor_realistic)!= 0);

        /// Mark \b this as showing solid movement into Varnode
        public void setAncestorSolid()
        {
            flags |= ParamFlags.ancestor_solid;
        }

        /// Does \b this show solid movement into Varnode
        public bool hasAncestorSolid() => ((flags & ParamFlags.ancestor_solid)!= 0);

        /// Get position of \b this within its parameter \e group
        public int slotGroup() => entry.getSlot(addr, size-1);

        /// Reset the memory range of \b this trial
        public void setAddress(Address ad, int sz)
        {
            addr = ad;
            size = sz;
        }

        /// Create a trial representing the first part of \b this
        /// Create a new ParamTrial based on the first bytes of the memory range.
        /// \param sz is the number of bytes to include in the new trial
        /// \return the new trial
        public ParamTrial splitHi(int sz)
        {
            ParamTrial res = new ParamTrial(addr, sz, slot);
            res.flags = flags;
            return res;
        }

        /// Create a trial representing the last part of \b this
        /// Create a new ParamTrial based on the last bytes of the memory range.
        /// \param sz is the number of bytes to include in the new trial
        /// \return the new trial
        public ParamTrial splitLo(int sz)
        {
            Address newaddr = addr + (size - sz);
            ParamTrial res = new ParamTrial(newaddr, sz, slot+1);
            res.flags = flags;
            return res;
        }

        /// Test if \b this trial can be made smaller
        /// A new address and size for the memory range is given, which
        /// must respect the endianness of the putative parameter and
        /// any existing match with the PrototypeModel
        /// \param newaddr is the new address
        /// \param sz is the new size
        /// \return true if the trial can be shrunk to the new range
        public bool testShrink(Address newaddr, int sz)
        {
            Address testaddr;
            if (addr.isBigEndian())
                testaddr = addr + (size - sz);
            else
                testaddr = addr;
            if (testaddr != newaddr)
                return false;
            if (entry != (ParamEntry)null) return false;
            //  if (entry != (ParamEntry)null) {
            //    int res = entry.justifiedContain(newaddr,sz);
            //    if (res < 0) return false;
            //  }
            return true;
        }

        /// Sort trials in formal parameter order
        /// Trials are sorted primarily by the \e group index assigned by the PrototypeModel.
        /// Trials within the same group are sorted in address order (or its reverse)
        /// \param b is the other trial to compare with \b this
        /// \return \b true if \b this should be ordered before the other trial
        public static bool operator <(ParamTrial a, ParamTrial b)
        {
            if (a.entry == (ParamEntry)null) return false;
            if (b.entry == (ParamEntry)null) return true;
            int grpa = a.entry.getGroup();
            int grpb = b.entry.getGroup();
            if (grpa != grpb)
                return (grpa < grpb);
            if (a.entry != b.entry)       // Compare entry pointers directly
                return (a.entry < b.entry);
            if (a.entry.isExclusion()) {
                return (a.offset < b.offset);
            }
            if (a.addr != b.addr) {
                if (a.entry.isReverseStack())
                    return (b.addr < a.addr);
                else
                    return (a.addr < b.addr);
            }
            return (a.size < b.size);
        }

        public static bool operator >(ParamTrial a, ParamTrial b)
        {
            if (a.entry == (ParamEntry)null) return false;
            if (b.entry == (ParamEntry)null) return true;
            int grpa = a.entry.getGroup();
            int grpb = b.entry.getGroup();
            if (grpa != grpb)
                return (grpa < grpb);
            if (a.entry != b.entry)
                // Compare entry pointers directly
                return (a.entry > b.entry);
            if (a.entry.isExclusion()) {
                return (a.offset > b.offset);
            }
            if (a.addr != b.addr) {
                if (a.entry.isReverseStack())
                    return (b.addr > a.addr);
                else
                    return (a.addr > b.addr);
            }
            return (a.size > b.size);
        }

        /// Set fixed position
        public void setFixedPosition(int pos)
        {
            fixedPosition = pos;
        }

        /// Sort by fixed position; stable for fixedPosition = -1
        /// Sort by fixed position then by ParamTrial::operator<
        /// \param a trial
        /// \param b trial
        /// \return \b true if \b a should be ordered before \b b
        public static int fixedPositionCompare(ParamTrial a, ParamTrial b)
        {
            if (a.fixedPosition == -1 && b.fixedPosition == -1) {
                return a.CompareTo(b);
            }
            if (a.fixedPosition == -1) {
                return 1;
            }
            if (b.fixedPosition == -1) {
                return -1;
            }
            return a.fixedPosition.CompareTo(b.fixedPosition);
        }
    }
}
