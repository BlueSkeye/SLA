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
    internal class RuleBoolNegate : Rule
    {
        public RuleBoolNegate(string g)
            : base(g, 0, "boolnegate")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleBoolNegate(getGroup());
        }

        /// \class RuleBoolNegate
        /// \brief Apply a set of identities involving BOOL_NEGATE
        ///
        /// The identities include:
        ///  - `!!V  =>  V`
        ///  - `!(V == W)  =>  V != W`
        ///  - `!(V < W)   =>  W <= V`
        ///  - `!(V <= W)  =>  W < V`
        ///  - `!(V != W)  =>  V == W`
        ///
        /// This supports signed and floating-point variants as well
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_BOOL_NEGATE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn;
            PcodeOp* flip_op;
            OpCode opc;
            bool flipyes;

            vn = op.getIn(0);
            if (!vn.isWritten()) return 0;
            flip_op = vn.getDef();

            list<PcodeOp*>::const_iterator iter;

            // ALL descendants must be negates
            for (iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
                if ((*iter).code() != CPUI_BOOL_NEGATE) return 0;

            opc = get_booleanflip(flip_op.code(), flipyes);
            if (opc == CPUI_MAX) return 0;
            data.opSetOpcode(flip_op, opc); // Set the negated opcode
            if (flipyes)            // Do we need to reverse the two operands
                data.opSwapInput(flip_op, 0, 1);
            for (iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
                data.opSetOpcode(*iter, CPUI_COPY); // Remove all the negates
            return 1;
        }
    }
}
