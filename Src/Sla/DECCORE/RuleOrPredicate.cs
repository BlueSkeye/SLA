using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Simplify predication constructions involving the INT_OR operator
    ///
    /// In this form of predication, two variables are set based on a condition and then ORed together.
    /// Both variables may be set to zero, or to some other value, based on the condition
    /// and the zero values are such that at least one of the variables is zero.
    /// \code
    ///     tmp1 = cond ? val1 : 0;
    ///     tmp2 = cond ?  0 : val2;
    ///     result = tmp1 | tmp2;
    /// \endcode
    /// The RuleOrPredicate simplifies this to
    /// \code
    ///     if (cond) result = val1; else result = val2;
    /// \endcode
    /// or to be precise
    /// \code
    ///     newtmp = val1 ? val2;			// Using a new MULTIEQUAL
    ///     result = newtmp;
    /// \endcode
    /// In an alternate form we have
    /// \code
    ///     tmp1 = (val2 == 0) ? val1 : 0
    ///     result = tmp1 | val2;
    /// \endcode
    /// again, one of val1 or val2 must be zero, so this gets replaced with
    /// \code
    ///     tmp1 = val1 ? val2
    ///     result = tmp1
    /// \endcode
    internal class RuleOrPredicate : Rule
    {
        /// \brief A helper class to mark up predicated INT_OR expressions
        private struct MultiPredicate
        {
            /// Base MULTIEQUAL op
            private PcodeOp op;
            /// Input slot containing path that sets zero
            private int zeroSlot;
            /// Final block in path that sets zero
            private FlowBlock zeroBlock;
            /// Conditional block determining if zero is set or not
            private FlowBlock condBlock;
            /// CBRANCH determining if zero is set
            private PcodeOp cbranch;
            /// Other (non-zero) Varnode getting set on other path
            private Varnode otherVn;
            /// True if path to zero set is the \b true path out of condBlock
            private bool zeroPathIsTrue;

            /// \brief  Check if \b vn is produced by a 2-branch MULTIEQUAL, one side of which is a zero constant
            /// \param vn is the given Varnode
            /// \return \b true if the expression producing \b vn matches the form
            internal bool discoverZeroSlot(Varnode vn)
            {
                if (!vn.isWritten()) {
                    return false;
                }
                op = vn.getDef();
                if (op.code() != OpCode.CPUI_MULTIEQUAL) {
                    return false;
                }
                if (op.numInput() != 2) {
                    return false;
                }
                for (zeroSlot = 0; zeroSlot < 2; ++zeroSlot) {
                    Varnode tmpvn = op.getIn(zeroSlot);
                    if (!tmpvn.isWritten()) {
                        continue;
                    }
                    PcodeOp copyop = tmpvn.getDef();
                    if (copyop.code() != OpCode.CPUI_COPY) {
                        // Multiequal must have OpCode.CPUI_COPY input
                        continue;
                    }
                    Varnode zerovn = copyop.getIn(0);
                    if (!zerovn.isConstant()) {
                        continue;
                    }
                    if (zerovn.getOffset() != 0) {
                        // which copies #0
                        continue;
                    }
                    // store off varnode from other path
                    otherVn = op.getIn(1 - zeroSlot);
                    if (otherVn.isFree()) {
                        return false;
                    }
                    return true;
                }
                return false;
            }

            /// \brief Find CBRANCH operation that determines whether zero is set or not
            /// Assuming that \b op is a 2-branch MULTIEQUAL as per discoverZeroSlot(),
            /// try to find a single CBRANCH whose two \b out edges correspond to the
            /// \b in edges of the MULTIEQUAL. In this case, the boolean expression
            /// controlling the CBRANCH is also controlling whether zero flows into
            /// the MULTIEQUAL output Varnode.
            /// \return \b true if a single controlling CBRANCH is found
            internal bool discoverCbranch()
            {
                FlowBlock baseBlock = op.getParent();
                zeroBlock = baseBlock.getIn(zeroSlot);
                FlowBlock otherBlock = baseBlock.getIn(1 - zeroSlot);
                if (zeroBlock.sizeOut() == 1) {
                    if (zeroBlock.sizeIn() != 1) {
                        return false;
                    }
                    condBlock = zeroBlock.getIn(0);
                }
                else if (zeroBlock.sizeOut() == 2) {
                    condBlock = zeroBlock;
                }
                else {
                    return false;
                }
                if (condBlock.sizeOut() != 2) {
                    return false;
                }
                if (otherBlock.sizeOut() == 1) {
                    if (otherBlock.sizeIn() != 1) {
                        return false;
                    }
                    if (condBlock != otherBlock.getIn(0)) {
                        return false;
                    }
                }
                else if (otherBlock.sizeOut() == 2) {
                    if (condBlock != otherBlock) {
                        return false;
                    }
                }
                else {
                    return false;
                }
                cbranch = condBlock.lastOp();
                if (cbranch == null) {
                    return false;
                }
                return (cbranch.code() == OpCode.CPUI_CBRANCH);
            }

            /// \brief Does the \b condBlock \b true outgoing edge flow to the block that sets zero
            /// The \b zeroPathIsTrue variable is set based on the current configuration
            internal void discoverPathIsTrue()
            {
                if (condBlock.getTrueOut() == zeroBlock) {
                    zeroPathIsTrue = true;
                }
                else if (condBlock.getFalseOut() == zeroBlock) {
                    zeroPathIsTrue = false;
                }
                else {
                    // condBlock must be zeroBlock
                    // True if "true" path does not override zero set
                    zeroPathIsTrue = (condBlock.getTrueOut() == op.getParent());
                }
            }

            /// \brief Verify that CBRANCH boolean expression is either (\b vn == 0) or (\b vn != 0)
            /// Modify \b zeroPathIsTrue so that if it is \b true, then: A \b vn value equal to zero,
            /// causes execution to flow to where the output of MULTIEQUAL is set to zero.
            /// \param vn is the given Varnode
            /// \return \b true if the boolean expression has a matching form
            internal bool discoverConditionalZero(Varnode vn)
            {
                Varnode boolvn = cbranch.getIn(1);
                if (!boolvn.isWritten()) {
                    return false;
                }
                PcodeOp compareop = boolvn.getDef();
                OpCode opc = compareop.code();
                if (opc == OpCode.CPUI_INT_NOTEQUAL) {
                    // Verify that CBRANCH depends on INT_NOTEQUAL
                    zeroPathIsTrue = !zeroPathIsTrue;
                }
                else if (opc != OpCode.CPUI_INT_EQUAL) {
                    // or INT_EQUAL
                    return false;
                }
                Varnode a1 = compareop.getIn(0);
                Varnode a2 = compareop.getIn(1);
                Varnode zerovn;
                if (a1 == vn) {
                    // Verify one side of compare is vn
                    zerovn = a2;
                }
                else if (a2 == vn) {
                    zerovn = a1;
                }
                else {
                    return false;
                }
                if (!zerovn.isConstant()) {
                    return false;
                }
                if (zerovn.getOffset() != 0) {
                    return false;
                }
                // Verify we are comparing to zero
                if (cbranch.isBooleanFlip()) {
                    zeroPathIsTrue = !zeroPathIsTrue;
                }
                return true;
            }
        }

        /// \brief Check for the \e alternate form, tmp1 = (val2 == 0) ? val1 : 0;
        /// We know we have the basic form
        /// \code
        ///     tmp1 = cond ?  val1 : 0;
        ///     result = tmp1 | other;
        /// \endcode
        /// So we just need to check that \b other plays the role of \b val2.
        /// If we match the \e alternate form, perform the simplification
        /// \param vn is the candidate \b other Varnode
        /// \param branch holds the basic form
        /// \param op is the INT_OR p-code op
        /// \param data is the function being analyzed
        /// \return 1 if the form was matched and simplified, 0 otherwise
        private int checkSingle(Varnode vn, MultiPredicate branch, PcodeOp op, Funcdata data)
        {
            if (vn.isFree()) {
                return 0;
            }
            if (!branch.discoverCbranch()) {
                return 0;
            }
            if (branch.op.getOut().loneDescend() != op) {
                // Must only be one use of MULTIEQUAL, because we rewrite it
                return 0;
            }
            branch.discoverPathIsTrue();
            if (!branch.discoverConditionalZero(vn)) {
                return 0;
            }
            if (branch.zeroPathIsTrue) {
                // true condition (vn == 0) must not go to zero set
                return 0;
            }
            data.opSetInput(branch.op, vn, branch.zeroSlot);
            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            data.opSetInput(op, branch.op.getOut(), 0);
            return 1;
        }

        ///< Constructor
        public RuleOrPredicate(string g)
            : base(g, 0, "orpredicate")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return null;
            return new RuleOrPredicate(getGroup());
        }
    
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_OR);
            oplist.Add(CPUI_INT_XOR);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            MultiPredicate branch0;
            MultiPredicate branch1;
            bool test0 = branch0.discoverZeroSlot(op.getIn(0));
            bool test1 = branch1.discoverZeroSlot(op.getIn(1));
            if ((test0 == false) && (test1 == false)) {
                return 0;
            }
            if (!test0) {
                // branch1 has MULTIEQUAL form, but branch0 does not
                return checkSingle(op.getIn(0), branch1, op, data);
            }
            else if (!test1) {
                // branch0 has MULTIEQUAL form, but branch1 does not
                return checkSingle(op.getIn(1), branch0, op, data);
            }
            if (!branch0.discoverCbranch()) {
                return 0;
            }
            if (!branch1.discoverCbranch()) {
                return 0;
            }
            if (branch0.condBlock == branch1.condBlock) {
                if (branch0.zeroBlock == branch1.zeroBlock) {
                    // zero sets must be along different paths
                    return 0;
                }
            }
            else {
                // Make sure cbranches have shared condition and the different zero sets have complementary paths
                ConditionMarker condmarker;
                if (!condmarker.verifyCondition(branch0.cbranch, branch1.cbranch)) {
                    return 0;
                }
                if (condmarker.getMultiSlot() != -1) {
                    return 0;
                }
                branch0.discoverPathIsTrue();
                branch1.discoverPathIsTrue();
                bool finalBool = branch0.zeroPathIsTrue == branch1.zeroPathIsTrue;
                if (condmarker.getFlip()) {
                    finalBool = !finalBool;
                }
                if (finalBool) {
                    // One path hits both zero sets, they must be on different paths
                    return 0;
                }
            }
            int order = branch0.op.compareOrder(branch1.op);
            if (order == 0) {
                // can this happen?
                return 0;
            }
            BlockBasic finalBlock;
            // True if non-zero setting of branch0 flows throw slot0
            bool slot0SetsBranch0;
            if (order < 0) {
                // branch1 happens after
                finalBlock = branch1.op.getParent();
                slot0SetsBranch0 = branch1.zeroSlot == 0;
            }
            else {
                // branch0 happens after
                finalBlock = branch0.op.getParent();
                slot0SetsBranch0 = branch0.zeroSlot == 1;
            }
            PcodeOp newMulti = data.newOp(2, finalBlock.getStart());
            data.opSetOpcode(newMulti, OpCode.CPUI_MULTIEQUAL);
            if (slot0SetsBranch0) {
                data.opSetInput(newMulti, branch0.otherVn, 0);
                data.opSetInput(newMulti, branch1.otherVn, 1);
            }
            else {
                data.opSetInput(newMulti, branch1.otherVn, 0);
                data.opSetInput(newMulti, branch0.otherVn, 1);
            }
            Varnode newvn = data.newUniqueOut(branch0.otherVn.getSize(), newMulti);
            data.opInsertBegin(newMulti, finalBlock);
            data.opRemoveInput(op, 1);
            data.opSetInput(op, newvn, 0);
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            return 1;
        }
    }
}
