using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A class for analyzing parameters to a sub-function call
    ///
    /// This can be viewed as a function prototype that evolves over the course of
    /// analysis. It derives off of FuncProto and includes facilities for analyzing
    /// data-flow for parameter information. This is the high-level object managing
    /// the examination of data-flow to recover a working prototype (ParamActive),
    /// holding a stack-pointer placeholder to facilitate stack analysis, and deciding
    /// on the working \e extrapop for the CALL.
    ///
    /// A \b stack-pointer \b placeholder is a temporary Varnode in the input operands
    /// of the CALL or CALLIND that is defined by a LOAD from the stack-pointer. By examining
    /// the pointer, the exact value of the stack-pointer (relative to its incoming value) can
    /// be computed at the point of the CALL.  The temporary can arise naturally if stack
    /// parameters are a possibility, otherwise a placeholder temporary is artificially
    /// inserted into the CALL input.  At the time heritage of the stack space is computed,
    /// the placeholder is examined to read off the active stack-pointer offset for the CALL
    /// and the placeholder is removed.
    internal class FuncCallSpecs : FuncProto
    {
        /// Pointer to CALL or CALLIND instruction
        private PcodeOp op;
        /// Name of function if present
        private string name;
        /// First executing address of function
        private Address entryaddress;
        /// The Funcdata object for the called functon (if known)
        private Funcdata fd;
        /// Working extrapop for the CALL
        private int4 effective_extrapop;
        /// Relative offset of stack-pointer at time of this call
        private uintb stackoffset;
        /// Slot containing temporary stack tracing placeholder (-1 means unused)
        private int4 stackPlaceholderSlot;
        /// Number of input parameters to ignore before prototype
        private int4 paramshift;
        /// Number of calls to this sub-function within the calling function
        private int4 matchCallCount;
        /// Info for recovering input parameters
        private ParamActive activeinput;
        /// Info for recovering output parameters
        private ParamActive activeoutput;
        /// Number of bytes consumed by sub-function, for each input parameter
        private /*mutable*/ List<int4> inputConsume;
        /// Are we actively trying to recover input parameters
        private bool isinputactive;
        /// Are we actively trying to recover output parameters
        private bool isoutputactive;
        /// Was the call originally a jump-table we couldn't recover
        private bool isbadjumptable;

        /// Get the active stack-pointer Varnode at \b this call site
        /// Find an instance of the stack-pointer (spacebase register) that is active at the
        /// point of \b this CALL, by examining the \e stack-pointer \e placeholder slot.
        /// \return the stack-pointer Varnode
        private Varnode getSpacebaseRelative()
        {
            if (stackPlaceholderSlot < 0) return (Varnode*)0;
            Varnode* tmpvn = op->getIn(stackPlaceholderSlot);
            if (!tmpvn->isSpacebasePlaceholder()) return (Varnode*)0;
            if (!tmpvn->isWritten()) return (Varnode*)0;
            PcodeOp* loadop = tmpvn->getDef();
            if (loadop->code() != CPUI_LOAD) return (Varnode*)0;
            return loadop->getIn(1);    // The load input (ptr) is the reference we want
        }

        /// \brief Build a Varnode representing a specific parameter
        ///
        /// If the Varnode holding the parameter directly as input to the CALL is available,
        /// it must be provided to this method.  If it is not available, this assumes an
        /// (indirect) stack Varnode is needed and builds one. If the holding Varnode is the
        /// correct size it is returned, otherwise a truncated Varnode is constructed.
        /// \param data is the calling function
        /// \param vn is the Varnode holding the parameter (or NULL for a stack parameter)
        /// \param param is the actual parameter description
        /// \param stackref is the stack-pointer placeholder for \b this function
        /// \return the Varnode that exactly matches the parameter
        private Varnode buildParam(Funcdata data, Varnode vn, ProtoParameter param,
            Varnode stackref)
        {
            if (vn == (Varnode*)0)
            {   // Need to build a spacebase relative varnode
                AddrSpace* spc = param->getAddress().getSpace();
                uintb off = param->getAddress().getOffset();
                int4 sz = param->getSize();
                vn = data.opStackLoad(spc, off, sz, op, stackref, false);
                return vn;
            }
            if (vn->getSize() == param->getSize()) return vn;
            PcodeOp* newop = data.newOp(2, op->getAddr());
            data.opSetOpcode(newop, CPUI_SUBPIECE);
            Varnode* newout = data.newUniqueOut(param->getSize(), newop);
            // Its possible vn is free, in which case the SetInput would give it multiple descendants
            // See we construct a new version
            if (vn->isFree() && !vn->isConstant() && !vn->hasNoDescend())
                vn = data.newVarnode(vn->getSize(), vn->getAddr());
            data.opSetInput(newop, vn, 0);
            data.opSetInput(newop, data.newConstant(4, 0), 1);
            data.opInsertBefore(newop, op);
            return newout;
        }

        /// \brief Get the index of the CALL input Varnode that matches the given parameter
        ///
        /// This method facilitates the building of a Varnode matching the given parameter
        /// from existing data-flow. Return either:
        ///   - 0      if the Varnode can't be built
        ///   - slot#  for the input Varnode to reuse
        ///   - -1     if the parameter needs to be built from the stack
        /// \param param is the given parameter to match
        /// \return the encoded slot
        private int4 transferLockedInputParam(ProtoParameter param)
        {
            int4 numtrials = activeinput.getNumTrials();
            Address startaddr = param->getAddress();
            int4 sz = param->getSize();
            Address lastaddr = startaddr + (sz - 1);
            for (int4 i = 0; i < numtrials; ++i)
            {
                ParamTrial & curtrial(activeinput.getTrial(i));
                if (startaddr < curtrial.getAddress()) continue;
                Address trialend = curtrial.getAddress() + (curtrial.getSize() - 1);
                if (trialend < lastaddr) continue;
                if (curtrial.isDefinitelyNotUsed()) return 0;   // Trial has already been stripped
                return curtrial.getSlot();
            }
            if (startaddr.getSpace()->getType() == IPTR_SPACEBASE)
                return -1;
            return 0;
        }

        /// Return the p-code op whose output Varnode corresponds to the given parameter (return value)
        ///
        /// The Varnode may be attached to the base CALL or CALLIND, but it also may be
        /// attached to an INDIRECT preceding the CALL. The output Varnode may not exactly match
        /// the dimensions of the given parameter. We return non-null if either:
        ///    - The parameter contains the Varnode   (the easier case)  OR if
        ///    - The Varnode properly contains the parameter
        /// \param param is the given paramter (return value)
        /// \return the matching PcodeOp or NULL
        private PcodeOp transferLockedOutputParam(ProtoParameter param)
        {
            Varnode* vn = op->getOut();
            if (vn != (Varnode*)0)
            {
                if (param->getAddress().justifiedContain(param->getSize(), vn->getAddr(), vn->getSize(), false) == 0)
                    return op;
                if (vn->getAddr().justifiedContain(vn->getSize(), param->getAddress(), param->getSize(), false) == 0)
                    return op;
                return (PcodeOp*)0;
            }
            PcodeOp* indop = op->previousOp();
            while ((indop != (PcodeOp*)0) && (indop->code() == CPUI_INDIRECT))
            {
                if (indop->isIndirectCreation())
                {
                    vn = indop->getOut();
                    if (param->getAddress().justifiedContain(param->getSize(), vn->getAddr(), vn->getSize(), false) == 0)
                        return indop;
                    if (vn->getAddr().justifiedContain(vn->getSize(), param->getAddress(), param->getSize(), false) == 0)
                        return indop;
                }
                indop = indop->previousOp();
            }
            return (PcodeOp*)0;
        }

        /// \brief List and/or create a Varnode for each input parameter of matching a source prototype
        ///
        /// Varnodes are taken for current trials associated with \b this call spec.
        /// Varnodes will be passed back in the order that they match the source input parameters.
        /// A NULL Varnode indicates a stack parameter. Varnode dimensions may not match
        /// parameter dimensions exactly.
        /// \param newinput will hold the resulting list of Varnodes
        /// \param source is the source prototype
        /// \return \b false only if the list needs to indicate stack variables and there is no stack-pointer placeholder
        private bool transferLockedInput(List<Varnode> newinput, FuncProto source)
        {
            newinput.push_back(op->getIn(0)); // Always keep the call destination address
            int4 numparams = source.numParams();
            Varnode* stackref = (Varnode*)0;
            for (int4 i = 0; i < numparams; ++i)
            {
                int4 reuse = transferLockedInputParam(source.getParam(i));
                if (reuse == 0) return false;
                if (reuse > 0)
                    newinput.push_back(op->getIn(reuse));
                else
                {
                    if (stackref == (Varnode*)0)
                        stackref = getSpacebaseRelative();
                    if (stackref == (Varnode*)0)
                        return false;
                    newinput.push_back((Varnode*)0);
                }
            }
            return true;
        }

        /// \brief Pass back the Varnode needed to match the output parameter (return value) of a source prototype
        ///
        /// Search for the Varnode matching the output parameter and pass
        /// it back. The dimensions of the Varnode may not exactly match the return value.
        /// If the return value is \e void, a NULL is passed back.
        /// \param newoutput will hold the passed back Varnode
        /// \param source is the source prototype
        /// \return \b true if the passed back value is accurate
        private bool transferLockedOutput(Varnode newoutput, FuncProto source)
        {
            ProtoParameter* param = source.getOutput();
            if (param->getType()->getMetatype() == TYPE_VOID)
            {
                newoutput = (Varnode*)0;
                return true;
            }
            PcodeOp* outop = transferLockedOutputParam(param);
            if (outop == (PcodeOp*)0)
                newoutput = (Varnode*)0;
            else
                newoutput = outop->getOut();
            return true;
        }

        /// \brief Update input Varnodes to \b this CALL to reflect the formal input parameters
        ///
        /// The current input parameters must be locked and are presumably out of date
        /// with the current state of the CALL Varnodes. These existing input Varnodes must
        /// already be gathered in a list. Each Varnode is updated to reflect the parameters,
        /// which may involve truncating or extending. Any active trials and stack-pointer
        /// placeholder is updated, and the new Varnodes are set as the CALL input.
        /// \param data is the calling function
        /// \param newinput holds old input Varnodes and will hold new input Varnodes
        private void commitNewInputs(Funcdata data, List<Varnode> newinput)
        {
            if (!isInputLocked()) return;
            Varnode* stackref = getSpacebaseRelative();
            Varnode* placeholder = (Varnode*)0;
            if (stackPlaceholderSlot >= 0)
                placeholder = op->getIn(stackPlaceholderSlot);
            bool noplacehold = true;

            // Clear activeinput and old placeholder
            stackPlaceholderSlot = -1;
            int4 numPasses = activeinput.getNumPasses();
            activeinput.clear();

            int4 numparams = numParams();
            for (int4 i = 0; i < numparams; ++i)
            {
                ProtoParameter* param = getParam(i);
                Varnode* vn = buildParam(data, newinput[1 + i], param, stackref);
                newinput[1 + i] = vn;
                activeinput.registerTrial(param->getAddress(), param->getSize());
                activeinput.getTrial(i).markActive(); // Parameter is not optional
                if (noplacehold && (param->getAddress().getSpace()->getType() == IPTR_SPACEBASE))
                {
                    // We have a locked stack parameter, use it to recover the stack offset
                    vn->setSpacebasePlaceholder();
                    noplacehold = false;    // Only set this on the first parameter
                    placeholder = (Varnode*)0;  // With a locked stack param, we don't need a placeholder
                }
            }
            if (placeholder != (Varnode*)0)
            {       // If we still need a placeholder
                newinput.push_back(placeholder);        // Add it at end of parameters
                setStackPlaceholderSlot(newinput.size() - 1);
            }
            data.opSetAllInput(op, newinput);
            if (!isDotdotdot())     // Unless we are looking for varargs
                clearActiveInput();     // turn off parameter recovery (we are locked and have all our varnodes)
            else
            {
                if (numPasses > 0)
                    activeinput.finishPass();       // Don't totally reset the pass counter
            }
        }

        /// \brief Update output Varnode to \b this CALL to reflect the formal return value
        ///
        /// The current return value must be locked and is presumably out of date
        /// with the current CALL output. Unless the return value is \e void, the
        /// output Varnode must exist and must be provided.
        /// The Varnode is updated to reflect the return value,
        /// which may involve truncating or extending. Any active trials are updated,
        /// and the new Varnode is set as the CALL output.
        /// \param data is the calling function
        /// \param newout is the provided old output Varnode (or NULL)
        private void commitNewOutputs(Funcdata data, Varnode newout)
        {
            if (!isOutputLocked()) return;
            activeoutput.clear();

            if (newout != (Varnode*)0)
            {
                ProtoParameter* param = getOutput();
                // We could conceivably truncate the output to the correct size to match the parameter
                activeoutput.registerTrial(param->getAddress(), param->getSize());
                PcodeOp* indop = newout->getDef();
                if (newout->getSize() == 1 && param->getType()->getMetatype() == TYPE_BOOL && data.isTypeRecoveryOn())
                    data.opMarkCalculatedBool(op);
                if (newout->getSize() == param->getSize())
                {
                    if (indop != op)
                    {
                        data.opUnsetOutput(indop);
                        data.opUnlink(indop);   // We know this is an indirect creation which is no longer used
                                                // If we reach here, we know -op- must have no output
                        data.opSetOutput(op, newout);
                    }
                }
                else if (newout->getSize() < param->getSize())
                {
                    // We know newout is properly justified within param
                    if (indop != op)
                    {
                        data.opUninsert(indop);
                        data.opSetOpcode(indop, CPUI_SUBPIECE);
                    }
                    else
                    {
                        indop = data.newOp(2, op->getAddr());
                        data.opSetOpcode(indop, CPUI_SUBPIECE);
                        data.opSetOutput(indop, newout);    // Move -newout- from -op- to -indop-
                    }
                    Varnode* realout = data.newVarnodeOut(param->getSize(), param->getAddress(), op);
                    data.opSetInput(indop, realout, 0);
                    data.opSetInput(indop, data.newConstant(4, 0), 1);
                    data.opInsertAfter(indop, op);
                }
                else
                {           // param->getSize() < newout->getSize()
                            // We know param is justified contained in newout
                    VarnodeData vardata;
                    // Test whether the new prototype naturally extends its output
                    OpCode opc = assumedOutputExtension(param->getAddress(), param->getSize(), vardata);
                    Address hiaddr = newout->getAddr();
                    if (opc != CPUI_COPY)
                    {
                        // If -newout- looks like a natural extension of the true output type, create the extension op
                        if (opc == CPUI_PIECE)
                        {   // Extend based on the datatype
                            if (param->getType()->getMetatype() == TYPE_INT)
                                opc = CPUI_INT_SEXT;
                            else
                                opc = CPUI_INT_ZEXT;
                        }
                        if (indop != op)
                        {
                            data.opUninsert(indop);
                            data.opRemoveInput(indop, 1);
                            data.opSetOpcode(indop, opc);
                            Varnode* outvn = data.newVarnodeOut(param->getSize(), param->getAddress(), op);
                            data.opSetInput(indop, outvn, 0);
                            data.opInsertAfter(indop, op);
                        }
                        else
                        {
                            PcodeOp* extop = data.newOp(1, op->getAddr());
                            data.opSetOpcode(extop, opc);
                            data.opSetOutput(extop, newout);    // Move newout from -op- to -extop-
                            Varnode* outvn = data.newVarnodeOut(param->getSize(), param->getAddress(), op);
                            data.opSetInput(extop, outvn, 0);
                            data.opInsertAfter(extop, op);
                        }
                    }
                    else
                    {   // If all else fails, concatenate in extra byte from something "indirectly created" by -op-
                        int4 hisz = newout->getSize() - param->getSize();
                        if (!newout->getAddr().getSpace()->isBigEndian())
                            hiaddr = hiaddr + param->getSize();
                        PcodeOp* newindop = data.newIndirectCreation(op, hiaddr, hisz, true);
                        if (indop != op)
                        {
                            data.opUninsert(indop);
                            data.opSetOpcode(indop, CPUI_PIECE);
                            Varnode* outvn = data.newVarnodeOut(param->getSize(), param->getAddress(), op);
                            data.opSetInput(indop, newindop->getOut(), 0);
                            data.opSetInput(indop, outvn, 1);
                            data.opInsertAfter(indop, op);
                        }
                        else
                        {
                            PcodeOp* concatop = data.newOp(2, op->getAddr());
                            data.opSetOpcode(concatop, CPUI_PIECE);
                            data.opSetOutput(concatop, newout); // Move newout from -op- to -concatop-
                            Varnode* outvn = data.newVarnodeOut(param->getSize(), param->getAddress(), op);
                            data.opSetInput(concatop, newindop->getOut(), 0);
                            data.opSetInput(concatop, outvn, 1);
                            data.opInsertAfter(concatop, op);
                        }
                    }
                }
            }
            clearActiveOutput();
        }

        /// Collect Varnode objects associated with each output trial
        ///
        /// Varnodes can be attached to the CALL or CALLIND or one of the
        /// preceding INDIRECTs. They are passed back in a list matching the
        /// order of the trials.
        /// \param trialvn holds the resulting list of Varnodes
        private void collectOutputTrialVarnodes(List<Varnode> trialvn)
        {
            if (op->getOut() != (Varnode*)0)
                throw LowlevelError("Output of call was determined prematurely");
            while (trialvn.size() < activeoutput.getNumTrials()) // Size of array should match number of trials
                trialvn.push_back((Varnode*)0);
            PcodeOp* indop = op->previousOp();
            while (indop != (PcodeOp*)0)
            {
                if (indop->code() != CPUI_INDIRECT) break;
                if (indop->isIndirectCreation())
                {
                    Varnode* vn = indop->getOut();
                    int4 index = activeoutput.whichTrial(vn->getAddr(), vn->getSize());
                    if (index >= 0)
                    {
                        trialvn[index] = vn;
                        // the exact varnode may have changed, so we reset the trial
                        activeoutput.getTrial(index).setAddress(vn->getAddr(), vn->getSize());
                    }
                }
                indop = indop->previousOp();
            }
        }

        /// Set the slot of the stack-pointer placeholder
        private void setStackPlaceholderSlot(int4 slot)
        {
            stackPlaceholderSlot = slot;
            if (isinputactive) activeinput.setPlaceholderSlot();
        }

        /// Release the stack-pointer placeholder
        private void clearStackPlaceholderSlot()
        {
            stackPlaceholderSlot = -1; if (isinputactive) activeinput.freePlaceholderSlot();
        }
        
        public enum Offsets
        {
            offset_unknown = 0xBADBEEF                  ///< "Magic" stack offset indicating the offset is unknown
        }

        /// Construct based on CALL or CALLIND
        /// \param call_op is the representative call site within the data-flow
        public FuncCallSpecs(PcodeOp call_op)
            : base()
        {
            activeinput = true;
            activeoutput = true;
            effective_extrapop = ProtoModel::extrapop_unknown;
            stackoffset = offset_unknown;
            stackPlaceholderSlot = -1;
            paramshift = 0;
            op = call_op;
            fd = (Funcdata*)0;
            if (call_op->code() == CPUI_CALL)
            {
                entryaddress = call_op->getIn(0)->getAddr();
                if (entryaddress.getSpace()->getType() == IPTR_FSPEC)
                {
                    // op->getIn(0) was already converted to fspec pointer
                    // This can happen if we are cloning an op for inlining
                    FuncCallSpecs* otherfc = FuncCallSpecs::getFspecFromConst(entryaddress);
                    entryaddress = otherfc->entryaddress;
                }
            }
            // If call is indirect, we leave address as invalid
            isinputactive = false;
            isoutputactive = false;
            isbadjumptable = false;
        }

        /// Set (override) the callee's entry address
        public void setAddress(Address addr)
        {
            entryaddress = addr;
        }

        /// Get the CALL or CALLIND corresponding to \b this
        public PcodeOp getOp() => op;

        /// Get the Funcdata object associated with the called function
        public Funcdata getFuncdata() => fd;

        /// Set the Funcdata object associated with the called function
        public void setFuncdata(Funcdata f)
        {
            if (fd != (Funcdata*)0)
                throw LowlevelError("Setting call spec function multiple times");
            fd = f;
            if (fd != (Funcdata*)0)
            {
                entryaddress = fd->getAddress();
                if (fd->getDisplayName().size() != 0)
                    name = fd->getDisplayName();
            }
        }

        /// Clone \b this given the mirrored p-code CALL
        /// \param newop replaces the CALL or CALLIND op in the clone
        /// \return the cloned FuncCallSpecs
        public FuncCallSpecs clone(PcodeOp newop)
        {
            FuncCallSpecs* res = new FuncCallSpecs(newop);
            res->setFuncdata(fd);
            // This sets op, name, address, fd
            res->effective_extrapop = effective_extrapop;
            res->stackoffset = stackoffset;
            res->paramshift = paramshift;
            // We are skipping activeinput, activeoutput
            res->isbadjumptable = isbadjumptable;
            res->copy(*this);       // Copy the FuncProto portion
            return res;
        }

        /// Get the function name associated with the callee
        public string getName() => name;

        /// Get the entry address of the callee
        public Address getEntryAddress() => entryaddress;

        /// Set the specific \e extrapop associate with \b this call site
        public void setEffectiveExtraPop(int4 epop)
        {
            effective_extrapop = epop;
        }

        public int4 getEffectiveExtraPop() => effective_extrapop; ///< Get the specific \e extrapop associate with \b this call site

        public uintb getSpacebaseOffset() => stackoffset; ///< Get the stack-pointer relative offset at the point of \b this call site

        /// Set a parameter shift for this call site
        public void setParamshift(int4 val)
        {
            paramshift = val;
        }

        /// Get the parameter shift for this call site
        public int4 getParamshift() => paramshift;

        /// Get the number of calls the caller makes to \b this sub-function
        public int4 getMatchCallCount() => matchCallCount;

        /// Get the slot of the stack-pointer placeholder
        public int4 getStackPlaceholderSlot() => stackPlaceholderSlot;

        /// Turn on analysis recovering input parameters
        public void initActiveInput()
        {
            isinputactive = true;
            int4 maxdelay = getMaxInputDelay();
            if (maxdelay > 0)
                maxdelay = 3;
            activeinput.setMaxPass(maxdelay);
        }

        /// Turn off analysis recovering input parameters
        public void clearActiveInput()
        {
            isinputactive = false;
        }

        /// Turn on analysis recovering the return value
        public void initActiveOutput()
        {
            isoutputactive = true;
        }

        /// Turn off analysis recovering the return value
        public void clearActiveOutput()
        {
            isoutputactive = false;
        }

        public bool isInputActive() => isinputactive; ///< Return \b true if input parameter recovery analysis is active

        public bool isOutputActive() => isoutputactive; ///< Return \b true if return value recovery analysis is active

        /// Toggle whether \b call site looked like an indirect jump
        public void setBadJumpTable(bool val)
        {
            isbadjumptable = val;
        }

        /// Return \b true if \b this call site looked like an indirect jump
        public bool isBadJumpTable() => isbadjumptable;

        /// Get the analysis object for input parameter recovery
        public ParamActive getActiveInput() => activeinput;

        /// Get the analysis object for return value recovery
        public ParamActive getActiveOutput() => activeoutput;

        /// \brief Check if adjacent parameter trials can be combined into a single logical parameter
        ///
        /// A slot must be provided indicating the trial and the only following it.
        /// \param slot1 is the first trial slot
        /// \param ishislot is \b true if the first slot will be the most significant piece
        /// \param vn1 is the Varnode corresponding to the first trial
        /// \param vn2 is the Varnode corresponding to the second trial
        /// \return \b true if the trials can be combined
        public bool checkInputJoin(int4 slot1, bool ishislot, Varnode vn1, Varnode vn2)
        {
            if (isInputActive()) return false;
            if (slot1 >= activeinput.getNumTrials()) return false; // Not enough params
            const ParamTrial* hislot,*loslot;
            if (ishislot)
            {       // slot1 looks like the high slot
                hislot = &activeinput.getTrialForInputVarnode(slot1);
                loslot = &activeinput.getTrialForInputVarnode(slot1 + 1);
                if (hislot->getSize() != vn1->getSize()) return false;
                if (loslot->getSize() != vn2->getSize()) return false;
            }
            else
            {
                loslot = &activeinput.getTrialForInputVarnode(slot1);
                hislot = &activeinput.getTrialForInputVarnode(slot1 + 1);
                if (loslot->getSize() != vn1->getSize()) return false;
                if (hislot->getSize() != vn2->getSize()) return false;
            }
            return FuncProto::checkInputJoin(hislot->getAddress(), hislot->getSize(), loslot->getAddress(), loslot->getSize());
        }

        /// \brief Join two parameter trials
        ///
        /// We assume checkInputJoin() has returned \b true. Perform the join, replacing
        /// the given adjacent trials with a single merged parameter.
        /// \param slot1 is the trial slot of the first trial
        /// \param ishislot is \b true if the first slot will be the most significant piece
        public void doInputJoin(int4 slot1, bool ishislot)
        {
            if (isInputLocked())
                throw LowlevelError("Trying to join parameters on locked function prototype");

            const ParamTrial &trial1(activeinput.getTrialForInputVarnode(slot1));
            const ParamTrial &trial2(activeinput.getTrialForInputVarnode(slot1 + 1));

            const Address &addr1(trial1.getAddress());
            const Address &addr2(trial2.getAddress());
            Architecture* glb = getArch();
            Address joinaddr;
            if (ishislot)
                joinaddr = glb->constructJoinAddress(glb->translate, addr1, trial1.getSize(), addr2, trial2.getSize());
            else
                joinaddr = glb->constructJoinAddress(glb->translate, addr2, trial2.getSize(), addr1, trial1.getSize());

            activeinput.joinTrial(slot1, joinaddr, trial1.getSize() + trial2.getSize());
        }

        /// \brief Update \b this prototype to match a given (more specialized) prototype
        ///
        /// This method assumes that \b this prototype is in some intermediate state during the
        /// parameter recovery process and that a new definitive (locked) prototype is discovered
        /// for \b this call site.  This method checks to see if \b this can be updated to match the
        /// new prototype without missing any data-flow.  If so, \b this is updated, and new input
        /// and output Varnodes for the CALL are passed back.
        /// \param restrictedProto is the new definitive function prototype
        /// \param newinput will hold the new list of input Varnodes for the CALL
        /// \param newoutput will hold the new output Varnode or NULL
        /// \return \b true if \b this can be fully converted
        public bool lateRestriction(FuncProto restrictedProto, List<Varnode> newinput, Varnode newoutput)
        {
            if (!hasModel())
            {
                copy(restrictedProto);
                return true;
            }

            if (!isCompatible(restrictedProto)) return false;
            if (restrictedProto.isDotdotdot() && (!isinputactive)) return false;

            if (restrictedProto.isInputLocked())
            {
                if (!transferLockedInput(newinput, restrictedProto))        // Redo all the varnode inputs (if possible)
                    return false;
            }
            if (restrictedProto.isOutputLocked())
            {
                if (!transferLockedOutput(newoutput, restrictedProto))  // Redo all the varnode outputs (if possible)
                    return false;
            }
            copy(restrictedProto);      // Convert ourselves to restrictedProto

            return true;
        }

        /// \brief Convert \b this call site from an indirect to a direct function call
        ///
        /// This call site must be a CALLIND, and the function that it is actually calling
        /// must be provided.  The method makes a determination if the current
        /// state of data-flow allows converting to the prototype of the new function without
        /// dropping information due to inaccurate dead-code elimination.  If conversion is
        /// safe, it is performed immediately. Otherwise a \e restart directive issued to
        /// force decompilation to restart from scratch (now with the direct function in hand)
        /// \param data is the calling function
        /// \param newfd is the Funcdata object that we know is the destination of \b this CALLIND
        public void deindirect(Funcdata data, Funcdata newfd)
        {
            entryaddress = newfd->getAddress();
            name = newfd->getDisplayName();
            fd = newfd;

            Varnode* vn = data.newVarnodeCallSpecs(this);
            data.opSetInput(op, vn, 0);
            data.opSetOpcode(op, CPUI_CALL);

            data.getOverride().insertIndirectOverride(op->getAddr(), entryaddress);

            // Try our best to merge existing prototype
            // with the one we have just been handed
            vector<Varnode*> newinput;
            Varnode* newoutput;
            FuncProto & newproto(newfd->getFuncProto());
            if ((!newproto.isNoReturn()) && (!newproto.isInline()))
            {
                if (isOverride())   // If we are overridden at the call-site
                    return;     // Don't use the discovered function prototype

                if (lateRestriction(newproto, newinput, newoutput))
                {
                    commitNewInputs(data, newinput);
                    commitNewOutputs(data, newoutput);
                    return; // We have successfully updated the prototype, don't restart
                }
            }
            data.setRestartPending(true);
        }

        /// \brief Force a more restrictive prototype on \b this call site
        ///
        /// A new prototype must be given, typically recovered from a function pointer
        /// data-type that has been propagated to \b this call site.
        /// The method makes a determination if the current
        /// state of data-flow allows converting to the new prototype without
        /// dropping information due to inaccurate dead-code elimination.  If conversion is
        /// safe, it is performed immediately. Otherwise a \e restart directive issued to
        /// force decompilation to restart from scratch (now with the new prototype in hand)
        /// \param data is the calling function
        /// \param fp is the new (more restrictive) function prototype
        public void forceSet(Funcdata data, FuncProto fp)
        {
            vector<Varnode*> newinput;
            Varnode* newoutput;

            // Copy the recovered prototype into the override manager so that
            // future restarts don't have to rediscover it
            FuncProto* newproto = new FuncProto();
            newproto->copy(fp);
            data.getOverride().insertProtoOverride(op->getAddr(), newproto);
            if (lateRestriction(fp, newinput, newoutput))
            {
                commitNewInputs(data, newinput);
                commitNewOutputs(data, newoutput);
            }
            else
            {
                // Too late to make restrictions to correct prototype
                // Force a restart
                data.setRestartPending(true);
            }
            // Regardless of what happened, lock the prototype so it doesn't happen again
            setInputLock(true);
            setInputErrors(fp.hasInputErrors());
            setOutputErrors(fp.hasOutputErrors());
        }

        /// \brief Inject any \e upon-return p-code at \b this call site
        ///
        /// This function prototype may trigger injection of p-code immediately after
        /// the CALL or CALLIND to mimic a portion of the callee that decompilation
        /// of the caller otherwise wouldn't see.
        /// \param data is the calling function
        public void insertPcode(Funcdata data)
        {
            int4 id = getInjectUponReturn();
            if (id < 0) return;     // Nothing to inject
            InjectPayload* payload = data.getArch()->pcodeinjectlib->getPayload(id);

            // do the insertion right after the callpoint
            list<PcodeOp*>::iterator iter = op->getBasicIter();
            ++iter;
            data.doLiveInject(payload, op->getAddr(), op->getParent(), iter);
        }

        /// \brief Add a an input parameter that will resolve to the current stack offset for \b this call site
        ///
        /// A LOAD from a free reference to the \e spacebase pointer of the given AddrSpace is created and
        /// its output is added as a parameter to the call.  Later the LOAD should resolve to a COPY from
        /// a Varnode in the AddrSpace, whose offset is then the current offset.
        /// \param data is the function where the LOAD is created
        /// \param spacebase is the given (stack) AddrSpace
        public void createPlaceholder(Funcdata data, AddrSpace spacebase)
        {
            int4 slot = op->numInput();
            Varnode* loadval = data.opStackLoad(spacebase, 0, 1, op, (Varnode*)0, false);
            data.opInsertInput(op, loadval, slot);
            setStackPlaceholderSlot(slot);
            loadval->setSpacebasePlaceholder();
        }

        /// \brief Calculate the stack offset of \b this call site
        ///
        /// The given Varnode must be the input to the CALL in the \e placeholder slot
        /// and must be defined by a COPY from a Varnode in the stack space.
        /// Calculate the offset of the stack-pointer at the point of \b this CALL,
        /// relative to the incoming stack-pointer value.  This can be obtained
        /// either be looking at a stack parameter, or if there is no stack parameter,
        /// the stack-pointer \e placeholder can be used.
        /// If the \e placeholder has no other purpose, remove it.
        /// \param data is the calling function
        /// \param phvn is the Varnode in the \e placeholder slot for \b this CALL
        public void resolveSpacebaseRelative(Funcdata data, Varnode phvn)
        {
            Varnode* refvn = phvn->getDef()->getIn(0);
            AddrSpace* spacebase = refvn->getSpace();
            if (spacebase->getType() != IPTR_SPACEBASE)
            {
                data.warningHeader("This function may have set the stack pointer");
            }
            stackoffset = refvn->getOffset();

            if (stackPlaceholderSlot >= 0)
            {
                if (op->getIn(stackPlaceholderSlot) == phvn)
                {
                    abortSpacebaseRelative(data);
                    return;
                }
            }

            if (isInputLocked())
            {
                // The prototype is locked and had stack parameters, we grab the relative offset from this
                // rather than from a placeholder
                int4 slot = op->getSlot(phvn) - 1;
                if (slot >= numParams())
                    throw LowlevelError("Stack placeholder does not line up with locked parameter");
                ProtoParameter* param = getParam(slot);
                Address addr = param->getAddress();
                if (addr.getSpace() != spacebase)
                {
                    if (spacebase->getType() == IPTR_SPACEBASE)
                        throw LowlevelError("Stack placeholder does not match locked space");
                }
                stackoffset -= addr.getOffset();
                stackoffset = spacebase->wrapOffset(stackoffset);
                return;
            }
            throw LowlevelError("Unresolved stack placeholder");
        }

        /// \brief Abort the attempt to recover the relative stack offset for \b this function
        ///
        /// Any stack-pointer \e placeholder is removed.
        /// \param data is the calling function
        public void abortSpacebaseRelative(Funcdata data)
        {
            if (stackPlaceholderSlot >= 0)
            {
                Varnode* vn = op->getIn(stackPlaceholderSlot);
                data.opRemoveInput(op, stackPlaceholderSlot);
                clearStackPlaceholderSlot();
                // Remove the op producing the placeholder as well
                if (vn->hasNoDescend() && vn->getSpace()->getType() == IPTR_INTERNAL && vn->isWritten())
                    data.opDestroy(vn->getDef());
            }
        }

        /// \brief Make final activity check on trials that might have been affected by conditional execution
        ///
        /// The activity level a trial may change once conditional execution has been analyzed.
        /// This routine (re)checks trials that might be affected by this, which may then
        /// be converted to \e not \e used.
        public void finalInputCheck()
        {
            AncestorRealistic ancestorReal;
            for (int4 i = 0; i < activeinput.getNumTrials(); ++i)
            {
                ParamTrial & trial(activeinput.getTrial(i));
                if (!trial.isActive()) continue;
                if (!trial.hasCondExeEffect()) continue;
                int4 slot = trial.getSlot();
                if (!ancestorReal.execute(op, slot, &trial, false))
                    trial.markNoUse();
            }
        }

        /// \brief Mark if input trials are being actively used
        ///
        /// Run through each input trial and try to make a determination if the trial is \e active or not,
        /// meaning basically that a write has occurred on the trial with no intervening reads between
        /// the write and the call.
        /// \param data is the calling function
        /// \param aliascheck holds local aliasing information about the function
        public void checkInputTrialUse(Funcdata data, AliasChecker aliascheck)
        {
            if (op->isDead())
                throw LowlevelError("Function call in dead code");

            int4 maxancestor = data.getArch()->trim_recurse_max;
            bool callee_pop = false;
            int4 expop = 0;
            if (hasModel())
            {
                callee_pop = (getModelExtraPop() == ProtoModel::extrapop_unknown);
                if (callee_pop)
                {
                    expop = getExtraPop();
                    // Tried to use getEffectiveExtraPop at one point, but it is too unreliable
                    if ((expop == ProtoModel::extrapop_unknown) || (expop <= 4))
                        callee_pop = false;
                    // If the subfunctions do their own parameter popping and
                    // if the extrapop is successfully recovered this is hard evidence
                    // about which trials are active
                    // If the extrapop is 4, this might be a _cdecl convention, and doesn't necessarily mean
                    // that there are no parameters
                }
            }

            AncestorRealistic ancestorReal;
            for (int4 i = 0; i < activeinput.getNumTrials(); ++i)
            {
                ParamTrial & trial(activeinput.getTrial(i));
                if (trial.isChecked()) continue;
                int4 slot = trial.getSlot();
                Varnode* vn = op->getIn(slot);
                if (vn->getSpace()->getType() == IPTR_SPACEBASE)
                {
                    if (aliascheck.hasLocalAlias(vn))
                        trial.markNoUse();
                    else if (!data.getFuncProto().getLocalRange().inRange(vn->getAddr(), 1))
                        trial.markNoUse();
                    else if (callee_pop)
                    {
                        if ((int4)(trial.getAddress().getOffset() + (trial.getSize() - 1)) < expop)
                            trial.markActive();
                        else
                            trial.markNoUse();
                    }
                    else if (ancestorReal.execute(op, slot, &trial, false))
                    {
                        if (data.ancestorOpUse(maxancestor, vn, op, trial, 0, 0))
                            trial.markActive();
                        else
                            trial.markInactive();
                    }
                    else
                        trial.markNoUse(); // Stackvar for unrealistic ancestor is definitely not a parameter
                }
                else
                {
                    if (ancestorReal.execute(op, slot, &trial, true))
                    {
                        if (data.ancestorOpUse(maxancestor, vn, op, trial, 0, 0))
                        {
                            trial.markActive();
                            if (trial.hasCondExeEffect())
                                activeinput.markNeedsFinalCheck();
                        }
                        else
                            trial.markInactive();
                    }
                    else if (vn->isInput()) // Not likely a parameter but maybe
                        trial.markInactive();
                    else
                        trial.markNoUse();  // An ancestor is unaffected, an unusual input, or killed by a call
                }
                if (trial.isDefinitelyNotUsed())    // If definitely not used, free up the dataflow
                    data.opSetInput(op, data.newConstant(vn->getSize(), 0), slot);
            }
        }

        /// \brief Mark if output trials are being actively used
        ///
        /// Run through each output trial and try to make a determination if the trial is \e active or not,
        /// meaning basically that the first occurrence of a trial after the call is a read.
        /// \param data is the calling function
        /// \param trialvn will hold Varnodes corresponding to the trials
        public void checkOutputTrialUse(Funcdata data, List<Varnode> trialvn)
        {
            collectOutputTrialVarnodes(trialvn);
            // The location is either used or not.  If it is used it can either be the official output
            // or a killedbycall, so whether the trial is present as a varnode (as determined by dataflow
            // and deadcode analysis) determines whether we consider the trial active or not
            for (int4 i = 0; i < trialvn.size(); ++i)
            {
                ParamTrial & curtrial(activeoutput.getTrial(i));
                if (curtrial.isChecked())
                    throw LowlevelError("Output trial has been checked prematurely");
                if (trialvn[i] != (Varnode*)0)
                    curtrial.markActive();
                else
                    curtrial.markInactive(); // don't call markNoUse, the value may be returned but not used
            }
        }

        /// \brief Set the final input Varnodes to \b this CALL based on ParamActive analysis
        ///
        /// Varnodes that don't look like parameters are removed. Parameters that are unreferenced
        /// are filled in. Other Varnode inputs may be truncated or extended.  This prototype
        /// itself is unchanged.
        /// \param data is the calling function
        public void buildInputFromTrials(Funcdata data)
        {
            AddrSpace* spc;
            uintb off;
            int4 sz;
            bool isspacebase;
            Varnode* vn;
            vector<Varnode*> newparam;

            newparam.push_back(op->getIn(0)); // Preserve the fspec parameter

            if (isDotdotdot() && isInputLocked())
            {
                //if varargs, move the fixed args to the beginning of the list in order
                //preserve relative order of variable args
                activeinput.sortFixedPosition();
            }

            for (int4 i = 0; i < activeinput.getNumTrials(); ++i)
            {
                const ParamTrial &paramtrial(activeinput.getTrial(i));
                if (!paramtrial.isUsed()) continue; // Don't keep unused parameters
                sz = paramtrial.getSize();
                isspacebase = false;
                const Address &addr(paramtrial.getAddress());
                spc = addr.getSpace();
                off = addr.getOffset();
                if (spc->getType() == IPTR_SPACEBASE)
                {
                    isspacebase = true;
                    off = spc->wrapOffset(stackoffset + off);   // Translate the parameter address relative to caller's spacebase
                }
                if (paramtrial.isUnref())
                {   // recovered unreferenced address as part of prototype
                    vn = data.newVarnode(sz, Address(spc, off)); // We need to create the varnode
                }
                else
                {
                    vn = op->getIn(paramtrial.getSlot()); // Where parameter is currently
                    if (vn->getSize() > sz)
                    {   // Varnode is bigger than type
                        Varnode* outvn; // Create truncate op
                        PcodeOp* newop = data.newOp(2, op->getAddr());
                        if (data.getArch()->translate->isBigEndian())
                            outvn = data.newVarnodeOut(sz, vn->getAddr() + (vn->getSize() - sz), newop);
                        else
                            outvn = data.newVarnodeOut(sz, vn->getAddr(), newop);
                        data.opSetOpcode(newop, CPUI_SUBPIECE);
                        data.opSetInput(newop, vn, 0);
                        data.opSetInput(newop, data.newConstant(1, 0), 1);
                        data.opInsertBefore(newop, op);
                        vn = outvn;
                    }
                }
                newparam.push_back(vn);
                // Mark the stack range used to pass this parameter as unmapped
                if (isspacebase)
                    data.getScopeLocal()->markNotMapped(spc, off, sz, true);
            }
            data.opSetAllInput(op, newparam); // Set final parameter list
            activeinput.deleteUnusedTrials();
        }

        /// \brief Set the final output Varnode of \b this CALL based on ParamActive analysis of trials
        ///
        /// If it exists, the active output trial is moved to be the output Varnode of \b this CALL.
        /// If there are two active trials, they are merged as a single output of the CALL.
        /// Any INDIRECT ops that were holding the active trials are removed.
        /// This prototype itself is unchanged.
        /// \param data is the calling function
        /// \param trialvn is the list of Varnodes associated with trials
        public void buildOutputFromTrials(Funcdata data, List<Varnode> trialvn)
        {
            Varnode* finaloutvn;
            vector<Varnode*> finalvn;

            for (int4 i = 0; i < activeoutput.getNumTrials(); ++i)
            { // Reorder the varnodes
                ParamTrial & curtrial(activeoutput.getTrial(i));
                if (!curtrial.isUsed()) break;
                Varnode* vn = trialvn[curtrial.getSlot() - 1];
                finalvn.push_back(vn);
            }
            activeoutput.deleteUnusedTrials(); // This deletes unused, and renumbers used  (matches finalvn)
            if (activeoutput.getNumTrials() == 0) return; // Nothing is a formal output

            vector<PcodeOp*> deletedops;

            if (activeoutput.getNumTrials() == 1)
            {       // We have a single, properly justified output
                finaloutvn = finalvn[0];
                PcodeOp* indop = finaloutvn->getDef();
                //     ParamTrial &curtrial(activeoutput.getTrial(0));
                //     if (finaloutvn->getSize() != curtrial.getSize()) { // If the varnode does not exactly match the original trial
                //       int4 res = curtrial.getEntry()->justifiedContain(finaloutvn->getAddress(),finaloutvn->getSize());
                //       if (res > 0) {
                // 	data.opUninsert(indop);
                // 	data.opSetOpcode(indop,CPUI_SUBPIECE); // Insert a subpiece
                // 	Varnode *wholevn = data.newVarnodeOut(curtrial.getSize(),curtrial.getAddress(),op);
                // 	data.opSetInput(indop,wholevn,0);
                // 	data.opSetInput(indop,data.newConstant(4,res),1);
                // 	data.opInsertAfter(indop,op);
                // 	return;
                //       }
                //     }
                deletedops.push_back(indop);
                data.opSetOutput(op, finaloutvn); // Move varnode to its new position as output of call
            }
            else if (activeoutput.getNumTrials() == 2)
            {
                Varnode* hivn = finalvn[1]; // orderOutputPieces puts hi last
                Varnode* lovn = finalvn[0];
                if (data.isDoublePrecisOn())
                {
                    lovn->setPrecisLo();    // Mark that these varnodes are part of a larger precision whole
                    hivn->setPrecisHi();
                }
                deletedops.push_back(hivn->getDef());
                deletedops.push_back(lovn->getDef());
                finaloutvn = findPreexistingWhole(hivn, lovn);
                if (finaloutvn == (Varnode*)0)
                {
                    Address joinaddr = data.getArch()->constructJoinAddress(data.getArch()->translate,
                                                hivn->getAddr(), hivn->getSize(),
                                                lovn->getAddr(), lovn->getSize());
                    finaloutvn = data.newVarnode(hivn->getSize() + lovn->getSize(), joinaddr);
                    data.opSetOutput(op, finaloutvn);
                    PcodeOp* sublo = data.newOp(2, op->getAddr());
                    data.opSetOpcode(sublo, CPUI_SUBPIECE);
                    data.opSetInput(sublo, finaloutvn, 0);
                    data.opSetInput(sublo, data.newConstant(4, 0), 1);
                    data.opSetOutput(sublo, lovn);
                    data.opInsertAfter(sublo, op);
                    PcodeOp* subhi = data.newOp(2, op->getAddr());
                    data.opSetOpcode(subhi, CPUI_SUBPIECE);
                    data.opSetInput(subhi, finaloutvn, 0);
                    data.opSetInput(subhi, data.newConstant(4, lovn->getSize()), 1);
                    data.opSetOutput(subhi, hivn);
                    data.opInsertAfter(subhi, op);
                }
                else
                {           // Preexisting whole
                    deletedops.push_back(finaloutvn->getDef()); // Its inputs are used only in this op
                    data.opSetOutput(op, finaloutvn);
                }
            }
            else
                return;

            for (int4 i = 0; i < deletedops.size(); ++i)
            { // Destroy the original INDIRECT ops
                PcodeOp* dop = deletedops[i];
                Varnode* in0 = dop->getIn(0);
                Varnode* in1 = dop->getIn(1);
                data.opDestroy(dop);
                if (in0 != (Varnode*)0)
                    data.deleteVarnode(in0);
                if (in1 != (Varnode*)0)
                    data.deleteVarnode(in1);
            }
        }

        /// \brief Get the estimated number of bytes within the given parameter that are consumed
        ///
        /// As a function is decompiled, there may hints about how many of the bytes, within the
        /// storage location used to pass the parameter, are used by \b this sub-function. A non-zero
        /// value means that that many least significant bytes of the storage location are used. A value
        /// of zero means all bytes are presumed used.
        /// \param slot is the slot of the given input parameter
        /// \return the number of bytes used (or 0)
        public int4 getInputBytesConsumed(int4 slot)
        {
            if (slot >= inputConsume.size())
                return 0;
            return inputConsume[slot];
        }

        /// \brief Set the estimated number of bytes within the given parameter that are consumed
        ///
        /// This provides a hint to the dead code \e consume algorithm, while examining the calling
        /// function, about how the given parameter within the subfunction is used.
        /// A non-zero value means that that many least significant bytes of the storage location
        /// are used. A value of zero means all bytes are presumed used.
        /// \param slot is the slot of the given input parameter
        /// \param val is the number of bytes consumed (or 0)
        /// \return \b true if there was a change in the estimate
        public bool setInputBytesConsumed(int4 slot, int4 val)
        {
            while (inputConsume.size() <= slot)
                inputConsume.push_back(0);
            int4 oldVal = inputConsume[slot];
            if (oldVal == 0 || val < oldVal)
                inputConsume[slot] = val;
            return (oldVal != val);
        }

        /// \brief Prepend any extra parameters if a paramshift is required
        public void paramshiftModifyStart()
        {
            if (paramshift == 0) return;
            paramShift(paramshift);
        }

        /// \brief Throw out any paramshift parameters
        /// \param data is the calling function
        /// \return \b true if a change was made
        public bool paramshiftModifyStop(Funcdata data)
        {
            if (paramshift == 0) return false;
            if (isParamshiftApplied()) return false;
            setParamshiftApplied(true);
            if (op->numInput() < paramshift + 1)
                throw LowlevelError("Paramshift mechanism is confused");
            for (int4 i = 0; i < paramshift; ++i)
            {
                // ProtoStore should have been converted to ProtoStoreInternal by paramshiftModifyStart
                data.opRemoveInput(op, 1);
                removeParam(0);
            }
            return true;
        }

        /// \brief Calculate type of side-effect for a given storage location (with caller translation)
        ///
        /// Stack locations should be provided from the caller's perspective.  They are automatically
        /// translated to the callee's perspective before making the underlying query.
        /// \param addr is the starting address of the storage location
        /// \param size is the number of bytes in the storage
        /// \return the effect type
        public uint4 hasEffectTranslate(Address addr, int4 size)
        {
            AddrSpace* spc = addr.getSpace();
            if (spc->getType() != IPTR_SPACEBASE)
                return hasEffect(addr, size);
            if (stackoffset == offset_unknown) return EffectRecord::unknown_effect;
            uintb newoff = spc->wrapOffset(addr.getOffset() - stackoffset); // Translate to callee's spacebase point of view
            return hasEffect(Address(spc, newoff), size);
        }

        /// \brief Check if given two Varnodes are merged into a whole
        ///
        /// If the Varnodes are merged immediately into a common whole
        /// and aren't used for anything else, return the whole Varnode.
        /// \param vn1 is the first given Varnode
        /// \param vn2 is the second given Varnode
        /// \return the combined Varnode or NULL
        public static Varnode findPreexistingWhole(Varnode vn1, Varnode vn2)
        {
            PcodeOp* op1 = vn1->loneDescend();
            if (op1 == (PcodeOp*)0) return (Varnode*)0;
            PcodeOp* op2 = vn2->loneDescend();
            if (op2 == (PcodeOp*)0) return (Varnode*)0;
            if (op1 != op2) return (Varnode*)0;
            if (op1->code() != CPUI_PIECE) return (Varnode*)0;
            return op1->getOut();
        }

        /// \brief Convert FspecSpace addresses to the underlying FuncCallSpecs object
        ///
        /// \param addr is the given \e fspec address
        /// \return the FuncCallSpecs object
        public static FuncCallSpecs getFspecFromConst(Address addr) => (FuncCallSpecs)(uintp)addr.getOffset();

        /// \brief Compare FuncCallSpecs by function entry address
        ///
        /// \param a is the first FuncCallSpecs to compare
        /// \param b is the second to compare
        /// \return \b true if the first should be ordered before the second
        public static bool compareByEntryAddress(FuncCallSpecs a, FuncCallSpecs b) => a->entryaddress < b->entryaddress;

        /// \brief Calculate the number of times an individual sub-function is called.
        ///
        /// Provided a list of all call sites for a calling function, tally the number of calls
        /// to the same sub-function.  Update the \b matchCallCount field of each FuncCallSpecs
        /// \param qlst is the list of call sites (FuncCallSpecs) for the calling function
        public static void countMatchingCalls(List<FuncCallSpecs> qlst)
        {
            vector<FuncCallSpecs*> copyList(qlst);
            sort(copyList.begin(), copyList.end(), compareByEntryAddress);
            int4 i;
            for (i = 0; i < copyList.size(); ++i)
            {
                if (!copyList[i]->entryaddress.isInvalid()) break;
                copyList[i]->matchCallCount = 1;            // Mark all invalid addresses as a singleton
            }
            if (i == copyList.size()) return;
            Address lastAddr = copyList[i]->entryaddress;
            int4 lastChange = i++;
            int4 num;
            for (; i < copyList.size(); ++i)
            {
                if (copyList[i]->entryaddress == lastAddr) continue;
                num = i - lastChange;
                for (; lastChange < i; ++lastChange)
                    copyList[lastChange]->matchCallCount = num;
                lastAddr = copyList[i]->entryaddress;
            }
            num = i - lastChange;
            for (; lastChange < i; ++lastChange)
                copyList[lastChange]->matchCallCount = num;
        }
    }
}
