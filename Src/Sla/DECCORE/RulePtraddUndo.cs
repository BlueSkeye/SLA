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
    internal class RulePtraddUndo : Rule
    {
        public RulePtraddUndo(string g)
            : base(g, 0, "ptraddundo")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePtraddUndo(getGroup());
        }

        /// \class RulePtraddUndo
        /// \brief Remove PTRADD operations with mismatched data-type information
        ///
        /// It is possible for Varnodes to be assigned incorrect types in the
        /// middle of simplification. This leads to incorrect PTRADD conversions.
        /// Once the correct type is found, the PTRADD must be converted back to an INT_ADD.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_PTRADD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* basevn;
            TypePointer* tp;

            if (!data.hasTypeRecoveryStarted()) return 0;
            int size = (int)op.getIn(2).getOffset(); // Size the PTRADD thinks we are pointing
            basevn = op.getIn(0);
            tp = (TypePointer*)basevn.getTypeReadFacing(op);
            if (tp.getMetatype() == TYPE_PTR)                              // Make sure we are still a pointer
                if (tp.getPtrTo().getSize() == AddrSpace::addressToByteInt(size, tp.getWordSize()))
                {   // of the correct size
                    Varnode* indVn = op.getIn(1);
                    if ((!indVn.isConstant()) || (indVn.getOffset() != 0))                    // and that index isn't zero
                        return 0;
                }

            data.opUndoPtradd(op, false);
            return 1;
        }
    }
}
