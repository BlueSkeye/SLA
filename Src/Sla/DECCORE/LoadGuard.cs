﻿using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Description of a LOAD operation that needs to be guarded
    ///
    /// Heritage maintains a list of OpCode.CPUI_LOAD ops that reference the stack dynamically. These
    /// can potentially alias stack Varnodes, so we maintain what (possibly limited) information
    /// we known about the range of stack addresses that can be referenced.
    internal class LoadGuard
    {
        // friend class Heritage;
        /// The LOAD op
        internal PcodeOp op;
        /// The stack space being loaded from
        internal AddrSpace spc;
        /// Base offset of the pointer
        private ulong pointerBase;
        /// Minimum offset of the LOAD
        internal ulong minimumOffset;
        /// Maximum offset of the LOAD
        internal ulong maximumOffset;
        /// Step of any access into this range (0=unknown)
        private int step;
        /// 0=unanalyzed, 1=analyzed(partial result), 2=analyzed(full result)
        internal int analysisState;

        /// Convert partial value set analysis into guard range
        /// Make some determination of the range of possible values for a LOAD based
        /// an partial value set analysis. This can sometimes get
        ///   - minimumOffset - otherwise the original constant pulled with the LOAD is used
        ///   - step          - the partial analysis shows step and direction
        ///   - maximumOffset - in rare cases
        ///
        /// isAnalyzed is set to \b true, if full range analysis is not needed
        /// \param valueSet is the calculated value set as seen by the LOAD operation
        internal void establishRange(ValueSetRead valueSet)
        {
            CircleRange range = valueSet.getRange();
            ulong rangeSize = range.getSize();
            ulong size;
            if (range.isEmpty())
            {
                minimumOffset = pointerBase;
                size = 0x1000;
            }
            else if (range.isFull() || rangeSize > 0xffffff)
            {
                minimumOffset = pointerBase;
                size = 0x1000;
                analysisState = 1;  // Don't bother doing more analysis
            }
            else
            {
                step = (rangeSize == 3) ? range.getStep() : 0;  // Check for consistent step
                size = 0x1000;
                if (valueSet.isLeftStable())
                {
                    minimumOffset = range.getMin();
                }
                else if (valueSet.isRightStable())
                {
                    if (pointerBase < range.getEnd())
                    {
                        minimumOffset = pointerBase;
                        size = (range.getEnd() - pointerBase);
                    }
                    else
                    {
                        minimumOffset = range.getMin();
                        size = rangeSize * range.getStep();
                    }
                }
                else
                    minimumOffset = pointerBase;
            }
            ulong max = spc.getHighest();
            if (minimumOffset > max)
            {
                minimumOffset = max;
                maximumOffset = minimumOffset;  // Something is seriously wrong
            }
            else
            {
                ulong maxSize = (max - minimumOffset) + 1;
                if (size > maxSize)
                    size = maxSize;
                maximumOffset = minimumOffset + size - 1;
            }
        }

        /// Convert value set analysis to final guard range
        internal void finalizeRange(ValueSetRead valueSet)
        {
            analysisState = 1;      // In all cases the settings determined here are final
            CircleRange range = valueSet.getRange();
            ulong rangeSize = range.getSize();
            if (rangeSize == 0x100 || rangeSize == 0x10000) {
                // These sizes likely result from the storage size of the index
                if (step == 0)  // If we didn't see signs of iteration
                    rangeSize = 0;  // don't use this range
            }
            if (rangeSize > 1 && rangeSize < 0xffffff) {
                // Did we converge to something reasonable
                analysisState = 2;          // Mark that we got a definitive result
                if (rangeSize > 2)
                    step = range.getStep();
                minimumOffset = range.getMin();
                maximumOffset = (range.getEnd() - 1) & range.getMask(); // NOTE: Don't subtract a whole step
                if (maximumOffset < minimumOffset) {
                    // Values extend into what is usually stack parameters
                    maximumOffset = spc.getHighest();
                    analysisState = 1;  // Remove the lock as we have likely overflowed
                }
            }
            if (minimumOffset > spc.getHighest())
                minimumOffset = spc.getHighest();
            if (maximumOffset > spc.getHighest())
                maximumOffset = spc.getHighest();
        }

        /// \brief Set a new unanalyzed LOAD guard that initially guards everything
        ///
        /// \param o is the LOAD op
        /// \param s is the (stack) space it is loading from
        /// \param off is the base offset that is indexed from
        internal void set(PcodeOp o, AddrSpace s, ulong off)
        {
            op = o;
            spc = s;
            pointerBase = off;
            minimumOffset = 0;
            maximumOffset = s.getHighest();
            step = 0;
            analysisState = 0;
        }

        /// Get the PcodeOp being guarded
        public PcodeOp getOp() => op;

        /// Get minimum offset of the guarded range
        public ulong getMinimum() => minimumOffset;

        /// Get maximum offset of the guarded range
        public ulong getMaximum() => maximumOffset;

        /// Get the calculated step associated with the range (or 0)
        public int getStep() => step;

        /// Does \b this guard apply to the given address
        /// Check if the address falls within the range defined by \b this
        /// \param addr is the given address
        /// \return \b true if the address is contained
        public bool isGuarded(Address addr)
        {
            if (addr.getSpace() != spc) return false;
            if (addr.getOffset() < minimumOffset) return false;
            if (addr.getOffset() > maximumOffset) return false;
            return true;
        }

        /// Return \b true if the range is fully determined
        public bool isRangeLocked() => (analysisState == 2);

        /// Return \b true if the record still describes an active LOAD
        public bool isValid(OpCode opc) => (!op.isDead() && op.code() == opc);
    }
}
