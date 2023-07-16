using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Mark all the \e implied Varnode objects, which will have no explicit token in the output
    internal class ActionMarkImplied : Action
    {
        /// This class holds a single entry in a stack used to forward traverse Varnode expressions
        internal struct DescTreeElement
        {
            /// The Varnode at this particular point in the path
            private Varnode vn;
            /// The current edge being traversed
            private List<PcodeOp>::const_iterator desciter;
            internal DescTreeElement(Varnode v)
            {
                vn = v; desciter = v->beginDescend();
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
            Varnode* var[2];
            var[0] = vn1;
            var[1] = vn2;
            for (int4 i = 0; i < 2; ++i)
            {
                Varnode* vncur = var[i];
                if (!vncur->isWritten()) continue;
                PcodeOp* op = vncur->getDef();
                OpCode opc = op->code();
                if ((opc != CPUI_INT_ADD) && (opc != CPUI_PTRSUB) && (opc != CPUI_PTRADD) && (opc != CPUI_INT_XOR)) continue;
                if (var[1 - i] != op->getIn(0)) continue;
                if (op->getIn(1)->isConstant()) return false;
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
            if ((!vn1->isWritten()) || (!vn2->isWritten()))
            {
                if (vn1->isConstant() && vn2->isConstant())
                    return (vn1->getOffset() == vn2->getOffset()); // FIXME: these could be NEAR each other and still have an alias
                return isPossibleAliasStep(vn1, vn2);
            }

            if (!isPossibleAliasStep(vn1, vn2))
                return false;
            Varnode* cvn1,*cvn2;
            PcodeOp* op1 = vn1->getDef();
            PcodeOp* op2 = vn2->getDef();
            OpCode opc1 = op1->code();
            OpCode opc2 = op2->code();
            int4 mult1 = 1;
            int4 mult2 = 1;
            if (opc1 == CPUI_PTRSUB)
                opc1 = CPUI_INT_ADD;
            else if (opc1 == CPUI_PTRADD)
            {
                opc1 = CPUI_INT_ADD;
                mult1 = (int4)op1->getIn(2)->getOffset();
            }
            if (opc2 == CPUI_PTRSUB)
                opc2 = CPUI_INT_ADD;
            else if (opc2 == CPUI_PTRADD)
            {
                opc2 = CPUI_INT_ADD;
                mult2 = (int4)op2->getIn(2)->getOffset();
            }
            if (opc1 != opc2) return true;
            if (depth == 0) return true;    // Couldn't find absolute difference
            depth -= 1;
            switch (opc1)
            {
                case CPUI_COPY:
                case CPUI_INT_ZEXT:
                case CPUI_INT_SEXT:
                case CPUI_INT_2COMP:
                case CPUI_INT_NEGATE:
                    return isPossibleAlias(op1->getIn(0), op2->getIn(0), depth);
                case CPUI_INT_ADD:
                    cvn1 = op1->getIn(1);
                    cvn2 = op2->getIn(1);
                    if (cvn1->isConstant() && cvn2->isConstant())
                    {
                        uintb val1 = mult1 * cvn1->getOffset();
                        uintb val2 = mult2 * cvn2->getOffset();
                        if (val1 == val2)
                            return isPossibleAlias(op1->getIn(0), op2->getIn(0), depth);
                        return !functionalEquality(op1->getIn(0), op2->getIn(0));
                    }
                    if (mult1 != mult2) return true;
                    if (functionalEquality(op1->getIn(0), op2->getIn(0)))
                        return isPossibleAlias(op1->getIn(1), op2->getIn(1), depth);
                    if (functionalEquality(op1->getIn(1), op2->getIn(1)))
                        return isPossibleAlias(op1->getIn(0), op2->getIn(0), depth);
                    if (functionalEquality(op1->getIn(0), op2->getIn(1)))
                        return isPossibleAlias(op1->getIn(1), op2->getIn(0), depth);
                    if (functionalEquality(op1->getIn(1), op2->getIn(0)))
                        return isPossibleAlias(op1->getIn(0), op2->getIn(1), depth);
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
            PcodeOp* op,*storeop,*callop;
            Varnode* defvn;
            int4 i;

            op = vn->getDef();
            if (op->code() == CPUI_LOAD)
            { // Check for loads crossing stores
                list<PcodeOp*>::const_iterator oiter, iterend;
                iterend = data.endOp(CPUI_STORE);
                for (oiter = data.beginOp(CPUI_STORE); oiter != iterend; ++oiter)
                {
                    storeop = *oiter;
                    if (storeop->isDead()) continue;
                    if (vn->getCover()->contain(storeop, 2))
                    {
                        // The LOAD crosses a STORE. We are cavalier
                        // and let it through unless we can verify
                        // that the pointers are actually the same
                        if (storeop->getIn(0)->getOffset() == op->getIn(0)->getOffset())
                        {
                            //	  if (!functionalDifference(storeop->getIn(1),op->getIn(1),2)) return false;
                            if (isPossibleAlias(storeop->getIn(1), op->getIn(1), 2)) return false;
                        }
                    }
                }
            }
            if (op->isCall() || (op->code() == CPUI_LOAD))
            { // loads crossing calls
                for (i = 0; i < data.numCalls(); ++i)
                {
                    callop = data.getCallSpecs(i)->getOp();
                    if (vn->getCover()->contain(callop, 2)) return false;
                }
            }
            for (i = 0; i < op->numInput(); ++i)
            {
                defvn = op->getIn(i);
                if (defvn->isConstant()) continue;
                if (data.getMerge().inflateTest(defvn, vn->getHigh()))  // Test for intersection
                    return false;
            }
            return true;
        }

        public ActionMarkImplied(string g)
            : base(rule_onceperfunc, "markimplied", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMarkImplied(getGroup());
        }

        public override int apply(Funcdata data)
        {
            VarnodeLocSet::const_iterator viter;
            list<PcodeOp*>::const_iterator oiter;
            Varnode* vn,*vncur,*defvn,*outvn;
            PcodeOp* op;
            vector<DescTreeElement> varstack; // Depth first varnode traversal stack

            for (viter = data.beginLoc(); viter != data.endLoc(); ++viter)
            {
                vn = *viter;
                if (vn->isFree()) continue;
                if (vn->isExplicit()) continue;
                if (vn->isImplied()) continue;
                varstack.push_back(vn);
                do
                {
                    vncur = varstack.back().vn;
                    if (varstack.back().desciter == vncur->endDescend())
                    {
                        // All descendants are traced first, try to make vncur implied
                        count += 1;     // Will be marked either explicit or implied
                        if (!checkImpliedCover(data, vncur)) // Can this variable be implied
                            vncur->setExplicit();   // if not, mark explicit
                        else
                        {
                            vncur->setImplied();    // Mark as implied
                            op = vncur->getDef();
                            // setting the implied type is now taken care of by ActionSetCasts
                            //    vn->updatetype(op->outputtype_token(),false,false); // implied must have parsed type
                            // Back propagate varnode's cover to inputs of defining op
                            for (int4 i = 0; i < op->numInput(); ++i)
                            {
                                defvn = op->getIn(i);
                                if (!defvn->hasCover()) continue;
                                data.getMerge().inflate(defvn, vncur->getHigh());
                            }
                        }
                        varstack.pop_back();
                    }
                    else
                    {
                        outvn = (*varstack.back().desciter++)->getOut();
                        if (outvn != (Varnode*)0)
                        {
                            if ((!outvn->isExplicit()) && (!outvn->isImplied()))
                                varstack.push_back(outvn);
                        }
                    }
                } while (!varstack.empty());
            }

            return 0;
        }
    }
}
