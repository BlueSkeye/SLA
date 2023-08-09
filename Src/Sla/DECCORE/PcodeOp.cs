using Sla.CORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Lowest level operation of the \b p-code language
    ///
    /// The philosophy here is to have only one version of any type of operation,
    /// and to be completely explicit about all effects.
    /// All operations except the control flow operations have exactly one
    /// explicit output. Any given operation can have multiple inputs, but all
    /// are listed explicitly.
    ///
    /// Input and output size for an operation are specified explicitly. All
    /// inputs must be of the same size. 
    /// Except for the above restrictions, input and output can be any size
    /// in bytes. 
    ///
    /// P-code can be either big or little endian, this is determined
    /// by the language being translated from
    internal class PcodeOp
    {
        //friend class BlockBasic; // Just insert_before, insert_after, setOrder
        //friend class Funcdata;
        //friend class PcodeOpBank;
        //friend class VarnodeBank;    // Only uses setInput

        /// Boolean attributes (flags) that can be placed on a PcodeOp. Even though this enum is public, these are
        /// all set and read internally, although many are read publicly via \e get or \e is methods.
        [Flags()]
        public enum Flags
        {
            /// This instruction starts a basic block
            startbasic = 1,
            /// This instruction is a branch
            branch = 2,
            /// This instruction calls a subroutine
            call = 4,
            /// This instruction returns to caller
            returns = 0x8,
            /// This op cannot be collapsed further
            nocollapse = 0x10,
            /// This operation is dead
            dead = 0x20,
            /// special placeholder op (multiequal or indirect)
            /// or OpCode.CPUI_COPY between different copies
            /// of same variable
            marker = 0x40,
            /// Boolean operation
            booloutput = 0x80,
            /// Set if condition must be false to take branch
            boolean_flip = 0x100,
            /// Set if fallthru happens on true condition
            fallthru_true = 0x200,
            /// Op is source of (one or more) CPUI_INDIRECTs
            indirect_source = 0x400,
            /// The first parameter to this op is a coderef
            coderef = 0x800,
            /// This op is the first in its instruction
            startmark = 0x1000,
            /// Used by many algorithms that need to detect loops or avoid repeats
            mark = 0x2000,
            /// Order of input parameters does not matter
            commutative = 0x4000,
            /// Evaluate as unary expression
            unary = 0x8000,
            /// Evaluate as binary expression
            binary = 0x10000,
            /// Cannot be evaluated (without special processing)
            special = 0x20000,
            /// Evaluate as ternary operator (or higher)
            ternary = 0x40000,
            /// Op does not allow COPY propagation through its inputs
            no_copy_propagation = 0x80000,
            /// Op should not be directly printed as source
            nonprinting = 0x100000,
            /// instruction causes processor or process to halt
            halt = 0x200000,
            /// placeholder for bad instruction data
            badinstruction = 0x400000,
            /// placeholder for unimplemented instruction
            unimplemented = 0x800000,
            /// placeholder for previous call that doesn't exit
            noreturn = 0x1000000,
            /// ops at this address were not generated
            missing = 0x2000000,
            /// Loads or stores from a dynamic pointer into a spacebase
            spacebase_ptr = 0x4000000,
            /// Output varnode is created by indirect effect
            indirect_creation = 0x8000000,
            /// Output has been determined to be a 1-bit boolean value
            calculated_bool = 0x10000000,
            /// Op has a call specification associated with it
            has_callspec = 0x20000000,
            /// Op consumes or produces a ptr
            ptrflow = 0x40000000,
            /// OpCode.CPUI_INDIRECT is caused by OpCode.CPUI_STORE
            indirect_store = 0x80000000
        }

        [Flags()]
        public enum AdditionalFlags
        {
            /// Does some special form of datatype propagation
            special_prop = 1,
            /// Op is marked for special printing
            special_print = 2,
            /// This op has been modified by the current action
            modified = 4,
            /// Warning has been generated for this op
            warning = 8,
            /// Treat this as \e incidental for parameter recovery algorithms
            incidental_copy = 0x10,
            /// Have we checked for cpool transforms
            is_cpool_transformed = 0x20,
            /// Stop data-type propagation into output from descendants
            stop_type_propagation = 0x40,
            /// Output varnode (of call) should not be removed if it is unread
            hold_output = 0x80,
            /// Output of \b this is root of a CONCAT tree
            concat_root = 0x100,
            /// Do not collapse \b this INDIRECT (via RuleIndirectCollapse)
            no_indirect_collapse = 0x200
        }

        /// Pointer to class providing behavioral details of the operation
        private TypeOp opcode;
        /// Collection of boolean attributes on this op
        internal /*mutable*/ Flags flags;
        /// Additional boolean attributes for this op
        private /*mutable*/ AdditionalFlags addlflags;
        /// What instruction address is this attached to
        private SeqNum start;
        /// Basic block in which this op is contained
        private BlockBasic parent;
        /// Iterator within basic block
        private IEnumerator<PcodeOp> basiciter;
        /// Position in alive/dead list
        private IEnumerator<PcodeOp> insertiter;
        /// Position in opcode list
        private IEnumerator<PcodeOp> codeiter;
        /// The one possible output Varnode of this op
        private Varnode? output;
        /// The ordered list of input Varnodes for this op
        private List<Varnode?> inrefs;

        // Only used by Funcdata
        /// Set the opcode for this PcodeOp
        /// Set the behavioral class (opcode) of this operation. For most applications this should only be called
        /// by the PcodeOpBank.  This is fairly low-level but does cache various boolean flags associated with the opcode
        /// \param t_op is the behavioural class to set
        private void setOpcode(TypeOp t_op)
        {
            flags &= ~(PcodeOp.Flags.branch | PcodeOp.Flags.call | PcodeOp.Flags.coderef | PcodeOp.Flags.commutative |
                   PcodeOp.Flags.returns | PcodeOp.Flags.nocollapse | PcodeOp.Flags.marker | PcodeOp.Flags.booloutput |
                   PcodeOp.Flags.unary | PcodeOp.Flags.binary | PcodeOp.Flags.ternary | PcodeOp.Flags.special |
                   PcodeOp.Flags.has_callspec | PcodeOp.Flags.no_copy_propagation);
            opcode = t_op;
            flags |= t_op.getFlags();
        }

        /// Set the output Varnode of this op
        internal void setOutput(Varnode? vn)
        {
            output = vn;
        }

        /// Clear a specific input Varnode to \e null
        internal void clearInput(int slot)
        {
            inrefs[slot] = (Varnode)null;
        }

        /// Set a specific input Varnode
        internal void setInput(Varnode vn, int slot)
        {
            inrefs[slot] = vn;
        }

        /// Set specific boolean attribute(s) on this op
        internal void setFlag(Flags fl)
        {
            flags |= fl;
        }

        /// Clear specific boolean attribute(s)
        internal void clearFlag(Flags fl)
        {
            flags &= ~fl;
        }

        /// Set specific boolean attribute
        internal void setAdditionalFlag(AdditionalFlags fl)
        {
            addlflags |= fl;
        }

        /// Clear specific boolean atribute
        private void clearAdditionalFlag(AdditionalFlags fl)
        {
            addlflags &= ~fl;
        }

        /// Flip the setting of specific boolean attribute(s)
        internal void flipFlag(Flags fl)
        {
            flags ^= fl;
        }

        /// Make sure this op has \b num inputs
        /// Make sure there are exactly \e num input slots for this op.
        /// All slots, regardless of the total being increased or decreased, are set to \e null.
        /// \param num is the number of inputs to set
        internal void setNumInputs(int num)
        {
            inrefs.resize(num);
            for (int i = 0; i < num; ++i)
                inrefs[i] = (Varnode)null;
        }

        /// Eliminate a specific input Varnode
        /// Remove the input Varnode in a specific slot.  The slot is eliminated and all Varnodes beyond this
        /// slot are renumbered.  All the other Varnodes are otherwise undisturbed.
        /// \param slot is the index of the Varnode to remove
        internal void removeInput(int slot)
        {
            for (int i = slot + 1; i < inrefs.size(); ++i)
                inrefs[i - 1] = inrefs[i];
            inrefs.RemoveLastItem();
        }

        /// Make room for a new input Varnode at a specific position
        /// Insert space for a new Varnode before \e slot.  The new space is filled with \e null.
        /// \param slot is index of the slot where the new space is inserted
        internal void insertInput(int slot)
        {
            inrefs.Add((Varnode)null);
            for (int i = inrefs.Count - 1; i > slot; --i)
                inrefs[i] = inrefs[i - 1];
            inrefs[slot] = (Varnode)null;
        }

        /// Order this op within the ops for a single instruction
        internal void setOrder(uint ord)
        {
            start.setOrder(ord);
        }

        /// Set the parent basic block of this op
        internal void setParent(BlockBasic p)
        {
            parent = p;
        }

        /// Store the iterator into this op's basic block
        internal void setBasicIter(IEnumerator<PcodeOp> iter)
        {
            basiciter = iter;
        }

        /// Construct an unattached PcodeOp
        /// Construct a completely unattached PcodeOp.  Space is reserved for input and output Varnodes
        /// but all are set initially to null.
        /// \param s indicates the number of input slots reserved
        /// \param sq is the sequence number to associate with the new PcodeOp
        public PcodeOp(int s, SeqNum sq)
        {
            flags = 0;          // Start out life as dead
            addlflags = 0;
            parent = (BlockBasic)null; // No parent yet

            output = (Varnode)null;
            opcode = (TypeOp*)0;
            for (int i = 0; i < inrefs.size(); ++i)
                inrefs[i] = (Varnode)null;
        }

        ~PcodeOp()
        {
        }

        /// Get the number of inputs to this op
        public int numInput() => inrefs.size();

        /// Get the output Varnode of this op or \e null
        public Varnode getOut() => output;

        /// Get a specific input Varnode to this op
        public Varnode getIn(int slot) => inrefs[slot];

        /// Get the parent basic block
        public BlockBasic getParent() => (BlockBasic) parent;

        /// Get the instruction address associated with this op
        public Address getAddr() => start.getAddr();

        /// Get the time index indicating when this op was created
        public uint getTime() => start.getTime();

        /// Get the sequence number associated with this op
        public SeqNum getSeqNum() => start;

        /// Get position within alive/dead list
        public IEnumerator<PcodeOp> getInsertIter() => insertiter;

        /// Get position within basic block
        public IEnumerator<PcodeOp> getBasicIter() => basiciter;

        /// \brief Get the slot number of the indicated input varnode
        public int getSlot(Varnode vn)
        {
            int i, n;
            n = inrefs.size();
            for (i = 0; i < n; ++i)
                if (inrefs[i] == vn)
                    break;
            return i;
        }

        /// \brief Find the slot for a given Varnode, which may be take up multiple input slots
        ///
        /// In the rare case that \b this PcodeOp takes the same Varnode as input multiple times,
        /// use the specific descendant iterator producing \b this PcodeOp to work out the corresponding slot.
        /// Every slot containing the given Varnode will be produced exactly once over the course of iteration.
        /// \param vn is the given Varnode
        /// \param firstSlot is the first instance of the Varnode in \b this input list
        /// \param iter is the specific descendant iterator producing \b this
        /// \return the slot corresponding to the iterator
        public int getRepeatSlot(Varnode vn, int firstSlot, IEnumerator<PcodeOp> iter)
        {
            int count = 1;
            IEnumerator<PcodeOp> oiter = vn.beginDescend();
            while (oiter.MoveNext()) {
                if ((oiter.Current) == this)
                    count += 1;
            }
            if (count == 1) return firstSlot;
            int recount = 1;
            for (int i = firstSlot + 1; i < inrefs.Count; ++i) {
                if (inrefs[i] == vn) {
                    recount += 1;
                    if (recount == count)
                        return i;
                }
            }
            return -1;
        }

        /// \brief Get the evaluation type of this op
        public Flags getEvalType()
            => (flags & (Flags.unary | Flags.binary | Flags.special | Flags.ternary));

        /// \brief Get type which indicates unusual halt in control-flow
        public Flags getHaltType()
            => (flags & (Flags.halt | Flags.badinstruction | Flags.unimplemented | Flags.noreturn | Flags.missing));

        /// Return \b true if this op is dead
        public bool isDead() => ((flags & Flags.dead) != 0);

        /// Return \b true is this op has an output
        public bool isAssignment() => (output != (Varnode)null);

        /// Return \b true if this op indicates call semantics
        public bool isCall() => ((flags & Flags.call) != 0);

        /// \brief Return \b true if this op acts as call but does not have a full specification
        public bool isCallWithoutSpec() => ((flags&(PcodeOp::call|PcodeOp::has_callspec))== PcodeOp::call);

        /// Return \b true is a special SSA form op
        public bool isMarker() => ((flags&PcodeOp::marker)!= 0);

        /// Return \b true if op creates a varnode indirectly
        public bool isIndirectCreation() => ((flags&PcodeOp.Flags.indirect_creation)!= 0);

        /// Return \b true if \b this INDIRECT is caused by STORE
        public bool isIndirectStore() => ((flags&PcodeOp::indirect_store)!= 0);

        /// \brief Return \b true if this op is not directly represented in C output
        public bool notPrinted() => ((flags&(PcodeOp::marker|PcodeOp.Flags.nonprinting|PcodeOp::noreturn))!= 0);
        
        /// \brief Return \b true if this op produces a boolean output
        public bool isBoolOutput() => ((flags&PcodeOp::booloutput)!= 0);

        /// Return \b true if this op is a branch
        public bool isBranch() => ((flags&PcodeOp::branch)!= 0);

        /// \brief Return \b true if this op is a call or branch
        public bool isCallOrBranch() => ((flags&(PcodeOp::branch|PcodeOp::call))!= 0);

        /// \brief Return \b true if this op breaks fall-thru flow
        public bool isFlowBreak() => ((flags&(PcodeOp::branch|PcodeOp::returns))!= 0);

        /// \brief Return \b true if this op flips the true/false meaning of its control-flow branching
        public bool isBooleanFlip() => ((flags&PcodeOp.Flags.boolean_flip)!= 0);

        /// \brief Return \b true if the fall-thru branch is taken when the boolean input is true
        public bool isFallthruTrue() => ((flags&PcodeOp::fallthru_true)!= 0);

        /// Return \b true if the first input is a code reference
        public bool isCodeRef() => ((flags&PcodeOp::coderef)!= 0);

        /// Return \b true if this starts an instruction
        public bool isInstructionStart() => ((flags&PcodeOp.Flags.startmark)!= 0);

        /// Return \b true if this starts a basic block
        public bool isBlockStart() => ((flags&PcodeOp.Flags.startbasic)!= 0);

        /// Return \b true if this is modified by the current action
        public bool isModified() => ((addlflags&PcodeOp::modified)!= 0);

        /// Return \b true if this op has been marked
        public bool isMark() => ((flags&PcodeOp::mark)!= 0);

        /// Set the mark on this op
        public void setMark() => flags |= PcodeOp::mark;

        /// Return \b true if a warning has been generated for this op
        public bool isWarning() => ((addlflags&PcodeOp::warning)!= 0);

        /// Clear any mark on this op
        public void clearMark() => flags &= ~PcodeOp::mark;

        /// Return \b true if this causes an INDIRECT
        public bool isIndirectSource() => ((flags&PcodeOp::indirect_source)!= 0);

        /// Mark this op as source of INDIRECT
        public void setIndirectSource()
        {
            flags |= PcodeOp::indirect_source;
        }

        /// Clear INDIRECT source flag
        public void clearIndirectSource()
        {
            flags &= ~PcodeOp::indirect_source;
        }

        /// Return \b true if this produces/consumes ptrs
        public bool isPtrFlow() => ((flags&PcodeOp::ptrflow)!= 0);

        /// Mark this op as consuming/producing ptrs
        public void setPtrFlow()
        {
            flags |= PcodeOp::ptrflow;
        }

        /// Return \b true if this does datatype propagation
        public bool doesSpecialPropagation() => ((addlflags&PcodeOp::special_prop)!= 0);

        /// Return \b true if this needs to special printing
        public bool doesSpecialPrinting() => ((addlflags&PcodeOp::special_print)!= 0);

        /// Return \b true if \b this COPY is \e incidental
        public bool isIncidentalCopy() => ((addlflags&PcodeOp::incidental_copy)!= 0);

        /// \brief Return \b true if output is 1-bit boolean
        public bool isCalculatedBool() => ((flags&(PcodeOp.Flags.calculated_bool|PcodeOp::booloutput))!= 0);
        
        /// \brief Return \b true if we have already examined this cpool
        public bool isCpoolTransformed() => ((addlflags&PcodeOp::is_cpool_transformed)!= 0);

        /// Return \b true if this can be collapsed to a COPY of a constant
        /// Can this be collapsed to a copy op, i.e. are all inputs constants
        /// \return \b true if this op can be callapsed
        public bool isCollapsible()
        {
            if ((flags & PcodeOp.Flags.nocollapse) != 0) return false;
            if (!isAssignment()) return false;
            if (inrefs.size() == 0) return false;
            for (int i = 0; i < inrefs.size(); ++i)
                if (!getIn(i).isConstant()) return false;
            if (getOut().getSize() > sizeof(ulong)) return false;
            return true;
        }

        /// Is data-type propagation from below stopped
        public bool stopsTypePropagation() => ((addlflags&stop_type_propagation)!= 0);

        /// Stop data-type propagation from below
        public void setStopTypePropagation()
        {
            addlflags |= stop_type_propagation;
        }

        /// Allow data-type propagation from below
        public void clearStopTypePropagation()
        {
            addlflags &= ~stop_type_propagation;
        }

        /// If \b true, do not remove output as dead code
        public bool holdOutput() => ((addlflags&hold_output)!= 0);

        /// Prevent output from being removed as dead code
        public void setHoldOutput()
        {
            addlflags |= hold_output;
        }

        /// Output is root of CONCAT tree
        public bool isPartialRoot() => ((addlflags&concat_root)!= 0);

        /// Mark \b this as root of CONCAT tree
        public void setPartialRoot()
        {
            addlflags |= concat_root;
        }

        /// Does \b this allow COPY propagation
        public bool stopsCopyPropagation() => ((flags&no_copy_propagation)!= 0);

        /// Stop COPY propagation through inputs
        public void setStopCopyPropagation()
        {
            flags |= no_copy_propagation;
        }

        /// Check if INDIRECT collapse is possible
        public bool noIndirectCollapse() => ((addlflags & no_indirect_collapse)!= 0);

        /// Prevent collapse of INDIRECT
        public void setNoIndirectCollapse()
        {
            addlflags |= no_indirect_collapse;
        }

        /// \brief Return \b true if this LOADs or STOREs from a dynamic \e spacebase pointer
        public bool usesSpacebasePtr() => ((flags&PcodeOp.Flags.spacebase_ptr)!= 0);

        /// Return hash indicating possibility of common subexpression elimination
        /// Produce a hash of the following attributes: output size, the opcode, and the identity
        /// of each input varnode.  This is suitable for determining if two PcodeOps calculate identical values
        /// \return the calculated hash or 0 if the op is not cse hashable
        public uint getCseHash()
        {
            uint hash;
            if ((getEvalType() & (PcodeOp.Flags.unary | PcodeOp.Flags.binary)) == 0) return ((uint)0);
            if (code() == OpCode.CPUI_COPY) return ((uint)0); // Let copy propagation deal with this

            hash = (output.getSize() << 8) | (uint)code();
            for (int i = 0; i < inrefs.size(); ++i)
            {
                Varnode vn = getIn(i);
                hash = (hash << 8) | (hash >> (sizeof(uint) * 8 - 8));
                if (vn.isConstant())
                    hash ^= (uint)vn.getOffset();
                else
                    hash ^= (uint)vn.getCreateIndex(); // Hash in pointer itself as unique id
            }
            return hash;
        }

        /// Return \b true if this and \e op represent common subexpressions
        /// Do these two ops represent a common subexpression?
        /// This is the full test of matching indicated by getCseHash
        /// \param op is the PcodeOp to compare with this
        /// \return \b true if the two ops are a common subexpression match
        public bool isCseMatch(PcodeOp op)
        {
            if ((getEvalType() & (PcodeOp.Flags.unary | PcodeOp.Flags.binary)) == 0) return false;
            if ((op.getEvalType() & (PcodeOp.Flags.unary | PcodeOp.Flags.binary)) == 0) return false;
            if (output.getSize() != op.output.getSize()) return false;
            if (code() != op.code()) return false;
            if (code() == OpCode.CPUI_COPY) return false; // Let copy propagation deal with this
            if (inrefs.size() != op.inrefs.size()) return false;
            for (int i = 0; i < inrefs.size(); ++i)
            {
                Varnode vn1 = getIn(i);
                Varnode vn2 = op.getIn(i);
                if (vn1 == vn2) continue;
                if (vn1.isConstant() && vn2.isConstant() && (vn1.getOffset() == vn2.getOffset()))
                    continue;
                return false;
            }
            return true;
        }

        /// Can \b this be moved to after \e point, without disturbing data-flow
        /// Its possible for the order of operations to be rearranged in some instances but still keep
        /// equivalent data-flow.  Test if \b this operation can be moved to occur immediately after
        /// a specified \e point operation. This currently only tests for movement within a basic block.
        /// \param point is the specified point to move \b this after
        /// \return \b true if the move is possible
        public bool isMoveable(PcodeOp point)
        {
            if (this == point) return true; // No movement necessary
            bool movingLoad = false;
            if (getEvalType() == PcodeOp.Flags.special) {
                if (code() == OpCode.CPUI_LOAD)
                    movingLoad = true;  // Allow LOAD to be moved with additional restrictions
                else
                    return false;   // Don't move special ops
            }
            if (parent != point.parent) return false;  // Not in the same block
            if (output != (Varnode)null) {
                // Output cannot be moved past an op that reads it
                IEnumerator<PcodeOp> iter = output.beginDescend();
                while (iter.MoveNext()) {
                    PcodeOp readOp = iter.Current;
                    if (readOp.parent != parent) continue;
                    if (readOp.start.getOrder() <= point.start.getOrder())
                        return false;       // Is in the block and is read before (or at) -point-
                }
            }
            // Only allow this op to be moved across a CALL in very restrictive circumstances
            bool crossCalls = false;
            if (getEvalType() != PcodeOp.Flags.special)
            {
                // Check for a normal op where all inputs and output are not address tied
                if (output != (Varnode)null && !output.isAddrTied() && !output.isPersist())
                {
                    int i;
                    for (i = 0; i < numInput(); ++i)
                    {
                        Varnode vn = getIn(i);
                        if (vn.isAddrTied() || vn.isPersist())
                            break;
                    }
                    if (i == numInput())
                        crossCalls = true;
                }
            }
            List<Varnode> tiedList = new List<Varnode>();
            for (int i = 0; i < numInput(); ++i)
            {
                Varnode vn = getIn(i);
                if (vn.isAddrTied())
                    tiedList.Add(vn);
            }
            list<PcodeOp*>::iterator biter = basiciter;
            do
            {
                ++biter;
                PcodeOp* op = *biter;
                if (op.getEvalType() == PcodeOp.Flags.special)
                {
                    switch (op.code())
                    {
                        case OpCode.CPUI_LOAD:
                            if (output != (Varnode)null)
                            {
                                if (output.isAddrTied()) return false;
                            }
                            break;
                        case OpCode.CPUI_STORE:
                            if (movingLoad)
                                return false;
                            else
                            {
                                if (!tiedList.empty()) return false;
                                if (output != (Varnode)null)
                                {
                                    if (output.isAddrTied()) return false;
                                }
                            }
                            break;
                        case OpCode.CPUI_INDIRECT:     // Let thru, deal with what's INDIRECTed around separately
                        case OpCode.CPUI_SEGMENTOP:
                        case OpCode.CPUI_CPOOLREF:
                            break;
                        case OpCode.CPUI_CALL:
                        case OpCode.CPUI_CALLIND:
                        case OpCode.CPUI_NEW:
                            if (!crossCalls) return false;
                            break;
                        default:
                            return false;
                    }
                }
                if (op.output != (Varnode)null)
                {
                    if (movingLoad)
                    {
                        if (op.output.isAddrTied()) return false;
                    }
                    for (int i = 0; i < tiedList.size(); ++i)
                    {
                        Varnode vn = tiedList[i];
                        if (vn.overlap(*op.output) >= 0)
                            return false;
                        if (op.output.overlap(*vn) >= 0)
                            return false;
                    }
                }
            } while (biter != point.basiciter);
            return true;
        }

        /// Get the opcode for this op
        public TypeOp getOpcode() => opcode;

        /// Get the opcode id (enum) for this op
        public OpCode code() => opcode.getOpcode();

        /// Return \b true if inputs commute
        public bool isCommutative() => ((flags & PcodeOp::commutative)!= 0);

        /// Calculate the constant output produced by this op
        /// Assuming all the inputs to this op are constants, compute the constant result of evaluating
        /// this op on this inputs. If one if the inputs has attached symbol information,
        /// pass-back "the fact of" as we may want to propagate the info to the new constant.
        /// Throw an exception if a constant result cannot be produced.
        /// \param markedInput will pass-back whether or not one of the inputs is a marked constant
        /// \return the constant result
        public ulong collapse(bool markedInput)
        {
            Varnode vn0;
            Varnode vn1;

            vn0 = getIn(0);
            if (vn0.getSymbolEntry() != (SymbolEntry)null)
            {
                markedInput = true;
            }
            switch (getEvalType())
            {
                case PcodeOp.Flags.unary:
                    return opcode.evaluateUnary(output.getSize(), vn0.getSize(), vn0.getOffset());
                case PcodeOp.Flags.binary:
                    vn1 = getIn(1);
                    if (vn1.getSymbolEntry() != (SymbolEntry)null)
                    {
                        markedInput = true;
                    }
                    return opcode.evaluateBinary(output.getSize(), vn0.getSize(),
                                  vn0.getOffset(), vn1.getOffset());
                default:
                    break;
            }
            throw new LowlevelError("Invalid constant collapse");
        }

        /// Propagate constant symbol from inputs to given output
        /// Knowing that \b this PcodeOp has collapsed its constant inputs, one of which has
        /// symbol content, figure out if the symbol should propagate to the new given output constant.
        /// \param newConst is the given output constant
        public void collapseConstantSymbol(Varnode newConst)
        {
            Varnode copyVn = (Varnode)null;
            switch (code())
            {
                case OpCode.CPUI_SUBPIECE:
                    if (getIn(1).getOffset() != 0)
                        return;             // Must be truncating high bytes
                    copyVn = getIn(0);
                    break;
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_INT_ZEXT:
                case OpCode.CPUI_INT_NEGATE:
                case OpCode.CPUI_INT_2COMP:
                    copyVn = getIn(0);
                    break;
                case OpCode.CPUI_INT_LEFT:
                case OpCode.CPUI_INT_RIGHT:
                case OpCode.CPUI_INT_SRIGHT:
                    copyVn = getIn(0);  // Marked varnode must be first input
                    break;
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_MULT:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                case OpCode.CPUI_INT_XOR:
                    copyVn = getIn(0);
                    if (copyVn.getSymbolEntry() == (SymbolEntry)null)
                    {
                        copyVn = getIn(1);
                    }
                    break;
                default:
                    return;
            }
            if (copyVn.getSymbolEntry() == (SymbolEntry)null)
                return;             // The first input must be marked
            newConst.copySymbolIfValid(copyVn);
        }

        /// Return the next op in the control-flow from this or \e null
        // Find the next op in sequence from this op.  This is usually in the same basic block, but this
        // routine will follow flow into successive blocks during its search, so long as there is only one path
        // \return the next PcodeOp or \e null
        public PcodeOp nextOp()
        {
            list<PcodeOp*>::iterator iter;
            BlockBasic* p;

            p = parent;         // Current parent
            iter = basiciter;       // Current iterator

            iter++;
            while (iter == p.endOp())
            {
                if ((p.sizeOut() != 1) && (p.sizeOut() != 2)) return (PcodeOp)null;
                p = (BlockBasic*)p.getOut(0);
                iter = p.beginOp();
            }
            return *iter;
        }

        /// Return the previous op within this op's basic block or \e null
        /// Find the previous op that flowed uniquely into this op, if it exists.  This routine will not search
        /// farther than the basic block containing this.
        /// \return the previous PcodeOp or \e null
        public PcodeOp previousOp()
        {
            list<PcodeOp>::iterator iter;

            if (basiciter == parent.beginOp()) return (PcodeOp)null;
            iter = basiciter;
            iter--;
            return *iter;
        }

        /// Return starting op for instruction associated with this op
        /// Scan backward within the basic block containing this op and find the first op marked as the
        /// start of an instruction.  This also works if basic blocks haven't been calculated yet, and all
        /// the ops are still in the dead list.  The starting op may be from a different instruction if
        /// this op was from an instruction in a delay slot
        /// \return the starting PcodeOp
        public PcodeOp target()
        {
            PcodeOp* retop;
            list<PcodeOp*>::iterator iter;
            iter = isDead() ? insertiter : basiciter;
            retop = *iter;
            while ((retop.flags & PcodeOp.Flags.startmark) == 0)
            {
                --iter;
                retop = *iter;
            }
            return retop;
        }

        /// Calculate known zero bits for output to this op
        /// Compute nonzeromask assuming inputs to op have their masks properly defined. Assume the op has an output.
        /// For any inputs to this op, that have zero bits where their nzmasks have zero bits, then the output
        /// produced by this op is guaranteed to have zero bits at every location in the nzmask calculated by this function.
        /// \param cliploop indicates the calculation shouldn't include inputs from known looping edges
        /// \return the calculated non-zero mask
        public ulong getNZMaskLocal(bool cliploop)
        {
            int sa, sz1, sz2, size;
            ulong resmask, val;

            size = output.getSize();
            ulong fullmask = Globals.calc_mask(size);

            switch (opcode.getOpcode())
            {
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_INT_NOTEQUAL:
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_INT_CARRY:
                case OpCode.CPUI_INT_SCARRY:
                case OpCode.CPUI_INT_SBORROW:
                case OpCode.CPUI_BOOL_NEGATE:
                case OpCode.CPUI_BOOL_XOR:
                case OpCode.CPUI_BOOL_AND:
                case OpCode.CPUI_BOOL_OR:
                case OpCode.CPUI_FLOAT_EQUAL:
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                case OpCode.CPUI_FLOAT_LESS:
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                case OpCode.CPUI_FLOAT_NAN:
                    resmask = 1;            // Only 1 bit not guaranteed to be 0
                    break;
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_INT_ZEXT:
                    resmask = getIn(0).getNZMask();
                    break;
                case OpCode.CPUI_INT_SEXT:
                    resmask = Globals.sign_extend(getIn(0).getNZMask(), getIn(0).getSize(), size);
                    break;
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_OR:
                    resmask = getIn(0).getNZMask();
                    if (resmask != fullmask)
                        resmask |= getIn(1).getNZMask();
                    break;
                case OpCode.CPUI_INT_AND:
                    resmask = getIn(0).getNZMask();
                    if (resmask != 0)
                        resmask &= getIn(1).getNZMask();
                    break;
                case OpCode.CPUI_INT_LEFT:
                    if (!getIn(1).isConstant())
                        resmask = fullmask;
                    else {
                        sa = (int)getIn(1).getOffset(); // Get shift amount
                        resmask = getIn(0).getNZMask();
                        resmask = Globals.pcode_left(resmask, sa) & fullmask;
                    }
                    break;
                case OpCode.CPUI_INT_RIGHT:
                    if (!getIn(1).isConstant())
                        resmask = fullmask;
                    else {
                        sz1 = getIn(0).getSize();
                        sa = (int)getIn(1).getOffset(); // Get shift amount
                        resmask = getIn(0).getNZMask();
                        resmask = Globals.pcode_right(resmask, sa);
                        if (sz1 > sizeof(ulong)) {
                            // resmask did not hold most sig bits of mask
                            if (sa >= 8 * sz1)
                                resmask = 0;
                            else if (sa >= 8 * sizeof(ulong)) {
                                // Full mask shifted over 8*sizeof(ulong)
                                resmask = Globals.calc_mask(sz1 - sizeof(ulong));
                                // Shift over remaining portion of sa
                                resmask >>= (sa - 8 * sizeof(ulong));
                            }
                            else
                            {
                                // Fill in one bits from part of mask not originally
                                // calculated
                                ulong tmp = 0;
                                tmp -= 1;
                                tmp <<= (8 * sizeof(ulong) - sa);
                                resmask |= tmp;
                            }
                        }
                    }
                    break;
                case OpCode.CPUI_INT_SRIGHT:
                    if ((!getIn(1).isConstant()) || (size > sizeof(ulong)))
                        resmask = fullmask;
                    else {
                        sa = (int)getIn(1).getOffset(); // Get shift amount
                        resmask = getIn(0).getNZMask();
                        if ((resmask & (fullmask ^ (fullmask >> 1))) == 0) {
                            // If we know sign bit is zero
                            // Same as OpCode.CPUI_INT_RIGHT
                            resmask = Globals.pcode_right(resmask, sa);
                        }
                        else {
                            resmask = Globals.pcode_right(resmask, sa);
                            // Don't know what the new high bits are
                            resmask |= (fullmask >> sa) ^ fullmask;
                        }
                    }
                    break;
                case OpCode.CPUI_INT_DIV:
                    val = getIn(0).getNZMask();
                    resmask = Globals.coveringmask(val);
                    if (getIn(1).isConstant())
                    {
                        // Dividing by power of 2 is equiv to right shift
                        // if the denom is bigger than a power of 2, then
                        // the result still has at least that many highsig zerobits
                        sa = Globals.mostsigbit_set(getIn(1).getNZMask());
                        if (sa != -1)
                            resmask >>= sa;     // Add sa additional zerobits
                    }
                    break;
                case OpCode.CPUI_INT_REM:
                    val = (getIn(1).getNZMask() - 1); // Result is less than modulus
                    resmask = Globals.coveringmask(val);
                    break;
                case OpCode.CPUI_POPCOUNT:
                    sz1 = Globals.popcount(getIn(0).getNZMask());
                    resmask = Globals.coveringmask((ulong)sz1);
                    resmask &= fullmask;
                    break;
                case OpCode.CPUI_LZCOUNT:
                    resmask = Globals.coveringmask(getIn(0).getSize() * 8);
                    resmask &= fullmask;
                    break;
                case OpCode.CPUI_SUBPIECE:
                    resmask = getIn(0).getNZMask();
                    sz1 = (int)getIn(1).getOffset();
                    if ((int)getIn(0).getSize() <= sizeof(ulong))
                    {
                        if (sz1 < sizeof(ulong))
                            resmask >>= 8 * sz1;
                        else
                            resmask = 0;
                    }
                    else
                    {           // Extended precision
                        if (sz1 < sizeof(ulong))
                        {
                            resmask >>= 8 * sz1;
                            if (sz1 > 0)
                                resmask |= fullmask << (8 * (sizeof(ulong) - sz1));
                        }
                        else
                            resmask = fullmask;
                    }
                    resmask &= fullmask;
                    break;
                case OpCode.CPUI_PIECE:
                    resmask = getIn(0).getNZMask();
                    resmask <<= 8 * getIn(1).getSize();
                    resmask |= getIn(1).getNZMask();
                    break;
                case OpCode.CPUI_INT_MULT:
                    val = getIn(0).getNZMask();
                    resmask = getIn(1).getNZMask();
                    sz1 = (size > sizeof(ulong)) ? 8 * size - 1 : Globals.mostsigbit_set(val);
                    if (sz1 == -1)
                        resmask = 0;
                    else
                    {
                        sz2 = (size > sizeof(ulong)) ? 8 * size - 1 : Globals.mostsigbit_set(resmask);
                        if (sz2 == -1)
                            resmask = 0;
                        else
                        {
                            if (sz1 + sz2 < 8 * size - 2)
                                fullmask >>= (8 * size - 2 - sz1 - sz2);
                            sz1 = Globals.leastsigbit_set(val);
                            sz2 = Globals.leastsigbit_set(resmask);
                            resmask = (~((ulong)0)) << (sz1 + sz2);
                            resmask &= fullmask;
                        }
                    }
                    break;
                case OpCode.CPUI_INT_ADD:
                    resmask = getIn(0).getNZMask();
                    if (resmask != fullmask)
                    {
                        resmask |= getIn(1).getNZMask();
                        resmask |= (resmask << 1);  // Account for possible carries
                        resmask &= fullmask;
                    }
                    break;
                case OpCode.CPUI_MULTIEQUAL:
                    if (inrefs.size() == 0)
                        resmask = fullmask;
                    else
                    {
                        int i = 0;
                        resmask = 0;
                        if (cliploop)
                        {
                            for (; i < inrefs.size(); ++i)
                            {
                                if (parent.isLoopIn(i)) continue;
                                resmask |= getIn(i).getNZMask();
                            }
                        }
                        else
                        {
                            for (; i < inrefs.size(); ++i)
                                resmask |= getIn(i).getNZMask();
                        }
                    }
                    break;
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLIND:
                case OpCode.CPUI_CPOOLREF:
                    if (isCalculatedBool())
                        resmask = 1;        // In certain cases we know the output is strictly boolean
                    else
                        resmask = fullmask;
                    break;
                default:
                    resmask = fullmask;
                    break;
            }
            return resmask;
        }

        /// Compare the control-flow order of this and \e bop
        /// Compare the execution order of -this- and -bop-, if -this- executes earlier (dominates) return -1;
        /// if -bop- executes earlier return 1, otherwise return 0.  Note that 0 is returned if there is no absolute
        /// execution order.
        /// \param bop is the PcodeOp to compare this to
        /// \return -1, 0, or 1, depending on the comparison
        public int compareOrder(PcodeOp bop)
        {
            if (parent == bop.parent)
                return (start.getOrder() < bop.start.getOrder()) ? -1 : 1;

            FlowBlock common = FlowBlock.findCommonBlock(parent, bop.parent);
            if (common == parent)
                return -1;
            if (common == bop.parent)
                return 1;
            return 0;
        }

        /// Print raw info about this op to stream
        public void printRaw(TextWriter s)
        {
            opcode.printRaw(s,this);
        }

        /// Return the name of this op
        public string getOpName() => opcode.getName();

        /// Print debug description of this op to stream
        /// Print an address and a raw representation of this op to the stream, suitable for console debugging apps
        /// \param s is the stream to print to
        public void printDebug(TextWriter s)
        {
            s << start << ": ";
            if (isDead() || (parent == (BlockBasic)null))
                s << "**";
            else
                printRaw(s);
        }

        /// Encode a description of \b this op to stream
        /// Encode a description including: the opcode name, the sequence number, and separate elements
        /// providing a reference number for each input and output Varnode
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_OP);
            encoder.writeSignedInteger(AttributeId.ATTRIB_CODE, (int)code());
            start.encode(encoder);
            if (output == (Varnode)null)
            {
                encoder.openElement(ElementId.ELEM_VOID);
                encoder.closeElement(ElementId.ELEM_VOID);
            }
            else
            {
                encoder.openElement(ElementId.ELEM_ADDR);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_REF, output.getCreateIndex());
                encoder.closeElement(ElementId.ELEM_ADDR);
            }
            for (int i = 0; i < inrefs.size(); ++i)
            {
                Varnode* vn = getIn(i);
                if (vn == (Varnode)null) {
                    encoder.openElement(ElementId.ELEM_VOID);
                    encoder.closeElement(ElementId.ELEM_VOID);
                }
                else if (vn.getSpace().getType() == spacetype.IPTR_IOP)
                {
                    if ((i == 1) && (code() == OpCode.CPUI_INDIRECT))
                    {
                        PcodeOp* indop = PcodeOp.getOpFromConst(vn.getAddr());
                        encoder.openElement(ElementId.ELEM_IOP);
                        encoder.writeUnsignedInteger(AttributeId.ATTRIB_VALUE, indop.getSeqNum().getTime());
                        encoder.closeElement(ElementId.ELEM_IOP);
                    }
                    else
                    {
                        encoder.openElement(ElementId.ELEM_VOID);
                        encoder.closeElement(ElementId.ELEM_VOID);
                    }
                }
                else if (vn.getSpace().getType() == spacetype.IPTR_CONSTANT)
                {
                    if ((i == 0) && ((code() == OpCode.CPUI_STORE) || (code() == OpCode.CPUI_LOAD)))
                    {
                        AddrSpace* spc = vn.getSpaceFromConst();
                        encoder.openElement(ElementId.ELEM_SPACEID);
                        encoder.writeSpace(AttributeId.ATTRIB_NAME, spc);
                        encoder.closeElement(ElementId.ELEM_SPACEID);
                    }
                    else
                    {
                        encoder.openElement(ElementId.ELEM_ADDR);
                        encoder.writeUnsignedInteger(AttributeId.ATTRIB_REF, vn.getCreateIndex());
                        encoder.closeElement(ElementId.ELEM_ADDR);
                    }
                }
                else
                {
                    encoder.openElement(ElementId.ELEM_ADDR);
                    encoder.writeUnsignedInteger(AttributeId.ATTRIB_REF, vn.getCreateIndex());
                    encoder.closeElement(ElementId.ELEM_ADDR);
                }
            }
            encoder.closeElement(ElementId.ELEM_OP);
        }

        /// \brief Retrieve the PcodeOp encoded as the address \e addr
        public static PcodeOp getOpFromConst(Address addr) => (PcodeOp)(ulong)addr.getOffset();

        /// Calculate the local output type
        public Datatype outputTypeLocal() => opcode.getOutputLocal(this);

        /// Calculate the local input type
        public Datatype inputTypeLocal(int slot) => opcode.getInputLocal(this, slot);
    }
}
