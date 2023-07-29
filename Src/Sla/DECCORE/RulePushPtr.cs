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
    internal class RulePushPtr : Rule
    {
        /// \brief Build a duplicate of the given Varnode as an output of a PcodeOp, preserving the storage address if possible
        ///
        /// If the Varnode is already a \e unique or is \e addrtied
        /// \param vn is the given Varnode
        /// \param op is the PcodeOp to which the duplicate should be an output
        /// \param data is the function to add the duplicate to
        /// \return the duplicate Varnode
        private static Varnode buildVarnodeOut(Varnode vn, PcodeOp op, Funcdata data)
        {
            if (vn.isAddrTied() || vn.getSpace().getType() == IPTR_INTERNAL)
                return data.newUniqueOut(vn.getSize(), op);
            return data.newVarnodeOut(vn.getSize(), vn.getAddr(), op);
        }

        /// \brief Generate list of PcodeOps that need to be duplicated as part of pushing the pointer
        ///
        /// If the pointer INT_ADD is duplicated as part of the push, some of the operations building
        /// the offset to the pointer may also need to be duplicated.  Identify these and add them
        /// to the result list.
        /// \param reslist is the result list to be populated
        /// \param vn is the offset Varnode being added to the pointer
        private static void collectDuplicateNeeds(List<PcodeOp> reslist, Varnode vn)
        {
            for (; ; )
            {
                if (!vn.isWritten()) return;
                if (vn.isAutoLive()) return;
                if (vn.loneDescend() == (PcodeOp*)0) return;   // Already has multiple descendants
                PcodeOp* op = vn.getDef();
                OpCode opc = op.code();
                if (opc == CPUI_INT_ZEXT || opc == CPUI_INT_SEXT || opc == CPUI_INT_2COMP)
                    reslist.push_back(op);
                else if (opc == CPUI_INT_MULT)
                {
                    if (op.getIn(1).isConstant())
                        reslist.push_back(op);
                }
                else
                    return;
                vn = op.getIn(0);
            }
        }

        public RulePushPtr(string g)
            : base(g, 0, "pushptr")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePushPtr(getGroup());
        }

        /// \class RulePushPtr
        /// \brief Push a Varnode with known pointer data-type to the bottom of its additive expression
        ///
        /// This is part of the normalizing process for pointer expressions. The pointer should be added last
        /// onto the expression calculating the offset into its data-type.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_ADD);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            int4 slot;
            Varnode* vni = (Varnode*)0;

            if (!data.hasTypeRecoveryStarted()) return 0;
            for (slot = 0; slot < op.numInput(); ++slot)
            { // Search for pointer type
                vni = op.getIn(slot);
                if (vni.getTypeReadFacing(op).getMetatype() == TYPE_PTR) break;
            }
            if (slot == op.numInput()) return 0;

            if (RulePtrArith::evaluatePointerExpression(op, slot) != 1) return 0;
            Varnode* vn = op.getOut();
            Varnode* vnadd2 = op.getIn(1 - slot);
            List<PcodeOp*> duplicateList;
            if (vn.loneDescend() == (PcodeOp*)0)
                collectDuplicateNeeds(duplicateList, vnadd2);

            for (; ; )
            {
                list<PcodeOp*>::const_iterator iter = vn.beginDescend();
                if (iter == vn.endDescend()) break;
                PcodeOp* decop = *iter;
                int4 j = decop.getSlot(vn);

                Varnode* vnadd1 = decop.getIn(1 - j);
                Varnode* newout;

                // Create new INT_ADD for the intermediate result that didn't exist in original code.
                // We don't associate it with the address of the original INT_ADD
                // We don't preserve the Varnode address of the original INT_ADD
                PcodeOp* newop = data.newOp(2, decop.getAddr());       // Use the later address
                data.opSetOpcode(newop, CPUI_INT_ADD);
                newout = data.newUniqueOut(vnadd1.getSize(), newop);   // Use a temporary storage address

                data.opSetInput(decop, vni, 0);
                data.opSetInput(decop, newout, 1);

                data.opSetInput(newop, vnadd1, 0);
                data.opSetInput(newop, vnadd2, 1);

                data.opInsertBefore(newop, decop);
            }
            if (!vn.isAutoLive())
                data.opDestroy(op);
            for (int4 i = 0; i < duplicateList.size(); ++i)
                duplicateNeed(duplicateList[i], data);

            return 1;
        }

        /// \brief Duplicate the given PcodeOp so that the outputs have only 1 descendant
        ///
        /// Run through the descendants of the PcodeOp output and create a duplicate
        /// of the PcodeOp right before the descendant.  We assume the PcodeOp either has
        /// a single input, or has 2 inputs where the second is a constant.
        /// The (original) PcodeOp is destroyed.
        /// \param op is the given PcodeOp to duplicate
        /// \param data is function to build duplicates in
        public static void duplicateNeed(PcodeOp op, Funcdata data)
        {
            Varnode* outVn = op.getOut();
            Varnode* inVn = op.getIn(0);
            int num = op.numInput();
            OpCode opc = op.code();
            list<PcodeOp*>::const_iterator iter = outVn.beginDescend();
            do
            {
                PcodeOp* decOp = *iter;
                int4 slot = decOp.getSlot(outVn);
                PcodeOp* newOp = data.newOp(num, op.getAddr());    // Duplicate op associated with original address
                Varnode* newOut = buildVarnodeOut(outVn, newOp, data);  // Result contained in original storage
                newOut.updateType(outVn.getType(), false, false);
                data.opSetOpcode(newOp, opc);
                data.opSetInput(newOp, inVn, 0);
                if (num > 1)
                    data.opSetInput(newOp, op.getIn(1), 1);
                data.opSetInput(decOp, newOut, slot);
                data.opInsertBefore(newOp, decOp);
                iter = outVn.beginDescend();
            } while (iter != outVn.endDescend());
            data.opDestroy(op);
        }
    }
}
