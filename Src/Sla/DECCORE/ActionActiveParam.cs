using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Determine active parameters to sub-functions
    ///
    /// This is the final stage of the parameter recovery process, when
    /// a prototype for a sub-function is not explicitly known. Putative input Varnode
    /// parameters are collected by the Heritage process.  This class determines
    /// which of these Varnodes are being used as parameters.
    /// This needs to be called \b after ActionHeritage and \b after ActionDirectWrite
    /// but \b before any simplification or copy propagation has been performed.
    internal class ActionActiveParam : Action
    {
        public ActionActiveParam(string g)
            : base( 0, "activeparam", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionActiveParam(getGroup());
        }

        public override int apply(Funcdata data)
        {
            int i;
            FuncCallSpecs fc;
            AliasChecker aliascheck = new AliasChecker();
            aliascheck.gather(data, data.getArch().getStackSpace(), true);

            for (i = 0; i < data.numCalls(); ++i) {
                fc = data.getCallSpecs(i);
                // An indirect function is not trimmable until
                // there has been at least one simplification pass
                // there has been a change to deindirect
                try {
                    if (fc.isInputActive()) {
                        ParamActive activeinput = fc.getActiveInput();
                        bool trimmable = ((activeinput.getNumPasses() > 0)
                            || (fc.getOp().code() != OpCode.CPUI_CALLIND));
                        if (!activeinput.isFullyChecked())
                            fc.checkInputTrialUse(data, aliascheck);
                        activeinput.finishPass();
                        if (activeinput.getNumPasses() > activeinput.getMaxPass())
                            activeinput.markFullyChecked();
                        else
                            count += 1;     // Count a change, to indicate we still have work to do
                        if (trimmable && activeinput.isFullyChecked()) {
                            if (activeinput.needsFinalCheck())
                                fc.finalInputCheck();
                            fc.resolveModel(activeinput);
                            fc.deriveInputMap(activeinput);
                            fc.buildInputFromTrials(data);
                            fc.clearActiveInput();
                            count += 1;
                        }
                    }
                }
                catch (LowlevelError err) {
                    StringBuilder s = new StringBuilder();
                    s.Append($"Error processing {fc.getName()}");
                    PcodeOp? op = fc.getOp();
                    if (op != (PcodeOp)null)
                        s.Append($" called at {op.getSeqNum()}");
                    s.Append($": {err.ToString()}");
                    throw new LowlevelError(s.ToString());
                }
            }
            return 0;
        }
    }
}
