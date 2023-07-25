using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Prepare function prototypes for "normalize" simplification.
    ///
    /// The "normalize" simplification style has the fundamental requirement that the input parameter
    /// types must not be locked, as locking can cause changes in the data-flow that "normalize" is
    /// trying to normalize, because:
    ///   1)  The decompiler views locking as useful aliasing information
    ///   2)  Locking forces varnodes to exist up-front, which can affect subflow analysis
    ///   3)  ... probably other differences
    ///
    /// This action removes any input symbols on the function, locked or otherwise,
    /// Similarly there should be no lock on the output and no lock on the prototype model
    internal class ActionNormalizeSetup : Action
    {
        public ActionNormalizeSetup(string g)
            : base(rule_onceperfunc,"normalizesetup", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionNormalizeSetup(getGroup());
        }

        public override int apply(Funcdata data)
        {
            FuncProto & fp(data.getFuncProto());
            fp.clearInput();
            fp.setModelLock(false); // This will cause the model to get reevaluated
            fp.setOutputLock(false);

            // FIXME:  This should probably save and restore symbols, model, and state
            //   If we are calculating normalized trees in console mode, this currently eliminates locks
            //   that may be needed by other normalizing calculations
            return 0;
        }
    }
}
