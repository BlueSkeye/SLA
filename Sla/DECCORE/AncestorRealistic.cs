using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief Helper class for determining if Varnodes can trace their value from a legitimate source
    ///
    /// Try to determine if a Varnode (expressed as a particular input to a CALL, CALLIND, or RETURN op)
    /// makes sense as parameter passing (or return value) storage by examining the Varnode's ancestors.
    /// If it has ancestors that are \e unaffected, \e abnormal inputs, or \e killedbycall, then this is a sign
    /// that the Varnode doesn't make a good parameter.
    internal class AncestorRealistic
    {
        /// \brief Node in a depth first traversal of ancestors
        private class State
        {
            [Flags()]
            public enum MoveKind
            {
                /// Indicates a \e solid movement into the Varnode occurred on at least one path to MULTIEQUAL
                seen_solid0 = 1,
                /// Indicates a \e solid movement into anything other than slot 0 occurred.
                seen_solid1 = 2,
                /// Indicates the Varnode is killed by a call on at least path to MULTIEQUAL
                seen_kill = 4
            }
            private PcodeOp op;        ///< Operation along the path to the Varnode
            private int4 slot;          ///< vn = op->getIn(slot)
            private uint4 flags;        ///< Boolean properties of the node
            private int4 offset;        ///< Offset of the (eventual) trial value, within a possibly larger register

            /// \brief Constructor given a Varnode read
            ///
            /// \param o is the PcodeOp reading the Varnode
            /// \param s is the input slot
            private State(PcodeOp o, int4 s)
            {
                op = o;
                slot = s;
                flags = 0;
                offset = 0;
            }

            /// \brief Constructor from old state pulled back through a CPUI_SUBPIECE
            ///
            /// Data ultimately in SUBPIECE output is copied from a non-zero offset within the input Varnode. Note this offset
            /// \param o is the CPUI_SUBPIECE
            /// \param oldState is the old state being pulled back from
            private State(PcodeOp o, State oldState)
            {
                op = o;
                slot = 0;
                flags = 0;
                offset = oldState.offset + (int4)op->getIn(1)->getOffset();
            }

            /// Get slot associated with \e solid movement
            private int4 getSolidSlot() => ((flags & seen_solid0)!=0) ? 0 : 1;

            /// Mark given slot as having \e solid movement
            private void markSolid(int4 s)
            {
                flags |= (s == 0) ? seen_solid0 : seen_solid1;
            }

            /// Mark \e killedbycall seen
            private void markKill()
            {
                flags |= seen_kill;
            }

            /// Has \e solid movement been seen
            private bool seenSolid() => ((flags & (seen_solid0|seen_solid1))!=0);

            /// Has \e killedbycall been seen
            private bool seenKill() => ((flags & seen_kill)!= 0);
        }

        /// \brief Enumerations for state of depth first traversal
        private enum TraversalState
        {
            /// Extending path into new Varnode
            enter_node,
            /// Backtracking, from path that contained a reasonable ancestor
            pop_success,
            /// Backtracking, from path with successful, solid, movement, via COPY, LOAD, or other arith/logical
            pop_solid,
            /// Backtracking, from path with a bad ancestor
            pop_fail,
            /// Backtracking, from path with a bad ancestor, specifically killedbycall
            pop_failkill
        }

        /// Current trial being analyzed for suitability
        private ParamTrial trial;
        /// Holds the depth-first traversal stack
        private List<State> stateStack;
        /// Holds visited Varnodes to properly trim cycles
        private List<Varnode> markedVn;
        /// Number of MULTIEQUAL ops along current traversal path
        private int4 multiDepth;
        /// True if we allow and test for failing paths due to conditional execution
        private bool allowFailingPath;

        /// \brief Mark given Varnode is visited by the traversal
        ///
        /// \param vn is the given Varnode
        private void mark(Varnode vn)
        {
            markedVn.push_back(vn);
            vn->setMark();
        }

        /// Traverse into a new Varnode
        /// Analyze a new node that has just entered, during the depth-first traversal
        /// \return the command indicating the next traversal step: push (enter_node), or pop (pop_success, pop_fail, pop_solid...)
        private int4 enterNode()
        {
            State & state(stateStack.back());
            // If the node has already been visited, we truncate the traversal to prevent cycles.
            // We always return success assuming the proper result will get returned along the first path
            Varnode* stateVn = state.op->getIn(state.slot);
            if (stateVn->isMark()) return pop_success;
            if (!stateVn->isWritten())
            {
                if (stateVn->isInput())
                {
                    if (stateVn->isUnaffected()) return pop_fail;
                    if (stateVn->isPersist()) return pop_success;   // A global input, not active movement, but a valid possibility
                    if (!stateVn->isDirectWrite()) return pop_fail;
                }
                return pop_success;     // Probably a normal parameter, not active movement, but valid
            }
            mark(stateVn);      // Mark that the varnode has now been visited
            PcodeOp* op = stateVn->getDef();
            switch (op->code())
            {
                case CPUI_INDIRECT:
                    if (op->isIndirectCreation())
                    {   // Backtracking is stopped by a call
                        trial->setIndCreateFormed();
                        if (op->getIn(0)->isIndirectZero()) // True only if not a possible output
                            return pop_failkill;        // Truncate this path, indicating killedbycall
                        return pop_success;     // otherwise it could be valid
                    }
                    if (!op->isIndirectStore())
                    {   // If flow goes THROUGH a call
                        if (op->getOut()->isReturnAddress()) return pop_fail;   // Storage address location is completely invalid
                        if (trial->isKilledByCall()) return pop_fail;       // "Likely" killedbycall is invalid
                    }
                    stateStack.push_back(State(op, 0));
                    return enter_node;          // Enter the new node
                case CPUI_SUBPIECE:
                    // Extracting to a temporary, or to the same storage location, or otherwise incidental
                    // are viewed as just another node on the path to traverse
                    if (op->getOut()->getSpace()->getType() == IPTR_INTERNAL
                    || op->isIncidentalCopy() || op->getIn(0)->isIncidentalCopy()
                    || (op->getOut()->overlap(*op->getIn(0)) == (int4)op->getIn(1)->getOffset()))
                    {
                        stateStack.push_back(State(op, state));
                        return enter_node;      // Push into the new node
                    }
                    // For other SUBPIECES, do a minimal traversal to rule out unaffected or other invalid inputs,
                    // but otherwise treat it as valid, active, movement into the parameter
                    do
                    {
                        Varnode* vn = op->getIn(0);
                        if ((!vn->isMark()) && (vn->isInput()))
                        {
                            if (vn->isUnaffected() || (!vn->isDirectWrite()))
                                return pop_fail;
                        }
                        op = vn->getDef();
                    } while ((op != (PcodeOp*)0) && ((op->code() == CPUI_COPY) || (op->code() == CPUI_SUBPIECE)));
                    return pop_solid;   // treat the COPY as a solid movement
                case CPUI_COPY:
                    {
                        // Copies to a temporary, or between varnodes with same storage location, or otherwise incidental
                        // are viewed as just another node on the path to traverse
                        if (op->getOut()->getSpace()->getType() == IPTR_INTERNAL
                        || op->isIncidentalCopy() || op->getIn(0)->isIncidentalCopy()
                        || (op->getOut()->getAddr() == op->getIn(0)->getAddr()))
                        {
                            stateStack.push_back(State(op, 0));
                            return enter_node;      // Push into the new node
                        }
                        // For other COPIES, do a minimal traversal to rule out unaffected or other invalid inputs,
                        // but otherwise treat it as valid, active, movement into the parameter
                        Varnode* vn = op->getIn(0);
                        for (; ; )
                        {
                            if ((!vn->isMark()) && (vn->isInput()))
                            {
                                if (!vn->isDirectWrite())
                                    return pop_fail;
                            }
                            op = vn->getDef();
                            if (op == (PcodeOp*)0) break;
                            OpCode opc = op->code();
                            if (opc == CPUI_COPY || opc == CPUI_SUBPIECE)
                                vn = op->getIn(0);
                            else if (opc == CPUI_PIECE)
                                vn = op->getIn(1);      // Follow least significant piece
                            else
                                break;
                        }
                        return pop_solid;   // treat the COPY as a solid movement
                    }
                case CPUI_MULTIEQUAL:
                    multiDepth += 1;
                    stateStack.push_back(State(op, 0));
                    return enter_node;              // Nothing to check, start traversing inputs of MULTIEQUAL
                case CPUI_PIECE:
                    if (stateVn->getSize() > trial->getSize())
                    {   // Did we already pull-back from a SUBPIECE?
                        // If the trial is getting pieced together and then truncated in a register,
                        // this is evidence of artificial data-flow.
                        if (state.offset == 0 && op->getIn(1)->getSize() <= trial->getSize())
                        {
                            // Truncation corresponds to least significant piece, follow slot=1
                            stateStack.push_back(State(op, 1));
                            return enter_node;
                        }
                        else if (state.offset == op->getIn(1)->getSize() && op->getIn(0)->getSize() <= trial->getSize())
                        {
                            // Truncation corresponds to most significant piece, follow slot=0
                            stateStack.push_back(State(op, 0));
                            return enter_node;
                        }
                        if (stateVn->getSpace()->getType() != IPTR_SPACEBASE)
                        {
                            return pop_fail;
                        }
                    }
                    return pop_solid;
                default:
                    return pop_solid;               // Any other LOAD or arithmetic/logical operation is viewed as solid movement
            }
        }

        /// Pop a Varnode from the traversal stack
        /// Backtrack into a previously visited node
        /// \param pop_command is the type of pop (pop_success, pop_fail, pop_failkill, pop_solid) being performed
        /// \return the command to execute (push or pop) after the current pop
        private int4 uponPop(int4 command)
        {
            State & state(stateStack.back());
            if (state.op->code() == CPUI_MULTIEQUAL)
            {   // All the interesting action happens for MULTIEQUAL branch points
                State & prevstate(stateStack[stateStack.size() - 2]);   // State previous the one being popped
                if (pop_command == pop_fail)
                {       // For a pop_fail, we always pop and pass along the fail
                    multiDepth -= 1;
                    stateStack.pop_back();
                    return pop_command;
                }
                else if ((pop_command == pop_solid) && (multiDepth == 1) && (state.op->numInput() == 2))
                    prevstate.markSolid(state.slot);    // Indicate we have seen a "solid" that could override a "failkill"
                else if (pop_command == pop_failkill)
                    prevstate.markKill();       // Indicate we have seen a "failkill" along at least one path of MULTIEQUAL
                state.slot += 1;                // Move to the next sibling
                if (state.slot == state.op->numInput())
                {       // If we have traversed all siblings
                    if (prevstate.seenSolid())
                    {           // If we have seen an overriding "solid" along at least one path
                        pop_command = pop_success;          // this is always a success
                        if (prevstate.seenKill())
                        {           // UNLESS we have seen a failkill
                            if (allowFailingPath)
                            {
                                if (!checkConditionalExe(state))        // that can NOT be attributed to conditional execution
                                    pop_command = pop_fail;         // in which case we fail despite having solid movement
                                else
                                    trial->setCondExeEffect();          // Slate this trial for additional testing
                            }
                            else
                                pop_command = pop_fail;
                        }
                    }
                    else if (prevstate.seenKill())  // If we have seen a failkill without solid movement
                        pop_command = pop_failkill;         // this is always a failure
                    else
                        pop_command = pop_success;          // seeing neither solid nor failkill is still a success
                    multiDepth -= 1;
                    stateStack.pop_back();
                    return pop_command;
                }
                return enter_node;
            }
            else
            {
                stateStack.pop_back();
                return pop_command;
            }
        }

        /// Check if current Varnode produced by conditional flow
        /// \return \b true if there are two input flows, one of which is a normal \e solid flow
        private bool checkConditionalExe(State state)
        {
            const BlockBasic* bl = state.op->getParent();
            if (bl->sizeIn() != 2)
                return false;
            const FlowBlock* solidBlock = bl->getIn(state.getSolidSlot());
            if (solidBlock->sizeOut() != 1)
                return false;
            //  const BlockBasic *callbl = stateStack[0].op->getParent();
            //  if (callbl != bl) {
            //    bool dominates = false;
            //    FlowBlock *dombl = callbl->getImmedDom();
            //    for(int4 i=0;i<2;++i) {
            //      if (dombl == bl) {
            //	dominates = true;
            //	break;
            //      }
            //      if (dombl == (FlowBlock *)0) break;
            //      dombl = dombl->getImmedDom();
            //    }
            //    if (!dominates)
            //      return false;
            //  }
            return true;
        }

        /// \brief Perform a full ancestor check on a given parameter trial
        ///
        /// \param op is the CALL or RETURN to test parameter passing for
        /// \param slot is the index of the particular input varnode to test
        /// \param t is the ParamTrial object corresponding to the varnode
        /// \param allowFail is \b true if we allow and test for failing paths due to conditional execution
        /// \return \b true if the varnode has realistic ancestors for a parameter passing location
        public bool execute(PcodeOp op, int4 slot, ParamTrial t, bool allowFail)
        {
            trial = t;
            allowFailingPath = allowFail;
            markedVn.clear();       // Make sure to clear out any old data
            stateStack.clear();
            multiDepth = 0;
            // If the parameter itself is an input, we don't consider this realistic, we expect to see active
            // movement into the parameter. There are some cases where this doesn't happen, but they are rare and
            // failure here doesn't necessarily mean further analysis won't still declare this a parameter
            if (op->getIn(slot)->isInput())
            {
                if (!trial->hasCondExeEffect()) // Make sure we are not retesting
                    return false;
            }
            // Run the depth first traversal
            int4 command = enter_node;
            stateStack.push_back(State(op, slot));      // Start by entering the initial node
            while (!stateStack.empty())
            {           // Continue until all paths have been exhausted
                switch (command)
                {
                    case enter_node:
                        command = enterNode();
                        break;
                    case pop_success:
                    case pop_solid:
                    case pop_fail:
                    case pop_failkill:
                        command = uponPop(command);
                        break;
                }
            }
            for (int4 i = 0; i < markedVn.size(); ++i)      // Clean up marks we left along the way
                markedVn[i]->clearMark();
            if (command == pop_success)
            {
                trial->setAncestorRealistic();
                return true;
            }
            else if (command == pop_solid)
            {
                trial->setAncestorRealistic();
                trial->setAncestorSolid();
                return true;
            }
            return false;
        }
    }
}
