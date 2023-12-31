﻿using Sla.CORE;

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
            return (vn.isAddrTied() || vn.getSpace().getType() == spacetype.IPTR_INTERNAL)
                ? data.newUniqueOut(vn.getSize(), op)
                : data.newVarnodeOut(vn.getSize(), vn.getAddr(), op);
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
            while(true) {
                if (!vn.isWritten()) return;
                if (vn.isAutoLive()) return;
                if (vn.loneDescend() == (PcodeOp)null) return;   // Already has multiple descendants
                PcodeOp op = vn.getDef() ?? throw new ApplicationException();
                OpCode opc = op.code();
                if (opc == OpCode.CPUI_INT_ZEXT || opc == OpCode.CPUI_INT_SEXT || opc == OpCode.CPUI_INT_2COMP)
                    reslist.Add(op);
                else if (opc == OpCode.CPUI_INT_MULT) {
                    if (op.getIn(1).isConstant())
                        reslist.Add(op);
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

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null : new RulePushPtr(getGroup());
        }

        /// \class RulePushPtr
        /// \brief Push a Varnode with known pointer data-type to the bottom of its additive expression
        ///
        /// This is part of the normalizing process for pointer expressions. The pointer should be added last
        /// onto the expression calculating the offset into its data-type.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_ADD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int slot;
            Varnode? vni = (Varnode)null;

            if (!data.hasTypeRecoveryStarted()) return 0;
            for (slot = 0; slot < op.numInput(); ++slot) {
                // Search for pointer type
                vni = op.getIn(slot);
                if (vni.getTypeReadFacing(op).getMetatype() == type_metatype.TYPE_PTR) break;
            }
            if (slot == op.numInput()) return 0;

            if (RulePtrArith.evaluatePointerExpression(op, slot) != 1) return 0;
            Varnode vn = op.getOut();
            Varnode vnadd2 = op.getIn(1 - slot);
            List<PcodeOp> duplicateList = new List<PcodeOp>();
            if (vn.loneDescend() == (PcodeOp)null)
                collectDuplicateNeeds(duplicateList, vnadd2);

            while(true) {
                IEnumerator<PcodeOp> iter = vn.beginDescend();
                if (iter == vn.endDescend()) break;
                PcodeOp decop = iter.Current;
                int j = decop.getSlot(vn);

                Varnode vnadd1 = decop.getIn(1 - j);
                Varnode newout;

                // Create new INT_ADD for the intermediate result that didn't exist in original code.
                // We don't associate it with the address of the original INT_ADD
                // We don't preserve the Varnode address of the original INT_ADD
                PcodeOp newop = data.newOp(2, decop.getAddr());       // Use the later address
                data.opSetOpcode(newop, OpCode.CPUI_INT_ADD);
                newout = data.newUniqueOut(vnadd1.getSize(), newop);   // Use a temporary storage address

                data.opSetInput(decop, vni, 0);
                data.opSetInput(decop, newout, 1);

                data.opSetInput(newop, vnadd1, 0);
                data.opSetInput(newop, vnadd2, 1);

                data.opInsertBefore(newop, decop);
            }
            if (!vn.isAutoLive())
                data.opDestroy(op);
            for (int i = 0; i < duplicateList.size(); ++i)
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
            Varnode outVn = op.getOut();
            Varnode inVn = op.getIn(0);
            int num = op.numInput();
            OpCode opc = op.code();
            IEnumerator<PcodeOp> iter = outVn.beginDescend();
            do {
                PcodeOp decOp = iter.Current;
                int slot = decOp.getSlot(outVn);
                PcodeOp newOp = data.newOp(num, op.getAddr());    // Duplicate op associated with original address
                Varnode newOut = buildVarnodeOut(outVn, newOp, data);  // Result contained in original storage
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
