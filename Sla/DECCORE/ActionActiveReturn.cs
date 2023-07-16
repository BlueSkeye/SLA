using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Determine which sub-functions have active output Varnodes
    ///
    /// This is analogous to ActionActiveParam but for sub-function return values.
    internal class ActionActiveReturn : Action
    {
        public ActionActiveReturn(string g)
            : base( 0, "activereturn", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionActiveReturn(getGroup());
        }

        public override int apply(Funcdata data)
        {
            int4 i;
            FuncCallSpecs* fc;

            for (i = 0; i < data.numCalls(); ++i)
            {
                fc = data.getCallSpecs(i);
                if (fc->isOutputActive())
                {
                    ParamActive* activeoutput = fc->getActiveOutput();
                    vector<Varnode*> trialvn;
                    fc->checkOutputTrialUse(data, trialvn);
                    fc->deriveOutputMap(activeoutput);
                    fc->buildOutputFromTrials(data, trialvn);
                    fc->clearActiveOutput();
                    count += 1;
                }
            }
            return 0;
        }
    }
}
