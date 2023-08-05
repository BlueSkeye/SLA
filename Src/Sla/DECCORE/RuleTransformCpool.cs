using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleTransformCpool : Rule
    {
        public RuleTransformCpool(string g)
            : base(g, 0, "transformcpool")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleTransformCpool(getGroup());
        }

        /// \class RuleTransformCpool
        /// \brief Transform CPOOLREF operations by looking up the value in the constant pool
        ///
        /// If a reference into the constant pool is a constant, convert the CPOOLREF to
        /// a COPY of the constant.  Otherwise just append the type id of the reference to the top.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_CPOOLREF);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (op.isCpoolTransformed()) return 0;     // Already visited
            data.opMarkCpoolTransformed(op);    // Mark our visit
            List<ulong> refs;
            for (int i = 1; i < op.numInput(); ++i)
                refs.Add(op.getIn(i).getOffset());
            CPoolRecord rec = data.getArch().cpool.getRecord(refs);    // Recover the record
            if (rec != (CPoolRecord*)0) {
                if (rec.getTag() == CPoolRecord::instance_of)
                {
                    data.opMarkCalculatedBool(op);
                }
                else if (rec.getTag() == CPoolRecord::primitive)
                {
                    int sz = op.getOut().getSize();
                    Varnode* cvn = data.newConstant(sz, rec.getValue() & Globals.calc_mask(sz));
                    cvn.updateType(rec.getType(), true, true);
                    while (op.numInput() > 1)
                    {
                        data.opRemoveInput(op, op.numInput() - 1);
                    }
                    data.opSetOpcode(op, OpCode.CPUI_COPY);
                    data.opSetInput(op, cvn, 0);
                    return 1;
                }
                data.opInsertInput(op, data.newConstant(4, rec.getTag()), op.numInput());
            }
            return 1;
        }
    }
}
