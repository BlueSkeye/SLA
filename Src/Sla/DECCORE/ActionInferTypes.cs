using Sla.CORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Infer and propagate data-types.
    ///
    /// Atomic data-types are ordered from \e most specified to \e least specified.
    /// This is extended rescursively to an ordering on composite data-types via Datatype::typeOrder().
    /// A local data-type is calculated for each Varnode by looking at the data-types
    /// expected by the PcodeOps it is directly involved in (as input or output).
    /// Every Varnode has 1 chance to propagate its information throughout the graph
    /// along COPY,LOAD,STORE,ADD,MULTIEQUAL,and INDIRECT edges. The propagation is
    /// done with a depth first search along propagating edges.  If the propagated
    /// data-type is the same, less than, or if the varnode had been propagated through
    /// already, that branch is trimmed.  Every edge can theoretically get traversed
    /// once, i.e. the search allows the type to propagate through a looping edge,
    /// but immediately truncates.
    /// This is probably quadratic in the worst case, if each Varnode has a higher
    /// type and propagates it to the entire graph.  But it is linear in practice,
    /// because there are generally only two or three levels of type, so only one
    /// or two Varnodes are likely to propagate widely within a component, and
    /// the others get truncated immediately.  An initial sort on the data-type level
    /// of the Varnodes, so that the highest-level types are propagated first,
    /// would probably fix the worst-case, but this seems unnecessary.
    /// Complications:
    /// type_metatype.TYPE_SPACEBASE is a problem because we have to make sure that it doesn't
    /// propagate.
    /// Also, offsets off of pointers to type_metatype.TYPE_SPACEBASE look up the data-type in the
    /// local map. Then ActionRestructure uses data-type information recovered by
    /// this algorithm to reconstruct the local map.  This causes a feedback loop
    /// which allows type information recovered about mapped Varnodes to be propagated
    /// to pointer Varnodes which point to the mapped object.  Unfortunately under
    /// rare circumstances, this feedback-loop does not converge for some reason.
    /// Rather than hunt this down, I've put an arbitrary iteration limit on
    /// the data-type propagation algorithm, which reports a warning if the limit is
    /// reached and then aborts additional propagation so that decompiling can terminate.
    internal class ActionInferTypes : Action
    {
#if TYPEPROP_DEBUG
        /// \brief Log a particular data-type propagation action.
        ///
        /// Print the Varnode updated, the new data-type it contains, and
        /// where the data-type propagated from.
        /// \param glb is the Architecture holding the error console
        /// \param vn is the target Varnode
        /// \param newtype is the new data-type
        /// \param op is the PcodeOp through which the data-type propagated
        /// \param slot is the slot from which the data-type propagated
        /// \param ptralias if not NULL holds the pointer that aliased the target Varnode
        private static void propagationDebug(Architecture glb, Varnode vn, Datatype newtype,
            PcodeOp op, int slot, Varnode ptralias);
        {
          ostringstream s;

          vn.printRaw(s);
          s << " : ";
          newtype.printRaw(s);
          if ((op == (PcodeOp *)0)&&(ptralias == (Varnode *)0)) {
            s << " init";
          }
          else if (ptralias != (Varnode *)0) {
            s << " alias ";
            ptralias.printRaw(s);
          }
          else {
            s << " from ";
            op.printRaw(s);
            s << " slot=" << dec << slot;
          }
          glb.printDebug(s.str());
        }
#endif

        /// Number of passes performed for this function
        private int localcount;

        /// Assign initial data-type based on local info
        /// Collect \e local data-type information on each Varnode inferred
        /// from the PcodeOps that read and write to it.
        /// \param data is the function being analyzed
        private static void buildLocaltypes(Funcdata data)
        {
            Datatype* ct;
            Varnode* vn;
            VarnodeLocSet::const_iterator iter;
            TypeFactory* typegrp = data.getArch().types;

            for (iter = data.beginLoc(); iter != data.endLoc(); ++iter)
            {
                vn = *iter;
                if (vn.isAnnotation()) continue;
                if ((!vn.isWritten()) && (vn.hasNoDescend())) continue;
                bool needsBlock = false;
                SymbolEntry* entry = vn.getSymbolEntry();
                if (entry != (SymbolEntry)null && !vn.isTypeLock() && entry.getSymbol().isTypeLocked())
                {
                    int curOff = (vn.getAddr().getOffset() - entry.getAddr().getOffset()) + entry.getOffset();
                    ct = typegrp.getExactPiece(entry.getSymbol().getType(), curOff, vn.getSize());
                    if (ct == (Datatype)null || ct.getMetatype() == type_metatype.TYPE_UNKNOWN)    // If we can't resolve, or resolve to UNKNOWN
                        ct = vn.getLocalType(needsBlock);      // Let data-type float, even though parent symbol is type-locked
                }
                else
                    ct = vn.getLocalType(needsBlock);
                if (needsBlock)
                    vn.setStopUpPropagation();
#if TYPEPROP_DEBUG
                propagationDebug(data.getArch(), vn, ct, (PcodeOp)null, 0, (Varnode)null);
#endif
                vn.setTempType(ct);
            }
        }

        /// Commit the final propagated data-types to Varnodes
        /// For each Varnode copy the temporary data-type to the permament
        /// field, taking into account previous locks.
        /// \param data is the function being analyzed
        /// \return \b true if any Varnode's data-type changed from the last round of propagation
        private static bool writeBack(Funcdata data)
        {
            bool change = false;
            Datatype* ct;
            Varnode* vn;
            VarnodeLocSet::const_iterator iter;

            for (iter = data.beginLoc(); iter != data.endLoc(); ++iter)
            {
                vn = *iter;
                if (vn.isAnnotation()) continue;
                if ((!vn.isWritten()) && (vn.hasNoDescend())) continue;
                ct = vn.getTempType();
                if (vn.updateType(ct, false, false))
                    change = true;
            }
            return change;
        }

        /// \brief Attempt to propagate a data-type across a single PcodeOp edge
        ///
        /// Given an \e input Varnode and an \e output Varnode defining a directed edge
        /// through a PcodeOp, determine if and how the input data-type propagates to the
        /// output. Update the output Varnode's (temporary) data-type. An input to the
        /// edge may either an input or output to the PcodeOp.  A \e slot value of -1
        /// indicates the PcodeOp output, a non-negative value indicates a PcodeOp input index.
        /// \param typegrp is the TypeFactory for building a possibly transformed data-type
        /// \param op is the PcodeOp through which the propagation edge flows
        /// \param inslot indicates the edge's input Varnode
        /// \param outslot indicates the edge's output Varnode
        /// \return \b true if the data-type propagates
        private static bool propagateTypeEdge(TypeFactory typegrp, PcodeOp op, int inslot, int outslot)
        {
            Varnode invn, outvn;

            invn = (inslot == -1) ? op.getOut() : op.getIn(inslot);
            Datatype alttype = invn.getTempType();
            if (alttype.needsResolution()) {
                // Always give incoming data-type a chance to resolve, even if it would not otherwise propagate
                alttype = alttype.resolveInFlow(op, inslot);
            }
            if (inslot == outslot) return false; // don't backtrack
            if (outslot < 0)
                outvn = op.getOut();
            else {
                outvn = op.getIn(outslot);
                if (outvn.isAnnotation()) return false;
            }
            if (outvn.isTypeLock()) return false; // Can't propagate through typelock
            if (outvn.stopsUpPropagation() && outslot >= 0) return false;  // Propagation is blocked

            if (alttype.getMetatype() == type_metatype.TYPE_BOOL) {
                // Only propagate boolean
                if (outvn.getNZMask() > 1)         // If we know output can only take boolean values
                    return false;
            }

            Datatype newtype = op.getOpcode().propagateType(alttype, op, invn, outvn, inslot, outslot);
            if (newtype == (Datatype)null)
                return false;

            if (0 > newtype.typeOrder(*outvn.getTempType())) {
#if TYPEPROP_DEBUG
                propagationDebug(typegrp.getArch(), outvn, newtype, op, inslot, (Varnode)null);
#endif
                outvn.setTempType(newtype);
                return !outvn.isMark();
            }
            return false;
        }

        /// \brief Propagate a data-type starting from one Varnode across the function
        ///
        /// Given a starting Varnode, propagate its Datatype as far as possible through
        /// the data-flow graph, transforming the data-type through PcodeOps as necessary.
        /// The data-type is push through all possible propagating edges, but each
        /// Varnode is visited at most once.  Propagation is trimmed along any particular
        /// path if the pushed data-type isn't \e more \e specific than the current
        /// data-type on a Varnode, under the data-type ordering.
        /// \param typegrp is the TypeFactory for constructing transformed data-types
        /// \param vn is the Varnode holding the root data-type to push
        private static void propagateOneType(TypeFactory typegrp, Varnode vn)
        {
            PropagationState ptr;
            List<PropagationState> state = new List<PropagationState>();
            state.Add(new PropagationState(vn));
            vn.setMark();

            while (!state.empty()) {
                ptr = &state.GetLastItem();
                if (!ptr.valid()) {
                    // If we are out of edges to traverse
                    ptr.vn.clearMark();
                    state.RemoveLastItem();
                }
                else {
                    if (propagateTypeEdge(typegrp, ptr.op, ptr.inslot, ptr.slot)) {
                        vn = (ptr.slot == -1) ? ptr.op.getOut() : ptr.op.getIn(ptr.slot);
                        ptr.step();        // Make sure to step before push_back
                        state.Add(new PropagationState(vn));
                        vn.setMark();
                    }
                    else
                        ptr.step();
                }
            }
        }

        /// \brief Try to propagate a pointer data-type to known aliases.
        ///
        /// Given a Varnode which is a likely pointer and an Address that
        /// is a known alias of the pointer, attempt to propagate the Varnode's
        /// data-type to Varnodes at that address.
        /// \param data is the function being analyzed
        /// \param vn is the given Varnode
        /// \param addr is the aliased address
        private static void propagateRef(Funcdata data, Varnode vn, Address addr)
        {
            Datatype* ct = vn.getTempType();
            if (ct.getMetatype() != type_metatype.TYPE_PTR) return;
            ct = ((TypePointer*)ct).getPtrTo();
            if (ct.getMetatype() == type_metatype.TYPE_SPACEBASE) return;
            if (ct.getMetatype() == type_metatype.TYPE_UNKNOWN) return; // Don't bother propagating this
            VarnodeLocSet::const_iterator iter, enditer;
            ulong off = addr.getOffset();
            TypeFactory* typegrp = data.getArch().types;
            Address endaddr = addr + ct.getSize();
            if (endaddr.getOffset() < off) // If the address wrapped
                enditer = data.endLoc(addr.getSpace()); // Go to end of space
            else
                enditer = data.endLoc(endaddr);
            iter = data.beginLoc(addr);
            ulong lastoff = 0;
            int lastsize = ct.getSize();
            Datatype* lastct = ct;
            while (iter != enditer)
            {
                Varnode* curvn = *iter;
                ++iter;
                if (curvn.isAnnotation()) continue;
                if ((!curvn.isWritten()) && curvn.hasNoDescend()) continue;
                if (curvn.isTypeLock()) continue;
                if (curvn.getSymbolEntry() != (SymbolEntry)null) continue;
                ulong curoff = curvn.getOffset() - off;
                int cursize = curvn.getSize();
                if (curoff + cursize > ct.getSize()) continue;
                if ((cursize != lastsize) || (curoff != lastoff))
                {
                    lastoff = curoff;
                    lastsize = cursize;
                    lastct = typegrp.getExactPiece(ct, curoff, cursize);
                }
                if (lastct == (Datatype)null) continue;

                // Try to propagate the reference type into a varnode that is pointed to by that reference
                if (0 > lastct.typeOrder(*curvn.getTempType()))
                {
#if TYPEPROP_DEBUG
                    propagationDebug(data.getArch(), curvn, lastct, (PcodeOp)null, 0, vn);
#endif
                    curvn.setTempType(lastct);
                    propagateOneType(typegrp, curvn); // Try to propagate the new type as far as possible
                }
            }
        }

        /// \brief Search for pointers and propagate its data-type to known aliases
        ///
        /// This routine looks for ADD operations off of a specific
        /// \e spacebase register that produce output Varnodes with a known
        /// data-type. The offset of the ADD is calculated into the corresponding
        /// address space, and an attempt is made to propagate the Varnodes data-type
        /// to other Varnodes in the address space at that offset.
        /// \param data is the function being analyzed
        /// \param spcvn is the spacebase register
        private static void propagateSpacebaseRef(Funcdata data, Varnode spcvn)
        {
            Datatype spctype = spcvn.getType();   // This is an absolute property of the varnode, so not temptype
            if (spctype.getMetatype() != type_metatype.TYPE_PTR) return;
            spctype = ((TypePointer)spctype).getPtrTo();
            if (spctype.getMetatype() != type_metatype.TYPE_SPACEBASE) return;
            TypeSpacebase sbtype = (TypeSpacebase)spctype;
            IEnumerator<PcodeOp> iter = spcvn.beginDescend();
            Address addr;

            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                Varnode vn;
                switch (op.code()) {
                    case OpCode.CPUI_COPY:
                        vn = op.getIn(0);
                        addr = sbtype.getAddress(0, vn.getSize(), op.getAddr());
                        propagateRef(data, op.getOut(), addr);
                        break;
                    case OpCode.CPUI_INT_ADD:
                    case OpCode.CPUI_PTRSUB:
                        vn = op.getIn(1);
                        if (vn.isConstant()) {
                            addr = sbtype.getAddress(vn.getOffset(), vn.getSize(), op.getAddr());
                            propagateRef(data, op.getOut(), addr);
                        }
                        break;
                    case OpCode.CPUI_PTRADD:
                        vn = op.getIn(1);
                        if (vn.isConstant()) {
                            ulong off = vn.getOffset() * op.getIn(2).getOffset();
                            addr = sbtype.getAddress(off, vn.getSize(), op.getAddr());
                            propagateRef(data, op.getOut(), addr);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// Return the OpCode.CPUI_RETURN op with the most specialized data-type, which is not
        /// dead and is not a special halt.
        /// \param data is the function
        /// \return the representative OpCode.CPUI_RETURN op or NULL
        private static PcodeOp canonicalReturnOp(Funcdata data)
        {
            PcodeOp? res = (PcodeOp)null;
            Datatype? bestdt = (Datatype)null;
            IEnumerator<PcodeOp> iter, iterend;
            iterend = data.endOp(OpCode.CPUI_RETURN);
            for (iter = data.beginOp(OpCode.CPUI_RETURN); iter != iterend; ++iter)
            {
                PcodeOp retop = *iter;
                if (retop.isDead()) continue;
                if (retop.getHaltType() != 0) continue;
                if (retop.numInput() > 1)
                {
                    Varnode* vn = retop.getIn(1);
                    Datatype* ct = vn.getTempType();
                    if (bestdt == (Datatype)null)
                    {
                        res = retop;
                        bestdt = ct;
                    }
                    else if (ct.typeOrder(*bestdt) < 0)
                    {
                        res = retop;
                        bestdt = ct;
                    }
                }
            }
            return res;
        }

        /// \brief Give data-types a chance to propagate between OpCode.CPUI_RETURN operations.
        ///
        /// Since a function is intended to return a single data-type, data-types effectively
        /// propagate between the input Varnodes to OpCode.CPUI_RETURN ops, if there are more than one.
        private static void propagateAcrossReturns(Funcdata data)
        {
            if (data.getFuncProto().isOutputLocked()) return;
            PcodeOp? op = canonicalReturnOp(data);
            if (op == (PcodeOp)null) return;
            TypeFactory typegrp = data.getArch().types;
            Varnode baseVn = op.getIn(1);
            Datatype ct = baseVn.getTempType();
            int baseSize = baseVn.getSize();
            bool isBool = ct.getMetatype() == type_metatype.TYPE_BOOL;
            IEnumerator<PcodeOp> iter, iterend;
            iterend = data.endOp(OpCode.CPUI_RETURN);
            for (iter = data.beginOp(OpCode.CPUI_RETURN); iter != iterend; ++iter) {
                PcodeOp retop = *iter;
                if (retop == op) continue;
                if (retop.isDead()) continue;
                if (retop.getHaltType() != 0) continue;
                if (retop.numInput() > 1) {
                    Varnode vn = retop.getIn(1);
                    if (vn.getSize() != baseSize) continue;
                    if (isBool && vn.getNZMask() > 1) continue;    // Don't propagate bool if value is not necessarily 0 or 1
                    if (vn.getTempType() == ct) continue;      // Already propagated
                    vn.setTempType(ct);
#if TYPEPROP_DEBUG
                    propagationDebug(typegrp.getArch(), vn, ct, op, 1, (Varnode)null);
#endif
                    propagateOneType(typegrp, vn);
                }
            }
        }

        public ActionInferTypes(string g)
            : base(0,"infertypes", g)
        {
        }

        public override void reset(Funcdata data)
        {
            localcount = 0;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionInferTypes(getGroup());
        }

        public override int apply(Funcdata data)
        {
            // Make sure spacebase is accurate or bases could get typed and then ptrarithed
            if (!data.hasTypeRecoveryStarted()) return 0;
            TypeFactory* typegrp = data.getArch().types;
            Varnode* vn;
            VarnodeLocSet::const_iterator iter;

#if TYPEPROP_DEBUG
            ostringstream s;
            s << "Type propagation pass - " << dec << localcount;
            data.getArch().printDebug(s.str());
#endif
            if (localcount >= 7)
            {       // This constant arrived at empirically
                if (localcount == 7)
                {
                    data.warningHeader("Type propagation algorithm not settling");
                    localcount += 1;
                }
                return 0;
            }
            data.getScopeLocal().applyTypeRecommendations();
            buildLocaltypes(data);  // Set up initial types (based on local info)
            for (iter = data.beginLoc(); iter != data.endLoc(); ++iter)
            {
                vn = *iter;
                if (vn.isAnnotation()) continue;
                if ((!vn.isWritten()) && (vn.hasNoDescend())) continue;
                propagateOneType(typegrp, vn);
            }
            propagateAcrossReturns(data);
            AddrSpace* spcid = data.getScopeLocal().getSpaceId();
            Varnode* spcvn = data.findSpacebaseInput(spcid);
            if (spcvn != (Varnode)null)
                propagateSpacebaseRef(data, spcvn);
            if (writeBack(data))
            {
                // count += 1;			// Do not consider this a data-flow change
                localcount += 1;
            }
            return 0;
        }
    }
}
