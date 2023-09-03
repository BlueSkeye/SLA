using Sla.CORE;
using static Sla.DECCORE.ScoreProtoModel;
using System.Collections.Generic;
using System;

namespace Sla.DECCORE
{
    /// \brief A union of other input parameter passing models
    ///
    /// This model is viewed as a union of a constituent set of resource lists.
    /// This allows initial data-flow analysis to proceed when the exact model
    /// isn't known.  The assignMap() and fillinMap() methods are disabled for
    /// instances of this class. The controlling prototype model (ProtoModelMerged)
    /// decides from among the constituent ParamList models before these routines
    /// need to be invoked.
    internal class ParamListMerged : ParamListStandard
    {
        /// Constructor for use with decode
        public ParamListMerged()
            : base()
        {
        }

        /// Copy constructor
        public ParamListMerged(ParamListMerged op2)
            : base(op2)
        {
        }

        // Add another model to the union
        /// The given set of parameter entries are folded into \b this set.
        /// Duplicate entries are eliminated. Containing entries subsume what
        /// they contain.
        /// \param op2 is the list model to fold into \b this
        public void foldIn(ParamListStandard op2)
        {
            if (entry.empty()) {
                spacebase = op2.getSpacebase();
                entry = op2.getEntry();
                return;
            }
            if ((spacebase != op2.getSpacebase()) && (op2.getSpacebase() != (AddrSpace)null))
                throw new LowlevelError("Cannot merge prototype models with different stacks");

            IEnumerator<ParamEntry> iter2 = op2.getEntry().GetEnumerator();
            while (iter2.MoveNext()) {
                ParamEntry opentry = iter2.Current;
                int typeint = 0;
                int iterationIndex;
                IEnumerator<ParamEntry> iter = entry.GetEnumerator();
                for (iterationIndex = 0; iterationIndex < entry.Count; iterationIndex++) {
                    ParamEntry scannedEntry = entry[iterationIndex];
                    if (scannedEntry.subsumesDefinition(opentry)) {
                        typeint = 2;
                        break;
                    }
                    if (opentry.subsumesDefinition(scannedEntry)) {
                        typeint = 1;
                        break;
                    }
                }
                if (typeint == 2) {
                    if (entry[iterationIndex].getMinSize() != opentry.getMinSize())
                        typeint = 0;
                }
                else if (typeint == 1) {
                    if (entry[iterationIndex].getMinSize() != opentry.getMinSize())
                        typeint = 0;
                    else
                        // Replace with the containing entry
                        entry[iterationIndex] = opentry;
                }
                if (typeint == 0)
                    entry.Add(opentry);
            }
        }

        // Fold-ins are finished, finalize \b this
        public void finalize()
        {
            populateResolver();
        }

        public override Model getType() => Model.p_merged;

        public override void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res)
        {
            throw new LowlevelError("Cannot assign prototype before model has been resolved");
        }

        public override void fillinMap(ParamActive active)
        {
            throw new LowlevelError("Cannot determine prototype before model has been resolved");
        }

        public override ParamList clone()
        {
            ParamList res = new ParamListMerged(this);
            return res;
        }
    }
}
