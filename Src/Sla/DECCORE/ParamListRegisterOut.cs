using Sla.CORE;
using System.Numerics;
using System;
using Sla.DECCORE;
using System.Collections.Generic;
using System.Formats.Tar;

namespace Sla.DECCORE
{
    /// \brief A model for passing back return values from a function
    ///
    /// This is a resource list of potential storage locations for a return value,
    /// at most 1 of which will be chosen for a given function. This models a simple strategy
    /// for selecting a storage location. When assigning based on data-type (assignMap), the first list
    /// entry that fits is chosen.  When assigning from a set of actively used locations (fillinMap),
    /// this class chooses the location that is the closest fitting match to an entry in the resource list.
    internal class ParamListRegisterOut : ParamListStandard
    {
        /// Constructor
        public ParamListRegisterOut()
            : base()
        {
        }

        /// Copy constructor
        public ParamListRegisterOut(ParamListRegisterOut op2)
            : base(op2)
        {
        }
        
        public override Model getType() => Model.p_register_out;

        public override void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res)
        {
            List<int> status = new List<int>(numgroup);
            ParameterPieces newPieces = new ParameterPieces();
            res.Add(newPieces);
            if (proto[0].getMetatype() != type_metatype.TYPE_VOID) {
                newPieces.addr = assignAddress(proto[0], status);
                if (newPieces.addr.isInvalid())
                    throw new ParamUnassignedError(
                        $"Cannot assign parameter address for {proto[0].getName()}");
            }
            newPieces.type = proto[0];
            newPieces.flags = 0;
        }

        public override void fillinMap(ParamActive active)
        {
            if (active.getNumTrials() == 0) return; // No trials to check
            ParamEntry? bestentry = (ParamEntry)null;
            int bestcover = 0;
            type_metatype bestmetatype = type_metatype.TYPE_PTR;

            // Find entry which is best covered by the active trials
            IEnumerator<ParamEntry> iter = entry.begin();
            while (iter.MoveNext()) {
                ParamEntry curentry = iter.Current;
                bool putativematch = false;
                for (int j = 0; j < active.getNumTrials(); ++j) {
                    // Evaluate all trials in terms of current ParamEntry
                    ParamTrial paramtrial = active.getTrial(j);
                    if (paramtrial.isActive())
                    {
                        int res = curentry.justifiedContain(paramtrial.getAddress(), paramtrial.getSize());
                        if (res >= 0)
                        {
                            paramtrial.setEntry(curentry, res);
                            putativematch = true;
                        }
                        else
                            paramtrial.setEntry((ParamEntry)null, 0);
                    }
                    else
                        paramtrial.setEntry((ParamEntry)null, 0);
                }
                if (!putativematch) continue;
                active.sortTrials();
                // Calculate number of least justified, contiguous, bytes for this entry
                int offmatch = 0;
                int k;
                for(k=0;k<active.getNumTrials();++k) {
                    ParamTrial paramtrial = active.getTrial(k);
                    if (paramtrial.getEntry() == (ParamEntry)null) continue;
                    if (offmatch != paramtrial.getOffset()) break;
                    if (((offmatch == 0) && curentry.isParamCheckLow()) ||
                        ((offmatch != 0) && curentry.isParamCheckHigh()))
                    {
                        // If this is multi-precision
                        // Do extra checks that this portion isn't created normally
                        if (paramtrial.isRemFormed())
                            // Formed as a remainder of dual div/rem operation
                            break;
                        if (paramtrial.isIndCreateFormed())
                            // Formed indirectly by call
                            break;
                    }
                    offmatch += paramtrial.getSize();
                }
                // If we didn't match enough to cover minimum size
                if (offmatch < curentry.getMinSize())
                    // Don't use this entry
                    k = 0;
                // Prefer a more generic type restriction if we have it prefer the larger coverage
                if ((k == active.getNumTrials()) && ((curentry.getType() > bestmetatype) || (offmatch > bestcover)))
                {
                    bestentry = curentry;
                    bestcover = offmatch;
                    bestmetatype = curentry.getType();
                }
            }
            if (bestentry == (ParamEntry)null) {
                for (int i = 0; i < active.getNumTrials(); ++i)
                    active.getTrial(i).markNoUse(); 
            }
            else {
                for (int i = 0; i < active.getNumTrials(); ++i) {
                    ParamTrial paramtrial = active.getTrial(i);
                    if (paramtrial.isActive()) {
                        int res = bestentry.justifiedContain(paramtrial.getAddress(),
                            paramtrial.getSize());
                        if (res >= 0) {
                            // Only actives are ever marked used
                            paramtrial.markUsed();
                            paramtrial.setEntry(bestentry, res);
                        }
                        else {
                            paramtrial.markNoUse();
                            paramtrial.setEntry((ParamEntry)null, 0);
                        }
                    }
                    else {
                        paramtrial.markNoUse();
                        paramtrial.setEntry((ParamEntry)null, 0);
                    }
                }
                active.sortTrials();
            }
        }

        public override bool possibleParam(Address loc, int size)
        {
            IEnumerator<ParamEntry> iter = entry.GetEnumerator();
            while (iter.MoveNext()) {
                if (iter.Current.justifiedContain(loc, size) >= 0)
                    return true;
            }
            return false;
        }

        public override ParamList clone()
        {
            ParamList res = new ParamListRegisterOut(this);
            return res;
        }
    }
}
