using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Prepare for data-flow analysis of function parameters, when recovery isn't required.
    ///
    /// If the "protorecovery" action group is not enabled, this
    /// Action probably should be. It sets up only the potential
    /// sub-function outputs (not the inputs) otherwise local uses of
    /// the output registers may be incorrectly heritaged, screwing
    /// up the local analysis (i.e. for jump-tables) even though we
    /// don't care about the function inputs.
    internal class ActionFuncLinkOutOnly : Action
    {
        public ActionFuncLinkOutOnly(string g)
            : base(rule_onceperfunc,"funclink_outonly", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionFuncLinkOutOnly(getGroup());
        }

        public override int apply(Funcdata data)
        {
            int4 size = data.numCalls();
            for (int4 i = 0; i < size; ++i)
                ActionFuncLink::funcLinkOutput(data.getCallSpecs(i), data);
            return 0;
        }
    }
}
