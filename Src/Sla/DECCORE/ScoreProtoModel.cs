using Sla.CORE;

namespace Sla.DECCORE
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
            public int origIndex;
            /// Matching slot within the resource list
            public int slot;
            /// Number of slots occupied
            public int size;

            /// \brief Compare PEntry objects by slot
            ///
            /// \param op2 is the PEntry to compare \b this to
            /// \return \b true if \b this should be ordered before the other PEntry
            public static bool operator <(PEntry op1, PEntry op2)
            {
                return (op1.slot < op2.slot);
            }

            public static bool operator >(PEntry op1, PEntry op2)
            {
                return (op1.slot > op2.slot);
            }
        }

        /// True if scoring against input parameters, \b false for outputs
        private bool isinputscore;
        /// Map of parameter entries corresponding to trials
        private List<PEntry> entry;
        /// Prototype model to score against
        private ProtoModel model;
        /// The final fitness score
        private int finalscore;
        /// Number of trials that don't fit the prototype model at all
        private int mismatch;

        /// Constructor
        /// \param isinput is set to \b true to compute scores against the input part of the model
        /// \param mod is the prototype model to score against
        /// \param numparam is the presumed number of trials that will constitute the score
        public ScoreProtoModel(bool isinput, ProtoModel mod, int numparam)
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
        public void addParameter(Address addr, int sz)
        {
            int orig = entry.size();
            int slot, slotsize;
            bool isparam;
            if (isinputscore)
                isparam = model.possibleInputParamWithSlot(addr, sz, out slot, out slotsize);
            else
                isparam = model.possibleOutputParamWithSlot(addr, sz, out slot, out slotsize);
            if (isparam) {
                entry.Add(new PEntry() {
                    origIndex = orig,
                    slot = slot,
                    size = slotsize
                });
            }
            else {
                mismatch += 1;
            }
        }

        /// Compute the fitness score
        public void doScore()
        {
            // Sort our entries via slot
            entry.Sort();

            // Next slot we expect to see
            int nextfree = 0;
            int basescore = 0;
            int[] penalty = new int[4] { 16, 10, 7, 5 };
            int penaltyfinal = 3;
            int mismatchpenalty = 20;

            for (int i = 0; i < entry.size(); ++i) {
                PEntry p = entry[i];
                if (p.slot > nextfree) {
                    // We have some kind of hole in our slot coverage
                    while (nextfree < p.slot) {
                        basescore += (nextfree < 4) ? penalty[nextfree] : penaltyfinal;
                        nextfree += 1;
                    }
                    nextfree += p.size;
                }
                else if (nextfree > p.slot) {
                    // Some kind of slot duplication
                    basescore += mismatchpenalty;
                    if (p.slot + p.size > nextfree)
                        nextfree = p.slot + p.size;
                }
                else {
                    nextfree = p.slot + p.size;
                }
            }
            finalscore = basescore + mismatchpenalty * mismatch;
        }

        /// Get the fitness score
        public int getScore() => finalscore;

        /// Get the number of mismatched trials
        public int getNumMismatch() => mismatch; 
    }
}
