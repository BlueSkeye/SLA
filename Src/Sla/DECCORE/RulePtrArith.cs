using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RulePtrArith : Rule
    {
        /// \brief Test for other pointers in the ADD tree above the given op that might be a preferred base
        ///
        /// This tests the condition of RulePushPtr on the node immediately above the given putative base pointer
        /// \param op is the given op
        /// \param slot is the input slot of the putative base pointer
        /// \return \b true if the indicated slot holds the preferred pointer
        private static bool verifyPreferredPointer(PcodeOp op, int slot)
        {
            Varnode* vn = op.getIn(slot);
            if (!vn.isWritten()) return true;
            PcodeOp* preOp = vn.getDef();
            if (preOp.code() != CPUI_INT_ADD) return true;
            int preslot = 0;
            if (preOp.getIn(preslot).getTypeReadFacing(preOp).getMetatype() != TYPE_PTR)
            {
                preslot = 1;
                if (preOp.getIn(preslot).getTypeReadFacing(preOp).getMetatype() != TYPE_PTR)
                    return true;
            }
            return (1 != evaluatePointerExpression(preOp, preslot));    // Does earlier varnode look like the base pointer
        }

        public RulePtrArith(string g)
            : base(g, 0, "ptrarith")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePtrArith(getGroup());
        }

        /// \class RulePtrArith
        /// \brief Transform pointer arithmetic
        ///
        /// Rule for converting integer arithmetic to pointer arithmetic.
        /// A string of INT_ADDs is converted into PTRADDs and PTRSUBs.
        ///
        /// Basic algorithm:
        /// Starting with a varnode of known pointer type (with known size):
        ///  - Generate list of terms added to pointer
        ///  - Find all terms that are multiples of pointer size
        ///  - Find all terms that are smaller than pointer size
        ///  - Find sum of constants smaller than pointer size
        ///  - Multiples get converted to PTRADD
        ///  - Constant gets converted to nearest subfield offset
        ///  - Everything else is just added back on
        ///
        /// We need to be wary of most things being in the units of the
        /// space being pointed at. Type calculations are always in bytes
        /// so we need to convert between space units and bytes.
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_ADD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int slot;
            Datatype ct = (Datatype*)0; // Unnecessary initialization

            if (!data.hasTypeRecoveryStarted()) return 0;

            for (slot = 0; slot < op.numInput(); ++slot)
            { // Search for pointer type
                ct = op.getIn(slot).getTypeReadFacing(op);
                if (ct.getMetatype() == TYPE_PTR) break;
            }
            if (slot == op.numInput()) return 0;
            if (evaluatePointerExpression(op, slot) != 2) return 0;
            if (!verifyPreferredPointer(op, slot)) return 0;

            AddTreeState state(data, op, slot);
            if (state.apply()) return 1;
            if (state.initAlternateForm())
            {
                if (state.apply()) return 1;
            }
            return 0;
        }

        /// \brief Determine if the expression rooted at the given INT_ADD operation is ready for conversion
        ///
        /// Converting an expression of INT_ADDs into PTRSUBs and PTRADDs requires that the base pointer
        /// be at the root of the expression tree.  This method evaluates whether given root has the base
        /// pointer at the bottom.  If not, a \e push transform needs to be performed before RulePtrArith can apply.
        /// This method returns a command code:
        ///    -  0 if no action should be taken, the expression is not fully linked or should not be converted
        ///    -  1 if a \e push action should be taken, prior to conversion
        ///    -  2 if the pointer arithmetic conversion can proceed
        /// \param op is the given INT_ADD
        /// \param slot is the index of the pointer
        /// \return the command code
        public static int evaluatePointerExpression(PcodeOp op, int slot)
        {
            int res = 1;       // Assume we are going to push
            int count = 0; // Count descendants
            Varnode* ptrBase = op.getIn(slot);
            if (ptrBase.isFree() && !ptrBase.isConstant())
                return 0;
            if (op.getIn(1 - slot).getTypeReadFacing(op).getMetatype() == TYPE_PTR)
                res = 2;
            Varnode* outVn = op.getOut();
            list<PcodeOp*>::const_iterator iter;
            for (iter = outVn.beginDescend(); iter != outVn.endDescend(); ++iter)
            {
                PcodeOp* decOp = *iter;
                count += 1;
                OpCode opc = decOp.code();
                if (opc == CPUI_INT_ADD)
                {
                    Varnode* otherVn = decOp.getIn(1 - decOp.getSlot(outVn));
                    if (otherVn.isFree() && !otherVn.isConstant())
                        return 0;   // No action if the data-flow isn't fully linked
                    if (otherVn.getTypeReadFacing(decOp).getMetatype() == TYPE_PTR)
                        res = 2;    // Do not push in the presence of other pointers
                }
                else if ((opc == CPUI_LOAD || opc == CPUI_STORE) && decOp.getIn(1) == outVn)
                {   // If use is as pointer for LOAD or STORE
                    if (ptrBase.isSpacebase() && (ptrBase.isInput() || (ptrBase.isConstant())) &&
                        (op.getIn(1 - slot).isConstant()))
                        return 0;
                    res = 2;
                }
                else
                {   // Any other op besides ADD, do not push
                    res = 2;
                }
            }
            if (count == 0)
                return 0;
            if (count > 1)
            {
                if (outVn.isSpacebase())
                    return 0;       // For the RESULT to be a spacebase pointer it must have only 1 descendent
                                    //    res = 2;		// Uncommenting this line will not let pointers get pushed to multiple descendants
            }
            return res;
        }
    }
}
