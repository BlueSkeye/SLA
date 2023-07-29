using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Normalize jump-table construction.
    ///
    /// This involves folding switch variable normalization and the \b guard instructions into
    /// the \b switch action. The case labels are also calculated based on the normalization.
    internal class ActionSwitchNorm : Action
    {
        public ActionSwitchNorm(string g)
            : base(0,"switchnorm", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionSwitchNorm(getGroup());
        }

        public override int apply(Funcdata data)
        {
            for (int i = 0; i < data.numJumpTables(); ++i)
            {
                JumpTable* jt = data.getJumpTable(i);
                if (!jt.isLabelled())
                {
                    if (jt.recoverLabels(&data))
                    { // Recover case statement labels
                      // If this returns true, the jumptable was not fully recovered during flow analysis
                      // So we need to issue a restart
                        data.getOverride().insertMultistageJump(jt.getOpAddress());
                        data.setRestartPending(true);
                    }
                    jt.foldInNormalization(&data);
                    count += 1;
                }
                if (jt.foldInGuards(&data))
                {
                    data.getStructure().clear();    // Make sure we redo structure
                    count += 1;
                }
            }
            return 0;
        }
    }
}
