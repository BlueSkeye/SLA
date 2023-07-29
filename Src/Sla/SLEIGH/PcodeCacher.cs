using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief Class for caching a chunk of p-code, prior to emitting
    ///
    /// The engine accumulates PcodeData and VarnodeData objects for
    /// a single instruction.  Once the full instruction is constructed,
    /// the objects are passed to the emitter (PcodeEmit) via the emit() method.
    /// The class acts as a pool of memory for PcodeData and VarnodeData objects
    /// that can be reused repeatedly to emit multiple instructions.
    internal class PcodeCacher
    {
        internal VarnodeData poolstart;     ///< Start of the pool of VarnodeData objects
        internal VarnodeData curpool;           ///< First unused VarnodeData
        internal VarnodeData endpool;           ///< End of the pool of VarnodeData objects
        internal List<PcodeData> issued;       ///< P-code ops issued for the current instruction
        internal List<RelativeRecord> label_refs;    ///< References to labels
        internal List<uintb> labels;           ///< Locations of labels

        ///< Expand the memory pool
        /// Expand the VarnodeData pool so that \e size more elements fit, and return
        /// a pointer to first available element.
        /// \param size is the number of elements to expand the pool by
        /// \return the first available VarnodeData
        internal VarnodeData expandPool(uint4 size)
        {
            uint4 curmax = endpool - poolstart;
            uint4 cursize = curpool - poolstart;
            if (cursize + size <= curmax)
                return curpool;     // No expansion necessary
            uint4 increase = (cursize + size) - curmax;
            if (increase < 100)     // Increase by at least 100
                increase = 100;

            uint4 newsize = curmax + increase;

            VarnodeData* newpool = new VarnodeData[newsize];
            for (uint4 i = 0; i < cursize; ++i)
                newpool[i] = poolstart[i];  // Copy old data
                                            // Update references to the old pool
            for (uint4 i = 0; i < issued.size(); ++i)
            {
                VarnodeData* outvar = issued[i].outvar;
                if (outvar != (VarnodeData*)0)
                {
                    outvar = newpool + (outvar - poolstart);
                    issued[i].outvar = outvar;
                }
                VarnodeData* invar = issued[i].invar;
                if (invar != (VarnodeData*)0)
                {
                    invar = newpool + (invar - poolstart);
                    issued[i].invar = invar;
                }
            }
            list<RelativeRecord>::iterator iter;
            for (iter = label_refs.begin(); iter != label_refs.end(); ++iter)
            {
                VarnodeData * ref = (*iter).dataptr;
                (*iter).dataptr = newpool + (ref -poolstart);
            }

            delete[] poolstart;     // Free up old pool
            poolstart = newpool;
            curpool = newpool + (cursize + size);
            endpool = newpool + newsize;
            return newpool + cursize;
        }

        public PcodeCacher()
        {
            // We aim to allocate this array only once
            uint4 maxsize = 600;
            poolstart = new VarnodeData[maxsize];
            endpool = poolstart + maxsize;
            curpool = poolstart;
        }

        ~PcodeCacher()
        {
            delete[] poolstart;
        }

        /// \brief Allocate data objects for a new set of Varnodes
        ///
        /// \param size is the number of objects to allocate
        /// \return a pointer to the array of available VarnodeData objects
        public VarnodeData allocateVarnodes(uint4 size)
        {
            VarnodeData* newptr = curpool + size;
            if (newptr <= endpool)
            {
                VarnodeData* res = curpool;
                curpool = newptr;
                return res;
            }
            return expandPool(size);
        }

        /// \brief Allocate a data object for a new p-code operation
        ///
        /// \return the new PcodeData object
        public PcodeData allocateInstruction()
        {
            issued.emplace_back();
            PcodeData* res = &issued.back();
            res.outvar = (VarnodeData*)0;
            res.invar = (VarnodeData*)0;
            return res;
        }

        ///< Denote a Varnode holding a \e relative \e branch offset
        /// Store off a reference to the Varnode and the absolute index of the next
        /// instruction.  The Varnode must be an operand of the current instruction.
        /// \param ptr is the Varnode reference
        public void addLabelRef(VarnodeData ptr)
        {
            label_refs.emplace_back();
            label_refs.back().dataptr = ptr;
            label_refs.back().calling_index = issued.size();
        }

        /// Attach a label to the \e next p-code instruction
        ///< Pass the cached p-code data to the emitter
        public void addLabel(uint4 id)
        {
            while (labels.size() <= id)
                labels.push_back(0xbadbeef);
            labels[id] = issued.size();
        }

        /// Reset the cache so that all objects are unallocated
        public void clear()
        {
            curpool = poolstart;
            issued.clear();
            label_refs.clear();
            labels.clear();
        }

        /// Rewrite branch target Varnodes as \e relative offsets
        /// Assuming all the PcodeData has been generated for an
        /// instruction, go resolve any relative offsets and back
        /// patch their value(s) into the PcodeData
        public void resolveRelatives()
        {
            list<RelativeRecord>::const_iterator iter;
            for (iter = label_refs.begin(); iter != label_refs.end(); ++iter)
            {
                VarnodeData* ptr = (*iter).dataptr;
                uint4 id = ptr.offset;
                if ((id >= labels.size()) || (labels[id] == 0xbadbeef))
                    throw new LowlevelError("Reference to non-existant sleigh label");
                // Calculate the relative index given the two absolute indices
                uintb res = labels[id] - (*iter).calling_index;
                res &= calc_mask(ptr.size);
                ptr.offset = res;
            }
        }

        /// Pass the cached p-code data to the emitter
        /// Each p-code operation is presented to the emitter via its dump() method.
        /// \param addr is the Address associated with the p-code operation
        /// \param emt is the emitter
        public void emit(Address addr,PcodeEmit emt)
        {
            List<PcodeData>::const_iterator iter;

            for (iter = issued.begin(); iter != issued.end(); ++iter)
                emt.dump(addr, (*iter).opc, (*iter).outvar, (*iter).invar, (*iter).isize);
        }
    }
}
