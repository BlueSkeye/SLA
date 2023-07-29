using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.XmlScan;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Fill-in CPUI_CAST p-code ops as required by the casting strategy
    ///
    /// Setting the casts is complicated by type inference and
    /// implied variables.  By the time this Action is run, the
    /// type inference algorithm has labeled every Varnode with what
    /// it thinks the type should be.  This casting algorithm tries
    /// to get the code to legally match this inference result by
    /// adding casts.  Following the data flow, it tries the best it
    /// can to get each token to match the inferred type.  For
    /// implied variables, the type is completely determined by the
    /// syntax of the output language, so implied casts won't work in this case.
    /// For most of these cases, the algorithm just changes the type
    /// to that dictated by syntax and gets back on track at the
    /// next explicit variable in the flow. It tries to avoid losing
    /// pointer types however because any CPUI_PTRADD \b mst have a pointer
    /// input. In this case, it casts to the necessary pointer type
    /// immediately.
    internal class ActionSetCasts : Action
    {
        /// \brief Check if the data-type of the given value being used as a pointer makes sense
        ///
        /// If the data-type is a pointer make sure:
        ///   - The pointed-to size matches the size of the value being loaded are stored
        ///   - Any address space attached to the pointer matches the address space of the LOAD/STORE
        ///
        /// If any of the conditions are violated, a warning is added to the output.
        /// \param op is the LOAD/STORE acting on a pointer
        /// \param vn is the given value being used as a pointer
        /// \param data is the function containing the PcodeOp
        private static void checkPointerIssues(PcodeOp op, Varnode vn, Funcdata data)
        {
            Datatype* ptrtype = op.getIn(1).getHighTypeReadFacing(op);
            int4 valsize = vn.getSize();
            if ((ptrtype.getMetatype() != TYPE_PTR) || (((TypePointer*)ptrtype).getPtrTo().getSize() != valsize))
            {
                string name = op.getOpcode().getName();
                name[0] = toupper(name[0]);
                data.warning(name + " size is inaccurate", op.getAddr());
            }
            if (ptrtype.getMetatype() == TYPE_PTR)
            {
                AddrSpace* spc = ((TypePointer*)ptrtype).getSpace();
                if (spc != (AddrSpace*)0)
                {
                    AddrSpace* opSpc = op.getIn(0).getSpaceFromConst();
                    if (opSpc != spc && spc.getContain() != opSpc)
                    {
                        string name = op.getOpcode().getName();
                        name[0] = toupper(name[0]);
                        ostringstream s;
                        s << name << " refers to '" << opSpc.getName() << "' but pointer attribute is '";
                        s << spc.getName() << '\'';
                        data.warning(s.str(), op.getAddr());
                    }
                }
            }
        }

        /// \brief Test if the given cast conflict can be resolved by passing to the first structure field
        ///
        /// Test if the given Varnode data-type is a pointer to a structure and if interpreting
        /// the data-type as a pointer to the structure's first field will get it to match the
        /// desired data-type.
        /// \param vn is the given Varnode
        /// \param op is the PcodeOp reading the Varnode
        /// \param ct is the desired data-type
        /// \param castStrategy is used to determine if the data-types are compatible
        /// \return \b true if a pointer to the first field makes sense
        private static bool testStructOffset0(Varnode vn, PcodeOp op, Datatype ct,
            CastStrategy castStrategy)
        {
            if (ct.getMetatype() != TYPE_PTR) return false;
            Datatype* highType = vn.getHighTypeReadFacing(op);
            if (highType.getMetatype() != TYPE_PTR) return false;
            Datatype* highPtrTo = ((TypePointer*)highType).getPtrTo();
            if (highPtrTo.getMetatype() != TYPE_STRUCT) return false;
            TypeStruct* highStruct = (TypeStruct*)highPtrTo;
            if (highStruct.numDepend() == 0) return false;
            vector<TypeField>::const_iterator iter = highStruct.beginField();
            if ((*iter).offset != 0) return false;
            Datatype* reqtype = ((TypePointer*)ct).getPtrTo();
            Datatype* curtype = (*iter).type;
            if (reqtype.getMetatype() == TYPE_ARRAY)
                reqtype = ((TypeArray*)reqtype).getBase();
            if (curtype.getMetatype() == TYPE_ARRAY)
                curtype = ((TypeArray*)curtype).getBase();
            return (castStrategy.castStandard(reqtype, curtype, true, true) == (Datatype*)0);
        }

        /// \brief Try to adjust the input and output Varnodes to eliminate a CAST
        ///
        /// If input/output data-types are different, it may be due to late merges.  For
        /// unions, the CAST can sometimes be eliminated by adjusting the data-type resolutions
        /// of the Varnodes relative to the PcodeOp
        /// \param op is the PcodeOp reading the input Varnode and writing the output Varnode
        /// \param slot is the index of the input Varnode
        /// \param data is the function
        /// \return \b true if an adjustment is made so that a CAST is no longer needed
        private static bool tryResolutionAdjustment(PcodeOp op, int slot, Funcdata data)
        {
            Varnode* outvn = op.getOut();
            if (outvn == (Varnode*)0)
                return false;
            Datatype* outType = outvn.getHigh().getType();
            Datatype* inType = op.getIn(slot).getHigh().getType();
            if (!inType.needsResolution() && !outType.needsResolution()) return false;
            int4 inResolve = -1;
            int4 outResolve = -1;
            if (inType.needsResolution())
            {
                inResolve = inType.findCompatibleResolve(outType);
                if (inResolve < 0) return false;
            }
            if (outType.needsResolution())
            {
                if (inResolve >= 0)
                    outResolve = outType.findCompatibleResolve(inType.getDepend(inResolve));
                else
                    outResolve = outType.findCompatibleResolve(inType);
                if (outResolve < 0) return false;
            }

            TypeFactory* typegrp = data.getArch().types;
            if (inType.needsResolution())
            {
                ResolvedUnion resolve(inType, inResolve,* typegrp);
                if (!data.setUnionField(inType, op, slot, resolve))
                    return false;
            }
            if (outType.needsResolution())
            {
                ResolvedUnion resolve(outType, outResolve,* typegrp);
                if (!data.setUnionField(outType, op, -1, resolve))
                    return false;
            }
            return true;
        }

        /// \brief Test if two data-types are operation identical
        ///
        /// If, at a source code level, a variable with data-type \b ct1 can be
        /// legally substituted for another variable with data-type \b ct2, return \b true.
        /// The substitution must be allowed for all possible operations the variable
        /// may be involved in.
        /// \param ct1 is the first data-type
        /// \param ct2 is the second data-type
        private static bool isOpIdentical(Datatype ct1, Datatype ct2)
        {
            while ((ct1.getMetatype() == TYPE_PTR) && (ct2.getMetatype() == TYPE_PTR))
            {
                ct1 = ((TypePointer)ct1).getPtrTo();
                ct2 = ((TypePointer)ct2).getPtrTo();
            }
            while (ct1.getTypedef() != (Datatype*)0)
                ct1 = ct1.getTypedef();
            while (ct2.getTypedef() != (Datatype*)0)
                ct2 = ct2.getTypedef();
            return (ct1 == ct2);
        }

        /// \brief If the given op reads a pointer to a union, insert the CPUI_PTRSUB that resolves the union
        ///
        /// \param op is the given PcodeOp
        /// \param slot is index of the input slot being read
        /// \param data is the containing function
        /// \return 1 if a PTRSUB is inserted, 0 otherwise
        private static int4 resolveUnion(PcodeOp op, int slot, Funcdata data)
        {
            Varnode* vn = op.getIn(slot);
            if (vn.isAnnotation()) return 0;
            Datatype* dt = vn.getHigh().getType();
            if (!dt.needsResolution())
                return 0;
            if (dt != vn.getType())
                dt.resolveInFlow(op, slot);    // Last chance to resolve data-type based on flow
            ResolvedUnion resUnion = data.getUnionField(dt, op, slot);
            if (resUnion != (ResolvedUnion*)0 && resUnion.getFieldNum() >= 0)
            {
                // Insert specific placeholder indicating which field is accessed
                if (dt.getMetatype() == TYPE_PTR)
                {
                    PcodeOp* ptrsub = insertPtrsubZero(op, slot, resUnion.getDatatype(), data);
                    data.setUnionField(dt, ptrsub, -1, *resUnion);          // Attach the resolution to the PTRSUB
                }
                else if (vn.isImplied())
                {
                    if (vn.isWritten())
                    {
                        // If the writefacing and readfacing resolutions for vn (an implied variable) are the same,
                        // the resolutions are unnecessary and we treat the vn as if it had the field data-type
                        ResolvedUnion* writeRes = data.getUnionField(dt, vn.getDef(), -1);
                        if (writeRes != (ResolvedUnion)0 && writeRes.getFieldNum() == resUnion.getFieldNum())
                            return 0; // Don't print implied fields for vn
                    }
                    vn.setImpliedField();
                }
                return 1;
            }
            return 0;
        }

        /// \brief Insert cast to output Varnode type after given PcodeOp if it is necessary
        ///
        /// \param op is the given PcodeOp
        /// \param data is the function being analyzed
        /// \param castStrategy is used to determine if the cast is necessary
        /// \return 1 if a cast inserted, 0 otherwise
        private static int castOutput(PcodeOp op, Funcdata data, CastStrategy castStrategy)
        {
            Datatype* outct,*ct,*tokenct;
            Varnode* vn,*outvn;
            PcodeOp* newop;
            Datatype* outHighType;
            bool force = false;

            tokenct = op.getOpcode().getOutputToken(op, castStrategy);
            outvn = op.getOut();
            outHighType = outvn.getHigh().getType();
            if (tokenct == outHighType)
            {
                if (tokenct.needsResolution())
                {
                    // operation copies directly to outvn AS a union
                    ResolvedUnion resolve(tokenct); // Force the varnode to resolve to the parent data-type
                    data.setUnionField(tokenct, op, -1, resolve);
                }
                // Short circuit more sophisticated casting tests.  If they are the same type, there is no cast
                return 0;
            }
            Datatype* outHighResolve = outHighType;
            if (outHighType.needsResolution())
            {
                if (outHighType != outvn.getType())
                    outHighType.resolveInFlow(op, -1);     // Last chance to resolve data-type based on flow
                outHighResolve = outHighType.findResolve(op, -1);  // Finish fetching DefFacing data-type
            }
            if (outvn.isImplied())
            {
                // implied varnode must have parse type
                if (outvn.isTypeLock())
                {
                    PcodeOp* outOp = outvn.loneDescend();
                    // The Varnode input to a CPUI_RETURN is marked as implied but
                    // casting should act as if it were explicit
                    if (outOp == (PcodeOp*)0 || outOp.code() != CPUI_RETURN)
                    {
                        force = !isOpIdentical(outHighResolve, tokenct);
                    }
                }
                else if (outHighResolve.getMetatype() != TYPE_PTR)
                {   // If implied varnode has an atomic (non-pointer) type
                    outvn.updateType(tokenct, false, false); // Ignore it in favor of the token type
                    outHighResolve = outvn.getHighTypeDefFacing();
                }
                else if (tokenct.getMetatype() == TYPE_PTR)
                { // If the token is a pointer AND implied varnode is pointer
                    outct = ((TypePointer*)outHighResolve).getPtrTo();
                    type_metatype meta = outct.getMetatype();
                    // Preserve implied pointer if it points to a composite
                    if ((meta != TYPE_ARRAY) && (meta != TYPE_STRUCT) && (meta != TYPE_UNION))
                    {
                        outvn.updateType(tokenct, false, false); // Otherwise ignore it in favor of the token type
                        outHighResolve = outvn.getHighTypeDefFacing();
                    }
                }
            }
            if (!force)
            {
                outct = outHighResolve; // Type of result
                ct = castStrategy.castStandard(outct, tokenct, false, true);
                if (ct == (Datatype*)0) return 0;
            }
            // Generate the cast op
            vn = data.newUnique(outvn.getSize());
            vn.updateType(tokenct, false, false);
            vn.setImplied();
            newop = data.newOp(1, op.getAddr());
#if CPUI_STATISTICS
            data.getArch().stats.countCast();
#endif
            data.opSetOpcode(newop, CPUI_CAST);
            data.opSetOutput(newop, outvn);
            data.opSetInput(newop, vn, 0);
            data.opSetOutput(op, vn);
            data.opInsertAfter(newop, op); // Cast comes AFTER this operation
            if (tokenct.needsResolution())
                data.forceFacingType(tokenct, -1, newop, 0);
            if (outHighType.needsResolution())
                data.inheritResolution(outHighType, newop, -1, op, -1); // Inherit write resolution

            return 1;
        }

        /// \brief Insert cast to produce the input Varnode to a given PcodeOp if necessary
        ///
        /// This method can also mark a Varnode as an explicit integer constant.
        /// Guard against chains of casts.
        /// \param op is the given PcodeOp
        /// \param slot is the slot of the input Varnode
        /// \param data is the function being analyzed
        /// \param castStrategy is used to determine if a cast is necessary
        /// \return 1 if a change is made, 0 otherwise
        private static int castInput(PcodeOp op, int slot, Funcdata data, CastStrategy castStrategy)
        {
            Datatype* ct;
            Varnode* vn,*vnout;
            PcodeOp* newop;

            ct = op.getOpcode().getInputCast(op, slot, castStrategy); // Input type expected by this operation
            if (ct == (Datatype*)0)
            {
                bool resUnsigned = castStrategy.markExplicitUnsigned(op, slot);
                bool resSized = castStrategy.markExplicitLongSize(op, slot);
                if (resUnsigned || resSized)
                    return 1;
                return 0;
            }

            vn = op.getIn(slot);
            // Check to make sure we don't have a double cast
            if (vn.isWritten() && (vn.getDef().code() == CPUI_CAST))
            {
                if (vn.isImplied() && (vn.loneDescend() == op))
                {
                    vn.updateType(ct, false, false);
                    if (vn.getType() == ct)
                        return 1;
                }
            }
            else if (vn.isConstant())
            {
                vn.updateType(ct, false, false);
                if (vn.getType() == ct)
                    return 1;
            }
            else if (testStructOffset0(vn, op, ct, castStrategy))
            {
                // Insert a PTRSUB(vn,#0) instead of a CAST
                newop = insertPtrsubZero(op, slot, ct, data);
                if (vn.getHigh().getType().needsResolution())
                    data.inheritResolution(vn.getHigh().getType(), newop, 0, op, slot);
                return 1;
            }
            else if (tryResolutionAdjustment(op, slot, data))
            {
                return 1;
            }
            newop = data.newOp(1, op.getAddr());
            vnout = data.newUniqueOut(vn.getSize(), newop);
            vnout.updateType(ct, false, false);
            vnout.setImplied();
#if CPUI_STATISTICS
            data.getArch().stats.countCast();
#endif
            data.opSetOpcode(newop, CPUI_CAST);
            data.opSetInput(newop, vn, 0);
            data.opSetInput(op, vnout, slot);
            data.opInsertBefore(newop, op); // Cast comes AFTER operation
            if (ct.needsResolution())
            {
                data.forceFacingType(ct, -1, newop, -1);
            }
            if (vn.getHigh().getType().needsResolution())
            {
                data.inheritResolution(vn.getHigh().getType(), newop, 0, op, slot);
            }
            return 1;
        }

        /// \brief Insert a PTRSUB with offset 0 that accesses a field of the given data-type
        /// The data-type can be a structure, in which case the field at offset zero is being accessed.
        /// The data-type can reference a union, in which case a specific field is being accessed
        /// as indicated by Funcdata::getUnionField.  The PTRSUB is inserted right before the given
        /// PcodeOp.  The indicated input Varnode becomes the PTRSUB input, and the PTRSUB output
        /// replaces the Varnode in the PcodeOp.
        /// \param op is the given PcodeOp where the PTRSUB is inserted
        /// \param slot is the slot corresponding to the indicated Varnode
        /// \param ct is the data-type produced by the PTRSUB
        /// \param data is containing Function
        /// \return the new PTRSUB op
        private static PcodeOp insertPtrsubZero(PcodeOp op, int slot, Datatype ct, Funcdata data)
        {
            Varnode* vn = op.getIn(slot);
            PcodeOp* newop = data.newOp(2, op.getAddr());
            Varnode* vnout = data.newUniqueOut(vn.getSize(), newop);
            vnout.updateType(ct, false, false);
            vnout.setImplied();
            data.opSetOpcode(newop, CPUI_PTRSUB);
            data.opSetInput(newop, vn, 0);
            data.opSetInput(newop, data.newConstant(4, 0), 1);
            data.opSetInput(op, vnout, slot);
            data.opInsertBefore(newop, op);
            return newop;
        }

        public ActionSetCasts(string g)
            : base(rule_onceperfunc,"setcasts", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionSetCasts(getGroup());
        }

        public override int apply(Funcdata data)
        {
            list<PcodeOp*>::const_iterator iter;
            PcodeOp* op;

            data.startCastPhase();
            CastStrategy* castStrategy = data.getArch().print.getCastStrategy();
            // We follow data flow, doing basic blocks in dominance order
            // Doing operations in basic block order
            BlockGraph basicblocks = data.getBasicBlocks();
            for (int4 j = 0; j < basicblocks.getSize(); ++j)
            {
                BlockBasic* bb = (BlockBasic*)basicblocks.getBlock(j);
                for (iter = bb.beginOp(); iter != bb.endOp(); ++iter)
                {
                    op = *iter;
                    if (op.notPrinted()) continue;
                    OpCode opc = op.code();
                    if (opc == CPUI_CAST) continue;
                    if (opc == CPUI_PTRADD)
                    {   // Check for PTRADD that no longer fits its pointer
                        int4 sz = (int4)op.getIn(2).getOffset();
                        TypePointer* ct = (TypePointer*)op.getIn(0).getHighTypeReadFacing(op);
                        if ((ct.getMetatype() != TYPE_PTR) || (ct.getPtrTo().getSize() != AddrSpace::addressToByteInt(sz, ct.getWordSize())))
                            data.opUndoPtradd(op, true);
                    }
                    else if (opc == CPUI_PTRSUB)
                    {   // Check for PTRSUB that no longer fits pointer
                        if (!op.getIn(0).getHighTypeReadFacing(op).isPtrsubMatching(op.getIn(1).getOffset()))
                        {
                            if (op.getIn(1).getOffset() == 0)
                            {
                                data.opRemoveInput(op, 1);
                                data.opSetOpcode(op, CPUI_COPY);
                            }
                            else
                                data.opSetOpcode(op, CPUI_INT_ADD);
                        }
                    }
                    // Do input casts first, as output may depend on input
                    for (int4 i = 0; i < op.numInput(); ++i)
                    {
                        count += resolveUnion(op, i, data);
                        count += castInput(op, i, data, castStrategy);
                    }
                    if (opc == CPUI_LOAD)
                    {
                        checkPointerIssues(op, op.getOut(), data);
                    }
                    else if (opc == CPUI_STORE)
                    {
                        checkPointerIssues(op, op.getIn(2), data);
                    }
                    Varnode* vn = op.getOut();
                    if (vn == (Varnode*)0) continue;
                    count += castOutput(op, data, castStrategy);
                }
            }
            return 0;           // Indicate full completion
        }
    }
}
