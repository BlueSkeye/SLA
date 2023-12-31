﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A light-weight class for analyzing pointers and aliasing on the stack
    ///
    /// The gather() method looks for pointer references into a specific AddressSpace
    /// (usually the stack). Then hasLocalAlias() checks if a specific Varnode within
    /// the AddressSpace is (possibly) aliased by one of the gathered pointer references.
    internal class AliasChecker
    {
        /// \brief A helper class holding a Varnode pointer reference and a possible index added to it
        public struct AddBase
        {
            /// The Varnode holding the base pointer
            internal Varnode @base;
            /// The index value or NULL
            internal Varnode index;
            
            internal AddBase(Varnode b, Varnode i)
            {
                @base = b;
                index = i;
            }
        }

        /// Function being searched for aliases
        private Funcdata? fd;
        /// AddressSpace in which to search
        private AddrSpace? space;
        /// Collection of pointers into the AddressSpace
        private /*mutable*/ List<AddBase> addBase;
        /// List of aliased addresses (as offsets)
        private /*mutable*/ List<ulong> alias;
        /// Have aliases been calculated
        private /*mutable*/ bool calculated;
        /// Largest possible offset for a local variable
        private ulong localExtreme;
        /// Boundary offset separating locals and parameters
        private ulong localBoundary;
        /// Shallowest alias
        private /*mutable*/ ulong aliasBoundary;
        /// 1=stack grows negative, -1=positive
        private int direction;

        /// Set up basic boundaries for the stack layout
        /// Set up basic offset boundaries for what constitutes a local variable
        /// or a parameter on the stack. This can be informed by the ProtoModel if available.
        /// \param proto is the function prototype to use as a prototype model
        private void deriveBoundaries(FuncProto proto)
        {
            localExtreme = ulong.MaxValue;         // Default settings
            localBoundary = 0x1000000;
            if (direction == -1)
                localExtreme = localBoundary;

            if (proto.hasModel())
            {
                RangeList localrange = proto.getLocalRange();
                RangeList paramrange = proto.getParamRange();

                Sla.CORE.Range? local = localrange.getFirstRange();
                Sla.CORE.Range? param = paramrange.getLastRange();
                if ((local != (Sla.CORE.Range)null)&& (param != (Sla.CORE.Range)null)) {
                    localBoundary = param.getLast();
                    if (direction == -1) {
                        localBoundary = paramrange.getFirstRange().getFirst();
                        localExtreme = localBoundary;
                    }
                }
            }
        }

        /// Run through Varnodes looking for pointers into the stack
        /// If there is an AddrSpace (stack) pointer, find its input Varnode, and look for additive uses
        /// of it. Once all these Varnodes are accumulated, calculate specific offsets that start a region
        /// being aliased.
        private void gatherInternal()
        {
            calculated = true;
            aliasBoundary = localExtreme;
            Varnode? spacebase = fd.findSpacebaseInput(space);
            if (spacebase == (Varnode)null)
                // No possible alias
                return;

            gatherAdditiveBase(spacebase, addBase);
            IEnumerator<AddBase> iter = addBase.GetEnumerator();
            while (iter.MoveNext()) {
                ulong offset = gatherOffset(iter.Current.@base);
                // Convert to byte offset
                offset = AddrSpace.addressToByte(offset, space.getWordSize());
                alias.Add(offset);
                if (direction == 1) {
                    if (offset < localBoundary)
                        // Parameter ref
                        continue;
                }
                else {
                    if (offset > localBoundary)
                        // Parameter ref
                        continue;
                }
                // Always consider anything AFTER a pointer reference as
                // aliased, regardless of the stack direction
                if (offset < aliasBoundary)
                    aliasBoundary = offset;
            }
        }

        public AliasChecker()
        {
            fd = (Funcdata)null;
            space = (AddrSpace)null;
            calculated = false;
        }

        /// Gather Varnodes that point on the stack
        /// For the given function and address space, gather all Varnodes that are pointers into the
        /// address space.  The actual calculation can be deferred until the first time
        /// hasLocalAlias() is called.
        /// \param f is the given function
        /// \param spc is the given address space
        /// \param defer is \b true is gathering is deferred
        public void gather(Funcdata f, AddrSpace spc,bool defer)
        {
            fd = f;
            space = spc;
            calculated = false;     // Defer calculation
            addBase.Clear();
            alias.Clear();
            // direction == 1 for normal negative stack growth
            direction = space.stackGrowsNegative() ? 1 : -1;
            deriveBoundaries(fd.getFuncProto());
            if (!defer)
                gatherInternal();
        }

        /// Return \b true if it looks like the given Varnode is aliased by a pointer
        /// This is gives a rough analysis of whether the given Varnode might be aliased by another pointer in
        /// the function. If \b false is returned, the Varnode is not likely to have an alias. If \b true is returned,
        /// the Varnode might have an alias.
        /// \param vn is the given Varnode
        /// \return \b true if the Varnode might have a pointer alias
        public bool hasLocalAlias(Varnode vn)
        {
            if (vn == (Varnode)null) return false;
            if (!calculated)
                gatherInternal();
            if (vn.getSpace() != space) return false;
            // For positive stack growth, this is not a good test because values being queued on the
            // stack to be passed to a subfunction always have offsets a little bit bigger than ALL
            // local variables on the stack
            if (direction == -1)
                return false;
            return (vn.getOffset() >= aliasBoundary);
        }

        /// Sort the alias starting offsets
        public void sortAlias()
        {
            alias.Sort();
        }

        /// Get the collection of pointer Varnodes
        public List<AddBase> getAddBase() => addBase;

        /// Get the list of alias starting offsets
        public List<ulong> getAlias() => alias;

        /// \brief Gather result Varnodes for all \e sums that the given starting Varnode is involved in
        ///
        /// For every sum that involves \b startvn, collect the final result Varnode of the sum.
        /// A sum is any expression involving only the additive operators
        /// INT_ADD, INT_SUB, PTRADD, PTRSUB, and SEGMENTOP.  The routine traverses forward recursively
        /// through all descendants of \b vn that are additive operations and collects all the roots
        /// of the traversed trees.
        /// \param startvn is the Varnode to trace
        /// \param addbase will contain all the collected roots
        public static void gatherAdditiveBase(Varnode startvn, List<AddBase> addbase)
        {
            // varnodes involved in addition with original vn
            List<AddBase> vnqueue  = new List<AddBase>();
            Varnode vn;
            Varnode subvn;
            Varnode indexvn;
            Varnode othervn;
            PcodeOp op;
            bool nonadduse;
            int i = 0;

            vn = startvn;
            vn.setMark();
            vnqueue.Add(new AddBase(vn, (Varnode)null));
            while (i < vnqueue.Count) {
                vn = vnqueue[i].@base;
                indexvn = vnqueue[i++].index;
                nonadduse = false;
                IEnumerator<PcodeOp> iter = vn.beginDescend();
                while (iter.MoveNext()) {
                    op = iter.Current;
                    switch (op.code()) {
                        case OpCode.CPUI_COPY:
                            // Treat COPY as both non-add use and part of ADD expression
                            nonadduse = true;
                            subvn = op.getOut() ?? throw new ApplicationException();
                            if (!subvn.isMark()) {
                                subvn.setMark();
                                vnqueue.Add(new AddBase(subvn, indexvn));
                            }
                            break;
                        case OpCode.CPUI_INT_SUB:
                            if (vn == op.getIn(1)) {
                                // Subtracting the pointer
                                nonadduse = true;
                                break;
                            }
                            othervn = op.getIn(1) ?? throw new ApplicationException();
                            if (!othervn.isConstant())
                                indexvn = othervn;
                            subvn = op.getOut() ?? throw new ApplicationException();
                            if (!subvn.isMark()) {
                                subvn.setMark();
                                vnqueue.Add(new AddBase(subvn, indexvn));
                            }
                            break;
                        case OpCode.CPUI_INT_ADD:
                        case OpCode.CPUI_PTRADD:
                            // Check if something else is being added in besides a constant
                            othervn = op.getIn(1) ?? throw new ApplicationException();
                            if (othervn == vn)
                                othervn = op.getIn(0) ?? throw new ApplicationException();
                            if (!othervn.isConstant())
                                indexvn = othervn;
                            goto case OpCode.CPUI_PTRSUB;
                        // fallthru
                        case OpCode.CPUI_PTRSUB:
                        case OpCode.CPUI_SEGMENTOP:
                            subvn = op.getOut() ?? throw new ApplicationException();
                            if (!subvn.isMark()) {
                                subvn.setMark();
                                vnqueue.Add(new AddBase(subvn, indexvn));
                            }
                            break;
                        default:
                            // Used in non-additive expression
                            nonadduse = true;
                            break;
                    }
                }
                if (nonadduse)
                    addbase.Add(new AddBase(vn, indexvn));
            }
            for (i = 0; i < vnqueue.Count; ++i)
                vnqueue[i].@base.clearMark();
        }

        // \brief If the given Varnode is a sum result, return the constant portion of
        // this sum.
        // Treat \b vn as the result of a series of ADD operations.
        // Examine all the constant terms of this sum and add them together by traversing
        // the syntax tree rooted at \b vn, backwards, only through additive operations.
        // \param vn is the given Varnode to gather off of
        // \return the resulting sub-sum
        public static ulong gatherOffset(Varnode vn)
        {
            ulong retval;
            Varnode othervn;

            if (vn.isConstant()) return vn.getOffset();
            PcodeOp? def = vn.getDef() ?? throw new ApplicationException();
            if (def == (PcodeOp)null) return 0;
            switch (def.code()) {
                case OpCode.CPUI_COPY:
                    retval = gatherOffset(def.getIn(0));
                    break;
                case OpCode.CPUI_PTRSUB:
                case OpCode.CPUI_INT_ADD:
                    retval = gatherOffset(def.getIn(0));
                    retval += gatherOffset(def.getIn(1));
                    break;
                case OpCode.CPUI_INT_SUB:
                    retval = gatherOffset(def.getIn(0));
                    retval -= gatherOffset(def.getIn(1));
                    break;
                case OpCode.CPUI_PTRADD:
                    othervn = def.getIn(2);
                    retval = gatherOffset(def.getIn(0));
                    // We need to treat PTRADD exactly as if it were encoded as an ADD and MULT
                    // Because a plain MULT truncates the ADD tree
                    // We only follow getIn(1) if the PTRADD multiply is by 1
                    if (othervn.isConstant() && (othervn.getOffset() == 1))
                        retval = retval + gatherOffset(def.getIn(1));
                    break;
                case OpCode.CPUI_SEGMENTOP:
                    retval = gatherOffset(def.getIn(2));
                    break;
                default:
                    retval = 0;
                    break;
            }
            return retval & Globals.calc_mask((uint)vn.getSize());
        }
    }
}
