﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief Class for calculating "goodness of fit" of parameter trials against a prototype model
    ///
    /// The class is instantiated with a prototype model (ProtoModel). A set of Varnode parameter trials
    /// are registered by calling addParameter() for each trial.  Then calling doScore() computes a score
    /// that evaluates how well the set of registered trials fit the prototype model.  A lower score
    /// indicates a better fit.
    internal class ScoreProtoModel
    {
        /// \brief A record mapping trials to parameter entries in the prototype model
        internal class PEntry
        {
            /// Original index of trial
            public int4 origIndex;
            /// Matching slot within the resource list
            public int4 slot;
            /// Number of slots occupied
            public int4 size;

            /// \brief Compare PEntry objects by slot
            ///
            /// \param op2 is the PEntry to compare \b this to
            /// \return \b true if \b this should be ordered before the other PEntry
            public static bool operator <(PEntry op1, PEntry op2)
            {
                return (slot<op2.slot);
            }
        }

        /// True if scoring against input parameters, \b false for outputs
        private bool isinputscore;
        /// Map of parameter entries corresponding to trials
        private List<PEntry> entry;
        /// Prototype model to score against
        private ProtoModel model;
        /// The final fitness score
        private int4 finalscore;
        /// Number of trials that don't fit the prototype model at all
        private int4 mismatch;

        /// Constructor
        /// \param isinput is set to \b true to compute scores against the input part of the model
        /// \param mod is the prototype model to score against
        /// \param numparam is the presumed number of trials that will constitute the score
        public ScoreProtoModel(bool isinput, ProtoModel mod, int4 numparam)
        {
            isinputscore = isinput;
            model = mod;
            entry.reserve(numparam);
            finalscore = -1;
            mismatch = 0;
        }

        /// Register a trial to be scored
        /// \param addr is the starting address of the trial
        /// \param sz is the number of bytes in the trial
        public void addParameter(Address addr, int4 sz)
        {
            int4 orig = entry.size();
            int4 slot, slotsize;
            bool isparam;
            if (isinputscore)
                isparam = model->possibleInputParamWithSlot(addr, sz, slot, slotsize);
            else
                isparam = model->possibleOutputParamWithSlot(addr, sz, slot, slotsize);
            if (isparam)
            {
                entry.emplace_back();
                entry.back().origIndex = orig;
                entry.back().slot = slot;
                entry.back().size = slotsize;
            }
            else
            {
                mismatch += 1;
            }
        }

        /// Compute the fitness score
        public void doScore()
        {
            sort(entry.begin(), entry.end()); // Sort our entries via slot

            int4 nextfree = 0;      // Next slot we expect to see
            int4 basescore = 0;
            int4 penalty[4];
            penalty[0] = 16;
            penalty[1] = 10;
            penalty[2] = 7;
            penalty[3] = 5;
            int4 penaltyfinal = 3;
            int4 mismatchpenalty = 20;

            for (int4 i = 0; i < entry.size(); ++i)
            {
                const PEntry &p(entry[i]);
                if (p.slot > nextfree)
                {   // We have some kind of hole in our slot coverage
                    while (nextfree < p.slot)
                    {
                        if (nextfree < 4)
                            basescore += penalty[nextfree];
                        else
                            basescore += penaltyfinal;
                        nextfree += 1;
                    }
                    nextfree += p.size;
                }
                else if (nextfree > p.slot)
                { // Some kind of slot duplication
                    basescore += mismatchpenalty;
                    if (p.slot + p.size > nextfree)
                        nextfree = p.slot + p.size;
                }
                else
                {
                    nextfree = p.slot + p.size;
                }
            }
            finalscore = basescore + mismatchpenalty * mismatch;
        }

        /// Get the fitness score
        public int4 getScore() => finalscore;

        /// Get the number of mismatched trials
        public int4 getNumMismatch() => mismatch; 
    }
}