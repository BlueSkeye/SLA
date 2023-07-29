using ghidra;
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
    internal class RuleConditionalMove : Rule
    {
        /// \brief Class for categorizing and rebuilding a boolean expression
        private class BoolExpress
        {
            /// 0=constant 1=unary 2=binary
            private int4 optype;
            /// OpCode constructing the boolean value
            private OpCode opc;
            /// PcodeOp constructing the boolean value
            private PcodeOp op;
            /// Value (if boolean is constant)
            private uintb val;
            /// First input
            private Varnode in0;
            /// Second input
            private Varnode in1;
            /// Must make a copy of final boolean operation
            private bool mustreconstruct;

            /// Return \b true if boolean is a constant
            public bool isConstant() => (optype==0);

            /// Get the constant boolean value
            public uintb getVal() => val;

            /// Initialize based on output Varnode
            /// Check if given Varnode is a boolean value and break down its construction.
            /// Varnode is assumed to be an input to a MULTIEQUAL
            /// \param vn is the given root Varnode
            /// \return \b true if it is a boolean expression
            public bool initialize(Varnode vn)
            {
                if (!vn.isWritten()) return false;
                op = vn.getDef();
                opc = op.code();
                switch (opc)
                {
                    case CPUI_COPY:
                        in0 = op.getIn(0);
                        if (in0.isConstant())
                        {
                            optype = 0;
                            val = in0.getOffset();
                            return ((val & ~((uintb)1)) == 0);
                        }
                        return false;
                    case CPUI_INT_EQUAL:
                    case CPUI_INT_NOTEQUAL:
                    case CPUI_INT_SLESS:
                    case CPUI_INT_SLESSEQUAL:
                    case CPUI_INT_LESS:
                    case CPUI_INT_LESSEQUAL:
                    case CPUI_INT_CARRY:
                    case CPUI_INT_SCARRY:
                    case CPUI_INT_SBORROW:
                    case CPUI_BOOL_XOR:
                    case CPUI_BOOL_AND:
                    case CPUI_BOOL_OR:
                    case CPUI_FLOAT_EQUAL:
                    case CPUI_FLOAT_NOTEQUAL:
                    case CPUI_FLOAT_LESS:
                    case CPUI_FLOAT_LESSEQUAL:
                    case CPUI_FLOAT_NAN:
                        in0 = op.getIn(0);
                        in1 = op.getIn(1);
                        optype = 2;
                        break;
                    case CPUI_BOOL_NEGATE:
                        in0 = op.getIn(0);
                        optype = 1;
                        break;
                    default:
                        return false;
                }
                return true;
            }

            /// Can this expression be propagated
            /// Evaluate if \b this expression can be easily propagated past a merge point.
            /// Also can the Varnode be used past the merge, or does its value need to be reconstructed.
            /// \param root is the split point
            /// \param branch is the block on which the expression exists and after which is the merge
            /// \return \b true if the expression can be propagated
            public bool evaluatePropagation(FlowBlock root, FlowBlock branch)
            {
                mustreconstruct = false;
                if (optype == 0) return true;   // Constants can always be propagated
                if (root == branch) return true; // Can always propagate if there is no branch
                if (op.getParent() != branch) return true; // Can propagate if value formed before branch
                mustreconstruct = true; // Final op is performed in branch, so it must be reconstructed
                if (in0.isFree() && !in0.isConstant()) return false;
                if (in0.isWritten() && (in0.getDef().getParent() == branch)) return false;
                if (optype == 2)
                {
                    if (in1.isFree() && !in1.isConstant()) return false;
                    if (in1.isWritten() && (in1.getDef().getParent() == branch)) return false;
                }
                return true;
            }

            /// Construct the expression after the merge
            /// Produce the boolean Varnode to use after the merge.
            /// Either reuse the existing Varnode or reconstruct it,
            /// making sure the expression does not depend on data in the branch.
            /// \param insertop is point at which any reconstruction should be inserted
            /// \param data is the function being analyzed
            /// \return the Varnode representing the boolean expression
            public Varnode constructBool(PcodeOp insertop, Funcdata data)
            {
                Varnode* resvn;
                if (mustreconstruct)
                {
                    PcodeOp* newop = data.newOp(optype, op.getAddr()); // Keep the original address
                    data.opSetOpcode(newop, opc);
                    resvn = data.newUniqueOut(1, newop);
                    if (in0.isConstant())
                        in0 = data.newConstant(in0.getSize(), in0.getOffset());
                    data.opSetInput(newop, in0, 0);
                    if (optype == 2)
                    {       // Binary op
                        if (in1.isConstant())
                            in1 = data.newConstant(in1.getSize(), in1.getOffset());
                        data.opSetInput(newop, in1, 1);
                    }
                    data.opInsertBefore(newop, insertop);
                }
                else
                {
                    if (optype == 0)
                        resvn = data.newConstant(1, val);
                    else
                        resvn = op.getOut();
                }
                return resvn;
            }
        }

        /// \brief Construct the boolean negation of a given boolean Varnode
        ///
        /// \param vn is the given Varnode
        /// \param op is the point at which to insert the BOOL_NEGATE op
        /// \param data is the function being analyzed
        /// \return the output of the new op
        private static Varnode constructNegate(Varnode vn, PcodeOp op, Funcdata data)
        {
            PcodeOp* negateop = data.newOp(1, op.getAddr());
            data.opSetOpcode(negateop, CPUI_BOOL_NEGATE);
            Varnode* resvn = data.newUniqueOut(1, negateop);
            data.opSetInput(negateop, vn, 0);
            data.opInsertBefore(negateop, op);
            return resvn;
        }

        public RuleConditionalMove(string g)
            : base(g, 0, "conditionalmove")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleConditionalMove(getGroup());
        }

        /// \class RuleConditionalMove
        /// \brief Simplify various conditional move situations
        ///
        /// The simplest situation is when the code looks like
        /// \code
        /// if (boolcond)
        ///   res0 = 1;
        /// else
        ///   res1 = 0;
        /// res = ? res0 : res1
        /// \endcode
        ///
        /// which gets simplified to `res = zext(boolcond)`
        /// The other major variation looks like
        /// \code
        /// if (boolcond)
        ///    res0 = boolcond;
        /// else
        ///    res1 = differentcond;
        /// res = ? res0 : res1
        /// \endcode
        ///
        /// which gets simplified to `res = boolcond || differentcond`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_MULTIEQUAL);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            BoolExpress bool0;
            BoolExpress bool1;
            BlockBasic* bb;
            FlowBlock* inblock0,*inblock1;
            FlowBlock* rootblock0,*rootblock1;

            if (op.numInput() != 2) return 0; // MULTIEQUAL must have exactly 2 inputs

            if (!bool0.initialize(op.getIn(0))) return 0;
            if (!bool1.initialize(op.getIn(1))) return 0;

            // Look for the situation
            //               inblock0
            //             /         |
            // rootblock .            bb
            //             |         /
            //               inblock1
            //
            // Either inblock0 or inblock1 can be empty
            bb = op.getParent();
            inblock0 = bb.getIn(0);
            if (inblock0.sizeOut() == 1)
            {
                if (inblock0.sizeIn() != 1) return 0;
                rootblock0 = inblock0.getIn(0);
            }
            else
                rootblock0 = inblock0;
            inblock1 = bb.getIn(1);
            if (inblock1.sizeOut() == 1)
            {
                if (inblock1.sizeIn() != 1) return 0;
                rootblock1 = inblock1.getIn(0);
            }
            else
                rootblock1 = inblock1;
            if (rootblock0 != rootblock1) return 0;

            // rootblock must end in CBRANCH, which gives the boolean for the conditional move
            PcodeOp* cbranch = rootblock0.lastOp();
            if (cbranch == (PcodeOp*)0) return 0;
            if (cbranch.code() != CPUI_CBRANCH) return 0;

            if (!bool0.evaluatePropagation(rootblock0, inblock0)) return 0;
            if (!bool1.evaluatePropagation(rootblock0, inblock1)) return 0;

            bool path0istrue;
            if (rootblock0 != inblock0)
                path0istrue = (rootblock0.getTrueOut() == inblock0);
            else
                path0istrue = (rootblock0.getTrueOut() != inblock1);
            if (cbranch.isBooleanFlip())
                path0istrue = !path0istrue;

            if (!bool0.isConstant() && !bool1.isConstant())
            {
                if (inblock0 == rootblock0)
                {
                    Varnode* boolvn = cbranch.getIn(1);
                    bool andorselect = path0istrue;
                    // Force 0 branch to either be boolvn OR !boolvn
                    if (boolvn != op.getIn(0))
                    {
                        if (!boolvn.isWritten()) return 0;
                        PcodeOp* negop = boolvn.getDef();
                        if (negop.code() != CPUI_BOOL_NEGATE) return 0;
                        if (negop.getIn(0) != op.getIn(0)) return 0;
                        andorselect = !andorselect;
                    }
                    OpCode opc = andorselect ? CPUI_BOOL_OR : CPUI_BOOL_AND;
                    data.opUninsert(op);
                    data.opSetOpcode(op, opc);
                    data.opInsertBegin(op, bb);
                    Varnode* firstvn = bool0.constructBool(op, data);
                    Varnode* secondvn = bool1.constructBool(op, data);
                    data.opSetInput(op, firstvn, 0);
                    data.opSetInput(op, secondvn, 1);
                    return 1;
                }
                else if (inblock1 == rootblock0)
                {
                    Varnode* boolvn = cbranch.getIn(1);
                    bool andorselect = !path0istrue;
                    // Force 1 branch to either be boolvn OR !boolvn
                    if (boolvn != op.getIn(1))
                    {
                        if (!boolvn.isWritten()) return 0;
                        PcodeOp* negop = boolvn.getDef();
                        if (negop.code() != CPUI_BOOL_NEGATE) return 0;
                        if (negop.getIn(0) != op.getIn(1)) return 0;
                        andorselect = !andorselect;
                    }
                    data.opUninsert(op);
                    OpCode opc = andorselect ? CPUI_BOOL_OR : CPUI_BOOL_AND;
                    data.opSetOpcode(op, opc);
                    data.opInsertBegin(op, bb);
                    Varnode* firstvn = bool1.constructBool(op, data);
                    Varnode* secondvn = bool0.constructBool(op, data);
                    data.opSetInput(op, firstvn, 0);
                    data.opSetInput(op, secondvn, 1);
                    return 1;
                }
                return 0;
            }

            // Below here some change is being made
            data.opUninsert(op);    // Changing from MULTIEQUAL, this should be reinserted
            int4 sz = op.getOut().getSize();
            if (bool0.isConstant() && bool1.isConstant())
            {
                if (bool0.getVal() == bool1.getVal())
                {
                    data.opRemoveInput(op, 1);
                    data.opSetOpcode(op, CPUI_COPY);
                    data.opSetInput(op, data.newConstant(sz, bool0.getVal()), 0);
                    data.opInsertBegin(op, bb);
                }
                else
                {
                    data.opRemoveInput(op, 1);
                    Varnode* boolvn = cbranch.getIn(1);
                    bool needcomplement = ((bool0.getVal() == 0) == path0istrue);
                    if (sz == 1)
                    {
                        if (needcomplement)
                            data.opSetOpcode(op, CPUI_BOOL_NEGATE);
                        else
                            data.opSetOpcode(op, CPUI_COPY);
                        data.opInsertBegin(op, bb);
                        data.opSetInput(op, boolvn, 0);
                    }
                    else
                    {
                        data.opSetOpcode(op, CPUI_INT_ZEXT);
                        data.opInsertBegin(op, bb);
                        if (needcomplement)
                            boolvn = constructNegate(boolvn, op, data);
                        data.opSetInput(op, boolvn, 0);
                    }
                }
            }
            else if (bool0.isConstant())
            {
                bool needcomplement = (path0istrue != (bool0.getVal() != 0));
                OpCode opc = (bool0.getVal() != 0) ? CPUI_BOOL_OR : CPUI_BOOL_AND;
                data.opSetOpcode(op, opc);
                data.opInsertBegin(op, bb);
                Varnode* boolvn = cbranch.getIn(1);
                if (needcomplement)
                    boolvn = constructNegate(boolvn, op, data);
                Varnode* body1 = bool1.constructBool(op, data);
                data.opSetInput(op, boolvn, 0);
                data.opSetInput(op, body1, 1);
            }
            else
            {           // bool1 must be constant
                bool needcomplement = (path0istrue == (bool1.getVal() != 0));
                OpCode opc = (bool1.getVal() != 0) ? CPUI_BOOL_OR : CPUI_BOOL_AND;
                data.opSetOpcode(op, opc);
                data.opInsertBegin(op, bb);
                Varnode* boolvn = cbranch.getIn(1);
                if (needcomplement)
                    boolvn = constructNegate(boolvn, op, data);
                Varnode* body0 = bool0.constructBool(op, data);
                data.opSetInput(op, boolvn, 0);
                data.opSetInput(op, body0, 1);
            }
            return 1;
        }
    }
}
