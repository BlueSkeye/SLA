using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Container class for ParamTrial objects
    ///
    /// The parameter analysis algorithms use this class to maintain the collection
    /// of parameter trials being actively considered for a given function. It holds the
    /// ParamTrial objects and other information about the current state of analysis.
    ///
    /// Trials are maintained in two stages, \e before parameter decisions have been made and \e after.
    /// Before, trials are in input index order relative to the CALL or CALLIND op for a sub-function, or
    /// they are in address order for input Varnodes to the active function.
    /// After, the trials are put into formal parameter order, as dictated by the PrototypeModel.
    internal class ParamActive
    {
        /// The list of parameter trials
        private List<ParamTrial> trial;
        /// Slot where next parameter will go
        private int slotbase;
        /// Which call input slot holds the stack placeholder
        private int stackplaceholder;
        /// Number of attempts at evaluating parameters
        private int numpasses;
        /// Number of passes before we assume we have seen all params
        private int maxpass;
        /// True if all trials are fully examined (and no new trials are expected)
        private bool isfullychecked;
        /// Should a final pass be made on trials (to take into account control-flow changes)
        private bool needsfinalcheck;
        /// True if \b this is being used to recover prototypes of a sub-function call
        private bool recoversubcall;

        /// Construct an empty container
        /// \param recoversub selects whether a sub-function or the active function is being tested
        public ParamActive(bool recoversub)
        {
            slotbase = 1;
            stackplaceholder = -1;
            numpasses = 0;
            maxpass = 0;
            isfullychecked = false;
            needsfinalcheck = false;
            recoversubcall = recoversub;
        }

        /// Reset to an empty container
        public void clear()
        {
            trial.clear();
            slotbase = 1;
            stackplaceholder = -1;
            numpasses = 0;
            isfullychecked = false;
        }

        /// Add a new trial to the container
        /// A ParamTrial object is created and a slot is assigned.
        /// \param addr is the starting address of the memory range
        /// \param sz is the number of bytes in the range
        public void registerTrial(Address addr,int sz)
        {
            trial.Add(ParamTrial(addr, sz, slotbase));
            // It would require too much work to calculate whether a specific data location is changed
            // by a subfunction, but a fairly strong assumption is that (unless it is explicitly saved) a
            // register may change and is thus unlikely to be used as a location for passing parameters.
            // However stack locations saving a parameter across a function call is a common construction
            // Since this all a heuristic for recovering parameters, we assume this rule is always true
            // to get an efficient test
            if (addr.getSpace().getType() != IPTR_SPACEBASE)
                trial.back().markKilledByCall();
            slotbase += 1;
        }

        /// Get the number of trials in \b this container
        public int getNumTrials() => trial.size();

        /// Get the i-th trial
        public ParamTrial getTrial(int i) => trial[i];

        /// Get trial corresponding to the given input Varnode
        /// Return the trial associated with the input Varnode to the associated p-code CALL or CALLIND.
        /// We take into account the call address parameter (subtract 1) and if the index occurs \e after the
        /// index holding the stackpointer placeholder, we subtract an additional 1.
        /// \param slot is the input index of the input Varnode
        /// \return the corresponding parameter trial
        public ParamTrial getTrialForInputVarnode(int slot)
        {
            slot -= ((stackplaceholder < 0) || (slot < stackplaceholder)) ? 1 : 2;
            return trial[slot];
        }

        /// Get the trial overlapping with the given memory range
        /// The (index of) the first overlapping trial is returned.
        /// \param addr is the starting address of the given range
        /// \param sz is the number of bytes in the range
        /// \return the index of the overlapping trial, or -1 if no trial overlaps
        public int whichTrial(Address addr, int sz)
        {
            for (int i = 0; i < trial.size(); ++i)
            {
                if (addr.overlap(0, trial[i].getAddress(), trial[i].getSize()) >= 0) return i;
                if (sz <= 1) return -1;
                Address endaddr = addr + (sz - 1);
                if (endaddr.overlap(0, trial[i].getAddress(), trial[i].getSize()) >= 0) return i;
            }
            return -1;
        }

        /// Is a final check required
        public bool needsFinalCheck() => needsfinalcheck;

        /// Mark that a final check is required
        public void markNeedsFinalCheck()
        {
            needsfinalcheck = true;
        }

        /// Are these trials for a call to a sub-function
        public bool isRecoverSubcall() => recoversubcall;

        /// Are all trials checked with no new trials expected
        public bool isFullyChecked() => isfullychecked;

        /// Mark that all trials are checked
        public void markFullyChecked()
        {
            isfullychecked = true;
        }

        /// Establish a stack placedholder slot
        public void setPlaceholderSlot()
        {
            stackplaceholder = slotbase;
            slotbase += 1;
        }

        /// Free the stack placeholder slot
        /// Free up the stack placeholder slot, which may cause trial slots to get adjusted
        public void freePlaceholderSlot()
        {
            for (int i = 0; i < trial.size(); ++i)
            {
                if (trial[i].getSlot() > stackplaceholder)
                    trial[i].setSlot(trial[i].getSlot() - 1);
            }
            stackplaceholder = -2;
            slotbase -= 1;
            // If we've found the placeholder, then the -next- time we
            // analyze parameters, we will have given all locations the
            // chance to show up, so we prevent any analysis after -next-
            maxpass = 0;
        }

        /// How many trial analysis passes were performed
        public int getNumPasses() => numpasses;

        /// What is the maximum number of passes
        public int getMaxPass() => maxpass;

        /// Set the maximum number of passes
        public void setMaxPass(int val)
        {
            maxpass = val;
        }

        /// Mark that an analysis pass has completed
        public void finishPass()
        {
            numpasses += 1;
        }

        /// Sort the trials in formal parameter order
        public void sortTrials()
        {
            sort(trial.begin(), trial.end());
        }

        /// Remove trials that were found not to be parameters
        /// Delete any trial for which isUsed() returns \b false.
        /// This is used in conjunction with setting the active Varnodes on a call, so the slot number is
        /// reordered too.
        public void deleteUnusedTrials()
        {
            List<ParamTrial> newtrials;
            int slot = 1;

            for (int i = 0; i < trial.size(); ++i)
            {
                ParamTrial & curtrial(trial[i]);
                if (curtrial.isUsed())
                {
                    curtrial.setSlot(slot);
                    slot += 1;
                    newtrials.Add(curtrial);
                }
            }
            trial = newtrials;
        }

        /// Split the given trial in two
        /// Split the trial into two trials, where the first piece has the given size.
        /// \param i is the index of the given trial
        /// \param sz is the given size
        public void splitTrial(int i, int sz)
        {
            if (stackplaceholder >= 0)
                throw new LowlevelError("Cannot split parameter when the placeholder has not been recovered");
            List<ParamTrial> newtrials;
            int slot = trial[i].getSlot();

            for (int j = 0; j < i; ++j)
            {
                newtrials.Add(trial[j]);
                int oldslot = newtrials.back().getSlot();
                if (oldslot > slot)
                    newtrials.back().setSlot(oldslot + 1);
            }
            newtrials.Add(trial[i].splitHi(sz));
            newtrials.Add(trial[i].splitLo(trial[i].getSize() - sz));
            for (int j = i + 1; j < trial.size(); ++j)
            {
                newtrials.Add(trial[j]);
                int oldslot = newtrials.back().getSlot();
                if (oldslot > slot)
                    newtrials.back().setSlot(oldslot + 1);
            }
            slotbase += 1;
            trial = newtrials;
        }

        /// Join adjacent parameter trials
        /// Join the trial at the given slot with the trial in the next slot
        /// \param slot is the given slot
        /// \param addr is the address of the new joined memory range
        /// \param sz is the size of the new memory range
        public void joinTrial(int slot, Address addr, int sz)
        {
            if (stackplaceholder >= 0)
                throw new LowlevelError("Cannot join parameters when the placeholder has not been removed");
            List<ParamTrial> newtrials;
            int sizecheck = 0;
            for (int i = 0; i < trial.size(); ++i)
            {
                ParamTrial & curtrial(trial[i]);
                int curslot = curtrial.getSlot();
                if (curslot < slot)
                    newtrials.Add(curtrial);
                else if (curslot == slot)
                {
                    sizecheck += curtrial.getSize();
                    newtrials.Add(ParamTrial(addr, sz, slot));
                    newtrials.back().markUsed();
                    newtrials.back().markActive();
                }
                else if (curslot == slot + 1)
                { // this slot is thrown out
                    sizecheck += curtrial.getSize();
                }
                else
                {
                    newtrials.Add(curtrial);
                    newtrials.back().setSlot(curslot - 1);
                }
            }
            if (sizecheck != sz)
                throw new LowlevelError("Size mismatch when joining parameters");
            slotbase -= 1;
            trial = newtrials;
        }

        /// Get number of trials marked as formal parameters
        /// This assumes the trials have been sorted. So \e used trials are first.
        /// \return the number of formally used trials
        public int getNumUsed()
        {
            int count;
            for (count = 0; count < trial.size(); ++count)
            {
                if (!trial[count].isUsed()) break;
            }
            return count;
        }

        /// \brief Test if the given trial can be shrunk to the given range
        /// \param i is the index of the given trial
        /// \param addr is the new address
        /// \param sz is the new size
        /// \return true if the trial can be shrunk to the new range
        public bool testShrink(int i, Address addr, int sz)
        {
            return trial[i].testShrink(addr, sz);
        }

        /// \brief Shrink the given trial to a new given range
        /// \param i is the index of the given trial
        /// \param addr is the new range's starting address
        /// \param sz is the new range's size in bytes
        public void shrink(int i, Address addr, int sz)
        {
            trial[i].setAddress(addr, sz);
        }

        /// sort the trials by fixed position then <
        public void sortFixedPosition()
        {
            sort(trial.begin(), trial.end(), ParamTrial::fixedPositionCompare);
        }
    }
}
