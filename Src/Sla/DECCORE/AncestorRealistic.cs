using Sla.CORE;

namespace Sla.DECCORE
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

            /// \brief Constructor given a Varnode read
            /// \param o is the PcodeOp reading the Varnode
            /// \param s is the input slot
            internal State(PcodeOp o, int s)
            {
                op = o;
                slot = s;
                flags = 0;
                offset = 0;
            }

            internal PcodeOp op;        ///< Operation along the path to the Varnode
            internal int slot;          ///< vn = op.getIn(slot)
            private MoveKind flags;        ///< Boolean properties of the node
            internal int offset;        ///< Offset of the (eventual) trial value, within a possibly larger register

            /// \brief Constructor from old state pulled back through a OpCode.CPUI_SUBPIECE
            ///
            /// Data ultimately in SUBPIECE output is copied from a non-zero offset within the input Varnode. Note this offset
            /// \param o is the OpCode.CPUI_SUBPIECE
            /// \param oldState is the old state being pulled back from
            internal State(PcodeOp o, State oldState)
            {
                op = o;
                slot = 0;
                flags = 0;
                offset = oldState.offset + (int)op.getIn(1).getOffset();
            }

            /// Get slot associated with \e solid movement
            internal int getSolidSlot() => ((flags & MoveKind.seen_solid0)!=0) ? 0 : 1;

            /// Mark given slot as having \e solid movement
            internal void markSolid(int s)
            {
                flags |= (s == 0) ? MoveKind.seen_solid0 : MoveKind.seen_solid1;
            }

            /// Mark \e killedbycall seen
            internal void markKill()
            {
                flags |= MoveKind.seen_kill;
            }

            /// Has \e solid movement been seen
            internal bool seenSolid() => ((flags & (MoveKind.seen_solid0 | MoveKind.seen_solid1)) != 0);

            /// Has \e killedbycall been seen
            internal bool seenKill() => ((flags & MoveKind.seen_kill) != 0);
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
        private List<State> stateStack = new List<State>();
        /// Holds visited Varnodes to properly trim cycles
        private List<Varnode> markedVn = new List<Varnode>();
        /// Number of MULTIEQUAL ops along current traversal path
        private int multiDepth;
        /// True if we allow and test for failing paths due to conditional execution
        private bool allowFailingPath;

        /// \brief Mark given Varnode is visited by the traversal
        ///
        /// \param vn is the given Varnode
        private void mark(Varnode vn)
        {
            markedVn.Add(vn);
            vn.setMark();
        }

        /// Traverse into a new Varnode
        /// Analyze a new node that has just entered, during the depth-first traversal
        /// \return the command indicating the next traversal step: push (enter_node), or pop (pop_success, pop_fail, pop_solid...)
        private TraversalState enterNode()
        {
            State state = stateStack.GetLastItem();
            // If the node has already been visited, we truncate the traversal to prevent cycles.
            // We always return success assuming the proper result will get returned along the first path
            Varnode stateVn = state.op.getIn(state.slot);
            if (stateVn.isMark()) return TraversalState.pop_success;
            if (!stateVn.isWritten()) {
                if (stateVn.isInput()) {
                    if (stateVn.isUnaffected()) return TraversalState.pop_fail;
                    if (stateVn.isPersist()) return TraversalState.pop_success;   // A global input, not active movement, but a valid possibility
                    if (!stateVn.isDirectWrite()) return TraversalState.pop_fail;
                }
                return TraversalState.pop_success;     // Probably a normal parameter, not active movement, but valid
            }
            mark(stateVn);      // Mark that the varnode has now been visited
            PcodeOp? op = stateVn.getDef() ?? throw new BugException();
            switch (op.code()) {
                case OpCode.CPUI_INDIRECT:
                    if (op.isIndirectCreation())
                    {   // Backtracking is stopped by a call
                        trial.setIndCreateFormed();
                        if (op.getIn(0).isIndirectZero()) // True only if not a possible output
                            return TraversalState.pop_failkill;        // Truncate this path, indicating killedbycall
                        return TraversalState.pop_success;     // otherwise it could be valid
                    }
                    if (!op.isIndirectStore())
                    {   // If flow goes THROUGH a call
                        if (op.getOut().isReturnAddress()) return TraversalState.pop_fail;   // Storage address location is completely invalid
                        if (trial.isKilledByCall()) return TraversalState.pop_fail;       // "Likely" killedbycall is invalid
                    }
                    stateStack.Add(new State(op, 0));
                    return TraversalState.enter_node;          // Enter the new node
                case OpCode.CPUI_SUBPIECE:
                    // Extracting to a temporary, or to the same storage location, or otherwise incidental
                    // are viewed as just another node on the path to traverse
                    if (op.getOut().getSpace().getType() == spacetype.IPTR_INTERNAL
                    || op.isIncidentalCopy() || op.getIn(0).isIncidentalCopy()
                    || (op.getOut().overlap(op.getIn(0)) == (int)op.getIn(1).getOffset())) {
                        stateStack.Add(new State(op, state));
                        return TraversalState.enter_node;      // Push into the new node
                    }
                    // For other SUBPIECES, do a minimal traversal to rule out unaffected or other invalid inputs,
                    // but otherwise treat it as valid, active, movement into the parameter
                    do {
                        Varnode vn = op.getIn(0);
                        if ((!vn.isMark()) && (vn.isInput())) {
                            if (vn.isUnaffected() || (!vn.isDirectWrite()))
                                return TraversalState.pop_fail;
                        }
                        op = vn.getDef();
                    } while ((op != (PcodeOp)null) && ((op.code() == OpCode.CPUI_COPY) || (op.code() == OpCode.CPUI_SUBPIECE)));
                    return TraversalState.pop_solid;   // treat the COPY as a solid movement
                case OpCode.CPUI_COPY:
                    {
                        // Copies to a temporary, or between varnodes with same storage location, or otherwise incidental
                        // are viewed as just another node on the path to traverse
                        if (op.getOut().getSpace().getType() == spacetype.IPTR_INTERNAL
                        || op.isIncidentalCopy() || op.getIn(0).isIncidentalCopy()
                        || (op.getOut().getAddr() == op.getIn(0).getAddr())) {
                            stateStack.Add(new State(op, 0));
                            return TraversalState.enter_node;      // Push into the new node
                        }
                        // For other COPIES, do a minimal traversal to rule out unaffected or other invalid inputs,
                        // but otherwise treat it as valid, active, movement into the parameter
                        Varnode vn = op.getIn(0);
                        while(true) {
                            if ((!vn.isMark()) && (vn.isInput())) {
                                if (!vn.isDirectWrite())
                                    return TraversalState.pop_fail;
                            }
                            op = vn.getDef();
                            if (op == (PcodeOp)null) break;
                            OpCode opc = op.code();
                            if (opc == OpCode.CPUI_COPY || opc == OpCode.CPUI_SUBPIECE)
                                vn = op.getIn(0);
                            else if (opc == OpCode.CPUI_PIECE)
                                vn = op.getIn(1);      // Follow least significant piece
                            else
                                break;
                        }
                        return TraversalState.pop_solid;   // treat the COPY as a solid movement
                    }
                case OpCode.CPUI_MULTIEQUAL:
                    multiDepth += 1;
                    stateStack.Add(new State(op, 0));
                    return TraversalState.enter_node;              // Nothing to check, start traversing inputs of MULTIEQUAL
                case OpCode.CPUI_PIECE:
                    if (stateVn.getSize() > trial.getSize()) {
                        // Did we already pull-back from a SUBPIECE?
                        // If the trial is getting pieced together and then truncated in a register,
                        // this is evidence of artificial data-flow.
                        if (state.offset == 0 && op.getIn(1).getSize() <= trial.getSize()) {
                            // Truncation corresponds to least significant piece, follow slot=1
                            stateStack.Add(new State(op, 1));
                            return TraversalState.enter_node;
                        }
                        else if (state.offset == op.getIn(1).getSize() && op.getIn(0).getSize() <= trial.getSize()) {
                            // Truncation corresponds to most significant piece, follow slot=0
                            stateStack.Add(new State(op, 0));
                            return TraversalState.enter_node;
                        }
                        if (stateVn.getSpace().getType() != spacetype.IPTR_SPACEBASE) {
                            return TraversalState.pop_fail;
                        }
                    }
                    return TraversalState.pop_solid;
                default:
                    return TraversalState.pop_solid;               // Any other LOAD or arithmetic/logical operation is viewed as solid movement
            }
        }

        /// Pop a Varnode from the traversal stack
        /// Backtrack into a previously visited node
        /// \param pop_command is the type of pop (pop_success, pop_fail, pop_failkill, pop_solid) being performed
        /// \return the command to execute (push or pop) after the current pop
        private TraversalState uponPop(TraversalState command)
        {
            State state = stateStack.GetLastItem();
            if (state.op.code() == OpCode.CPUI_MULTIEQUAL) {
                // All the interesting action happens for MULTIEQUAL branch points
                State prevstate = stateStack[stateStack.size() - 2];   // State previous the one being popped
                if (command == TraversalState.pop_fail) {
                    // For a pop_fail, we always pop and pass along the fail
                    multiDepth -= 1;
                    stateStack.RemoveLastItem();
                    return command;
                }
                else if ((command == TraversalState.pop_solid) && (multiDepth == 1) && (state.op.numInput() == 2))
                    prevstate.markSolid(state.slot);    // Indicate we have seen a "solid" that could override a "failkill"
                else if (command == TraversalState.pop_failkill)
                    prevstate.markKill();       // Indicate we have seen a "failkill" along at least one path of MULTIEQUAL
                state.slot += 1;                // Move to the next sibling
                if (state.slot == state.op.numInput()) {
                    // If we have traversed all siblings
                    if (prevstate.seenSolid()) {
                        // If we have seen an overriding "solid" along at least one path
                        command = TraversalState.pop_success;          // this is always a success
                        if (prevstate.seenKill()) {
                            // UNLESS we have seen a failkill
                            if (allowFailingPath) {
                                if (!checkConditionalExe(state))        // that can NOT be attributed to conditional execution
                                    command = TraversalState.pop_fail;         // in which case we fail despite having solid movement
                                else
                                    trial.setCondExeEffect();          // Slate this trial for additional testing
                            }
                            else
                                command = TraversalState.pop_fail;
                        }
                    }
                    else if (prevstate.seenKill())  // If we have seen a failkill without solid movement
                        command = TraversalState.pop_failkill;         // this is always a failure
                    else
                        command = TraversalState.pop_success;          // seeing neither solid nor failkill is still a success
                    multiDepth -= 1;
                    stateStack.RemoveLastItem();
                    return command;
                }
                return TraversalState.enter_node;
            }
            else {
                stateStack.RemoveLastItem();
                return command;
            }
        }

        /// Check if current Varnode produced by conditional flow
        /// \return \b true if there are two input flows, one of which is a normal \e solid flow
        private bool checkConditionalExe(State state)
        {
            BlockBasic bl = state.op.getParent();
            if (bl.sizeIn() != 2)
                return false;
            FlowBlock solidBlock = bl.getIn(state.getSolidSlot());
            if (solidBlock.sizeOut() != 1)
                return false;
            //  BlockBasic *callbl = stateStack[0].op.getParent();
            //  if (callbl != bl) {
            //    bool dominates = false;
            //    FlowBlock *dombl = callbl.getImmedDom();
            //    for(int i=0;i<2;++i) {
            //      if (dombl == bl) {
            //	dominates = true;
            //	break;
            //      }
            //      if (dombl == (FlowBlock *)0) break;
            //      dombl = dombl.getImmedDom();
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
        public bool execute(PcodeOp op, int slot, ParamTrial t, bool allowFail)
        {
            trial = t;
            allowFailingPath = allowFail;
            markedVn.Clear();       // Make sure to clear out any old data
            stateStack.Clear();
            multiDepth = 0;
            // If the parameter itself is an input, we don't consider this realistic, we expect to see active
            // movement into the parameter. There are some cases where this doesn't happen, but they are rare and
            // failure here doesn't necessarily mean further analysis won't still declare this a parameter
            if (op.getIn(slot).isInput()) {
                if (!trial.hasCondExeEffect()) // Make sure we are not retesting
                    return false;
            }
            // Run the depth first traversal
            TraversalState command = TraversalState.enter_node;
            stateStack.Add(new State(op, slot));      // Start by entering the initial node
            while (!stateStack.empty()) {
                // Continue until all paths have been exhausted
                switch (command) {
                    case TraversalState.enter_node:
                        command = enterNode();
                        break;
                    case TraversalState.pop_success:
                    case TraversalState.pop_solid:
                    case TraversalState.pop_fail:
                    case TraversalState.pop_failkill:
                        command = uponPop(command);
                        break;
                }
            }
            for (int i = 0; i < markedVn.size(); ++i)      // Clean up marks we left along the way
                markedVn[i].clearMark();
            if (command == TraversalState.pop_success) {
                trial.setAncestorRealistic();
                return true;
            }
            else if (command == TraversalState.pop_solid) {
                trial.setAncestorRealistic();
                trial.setAncestorSolid();
                return true;
            }
            return false;
        }
    }
}
