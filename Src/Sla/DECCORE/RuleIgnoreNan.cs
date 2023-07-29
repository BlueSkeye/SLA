using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleIgnoreNan : Rule
    {
        public RuleIgnoreNan(string g)
            : base(g, 0, "ignorenan")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleIgnoreNan(getGroup());
        }

        /// \class RuleIgnoreNan
        /// \brief Treat FLOAT_NAN as always evaluating to false
        ///
        /// This makes the assumption that all floating-point calculations
        /// give valid results (not NaN).
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_FLOAT_NAN);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (op.numInput() == 2)
                data.opRemoveInput(op, 1);

            // Treat these operations as always returning false (0)
            data.opSetOpcode(op, CPUI_COPY);
            data.opSetInput(op, data.newConstant(1, 0), 0);
            return 1;
        }
    }
}
