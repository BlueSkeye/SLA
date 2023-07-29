using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A helper class for describing the similarity of the boolean condition between 2 CBRANCH operations
    ///
    /// This class determines if two CBRANCHs share the same condition.  It also determines if the conditions
    /// are complements of each other, and/or they are shared along only one path.
    ///
    /// The expression computing the root boolean value for one CBRANCH is marked out
    /// by setupInitOp(). For the other CBRANCH, findMatch() tries to find common Varnode
    /// in its boolean expression and then maps a critical path from the Varnode to the final boolean.
    /// Assuming the common Varnode exists, the method finalJudgement() decides if the two boolean values
    /// are the same, uncorrelated, or complements of one another.
    internal class ConditionMarker
    {
        /// The root CBRANCH operation to compare against
        private PcodeOp? initop;
        /// The boolean Varnode on which the root CBRANCH keys
        private Varnode? basevn;
        /// If \b basevn is defined by BOOL_NEGATE, this is the unnegated Varnode
        private Varnode? boolvn;
        /// If the first param to \b binaryop is defined by BOOL_NEGATE, this is the unnegated Varnode
        private Varnode? bool2vn;
        /// If the second param to \b binaryop is defined by BOOL_NEGATE, this is the unnegated Varnode
        private Varnode? bool3vn;
        /// The binary operator producing the root boolean (if non-null)
        private PcodeOp? binaryop;

        /// True if the compared CBRANCH keys on the opposite boolean value of the root
        private bool matchflip;
        /// Depth of critical path
        private int state;
        /// p-code operations along the critical path
        private PcodeOp[] opstate = new PcodeOp[2];
        /// Boolean negation along the critical path
        private bool[] flipstate = new bool[2];
        /// Input Varnode to follow to stay on critical path
        private int[] slotstate = new int[2];
        /// True if MULTIEQUAL used in condition
        private bool multion;
        /// True if a binary operator is used in condition
        private bool binon;
        /// Input slot of MULTIEQUAL on critical path, -1 if no MULTIEQUAL
        private int multislot;

        /// Map out the root boolean expression
        /// Starting with the CBRANCH, the key Varnodes in the expression producing
        /// the boolean value are marked.  BOOL_NEGATE operations are traversed, but
        /// otherwise only one level of operator is walked.
        /// \param op is the root CBRANCH operation
        private void setupInitOp(PcodeOp op)
        {
            initop = op;
            basevn = op.getIn(1);
            Varnode curvn = basevn;
            curvn.setMark();
            if (curvn.isWritten()) {
                PcodeOp tmp = curvn.getDef();
                if (tmp.code() == CPUI_BOOL_NEGATE) {
                    boolvn = tmp.getIn(0);
                    curvn = boolvn;
                    curvn.setMark();
                }
            }
            if (curvn.isWritten()) {
                PcodeOp tmp = curvn.getDef();
                if (tmp.isBoolOutput() && (tmp.getEvalType() == PcodeOp::binary)) {
                    binaryop = tmp;
                    Varnode binvn = binaryop.getIn(0);
                    if (!binvn.isConstant()) {
                        if (binvn.isWritten()) {
                            PcodeOp negop = binvn.getDef();
                            if (negop.code() == CPUI_BOOL_NEGATE) {
                                if (!negop.getIn(0).isConstant()) {
                                    bool2vn = negop.getIn(0);
                                    bool2vn.setMark();
                                }
                            }
                        }
                        binvn.setMark();
                    }
                    binvn = binaryop.getIn(1);
                    if (!binvn.isConstant()) {
                        if (binvn.isWritten()) {
                            PcodeOp negop = binvn.getDef();
                            if (negop.code() == CPUI_BOOL_NEGATE) {
                                if (!negop.getIn(0).isConstant()) {
                                    bool3vn = negop.getIn(0);
                                    bool3vn.setMark();
                                }
                            }
                        }
                        binvn.setMark();
                    }
                }
            }
        }

        /// Find a matching Varnode in the root expression producing the given CBRANCH boolean
        /// Walk the tree rooted at the given p-code op, looking for things marked in
        /// the tree rooted at \b initop.  Trim everything but BOOL_NEGATE operations,
        /// one MULTIEQUAL, and one binary boolean operation.  If there is a Varnode
        /// in common with the root expression, this is returned, and the tree traversal
        /// state holds the path from the boolean value to the common Varnode.
        /// \param op is the given CBRANCH op to compare
        /// \return the Varnode in common with the root expression or NULL
        private Varnode findMatch(PcodeOp op)
        {
            PcodeOp curop;
            //  FlowBlock *bl = op.getParent();
            state = 0;
            Varnode curvn = op.getIn(1);
            multion = false;
            binon = false;

            matchflip = op.isBooleanFlip();

            for (; ; ) {
                if (curvn.isMark()) {
                    return curvn;
                }
                bool popstate = true;
                if (curvn.isWritten()) {
                    curop = curvn.getDef();
                    if (curop.code() == CPUI_BOOL_NEGATE) {
                        curvn = curop.getIn(0);
                        if (!binon) {
                            // Only flip if we haven't seen binop yet, as binops get compared directly
                            matchflip = !matchflip;
                        }
                        popstate = false;
                    }
                    //       else if (curop.code() == CPUI_MULTIEQUAL) {
                    // 	if ((curop.getParent()==bl)&&(!multion)) {
                    // 	  opstate[state] = curop;
                    // 	  slotstate[state] = 0;
                    // 	  flipstate[state] = matchflip;
                    // 	  state += 1;
                    // 	  curvn = curop.Input(0);
                    // 	  multion = true;
                    // 	  popstate = false;
                    // 	}
                    //       }
                    else if (curop.isBoolOutput() && (curop.getEvalType() == PcodeOp::binary)) {
                        if (!binon) {
                            opstate[state] = curop;
                            slotstate[state] = 0;
                            flipstate[state] = matchflip;
                            state += 1;
                            curvn = curop.getIn(0);
                            binon = true;
                            popstate = false;
                        }
                    }
                }
                if (popstate) {
                    while (state > 0) {
                        curop = opstate[state - 1];
                        matchflip = flipstate[state - 1];
                        slotstate[state - 1] += 1;
                        if (slotstate[state - 1] < curop.numInput()) {
                            curvn = curop.getIn(slotstate[state - 1]);
                            break;
                        }
                        state -= 1;
                        if (opstate[state].code() == CPUI_MULTIEQUAL) {
                            multion = false;
                        }
                        else {
                            binon = false;
                        }
                    }
                    if (state == 0) {
                        break;
                    }
                }
            }
            return null;
        }

        /// \brief Test if two operations with same opcode produce complementary boolean values
        ///
        /// This only tests for cases where the opcode is INT_LESS or INT_SLESS and one of the
        /// inputs is constant.
        /// \param bin1op is the first p-code op to compare
        /// \param bin2op is the second p-code op to compare
        /// \return \b true if the two operations always produce complementary values
        private bool sameOpComplement(PcodeOp bin1op, PcodeOp bin2op)
        {
            OpCode opcode = bin1op.code();
            if ((opcode == CPUI_INT_SLESS) || (opcode == CPUI_INT_LESS)) {
                // Basically we test for the scenario like:  x < 9   8 < x
                int constslot = 0;
                if (bin1op.getIn(1).isConstant()) {
                    constslot = 1;
                }
                if (!bin1op.getIn(constslot).isConstant()) {
                    return false;
                }
                if (!bin2op.getIn(1 - constslot).isConstant()) {
                    return false;
                }
                if (!varnodeSame(bin1op.getIn(1 - constslot), bin2op.getIn(constslot))) {
                    return false;
                }
                ulong val1 = bin1op.getIn(constslot).getOffset();
                ulong val2 = bin2op.getIn(1 - constslot).getOffset();
                if (constslot != 0) {
                    ulong tmp = val2;
                    val2 = val1;
                    val1 = tmp;
                }
                if (val1 + 1 != val2) {
                    return false;
                }
                if ((val2 == 0) && (opcode == CPUI_INT_LESS)) {
                    // Corner case for unsigned
                    return false;
                }
                if (opcode == CPUI_INT_SLESS) {
                    // Corner case for signed
                    int sz = bin1op.getIn(constslot).getSize();
                    if (signbit_negative(val2, sz) && (!signbit_negative(val1, sz))){
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// \brief Check if given p-code ops are complements where one is an BOOL_AND and the other is an BOOL_OR
        /// \param bin1op is the first PcodeOp
        /// \param bin2op is the second
        /// \return \b true if the p-code ops produce complementary values
        private bool andOrComplement(PcodeOp bin1op, PcodeOp bin2op)
        {
            if (bin1op.code() == CPUI_BOOL_AND) {
                if (bin2op.code() != CPUI_BOOL_OR) {
                    return false;
                }
            }
            else if (bin1op.code() == CPUI_BOOL_OR) {
                if (bin2op.code() != CPUI_BOOL_AND) {
                    return false;
                }
            }
            else {
                return false;
            }

            // Reaching here, one is AND and one is OR
            if (varnodeComplement(bin1op.getIn(0), bin2op.getIn(0))) {
                if (varnodeComplement(bin1op.getIn(1), bin2op.getIn(1))) {
                    return true;
                }
            }
            else if (varnodeComplement(bin1op.getIn(0), bin2op.getIn(1))) {
                if (varnodeComplement(bin1op.getIn(1), bin2op.getIn(0))) {
                    return true;
                }
            }
            return false;
        }

        /// \brief Determine if the two boolean expressions always produce the same or complementary values
        /// A common Varnode in the two expressions is given.  If the boolean expressions are
        /// uncorrelated, \b false is returned, otherwise \b true is returned.  If the expressions
        /// are correlated but always hold opposite values, the field \b matchflip is set to \b true.
        /// \param vn is the common Varnode
        /// \return \b true if the expressions are correlated
        private bool finalJudgement(Varnode vn)
        {
            if (initop.isBooleanFlip()) {
                matchflip = !matchflip;
            }
            if ((vn == basevn) && (!binon)) {
                // No binary operation involved
                return true;
            }
            if (boolvn != null) {
                matchflip = !matchflip;
            }
            if ((vn == boolvn) && (!binon)) {
                // Negations involved
                return true;
            }
            if ((binaryop == null) || (!binon)) {
                // Conditions don't match
                return false;
            }

            // Both conditions used binary op
            PcodeOp? binary2op = null;
            for (int i = 0; i < state; ++i) {
                // Find the binary op
                binary2op = opstate[i];
                if (binary2op.isBoolOutput()) {
                    break;
                }
            }
            // Check if the binary ops are exactly the same
            if (binaryop.code() == binary2op.code()) {
                if (   varnodeSame(binaryop.getIn(0), binary2op.getIn(0))
                    && varnodeSame(binaryop.getIn(1), binary2op.getIn(1)))
                {
                    return true;
                }
                if (sameOpComplement(binaryop, binary2op)) {
                    matchflip = !matchflip;
                    return true;
                }
                return false;
            }
            // If not, check if the binary ops are complements of one another
            matchflip = !matchflip;
            if (andOrComplement(binaryop, binary2op)) {
                return true;
            }
            int slot1 = 0;
            int slot2 = 0;
            bool reorder;
            if (binaryop.code() != get_booleanflip(binary2op.code(), reorder)) {
                return false;
            }
            if (reorder) {
                slot2 = 1;
            }
            if (!varnodeSame(binaryop.getIn(slot1), binary2op.getIn(slot2))) {
                return false;
            }
            return varnodeSame(binaryop.getIn(1 - slot1), binary2op.getIn(1 - slot2));
        }

        /// Constructor
        public ConditionMarker()
        {
            initop = null;
            basevn = null;
            boolvn = null;
            bool2vn = null;
            bool3vn = null;
            binaryop = null;
        }

        /// Destructor
        /// Any marks on Varnodes in the root expression are cleared
        ~ConditionMarker()
        {
            basevn.clearMark();
            if (boolvn != null) {
                boolvn.clearMark();
            }
            if (bool2vn != null) {
                bool2vn.clearMark();
            }
            if (bool3vn != null) {
                bool3vn.clearMark();
            }
            if (binaryop != null) {
                binaryop.getIn(0).clearMark();
                binaryop.getIn(1).clearMark();
            }
        }

        /// Perform the correlation test on two CBRANCH operations
        public bool verifyCondition(PcodeOp op, PcodeOp iop)
        {
            setupInitOp(iop);
            Varnode? matchvn = findMatch(op);
            if (matchvn == null) {
                return false;
            }
            if (!finalJudgement(matchvn)) {
                return false;
            }
            // Make final determination of what MULTIEQUAL slot is used
            if (!multion) {
                multislot = -1;
            }
            else {
                for (int i = 0; i < state; ++i) {
                    if (opstate[i].code() == CPUI_MULTIEQUAL) {
                        multislot = slotstate[i];
                        break;
                    }
                }
            }
            return true;
        }

        /// Get the MULTIEQUAL slot in the critical path
        public int getMultiSlot() => multislot;

        /// Return \b true is the expressions are anti-correlated
        public bool getFlip() => matchflip;

        /// \brief Do the given Varnodes hold the same value, possibly as constants
        /// \param a is the first Varnode to compare
        /// \param b is the second Varnode
        /// \return \b true if the Varnodes (always) hold the same value
        public static bool varnodeSame(Varnode a, Varnode b)
        {
            if (a == b) {
                return true;
            }
            return (a.isConstant() && b.isConstant() && (a.getOffset() == b.getOffset()));
        }

        /// \brief Do the given boolean Varnodes always hold complementary values
        /// Test if they are constants, 1 and 0, or if one is the direct BOOL_NEGATE of the other.
        /// \param a is the first Varnode to compare
        /// \param b is the second Varnode to compare
        /// \return \b true if the Varnodes (always) hold complementary values
        public static bool varnodeComplement(Varnode a, Varnode b)
        {
            if (a.isConstant() && b.isConstant()) {
                ulong vala = a.getOffset();
                ulong valb = b.getOffset();
                if ((vala == 0) && (valb == 1)) {
                    return true;
                }
                if ((vala == 1) && (valb == 0)) {
                    return true;
                }
                return false;
            }
            PcodeOp negop;
            if (a.isWritten()) {
                negop = a.getDef();
                if (negop.code() == CPUI_BOOL_NEGATE) {
                    if (negop.getIn(0) == b) {
                        return true;
                    }
                }
            }
            if (b.isWritten()) {
                negop = b.getDef();
                if (negop.code() == CPUI_BOOL_NEGATE) {
                    if (negop.getIn(0) == a) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
