using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Mark all the \e implied Varnode objects, which will have no explicit token in the output
    internal class ActionMarkImplied : Action
    {
        /// This class holds a single entry in a stack used to forward traverse Varnode expressions
        internal struct DescTreeElement
        {
            /// The Varnode at this particular point in the path
            internal Varnode vn;
            /// The current edge being traversed
            internal IEnumerator<PcodeOp> desciter;
            internal bool _traversalCompleted;

            internal DescTreeElement(Varnode v)
            {
                vn = v;
                desciter = v.beginDescend();
                _traversalCompleted = false;
            }
        }

        /// Check for additive relationship
        /// Return false only if one Varnode is obtained by adding non-zero thing to another Varnode.
        /// The order of the Varnodes is not important.
        /// \param vn1 is the first Varnode
        /// \param vn2 is the second Varnode
        /// \return false if the additive relationship holds
        private static bool isPossibleAliasStep(Varnode vn1, Varnode vn2)
        {
            Varnode[] var = new Varnode[] { vn1, vn2 };
            for (int i = 0; i < 2; ++i) {
                Varnode vncur = var[i];
                if (!vncur.isWritten()) continue;
                PcodeOp op = vncur.getDef() ?? throw new BugException();
                OpCode opc = op.code();
                if (opc != OpCode.CPUI_INT_ADD
                    && (opc != OpCode.CPUI_PTRSUB)
                    && (opc != OpCode.CPUI_PTRADD)
                    && (opc != OpCode.CPUI_INT_XOR))
                {
                    continue;
                }
                if (var[1 - i] != op.getIn(0)) continue;
                if (op.getIn(1).isConstant()) return false;
            }
            return true;
        }

        /// Check for possible duplicate value
        /// Return false \b only if we can guarantee two Varnodes have different values.
        /// \param vn1 is the first Varnode
        /// \param vn2 is the second Varnode
        /// \param depth is the maximum level to recurse
        /// \return true if its possible the Varnodes hold the same value
        private static bool isPossibleAlias(Varnode vn1, Varnode vn2, int depth)
        {
            if (vn1 == vn2) return true;    // Definite alias
            if ((!vn1.isWritten()) || (!vn2.isWritten())) {
                if (vn1.isConstant() && vn2.isConstant())
                    return (vn1.getOffset() == vn2.getOffset()); // FIXME: these could be NEAR each other and still have an alias
                return isPossibleAliasStep(vn1, vn2);
            }

            if (!isPossibleAliasStep(vn1, vn2))
                return false;
            Varnode cvn1;
            Varnode cvn2;
            PcodeOp op1 = vn1.getDef();
            PcodeOp op2 = vn2.getDef();
            OpCode opc1 = op1.code();
            OpCode opc2 = op2.code();
            int mult1 = 1;
            int mult2 = 1;
            if (opc1 == OpCode.CPUI_PTRSUB)
                opc1 = OpCode.CPUI_INT_ADD;
            else if (opc1 == OpCode.CPUI_PTRADD) {
                opc1 = OpCode.CPUI_INT_ADD;
                mult1 = (int)op1.getIn(2).getOffset();
            }
            if (opc2 == OpCode.CPUI_PTRSUB)
                opc2 = OpCode.CPUI_INT_ADD;
            else if (opc2 == OpCode.CPUI_PTRADD) {
                opc2 = OpCode.CPUI_INT_ADD;
                mult2 = (int)op2.getIn(2).getOffset();
            }
            if (opc1 != opc2) return true;
            if (depth == 0) return true;    // Couldn't find absolute difference
            depth -= 1;
            switch (opc1) {
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_INT_ZEXT:
                case OpCode.CPUI_INT_SEXT:
                case OpCode.CPUI_INT_2COMP:
                case OpCode.CPUI_INT_NEGATE:
                    return isPossibleAlias(op1.getIn(0), op2.getIn(0), depth);
                case OpCode.CPUI_INT_ADD:
                    cvn1 = op1.getIn(1);
                    cvn2 = op2.getIn(1);
                    if (cvn1.isConstant() && cvn2.isConstant()) {
                        ulong val1 = (uint)mult1 * cvn1.getOffset();
                        ulong val2 = (uint)mult2 * cvn2.getOffset();
                        if (val1 == val2)
                            return isPossibleAlias(op1.getIn(0), op2.getIn(0), depth);
                        return !PcodeOpBank.functionalEquality(op1.getIn(0), op2.getIn(0));
                    }
                    if (mult1 != mult2) return true;
                    if (PcodeOpBank.functionalEquality(op1.getIn(0), op2.getIn(0)))
                        return isPossibleAlias(op1.getIn(1), op2.getIn(1), depth);
                    if (PcodeOpBank.functionalEquality(op1.getIn(1), op2.getIn(1)))
                        return isPossibleAlias(op1.getIn(0), op2.getIn(0), depth);
                    if (PcodeOpBank.functionalEquality(op1.getIn(0), op2.getIn(1)))
                        return isPossibleAlias(op1.getIn(1), op2.getIn(0), depth);
                    if (PcodeOpBank.functionalEquality(op1.getIn(1), op2.getIn(0)))
                        return isPossibleAlias(op1.getIn(0), op2.getIn(1), depth);
                    break;
                default:
                    break;
            }
            return true;
        }

        ///< Check for cover violation if Varnode is implied
        /// Marking a Varnode as \e implied causes the input Varnodes to its defining op to propagate farther
        /// in the output.  This may cause eventual variables to hold different values at the same
        /// point in the code. Any input must test that its propagated Cover doesn't intersect its current Cover.
        /// \param data is the function being analyzed
        /// \param vn is the given Varnode
        /// \return \b true if there is a Cover violation
        private static bool checkImpliedCover(Funcdata data, Varnode vn)
        {
            PcodeOp storeop;
            PcodeOp callop;
            Varnode defvn;
            int i;

            PcodeOp op = vn.getDef() ?? throw new BugException();
            if (op.code() == OpCode.CPUI_LOAD) {
                // Check for loads crossing stores
                IEnumerator<PcodeOp> oiter = data.beginOp(OpCode.CPUI_STORE);
                while (oiter.MoveNext()) {
                    storeop = oiter.Current;
                    if (storeop.isDead()) continue;
                    if (vn.getCover().contain(storeop, 2)) {
                        // The LOAD crosses a STORE. We are cavalier
                        // and let it through unless we can verify
                        // that the pointers are actually the same
                        if (storeop.getIn(0).getOffset() == op.getIn(0).getOffset()) {
                            //	  if (!functionalDifference(storeop.getIn(1),op.getIn(1),2)) return false;
                            if (isPossibleAlias(storeop.getIn(1), op.getIn(1), 2)) return false;
                        }
                    }
                }
            }
            if (op.isCall() || (op.code() == OpCode.CPUI_LOAD)) {
                // loads crossing calls
                for (i = 0; i < data.numCalls(); ++i) {
                    callop = data.getCallSpecs(i).getOp();
                    if (vn.getCover().contain(callop, 2)) return false;
                }
            }
            for (i = 0; i < op.numInput(); ++i) {
                defvn = op.getIn(i);
                if (defvn.isConstant()) continue;
                if (data.getMerge().inflateTest(defvn, vn.getHigh()))  // Test for intersection
                    return false;
            }
            return true;
        }

        public ActionMarkImplied(string g)
            : base(ruleflags.rule_onceperfunc, "markimplied", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMarkImplied(getGroup());
        }

        public override int apply(Funcdata data)
        {
            Varnode vn;
            Varnode vncur;
            Varnode defvn;
            PcodeOp op;
            // Depth first varnode traversal stack
            List<DescTreeElement> varstack = new List<DescTreeElement>();

            IEnumerator<Varnode> viter = data.beginLoc();
            while (viter.MoveNext()) {
                vn = viter.Current;
                if (vn.isFree()) continue;
                if (vn.isExplicit()) continue;
                if (vn.isImplied()) continue;
                varstack.Add(new DescTreeElement(vn));
                do {
                    vncur = varstack.GetLastItem().vn;
                    if (varstack.GetLastItem().desciter == vncur.endDescend()) {
                        // All descendants are traced first, try to make vncur implied
                        count += 1;     // Will be marked either explicit or implied
                        if (!checkImpliedCover(data, vncur)) // Can this variable be implied
                            vncur.setExplicit();   // if not, mark explicit
                        else {
                            vncur.setImplied();    // Mark as implied
                            op = vncur.getDef() ?? throw new BugException();
                            // setting the implied type is now taken care of by ActionSetCasts
                            //    vn.updatetype(op.outputtype_token(),false,false); // implied must have parsed type
                            // Back propagate varnode's cover to inputs of defining op
                            for (int i = 0; i < op.numInput(); ++i) {
                                defvn = op.getIn(i);
                                if (!defvn.hasCover()) continue;
                                data.getMerge().inflate(defvn, vncur.getHigh());
                            }
                        }
                        varstack.RemoveLastItem();
                    }
                    else {
                        Varnode? outvn = (varstack.GetLastItem().desciter++).getOut();
                        if (outvn != (Varnode)null) {
                            if ((!outvn.isExplicit()) && (!outvn.isImplied()))
                                varstack.Add(new DescTreeElement(outvn));
                        }
                    }
                } while (0 != varstack.Count);
            }

            return 0;
        }
    }
}
