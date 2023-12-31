﻿using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for shrinking big Varnodes carrying smaller logical values
    ///
    /// Given a root within the syntax tree and dimensions
    /// of a logical variable, this class traces the flow of this
    /// logical variable through its containing Varnodes.  It then
    /// creates a subgraph of this flow, where there is a correspondence
    /// between nodes in the subgraph and nodes in the original graph
    /// containing the logical variable.  When doReplacement is called,
    /// this subgraph is duplicated as a new separate piece within the
    /// syntax tree.  Ops are replaced to reflect the manipulation of
    /// of the logical variable, rather than the containing variable.
    /// Operations in the original graph which pluck out the logical
    /// variable from the containing variable, are replaced with copies
    /// from the corresponding node in the new section of the graph,
    /// which frequently causes the operations on the original container
    /// Varnodes to becomes dead code.
    internal class SubvariableFlow
    {
        /// \brief Placeholder node for Varnode holding a smaller logical value
        private class ReplaceVarnode
        {
            // friend class SubvariableFlow;
            /// Varnode being shrunk
            internal Varnode vn;
            /// The new smaller Varnode
            internal Varnode replacement;
            /// Bits making up the logical sub-variable
            internal ulong mask;
            /// Value of constant (when vn==NULL)
            internal ulong val;
            /// Defining op for new Varnode
            internal ReplaceOp def;
        }

        /// \brief Placeholder node for PcodeOp operating on smaller logical values
        private class ReplaceOp
        {
            // friend class SubvariableFlow;
            /// op getting paralleled
            internal PcodeOp op;
            /// The new op
            internal PcodeOp replacement;
            /// Opcode of the new op
            internal OpCode opc;
            /// Number of parameters in (new) op
            internal int numparams;
            /// Varnode output
            internal ReplaceVarnode output;
            /// Varnode inputs
            internal List<ReplaceVarnode> input;
        }

        /// \brief Operation with a new logical value as (part of) input, but output Varnode is unchanged
        private class PatchRecord
        {
            // friend class SubvariableFlow;
            /// The possible types of patches on ops being performed
            internal enum patchtype
            {
                /// Turn op into a COPY of the logical value
                copy_patch,
                /// Turn compare op inputs into logical values
                compare_patch,
                /// Convert a CALL/CALLIND/RETURN/BRANCHIND parameter into logical value
                parameter_patch,
                /// Convert op into something that copies/extends logical value, adding zero bits
                extension_patch,
                /// Convert an operator output to the logical value
                push_patch
            }
            /// The type of \b this patch
            internal patchtype type;
            /// Op being affected
            internal PcodeOp patchOp;
            /// The logical variable input
            internal ReplaceVarnode in1;
            /// (optional second parameter)
            internal ReplaceVarnode in2;
            /// slot being affected or other parameter
            internal int slot;
        }

        /// Size of the logical data-flow in bytes
        private int flowsize;
        /// Number of bits in logical variable
        private int bitsize;
        /// Have we tried to flow logical value across CPUI_RETURNs
        private bool returnsTraversed;
        /// Do we "know" initial seed point must be a sub variable
        private bool aggressive;
        /// Check for logical variables that are always sign extended into their container
        private bool sextrestrictions;
        /// Containing function
        private Funcdata fd;
        /// Map from original Varnodes to the overlaying subgraph nodes
        private Dictionary<Varnode, ReplaceVarnode> varmap;
        /// Storage for subgraph variable nodes
        private List<ReplaceVarnode> newvarlist;
        /// Storage for subgraph op nodes
        private List<ReplaceOp> oplist;
        /// Operations getting patched (but with no flow thru)
        private List<PatchRecord> patchlist;
        /// Subgraph variable nodes still needing to be traced
        private List<ReplaceVarnode> worklist;
        /// Number of instructions pulling out the logical value
        private int pullcount;

        /// \brief Return \e slot of constant if INT_OR op sets all bits in mask, otherwise -1
        ///
        /// \param orop is the given OpCode.CPUI_INT_OR op
        /// \param mask is the given mask
        /// \return constant slot or -1
        private static int doesOrSet(PcodeOp orop, ulong mask)
        {
            int index = (orop.getIn(1).isConstant() ? 1 : 0);
            if (!orop.getIn(index).isConstant())
                return -1;
            ulong orval = orop.getIn(index).getOffset();
            if ((mask & (~orval)) == (ulong)0) // Are all masked bits one
                return index;
            return -1;
        }

        /// \brief Return \e slot of constant if INT_AND op clears all bits in mask, otherwise -1
        ///
        /// \param andop is the given OpCode.CPUI_INT_AND op
        /// \param mask is the given mask
        /// \return constant slot or -1
        private static int doesAndClear(PcodeOp andop, ulong mask)
        {
            int index = (andop.getIn(1).isConstant() ? 1 : 0);
            if (!andop.getIn(index).isConstant())
                return -1;
            ulong andval = andop.getIn(index).getOffset();
            if ((mask & andval) == (ulong)0) // Are all masked bits zero
                return index;
            return -1;
        }

        /// \brief Calculcate address of replacement Varnode for given subgraph variable node
        ///
        /// \param rvn is the given subgraph variable node
        /// \return the address of the new logical Varnode
        private Address getReplacementAddress(ReplaceVarnode rvn)
        {
            Address addr = rvn.vn.getAddr();
            int sa = Globals.leastsigbit_set(rvn.mask) / 8; // Number of bytes value is shifted into container
            if (addr.isBigEndian())
                addr = addr + (rvn.vn.getSize() - flowsize - sa);
            else
                addr = addr + sa;
            addr.renormalize(flowsize);
            return addr;
        }

        /// \brief Add the given Varnode as a new node in the logical subgraph
        ///
        /// A new ReplaceVarnode object is created, representing the given Varnode within
        /// the logical subgraph, and returned.  If an object representing the Varnode already
        /// exists it is returned.  A mask describing the subset of bits within the Varnode
        /// representing the logical value is also passed @in. This method also determines if
        /// the new node needs to be added to the worklist for continued tracing.
        /// \param vn is the given Varnode holding the logical value
        /// \param mask is the given mask describing the bits of the logical value
        /// \param inworklist will hold \b true if the new node should be traced further
        /// \return the new subgraph variable node
        private ReplaceVarnode? setReplacement(Varnode vn, ulong mask, out bool inworklist)
        {
            ReplaceVarnode res = new ReplaceVarnode();
            inworklist = false;
            if (vn.isMark()) {
                // Already seen before
                ReplaceVarnode replacer;
                Dictionary<Varnode, ReplaceVarnode>.Enumerator iter;
                if (!varmap.TryGetValue(vn, out replacer)) throw new ApplicationException();
                inworklist = false;
                return (res.mask != mask) ? (ReplaceVarnode)null : replacer;
            }

            if (vn.isConstant()) {
                inworklist = false;
                if (sextrestrictions) {
                    // Check that -vn- is a sign extension
                    ulong cval = vn.getOffset();
                    ulong smallval = cval & mask; // From its logical size
                    ulong sextval = Globals.sign_extend(smallval, flowsize, vn.getSize());// to its fullsize
                    if (sextval != cval)
                        return (ReplaceVarnode)null;
                }
                return addConstant((ReplaceOp)null, mask, 0, vn);
            }

            if (vn.isFree())
                return (ReplaceVarnode)null; // Abort

            if (vn.isAddrForce() && (vn.getSize() != flowsize))
                return (ReplaceVarnode)null;

            if (sextrestrictions) {
                if (vn.getSize() != flowsize) {
                    if ((!aggressive) && vn.isInput())
                        // Cannot assume input is sign extended
                        return (ReplaceVarnode)null;
                    if (vn.isPersist()) return (ReplaceVarnode)null;
                }
                if (vn.isTypeLock() && vn.getType().getMetatype() != type_metatype.TYPE_PARTIALSTRUCT) {
                    if (vn.getType().getSize() != flowsize)
                        return (ReplaceVarnode)null;
                }
            }
            else {
                if (bitsize >= 8) {
                    // Not a flag
                        // If the logical variable is not a flag, don't consider the case where multiple variables
                        // are packed into a single location, i.e. always consider it a single variable
                    if ((!aggressive) && ((vn.getConsume() & ~mask) != 0))
                        // If there is any use of value outside of the logical variable
                        // This probably means the whole thing is a variable, i.e. quit
                        return (ReplaceVarnode)null;
                    if (vn.isTypeLock() && vn.getType().getMetatype() != type_metatype.TYPE_PARTIALSTRUCT) {
                        int sz = vn.getType().getSize();
                        if (sz != flowsize)
                            return (ReplaceVarnode)null;
                    }
                }

                if (vn.isInput()) {
                    // Must be careful with inputs
                    // Inputs must come in from the right register/memory
                    if (bitsize < 8) return (ReplaceVarnode)null; // Dont create input flag
                    if ((mask & 1) == 0)
                        // Dont create unique input
                        // Its extremely important that the code (above) which doesn't allow packed variables be applied
                        // or the mechanisms we use for inputs will give us spurious temporary inputs
                        return (ReplaceVarnode)null;
                }
            }

            res = varmap[vn];
            vn.setMark();
            res.vn = vn;
            res.replacement = (Varnode)null;
            res.mask = mask;
            res.def = (ReplaceOp)null;
            inworklist = true;
            // Check if vn already represents the logical variable being traced
            if (vn.getSize() == flowsize) {
                if (mask == Globals.calc_mask((uint)flowsize)) {
                    inworklist = false;
                    res.replacement = vn;
                }
                else if (mask == 1) {
                    if ((vn.isWritten()) && (vn.getDef().isBoolOutput())) {
                        inworklist = false;
                        res.replacement = vn;
                    }
                }
            }
            return res;
        }

        /// \brief Create a logical subgraph operator node given its output variable node
        /// \param opc is the opcode of the new logical operator
        /// \param numparam is the number of parameters in the new operator
        /// \param outrvn is the given output variable node
        /// \return the new logical subgraph operator object
        private ReplaceOp createOp(OpCode opc, int numparam, ReplaceVarnode outrvn)
        {
            if (outrvn.def != (ReplaceOp)null)
                return outrvn.def;
            ReplaceOp rop = new ReplaceOp() {
                op = outrvn.vn.getDef(),
                numparams = numparam,
                opc = opc,
                output = outrvn
            };
            oplist.Add(rop);
            outrvn.def = rop;
            return rop;
        }

        /// \brief Create a logical subgraph operator node given one of its input variable nodes
        /// \param opc is the opcode of the new logical operator
        /// \param numparam is the number of parameters in the new operator
        /// \param op is the original PcodeOp being replaced
        /// \param inrvn is the given input variable node
        /// \param slot is the input slot of the variable node
        /// \return the new logical subgraph operator objects
        private ReplaceOp createOpDown(OpCode opc, int numparam, PcodeOp op, ReplaceVarnode inrvn, int slot)
        {
            ReplaceOp rop = new ReplaceOp() {
                op = op,
                opc = opc,
                numparams = numparam,
                output = (ReplaceVarnode)null
            };
            oplist.Add(rop);
            while (rop.input.size() <= slot)
                rop.input.Add((ReplaceVarnode)null);
            rop.input[slot] = inrvn;
            return rop;
        }

        /// \brief Determine if the given subgraph variable can act as a parameter to the given CALL op
        ///
        /// We assume the variable flows as a parameter to the CALL. If the CALL doesn't lock the parameter
        /// size, create a PatchRecord within the subgraph that allows the CALL to take the parameter
        /// with its smaller logical size.
        /// \param op is the given CALL op
        /// \param rvn is the given subgraph variable acting as a parameter
        /// \param slot is the input slot of the variable within the CALL
        /// \return \b true if the parameter can be successfully trimmed to its logical size
        private bool tryCallPull(PcodeOp op, ReplaceVarnode rvn, int slot)
        {
            if (slot == 0) return false;
            if (!aggressive) {
                if ((rvn.vn.getConsume() & ~rvn.mask) != 0)  // If there's something outside the mask being consumed
                    return false;               // Don't truncate
            }
            FuncCallSpecs? fc = fd.getCallSpecs(op);
            if (fc == (FuncCallSpecs)null) return false;
            if (fc.isInputActive()) return false; // Don't trim while in the middle of figuring out params
            if (fc.isInputLocked() && (!fc.isDotdotdot())) return false;

            patchlist.Add(new PatchRecord() {
                type = PatchRecord.patchtype.parameter_patch,
                patchOp = op,
                in1 = rvn,
                slot = slot
            });
            pullcount += 1;     // A true terminal modification
            return true;
        }

        /// \brief Determine if the given subgraph variable can act as return value for the given RETURN op
        ///
        /// We assume the variable flows the RETURN. If the return value size is not locked. Create a
        /// PatchRecord within the subgraph that allows the RETURN to take a smaller logical value.
        /// \param op is the given RETURN op
        /// \param rvn is the given subgraph variable flowing to the RETURN
        /// \param slot is the input slot of the subgraph variable
        /// \return \b true if the return value can be successfully trimmed to its logical size
        private bool tryReturnPull(PcodeOp op, ReplaceVarnode rvn, int slot)
        {
            if (slot == 0) return false;    // Don't deal with actual return address container
            if (fd.getFuncProto().isOutputLocked()) return false;
            if (!aggressive) {
                if ((rvn.vn.getConsume() & ~rvn.mask) != 0)  // If there's something outside the mask being consumed
                    return false;               // Don't truncate
            }

            if (!returnsTraversed) {
                // If we plan to truncate the size of a return variable, we need to propagate the logical size to any other
                // return variables so that there can still be a single return value type for the function
                IEnumerator<PcodeOp> iter = fd.beginOp(OpCode.CPUI_RETURN);
                while (iter.MoveNext()) {
                    PcodeOp retop = iter.Current;
                    if (retop.getHaltType() != 0) continue;        // Artificial halt
                    Varnode retvn = retop.getIn(slot);
                    bool inworklist;
                    ReplaceVarnode? rep = setReplacement(retvn, rvn.mask, out inworklist);
                    if (rep == (ReplaceVarnode)null)
                        return false;
                    if (inworklist)
                        worklist.Add(rep);
                    else if (retvn.isConstant() && retop != op) {
                        // Trace won't revisit this RETURN, so we need to generate patch now
                        patchlist.Add(new PatchRecord() {
                            type = PatchRecord.patchtype.parameter_patch,
                            patchOp = retop,
                            in1 = rep,
                            slot = slot
                        });
                        pullcount += 1;
                    }
                }
                returnsTraversed = true;
            }
            patchlist.Add(new PatchRecord() {
                type = PatchRecord.patchtype.parameter_patch,
                patchOp = op,
                in1 = rvn,
                slot = slot
            });
            pullcount += 1;     // A true terminal modification
            return true;
        }

        /// \brief Determine if the given subgraph variable can act as a \e created value for the given INDIRECT op
        ///
        /// Check if the INDIRECT is an \e indirect \e creation and is not representing a locked return value.
        /// If we can, create the INDIRECT node in the subgraph representing the logical \e indirect \e creation.
        /// \param op is the given INDIRECT
        /// \param rvn is the given subgraph variable acting as the output of the INDIRECT
        /// \return \b true if we can successfully trim the value to its logical size
        private bool tryCallReturnPush(PcodeOp op, ReplaceVarnode rvn)
        {
            if (!aggressive) {
                if ((rvn.vn.getConsume() & ~rvn.mask) != 0)  // If there's something outside the mask being consumed
                    return false;               // Don't truncate
            }
            if ((rvn.mask & 1) == 0) return false; // Verify the logical value is the least significant part
            if (bitsize < 8) return false;      // Make sure logical value is at least a byte
            FuncCallSpecs? fc = fd.getCallSpecs(op);
            if (fc == (FuncCallSpecs)null) return false;
            if (fc.isOutputLocked()) return false;
            if (fc.isOutputActive()) return false; // Don't trim while in the middle of figuring out return value

            addPush(op, rvn);
            // pullcount += 1;		// This is a push NOT a pull
            return true;
        }

        /// \brief Determine if the subgraph variable can act as a switch variable for the given BRANCHIND
        ///
        /// We query the JumpTable associated with the BRANCHIND to see if its switch variable
        /// can be trimmed as indicated by the logical flow.
        /// \param op is the given BRANCHIND op
        /// \param rvn is the subgraph variable flowing to the BRANCHIND
        /// \return \b true if the switch variable can be successfully trimmed to its logical size
        private bool trySwitchPull(PcodeOp op, ReplaceVarnode rvn)
        {
            op.AssertIsIndirectBranching();
            if ((rvn.mask & 1) == 0) return false; // Logical value must be justified
            if ((rvn.vn.getConsume() & ~rvn.mask) != 0)  // If there's something outside the mask being consumed
                return false;               //  we can't trim
            patchlist.Add(new PatchRecord() {
                type = PatchRecord.patchtype.parameter_patch,
                patchOp = op,
                in1 = rvn,
                slot = 0
            });
            pullcount += 1;     // A true terminal modification
            return true;
        }

        /// Trace the logical data-flow forward for the given subgraph variable
        /// Try to trace the logical variable through descendant Varnodes
        /// creating new nodes in the logical subgraph and updating the worklist.
        /// \param rvn is the given subgraph variable to trace
        /// \return \b true if the logical value can be traced forward one level
        private bool traceForward(ReplaceVarnode rvn)
        {
            ReplaceOp rop;
            PcodeOp op;
            Varnode? outvn;
            int slot;
            int sa;
            ulong newmask;
            bool booldir;
            int dcount = 0;
            int hcount = 0;
            int callcount = 0;

            IEnumerator<PcodeOp> iter = rvn.vn.beginDescend();
            while (iter.MoveNext()) {
                op = iter.Current;
                outvn = op.getOut();
                if ((outvn != (Varnode)null) && outvn.isMark() && !op.isCall())
                    continue;
                dcount += 1;        // Count this descendant
                slot = op.getSlot(rvn.vn);
                switch (op.code()) {
                    case OpCode.CPUI_COPY:
                    case OpCode.CPUI_MULTIEQUAL:
                    case OpCode.CPUI_INT_NEGATE:
                    case OpCode.CPUI_INT_XOR:
                        rop = createOpDown(op.code(), op.numInput(), op, rvn, slot);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_OR:
                        if (doesOrSet(op, rvn.mask) != -1) break; // Subvar set to 1s, truncate flow
                        rop = createOpDown(OpCode.CPUI_INT_OR, 2, op, rvn, slot);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_AND:
                        if ((op.getIn(1).isConstant()) && (op.getIn(1).getOffset() == rvn.mask)) {
                            if ((outvn.getSize() == flowsize) && ((rvn.mask & 1) != 0)) {
                                addTerminalPatch(op, rvn);
                                hcount += 1;        // Dealt with this descendant
                                break;
                            }
                            // Is the small variable getting zero padded into something that is fully consumed
                            if ((!aggressive) && ((outvn.getConsume() & rvn.mask) != outvn.getConsume())) {
                                addSuggestedPatch(rvn, op, -1);
                                hcount += 1;        // Dealt with this descendant
                                break;
                            }
                        }
                        if (doesAndClear(op, rvn.mask) != -1) break; // Subvar set to zero, truncate flow
                        rop = createOpDown(OpCode.CPUI_INT_AND, 2, op, rvn, slot);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_ZEXT:
                    case OpCode.CPUI_INT_SEXT:
                        rop = createOpDown(OpCode.CPUI_COPY, 1, op, rvn, 0);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_MULT:
                        if ((rvn.mask & 1) == 0)
                            return false;       // Cannot account for carry
                        sa = Globals.leastsigbit_set(op.getIn(1 - slot).getNZMask());
                        sa &= ~7;           // Should be nearest multiple of 8
                        if (bitsize + sa > 8 * rvn.vn.getSize()) return false;
                        rop = createOpDown(OpCode.CPUI_INT_MULT, 2, op, rvn, slot);
                        if (!createLink(rop, rvn.mask << sa, -1, outvn)) return false;
                        hcount += 1;
                        break;
                    case OpCode.CPUI_INT_ADD:
                        if ((rvn.mask & 1) == 0)
                            return false;       // Cannot account for carry
                        rop = createOpDown(OpCode.CPUI_INT_ADD, 2, op, rvn, slot);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_LEFT:
                        if (slot == 1) {
                            // Logical flow is into shift amount
                            if ((rvn.mask & 1) == 0) return false; // Cannot account for effect of extraneous bits
                            if (bitsize < 8) return false;
                            // Its possible that truncating to the logical value could have an effect, if there were non-zero bits
                            // being truncated.  Non-zero bits here would mean the shift-amount was very large (>255), indicating the
                            // the result was undefined
                            addTerminalPatchSameOp(op, rvn, slot);
                            hcount += 1;
                            break;
                        }
                        if (!op.getIn(1).isConstant()) return false; // Dynamic shift
                        sa = (int)op.getIn(1).getOffset();
                        newmask = (rvn.mask << sa) & Globals.calc_mask((uint)outvn.getSize());
                        if (newmask == 0) break;    // Subvar is cleared, truncate flow
                        if (rvn.mask != (newmask >> sa)) return false; // subvar is clipped
                                                                        // Is the small variable getting zero padded into something that is fully consumed
                        if (((rvn.mask & 1) != 0) && (sa + bitsize == 8 * outvn.getSize())
                            && (Globals.calc_mask((uint)outvn.getSize()) == outvn.getConsume()))
                        {
                            addSuggestedPatch(rvn, op, sa);
                            hcount += 1;
                            break;
                        }
                        rop = createOpDown(OpCode.CPUI_COPY, 1, op, rvn, 0);
                        if (!createLink(rop, newmask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_RIGHT:
                    case OpCode.CPUI_INT_SRIGHT:
                        if (slot == 1) {
                            // Logical flow is into shift amount
                            if ((rvn.mask & 1) == 0) return false; // Cannot account for effect of extraneous bits
                            if (bitsize < 8) return false;
                            addTerminalPatchSameOp(op, rvn, slot);
                            hcount += 1;
                            break;
                        }
                        if (!op.getIn(1).isConstant()) return false;
                        sa = (int)op.getIn(1).getOffset();
                        newmask = rvn.mask >> sa;
                        if (newmask == 0) {
                            if (op.code() == OpCode.CPUI_INT_RIGHT) break; // subvar is set to zero, truncate flow
                            return false;
                        }
                        if (rvn.mask != (newmask << sa)) return false;
                        if ((outvn.getSize() == flowsize) && ((newmask & 1) == 1) &&
                            (op.getIn(0).getNZMask() == rvn.mask))
                        {
                            addTerminalPatch(op, rvn);
                            hcount += 1;        // Dealt with this descendant
                            break;
                        }
                        // Is the small variable getting zero padded into something that is fully consumed
                        if (((newmask & 1) == 1) && (sa + bitsize == 8 * outvn.getSize())
                            && (Globals.calc_mask((uint)outvn.getSize()) == outvn.getConsume()))
                        {
                            addSuggestedPatch(rvn, op, 0);
                            hcount += 1;
                            break;
                        }
                        rop = createOpDown(OpCode.CPUI_COPY, 1, op, rvn, 0);
                        if (!createLink(rop, newmask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_SUBPIECE:
                        sa = (int)op.getIn(1).getOffset() * 8;
                        newmask = (rvn.mask >> sa) & Globals.calc_mask((uint)outvn.getSize());
                        if (newmask == 0) break;    // subvar is set to zero, truncate flow
                        if (rvn.mask != (newmask << sa)) {
                            // Some kind of truncation of the logical value
                            if (flowsize > ((sa / 8) + outvn.getSize()) && (rvn.mask & 1) != 0) {
                                // Only a piece of the logical value remains
                                addTerminalPatchSameOp(op, rvn, 0);
                                hcount += 1;
                                break;
                            }
                            return false;
                        }
                        if (((newmask & 1) != 0) && (outvn.getSize() == flowsize)) {
                            addTerminalPatch(op, rvn);
                            hcount += 1;        // Dealt with this descendant
                            break;
                        }
                        rop = createOpDown(OpCode.CPUI_COPY, 1, op, rvn, 0);
                        if (!createLink(rop, newmask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_PIECE:
                        if (rvn.vn == op.getIn(0))
                            newmask = rvn.mask << (8 * op.getIn(1).getSize());
                        else
                            newmask = rvn.mask;
                        rop = createOpDown(OpCode.CPUI_COPY, 1, op, rvn, 0);
                        if (!createLink(rop, newmask, -1, outvn)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_LESS:
                    case OpCode.CPUI_INT_LESSEQUAL:
                        outvn = op.getIn(1 - slot); // The OTHER side of the comparison
                        if ((!aggressive) && (((rvn.vn.getNZMask() | rvn.mask) != rvn.mask)))
                            return false;       // Everything but logical variable must definitely be zero (unless we are aggressive)
                        if (outvn.isConstant()) {
                            if ((rvn.mask | outvn.getOffset()) != rvn.mask)
                                return false;       // Must compare only bits of logical variable
                        }
                        else {
                            if ((!aggressive) && (((rvn.mask | outvn.getNZMask()) != rvn.mask))) // unused bits of otherside must be zero
                                return false;
                        }
                        if (!createCompareBridge(op, rvn, slot, outvn))
                            return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_INT_NOTEQUAL:
                    case OpCode.CPUI_INT_EQUAL:
                        outvn = op.getIn(1 - slot); // The OTHER side of the comparison
                        if (bitsize != 1) {
                            if ((!aggressive) && (((rvn.vn.getNZMask() | rvn.mask) != rvn.mask)))
                                return false;   // Everything but logical variable must definitely be zero (unless we are aggressive)
                            if (outvn.isConstant()) {
                                if ((rvn.mask | outvn.getOffset()) != rvn.mask)
                                    return false;   // Not comparing to just bits of the logical variable
                            }
                            else {
                                if ((!aggressive) && (((rvn.mask | outvn.getNZMask()) != rvn.mask))) // unused bits must be zero
                                    return false;
                            }
                            if (!createCompareBridge(op, rvn, slot, outvn))
                                return false;
                        }
                        else {
                            // Movement of boolean variables
                            if (!outvn.isConstant()) return false;
                            newmask = rvn.vn.getNZMask();
                            if (newmask != rvn.mask) return false;
                            if (op.getIn(1 - slot).getOffset() == (ulong)0)
                                booldir = true;
                            else if (op.getIn(1 - slot).getOffset() == newmask)
                                booldir = false;
                            else
                                return false;
                            if (op.code() == OpCode.CPUI_INT_EQUAL)
                                booldir = !booldir;
                            if (booldir)
                                addTerminalPatch(op, rvn);
                            else {
                                rop = createOpDown(OpCode.CPUI_BOOL_NEGATE, 1, op, rvn, 0);
                                createNewOut(rop, (ulong)1);
                                addTerminalPatch(op, rop.output);
                            }
                        }
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_CALL:
                    case OpCode.CPUI_CALLIND:
                        callcount += 1;
                        if (callcount > 1)
                            slot = op.getRepeatSlot(rvn.vn, slot, iter);
                        if (!tryCallPull(op, rvn, slot)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_RETURN:
                        if (!tryReturnPull(op, rvn, slot)) return false;
                        hcount += 1;
                        break;
                    case OpCode.CPUI_BRANCHIND:
                        if (!trySwitchPull(op, rvn)) return false;
                        hcount += 1;
                        break;
                    case OpCode.CPUI_BOOL_NEGATE:
                    case OpCode.CPUI_BOOL_AND:
                    case OpCode.CPUI_BOOL_OR:
                    case OpCode.CPUI_BOOL_XOR:
                        if (bitsize != 1) return false;
                        if (rvn.mask != 1) return false;
                        addBooleanPatch(op, rvn, slot);
                        break;
                    case OpCode.CPUI_CBRANCH:
                        if ((bitsize != 1) || (slot != 1)) return false;
                        if (rvn.mask != 1) return false;
                        addBooleanPatch(op, rvn, 1);
                        hcount += 1;
                        break;
                    default:
                        return false;
                }
            }
            if (dcount != hcount) {
                // Must account for all descendants of an input
                if (rvn.vn.isInput()) return false;
            }
            return true;
        }

        /// Trace the logical data-flow backward for the given subgraph variable
        /// Trace the logical value backward through one PcodeOp adding new nodes to the
        /// logical subgraph and updating the worklist.
        /// \param rvn is the given logical value to trace
        /// \return \b true if the logical value can be traced backward one level
        private bool traceBackward(ReplaceVarnode rvn)
        {
            PcodeOp? op = rvn.vn.getDef();
            if (op == (PcodeOp)null) return true; // If vn is input
            int sa;
            ulong newmask;
            ReplaceOp rop;

            switch (op.code()) {
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_MULTIEQUAL:
                case OpCode.CPUI_INT_NEGATE:
                case OpCode.CPUI_INT_XOR:
                    rop = createOp(op.code(), op.numInput(), rvn);
                    for (int i = 0; i < op.numInput(); ++i)
                        if (!createLink(rop, rvn.mask, i, op.getIn(i))) // Same inputs and mask
                            return false;
                    return true;
                case OpCode.CPUI_INT_AND:
                    sa = doesAndClear(op, rvn.mask);
                    if (sa != -1) {
                        rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                        addConstant(rop, rvn.mask, 0, op.getIn(sa));
                    }
                    else {
                        rop = createOp(OpCode.CPUI_INT_AND, 2, rvn);
                        if (!createLink(rop, rvn.mask, 0, op.getIn(0))) return false;
                        if (!createLink(rop, rvn.mask, 1, op.getIn(1))) return false;
                    }
                    return true;
                case OpCode.CPUI_INT_OR:
                    sa = doesOrSet(op, rvn.mask);
                    if (sa != -1) {
                        rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                        addConstant(rop, rvn.mask, 0, op.getIn(sa));
                    }
                    else {
                        rop = createOp(OpCode.CPUI_INT_OR, 2, rvn);
                        if (!createLink(rop, rvn.mask, 0, op.getIn(0))) return false;
                        if (!createLink(rop, rvn.mask, 1, op.getIn(1))) return false;
                    }
                    return true;
                case OpCode.CPUI_INT_ZEXT:
                case OpCode.CPUI_INT_SEXT:
                    if ((rvn.mask & Globals.calc_mask((uint)op.getIn(0).getSize())) != rvn.mask) {
                        if ((rvn.mask & 1) != 0 && flowsize > op.getIn(0).getSize()) {
                            addPush(op, rvn);
                            return true;
                        }
                        break;         // Check if subvariable comes through extension
                    }
                    rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                    if (!createLink(rop, rvn.mask, 0, op.getIn(0))) return false;
                    return true;
                case OpCode.CPUI_INT_ADD:
                    if ((rvn.mask & 1) == 0)
                        break;          // Cannot account for carry
                    if (rvn.mask == (ulong)1)
                        rop = createOp(OpCode.CPUI_INT_XOR, 2, rvn); // Single bit add
                    else
                        rop = createOp(OpCode.CPUI_INT_ADD, 2, rvn);
                    if (!createLink(rop, rvn.mask, 0, op.getIn(0))) return false;
                    if (!createLink(rop, rvn.mask, 1, op.getIn(1))) return false;
                    return true;
                case OpCode.CPUI_INT_LEFT:
                    if (!op.getIn(1).isConstant()) break; // Dynamic shift
                    sa = (int)op.getIn(1).getOffset();
                    newmask = rvn.mask >> sa;  // What mask looks like before shift
                    if (newmask == 0) {
                        // Subvariable filled with shifted zero
                        rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                        addNewConstant(rop, 0, (ulong)0);
                        return true;
                    }
                    if ((newmask << sa) != rvn.mask)
                        break;          // subvariable is truncated by shift
                    rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                    if (!createLink(rop, newmask, 0, op.getIn(0))) return false;
                    return true;
                case OpCode.CPUI_INT_RIGHT:
                    if (!op.getIn(1).isConstant()) break; // Dynamic shift
                    sa = (int)op.getIn(1).getOffset();
                    newmask = (rvn.mask << sa) & Globals.calc_mask((uint)op.getIn(0).getSize());
                    if (newmask == 0) {
                        // Subvariable filled with shifted zero
                        rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                        addNewConstant(rop, 0, (ulong)0);
                        return true;
                    }
                    if ((newmask >> sa) != rvn.mask)
                        break;          // subvariable is truncated by shift
                    rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                    if (!createLink(rop, newmask, 0, op.getIn(0))) return false;
                    return true;
                case OpCode.CPUI_INT_SRIGHT:
                    if (!op.getIn(1).isConstant()) break; // Dynamic shift
                    sa = (int)op.getIn(1).getOffset();
                    newmask = (rvn.mask << sa) & Globals.calc_mask((uint)op.getIn(0).getSize());
                    if ((newmask >> sa) != rvn.mask)
                        break;          // subvariable is truncated by shift
                    rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                    if (!createLink(rop, newmask, 0, op.getIn(0))) return false;
                    return true;
                case OpCode.CPUI_INT_MULT:
                    sa = Globals.leastsigbit_set(rvn.mask);
                    if (sa != 0) {
                        int sa2 = Globals.leastsigbit_set(op.getIn(1).getNZMask());
                        if (sa2 < sa) return false; // Cannot deal with carries into logical multiply
                        newmask = rvn.mask >> sa;
                        rop = createOp(OpCode.CPUI_INT_MULT, 2, rvn);
                        if (!createLink(rop, newmask, 0, op.getIn(0))) return false;
                        if (!createLink(rop, rvn.mask, 1, op.getIn(1))) return false;
                    }
                    else {
                        if (rvn.mask == (ulong)1)
                            rop = createOp(OpCode.CPUI_INT_AND, 2, rvn); // Single bit multiply
                        else
                            rop = createOp(OpCode.CPUI_INT_MULT, 2, rvn);
                        if (!createLink(rop, rvn.mask, 0, op.getIn(0))) return false;
                        if (!createLink(rop, rvn.mask, 1, op.getIn(1))) return false;
                    }
                    return true;
                case OpCode.CPUI_SUBPIECE:
                    sa = (int)op.getIn(1).getOffset() * 8;
                    newmask = rvn.mask << sa;
                    rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                    if (!createLink(rop, newmask, 0, op.getIn(0))) return false;
                    return true;
                case OpCode.CPUI_PIECE:
                    if ((rvn.mask & Globals.calc_mask((uint)op.getIn(1).getSize())) == rvn.mask) {
                        rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                        if (!createLink(rop, rvn.mask, 0, op.getIn(1))) return false;
                        return true;
                    }
                    sa = op.getIn(1).getSize() * 8;
                    newmask = rvn.mask >> sa;
                    if (newmask << sa == rvn.mask) {
                        rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                        if (!createLink(rop, newmask, 0, op.getIn(0))) return false;
                        return true;
                    }
                    break;
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLIND:
                    if (tryCallReturnPush(op, rvn))
                        return true;
                    break;
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
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                case OpCode.CPUI_FLOAT_NAN:
                    // Mask won't be 1, because setReplacement takes care of it
                    if ((rvn.mask & 1) == 1) break; // Not normal variable flow
                                                     // Variable is filled with zero
                    rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                    addNewConstant(rop, 0, (ulong)0);
                    return true;
                default:
                    break;          // Everything else we abort
            }

            return false;
        }

        /// Trace logical data-flow forward assuming sign-extensions
        /// Try to trace the logical variable through descendant Varnodes, updating the logical subgraph.
        /// We assume (and check) that the logical variable has always been sign extended (sextstate) into its container.
        /// \param rvn is the given subgraph variable to trace
        /// \return \b true if the logical value can successfully traced forward one level
        private bool traceForwardSext(ReplaceVarnode rvn)
        {
            ReplaceOp rop;
            PcodeOp op;
            Varnode? outvn;
            int slot;
            int dcount = 0;
            int hcount = 0;
            int callcount = 0;

            IEnumerator<PcodeOp> iter = rvn.vn.beginDescend();
            while (iter.MoveNext()) {
                op = iter.Current;
                outvn = op.getOut();
                if ((outvn != (Varnode)null) && outvn.isMark() && !op.isCall())
                    continue;
                dcount += 1;        // Count this descendant
                slot = op.getSlot(rvn.vn);
                switch (op.code()) {
                    case OpCode.CPUI_COPY:
                    case OpCode.CPUI_MULTIEQUAL:
                    case OpCode.CPUI_INT_NEGATE:
                    case OpCode.CPUI_INT_XOR:
                    case OpCode.CPUI_INT_OR:
                    case OpCode.CPUI_INT_AND:
                        rop = createOpDown(op.code(), op.numInput(), op, rvn, slot);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false;
                        hcount += 1;
                        break;
                    case OpCode.CPUI_INT_SEXT:     // extended logical variable into even larger container
                        rop = createOpDown(OpCode.CPUI_COPY, 1, op, rvn, 0);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false;
                        hcount += 1;
                        break;
                    case OpCode.CPUI_INT_SRIGHT:
                        if (!op.getIn(1).isConstant()) return false; // Right now we only deal with constant shifts
                        rop = createOpDown(OpCode.CPUI_INT_SRIGHT, 2, op, rvn, 0);
                        if (!createLink(rop, rvn.mask, -1, outvn)) return false; // Keep the same mask size
                        addConstant(rop, Globals.calc_mask((uint)op.getIn(1).getSize()), 1, op.getIn(1)); // Preserve the shift amount
                        hcount += 1;
                        break;
                    case OpCode.CPUI_SUBPIECE:
                        if (op.getIn(1).getOffset() != 0) return false;   // Only allow proper truncation
                        if (outvn.getSize() > flowsize) return false;
                        if (outvn.getSize() == flowsize)
                            addTerminalPatch(op, rvn);      // Termination of flow, convert SUBPIECE to COPY
                        else
                            addTerminalPatchSameOp(op, rvn, 0); // Termination of flow, SUBPIECE truncates even more
                        hcount += 1;
                        break;
                    case OpCode.CPUI_INT_LESS:     // Unsigned comparisons are equivalent at the 2 sizes on sign extended values
                    case OpCode.CPUI_INT_LESSEQUAL:
                    case OpCode.CPUI_INT_SLESS:
                    case OpCode.CPUI_INT_SLESSEQUAL:
                    case OpCode.CPUI_INT_EQUAL:    // Everything works if both sides are sign extended
                    case OpCode.CPUI_INT_NOTEQUAL:
                        outvn = op.getIn(1 - slot); // The OTHER side of the comparison
                        if (!createCompareBridge(op, rvn, slot, outvn)) return false;
                        hcount += 1;
                        break;
                    case OpCode.CPUI_CALL:
                    case OpCode.CPUI_CALLIND:
                        callcount += 1;
                        if (callcount > 1)
                            slot = op.getRepeatSlot(rvn.vn, slot, iter);
                        if (!tryCallPull(op, rvn, slot)) return false;
                        hcount += 1;        // Dealt with this descendant
                        break;
                    case OpCode.CPUI_RETURN:
                        if (!tryReturnPull(op, rvn, slot)) return false;
                        hcount += 1;
                        break;
                    case OpCode.CPUI_BRANCHIND:
                        if (!trySwitchPull(op, rvn)) return false;
                        hcount += 1;
                        break;
                    default:
                        return false;
                }
            }
            if (dcount != hcount) {
                // Must account for all descendants of an input
                if (rvn.vn.isInput()) return false;
            }
            return true;
        }

        /// Trace logical data-flow backward assuming sign-extensions
        /// Try to trace the logical variable up through its defining op, updating the logical subgraph.
        /// We assume (and check) that the logical variable has always been sign extended (sextstate) into its container.
        /// \param rvn is the given subgraph variable to trace
        /// \return \b true if the logical value can successfully traced backward one level
        private bool traceBackwardSext(ReplaceVarnode rvn)
        {
            PcodeOp? op = rvn.vn.getDef();
            if (op == (PcodeOp)null) return true; // If vn is input
            ReplaceOp rop;

            switch (op.code()) {
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_MULTIEQUAL:
                case OpCode.CPUI_INT_NEGATE:
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                    rop = createOp(op.code(), op.numInput(), rvn);
                    for (int i = 0; i < op.numInput(); ++i)
                        if (!createLink(rop, rvn.mask, i, op.getIn(i))) // Same inputs and mask
                            return false;
                    return true;
                case OpCode.CPUI_INT_ZEXT:
                    if (op.getIn(0).getSize() < flowsize) {
                        // zero extension from a smaller size still acts as a signed extension
                        addPush(op, rvn);
                        return true;
                    }
                    break;
                case OpCode.CPUI_INT_SEXT:
                    if (flowsize != op.getIn(0).getSize()) return false;
                    rop = createOp(OpCode.CPUI_COPY, 1, rvn);
                    if (!createLink(rop, rvn.mask, 0, op.getIn(0))) return false;
                    return true;
                case OpCode.CPUI_INT_SRIGHT:
                    // A sign-extended logical value is arithmetically right-shifted
                    // we can replace with the logical value, keeping the same shift amount
                    if (!op.getIn(1).isConstant()) return false;
                    rop = createOp(OpCode.CPUI_INT_SRIGHT, 2, rvn);
                    if (!createLink(rop, rvn.mask, 0, op.getIn(0))) return false; // Keep the same mask
                    if (rop.input.size() == 1)
                        addConstant(rop, Globals.calc_mask((uint)op.getIn(1).getSize()), 1, op.getIn(1)); // Preserve the shift amount
                    return true;
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLIND:
                    if (tryCallReturnPush(op, rvn))
                        return true;
                    break;
                default:
                    break;
            }
            return false;
        }

        /// \brief Add a new variable to the logical subgraph as an input to the given operation
        ///
        /// The subgraph is extended by the specified input edge, and a new variable node is created
        /// if necessary or a preexisting node corresponding to the Varnode is used.
        /// If the logical value described by the given mask cannot be made to line up with the
        /// subgraph variable node, \b false is returned.
        /// \param rop is the given operation
        /// \param mask is the mask describing the logical value within the input Varnode
        /// \param slot is the input slot of the Varnode to the operation
        /// \param vn is the original input Varnode holding the logical value
        /// \return \b true is the subgraph is successfully extended to the input
        private bool createLink(ReplaceOp rop, ulong mask, int slot, Varnode vn)
        {
            bool inworklist;
            ReplaceVarnode? rep = setReplacement(vn, mask, out inworklist);
            if (rep == (ReplaceVarnode)null) return false;

            if (rop != (ReplaceOp)null) {
                if (slot == -1) {
                    rop.output = rep;
                    rep.def = rop;
                }
                else {
                    while (rop.input.size() <= slot)
                        rop.input.Add((ReplaceVarnode)null);
                    rop.input[slot] = rep;
                }
            }

            if (inworklist)
                worklist.Add(rep);
            return true;
        }

        /// \brief Extend the logical subgraph through a given comparison operator if possible
        ///
        /// Given the variable already in the subgraph that is compared and the other side of the
        /// comparison, add the other side as a logical value to the subgraph and create a PatchRecord
        /// for the comparison operation.
        /// \param op is the given comparison operation
        /// \param inrvn is the variable already in the logical subgraph
        /// \param slot is the input slot to the comparison of the variable already in the subgraph
        /// \param othervn is the Varnode holding the other side of the comparison
        /// \return \b true if the logical subgraph can successfully be extended through the comparison
        private bool createCompareBridge(PcodeOp op, ReplaceVarnode inrvn, int slot, Varnode othervn)
        {
            bool inworklist;
            ReplaceVarnode? rep = setReplacement(othervn, inrvn.mask, out inworklist);
            if (rep == (ReplaceVarnode)null) return false;

            if (slot == 0)
                addComparePatch(inrvn, rep, op);
            else
                addComparePatch(rep, inrvn, op);

            if (inworklist)
                worklist.Add(rep);
            return true;
        }

        /// \brief Mark an operation where original data-flow is being pushed into a subgraph variable
        ///
        /// The operation is not manipulating the logical value, but it produces a variable containing
        /// the logical value. The original op will not change but will just produce a smaller value.
        /// \param pushOp is the operation to mark
        /// \param rvn is the output variable holding the logical value
        private void addPush(PcodeOp pushOp, ReplaceVarnode rvn)
        {
            // Push to the front of the patch list
            patchlist.Insert(0, new PatchRecord() {
                type = PatchRecord.patchtype.push_patch,
                patchOp = pushOp,
                in1 = rvn
            });
        }

        /// \brief Mark an operation where a subgraph variable is naturally copied into the original data-flow
        ///
        /// If the operations naturally takes the given logical value as input but the output
        /// doesn't need to be traced as a logical value, a subgraph terminator (PatchRecord) is created
        /// noting this. The original PcodeOp will be converted to a COPY.
        /// \param pullop is the PcodeOp pulling the logical value out of the subgraph
        /// \param rvn is the given subgraph variable holding the logical value
        private void addTerminalPatch(PcodeOp pullop, ReplaceVarnode rvn)
        {
            patchlist.Add(new PatchRecord() {
                // Ultimately gets converted to a COPY
                type = PatchRecord.patchtype.copy_patch,
                // Operation pulling the variable out
                patchOp = pullop,
                // Point in container flow for pull
                in1 = rvn
            });
            // a true terminal modification
            pullcount += 1;
        }

        /// \brief Mark an operation where a subgraph variable is naturally pulled into the original data-flow
        ///
        /// If the operations naturally takes the given logical value as input but the output
        /// doesn't need to be traced as a logical value, a subgraph terminator (PatchRecord) is created
        /// noting this. The opcode of the operation will not change.
        /// \param pullop is the PcodeOp pulling the logical value out of the subgraph
        /// \param rvn is the given subgraph variable holding the logical value
        /// \param slot is the input slot to the operation
        private void addTerminalPatchSameOp(PcodeOp pullop, ReplaceVarnode rvn, int slot)
        {
            patchlist.Add(new PatchRecord() {
                type = PatchRecord.patchtype.parameter_patch, // Keep the original op, just change input
                patchOp = pullop,  // Operation pulling the variable out
                in1 = rvn, // Point in container flow for pull
                slot = slot
            });
            pullcount += 1;     // a true terminal modification
        }

        /// \brief Mark a subgraph bit variable flowing into an operation taking a boolean input
        ///
        /// This doesn't count as a Varnode holding a logical value that needs to be patched (by itself).
        /// A PatchRecord terminating the logical subgraph along the given edge is created.
        /// \param pullop is the operation taking the boolean input
        /// \param rvn is the given bit variable
        /// \param slot is the input slot of the variable to the operation
        private void addBooleanPatch(PcodeOp pullop, ReplaceVarnode rvn, int slot)
        {
            patchlist.Add(new PatchRecord() {
                // Make no change to the operator, just put in the new input
                type = PatchRecord.patchtype.parameter_patch,
                // Operation pulling the variable out
                patchOp = pullop,
                // Point in container flow for pull
                in1 = rvn,
                slot = slot
            });
            // this is not a true modification
        }

        /// \brief Mark a subgraph variable flowing to an operation that expands it by padding with zero bits.
        ///
        /// Data-flow along the specified edge within the logical subgraph is terminated by added a PatchRecord.
        /// This doesn't count as a logical value that needs to be patched (by itself).
        /// \param rvn is the given subgraph variable
        /// \param pushop is the operation that pads the variable
        /// \param sa is the amount the logical value is shifted to the left
        private void addSuggestedPatch(ReplaceVarnode rvn, PcodeOp pushop, int sa)
        {
            patchlist.Add(new PatchRecord() {
                type = PatchRecord.patchtype.extension_patch,
                in1 = rvn,
                patchOp = pushop
            });
            if (sa == -1)
                sa = Globals.leastsigbit_set(rvn.mask);
            patchlist.GetLastItem().slot = sa;
            // This is not a true modification because the output is still the expanded size
        }

        /// \brief Mark subgraph variables flowing into a comparison operation
        ///
        /// The operation accomplishes the logical comparison by comparing the larger containers.
        /// A PatchRecord is created indicating that data-flow from the subgraph terminates at the comparison.
        /// \param in1 is the first logical value to the comparison
        /// \param in2 is the second logical value
        /// \param op is the comparison operation
        private void addComparePatch(ReplaceVarnode in1, ReplaceVarnode in2, PcodeOp op)
        {
            patchlist.Add(new PatchRecord() {
                type = PatchRecord.patchtype.compare_patch,
                patchOp = op,
                in1 = in1,
                in2 = in2
            });
            pullcount += 1;
        }

        /// \brief Add a constant variable node to the logical subgraph
        ///
        /// \param rop is the logical operation taking the constant as input
        /// \param mask is the set of bits holding the logical value (within a bigger value)
        /// \param slot is the input slot to the operation
        /// \param constvn is the original constant
        /// \return the new constant variable node
        private ReplaceVarnode addConstant(ReplaceOp rop, ulong mask, uint slot, Varnode constvn)
        {
            ReplaceVarnode res = new ReplaceVarnode() {
                vn = constvn,
                replacement = (Varnode)null,
                mask = mask
            };
            newvarlist.Add(res);

            // Calculate the actual constant value
            int sa = Globals.leastsigbit_set(mask);
            res.val = (mask & constvn.getOffset()) >> sa;
            res.def = (ReplaceOp)null;
            if (rop != (ReplaceOp)null) {
                while (rop.input.size() <= slot)
                    rop.input.Add((ReplaceVarnode)null);
                rop.input[(int)slot] = res;
            }
            return res;
        }

        /// \brief Add a new constant variable node as an input to a logical operation.
        ///
        /// The constant is new and isn't associated with a constant in the original graph.
        /// \param rop is the logical operation taking the constant as input
        /// \param slot is the input slot to the operation
        /// \param val is the constant value
        /// \return the new constant variable node
        private ReplaceVarnode addNewConstant(ReplaceOp rop, uint slot, ulong val)
        {
            ReplaceVarnode res = new ReplaceVarnode() {
                vn = (Varnode)null,
                replacement = (Varnode)null,
                mask = 0,
                val = val,
                def = (ReplaceOp)null
            };
            newvarlist.Add(res);
            if (rop != (ReplaceOp)null) {
                while (rop.input.size() <= slot)
                    rop.input.Add((ReplaceVarnode)null);
                rop.input[(int)slot] = res;
            }
            return res;
        }

        /// \brief Create a new, non-shadowing, subgraph variable node as an operation output
        ///
        /// The new node does not shadow a preexisting Varnode. Because the ReplaceVarnode record
        /// is defined by rop (the -def- field is filled in) this can still be distinguished from a constant.
        /// \param rop is the logical operation taking the new output
        /// \param mask describes the logical value
        private void createNewOut(ReplaceOp rop, ulong mask)
        {
            ReplaceVarnode res = new ReplaceVarnode() {
                vn = (Varnode)null,
                replacement = (Varnode)null,
                mask = mask
            };
            newvarlist.Add(res);
            rop.output = res;
            res.def = rop;
        }

        /// \brief Replace an input Varnode in the subgraph with a temporary register
        ///
        /// This is used to avoid overlapping input Varnode errors. The temporary register
        /// is typically short lived and gets quickly eliminated in favor of the new
        /// logically sized Varnode.
        /// \param rvn is the logical variable to replace
        private void replaceInput(ReplaceVarnode rvn)
        {
            Varnode newvn = fd.newUnique(rvn.vn.getSize());
            newvn = fd.setInputVarnode(newvn);
            fd.totalReplace(rvn.vn, newvn);
            fd.deleteVarnode(rvn.vn);
            rvn.vn = newvn;
        }

        /// \brief Decide if we use the same memory range of the original Varnode for the logical replacement
        ///
        /// Usually the logical Varnode can use the \e true storage bytes that hold the value,
        /// but there are a few corner cases where we want to use a new temporary register to hold the value.
        /// \param rvn is the subgraph variable
        /// \return \b true if the same memory range can be used to hold the value
        private bool useSameAddress(ReplaceVarnode rvn)
        {
            if (rvn.vn.isInput()) return true;
            // If we trim an addrtied varnode, because of required merges, we increase chance of conflicting forms for one variable
            if (rvn.vn.isAddrTied()) return false;
            if ((rvn.mask & 1) == 0) return false; // Not aligned
            if (bitsize >= 8) return true;
            if (aggressive) return true;
            uint bitmask = 1;
            // Try to decide if this is the ONLY subvariable passing through
            // this container
            bitmask = (bitmask << bitsize) - 1;
            ulong mask = rvn.vn.getConsume();
            mask |= (ulong)bitmask;
            if (mask == rvn.mask) return true;
            return false;           // If more of the varnode is consumed than is in just this flow
        }

        /// \brief Build the logical Varnode which will replace its original containing Varnode
        ///
        /// This is the main routine for converting a logical variable in the subgraph into
        /// an actual Varnode object.
        /// \param rvn is the logical variable
        /// \return the (new or existing) Varnode object
        private Varnode getReplaceVarnode(ReplaceVarnode rvn)
        {
            if (rvn.replacement != (Varnode)null)
                return rvn.replacement;
            if (rvn.vn == (Varnode)null) {
                if (rvn.def == (ReplaceOp)null) // A constant that did not come from an original Varnode
                    return fd.newConstant(flowsize, rvn.val);
                rvn.replacement = fd.newUnique(flowsize);
                return rvn.replacement;
            }
            if (rvn.vn.isConstant()) {
                Varnode newVn = fd.newConstant(flowsize, rvn.val);
                newVn.copySymbolIfValid(rvn.vn);
                return newVn;
            }

            bool isinput = rvn.vn.isInput();
            if (useSameAddress(rvn)) {
                Address addr = getReplacementAddress(rvn);
                if (isinput)
                    replaceInput(rvn);  // Replace input to avoid overlap errors
                rvn.replacement = fd.newVarnode(flowsize, addr);
            }
            else
                rvn.replacement = fd.newUnique(flowsize);
            if (isinput)    // Is this an input
                rvn.replacement = fd.setInputVarnode(rvn.replacement);
            return rvn.replacement;
        }

        /// Extend the subgraph from the next node in the worklist
        /// The subgraph is extended from the variable node at the top of the worklist.
        /// Data-flow is traced forward and backward one level, possibly extending the subgraph
        /// and adding new nodes to the worklist.
        /// \return \b true if the node was successfully processed
        private bool processNextWork()
        {
            ReplaceVarnode rvn = worklist.GetLastItem();

            worklist.RemoveLastItem();

            if (sextrestrictions) {
                if (!traceBackwardSext(rvn)) return false;
                return traceForwardSext(rvn);
            }
            if (!traceBackward(rvn)) return false;
            return traceForward(rvn);
        }

        /// \param f is the function to attempt the subvariable transform on
        /// \param root is a starting Varnode containing a smaller logical value
        /// \param mask is a mask where 1 bits indicate the position of the logical value within the \e root Varnode
        /// \param aggr is \b true if we should use aggressive (less restrictive) tests during the trace
        /// \param sext is \b true if we should assume sign extensions from the logical value into its container
        /// \param big is \b true if we look for subvariable flow for \e big (8-byte) logical values
        public SubvariableFlow(Funcdata f, Varnode root, ulong mask, bool aggr, bool sext, bool big)
        {
            fd = f;
            returnsTraversed = false;
            if (mask == (ulong)0) {
                fd = (Funcdata)null;
                return;
            }
            aggressive = aggr;
            sextrestrictions = sext;
            bitsize = (Globals.mostsigbit_set(mask) - Globals.leastsigbit_set(mask)) + 1;
            if (bitsize <= 8)
                flowsize = 1;
            else if (bitsize <= 16)
                flowsize = 2;
            else if (bitsize <= 24)
                flowsize = 3;
            else if (bitsize <= 32)
                flowsize = 4;
            else if (bitsize <= 64){
                if (!big) {
                    fd = (Funcdata)null;
                    return;
                }
                flowsize = 8;
            }
            else {
                fd = (Funcdata)null;
                return;
            }
            createLink((ReplaceOp)null, mask, 0, root);
        }

        /// Trace logical value through data-flow, constructing transform
        /// Push the logical value around, setting up explicit transforms as we go that convert them
        /// into explicit Varnodes. If at any point, we cannot naturally interpret the flow of the
        /// logical value, return \b false.
        /// \return \b true if a full transform has been constructed that can make logical values into explicit Varnodes
        public bool doTrace()
        {
            pullcount = 0;
            bool retval = false;
            if (fd != (Funcdata)null) {
                retval = true;
                while (!worklist.empty()) {
                    if (!processNextWork()) {
                        retval = false;
                        break;
                    }
                }
            }

            // Clear marks
            foreach (Varnode node in varmap.Keys)
                node.clearMark();

            if (!retval) return false;
            if (pullcount == 0) return false;
            return true;
        }

        /// Perform the discovered transform, making logical values explicit
        public void doReplacement()
        {
            IEnumerator<PatchRecord> piter = patchlist.GetEnumerator();

            // Do up front processing of the call return patches, which will be at the front of the list
            while (piter.MoveNext())
            {
                if (piter.Current.type != PatchRecord.patchtype.push_patch) break;
                PcodeOp pushOp = piter.Current.patchOp;
                Varnode newVn = getReplaceVarnode(piter.Current.in1);
                Varnode oldVn = pushOp.getOut();
                fd.opSetOutput(pushOp, newVn);

                // Create placeholder defining op for old Varnode, until dead code cleans it up
                PcodeOp newZext = fd.newOp(1, pushOp.getAddr());
                fd.opSetOpcode(newZext, OpCode.CPUI_INT_ZEXT);
                fd.opSetInput(newZext, newVn, 0);
                fd.opSetOutput(newZext, oldVn);
                fd.opInsertAfter(newZext, pushOp);
            }

            IEnumerator<ReplaceOp> iter = oplist.GetEnumerator();
            // Define all the outputs first
            while (iter.MoveNext()) {
                PcodeOp newop = fd.newOp(iter.Current.numparams, iter.Current.op.getAddr());
                iter.Current.replacement = newop;
                fd.opSetOpcode(newop, iter.Current.opc);
                ReplaceVarnode rout = iter.Current.output;
                //      if (rout != (ReplaceVarnode *)0) {
                //	if (rout.replacement == (Varnode *)0)
                //	  rout.replacement = fd.newUniqueOut(flowsize,newop);
                //	else
                //	  fd.opSetOutput(newop,rout.replacement);
                //      }
                fd.opSetOutput(newop, getReplaceVarnode(rout));
                fd.opInsertAfter(newop, iter.Current.op);
            }

            // Set all the inputs
            iter = oplist.GetEnumerator();
            while (iter.MoveNext()) {
                PcodeOp newop = iter.Current.replacement;
                for (int i = 0; i < iter.Current.input.size(); ++i)
                    fd.opSetInput(newop, getReplaceVarnode(iter.Current.input[i]), i);
            }

            // These are operations that carry flow from the small variable into an existing
            // variable of the correct size
            while (piter.MoveNext()) {
                PcodeOp pullop = piter.Current.patchOp;
                switch (piter.Current.type) {
                    case PatchRecord.patchtype.copy_patch:
                        while (pullop.numInput() > 1)
                            fd.opRemoveInput(pullop, pullop.numInput() - 1);
                        fd.opSetInput(pullop, getReplaceVarnode(piter.Current.in1), 0);
                        fd.opSetOpcode(pullop, OpCode.CPUI_COPY);
                        break;
                    case PatchRecord.patchtype.compare_patch:
                        fd.opSetInput(pullop, getReplaceVarnode(piter.Current.in1), 0);
                        fd.opSetInput(pullop, getReplaceVarnode(piter.Current.in2), 1);
                        break;
                    case PatchRecord.patchtype.parameter_patch:
                        fd.opSetInput(pullop, getReplaceVarnode(piter.Current.in1), piter.Current.slot);
                        break;
                    case PatchRecord.patchtype.extension_patch: {
                            // These are operations that flow the small variable into a bigger variable but
                            // where all the remaining bits are zero
                            int sa = piter.Current.slot;
                            List<Varnode> invec = new List<Varnode>();
                            Varnode inVn = getReplaceVarnode(piter.Current.in1);
                            int outSize = pullop.getOut().getSize();
                            if (sa == 0) {
                                invec.Add(inVn);
                                OpCode opc = (inVn.getSize() == outSize)
                                    ? OpCode.CPUI_COPY
                                    : OpCode.CPUI_INT_ZEXT;
                                fd.opSetOpcode(pullop, opc);
                                fd.opSetAllInput(pullop, invec);
                            }
                            else {
                                if (inVn.getSize() != outSize) {
                                    PcodeOp zextop = fd.newOp(1, pullop.getAddr());
                                    fd.opSetOpcode(zextop, OpCode.CPUI_INT_ZEXT);
                                    Varnode zextout = fd.newUniqueOut(outSize, zextop);
                                    fd.opSetInput(zextop, inVn, 0);
                                    fd.opInsertBefore(zextop, pullop);
                                    invec.Add(zextout);
                                }
                                else
                                    invec.Add(inVn);
                                invec.Add(fd.newConstant(4, (ulong)sa));
                                fd.opSetAllInput(pullop, invec);
                                fd.opSetOpcode(pullop, OpCode.CPUI_INT_LEFT);
                            }
                            break;
                        }
                    case PatchRecord.patchtype.push_patch:
                        break;  // Shouldn't see these here, handled earlier
                }
            }
        }
    }
}
