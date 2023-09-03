
using System;

namespace Sla.DECCORE
{
    /// \brief An unstructured model for passing input parameters to a function.
    ///
    /// This is the \b register model, meaning a collection of registers, any of which
    /// can be used to pass a parameter.  This is nearly identical to ParamListStandard, but
    /// rules banning \e holes are not enforced, any subset of the resource list can be used.
    /// This makes sense for executables where parameters follow no conventions or only loose
    /// conventions. The assignMap() method may make less sense in this scenario.
    internal class ParamListRegister : ParamListStandard
    {
        /// Constructor for use with decode()
        public ParamListRegister()
            : base()
        {
        }

        /// Copy constructor
        public ParamListRegister(ParamListRegister op2)
            : base(op2)
        {
        }
        
        public override Model getType() => Model.p_register;

        public override void fillinMap(ParamActive active)
        {
            if (active.getNumTrials() == 0) return; // No trials to check

            // Mark anything active as used
            for (int i = 0; i < active.getNumTrials(); ++i) {
                ParamTrial paramtrial = active.getTrial(i);
                ParamEntry? entrySlot = findEntry(paramtrial.getAddress(),
                    paramtrial.getSize());
                if (entrySlot == (ParamEntry)null)
                    // There may be no matching entry (if the model was recovered late)
                    paramtrial.markNoUse();
                else {
                    // Keep track of entry recovered for this trial
                    paramtrial.setEntry(entrySlot, 0);
                    if (paramtrial.isActive())
                        paramtrial.markUsed();
                }
            }
            active.sortTrials();
        }

        public override ParamList clone()
        {
            return new ParamListRegister(this);
        }
    }
}
