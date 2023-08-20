using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Structure for sorting out pointer expression trees
    ///
    /// Given a base pointer of known data-type and an additive expression involving
    /// the pointer, group the terms of the expression into:
    ///   - A constant multiple of the base data-type
    ///   - Non-constant multiples of the base data-type
    ///   - An constant offset to a sub-component of the base data-type
    ///   - An remaining terms
    ///
    /// The \e multiple terms are rewritten using a OpCode.CPUI_PTRADD. The constant offset
    /// is rewritten using a OpCode.CPUI_PTRSUB.  Other terms are added back @in.  Analysis may cause
    /// multiplication (CPUI_INT_MULT) by a constant to be distributed to its OpCode.CPUI_INT_ADD input.
    internal class AddTreeState
    {
        /// The function containing the expression
        private Funcdata data;
        /// Base of the ADD tree
        private PcodeOp baseOp;
        /// The pointer varnode
        private Varnode ptr;
        /// The pointer data-type
        private TypePointer ct;
        /// The base data-type being pointed at
        private Datatype baseType;
        /// A copy of \b ct, if it is a relative pointer
        private TypePointerRel pRelType;
        /// Size of the pointer
        private int ptrsize;
        /// Size of data-type being pointed to (in address units) or 0 for open ended pointer
        private int size;
        /// Slot of the ADD tree base that is holding the pointer
        private int baseSlot;
        /// Mask for modulo calculations in ptr space
        private ulong ptrmask;
        /// Number of bytes we dig into the base data-type
        private ulong offset;
        /// Number of bytes being double counted
        private ulong correct;
        /// Varnodes which are multiples of size
        private List<Varnode> multiple;
        /// Associated constant multiple
        private List<long> coeff;
        /// Varnodes which are not multiples
        private List<Varnode> nonmult;
        /// A OpCode.CPUI_INT_MULT op that needs to be distributed
        private PcodeOp distributeOp;
        /// Sum of multiple constants
        private ulong multsum;
        /// Sum of non-multiple constants
        private ulong nonmultsum;
        /// Do not distribute "multiply by constant" operation
        private bool preventDistribution;
        /// Are terms produced by distributing used
        private bool isDistributeUsed;
        /// Is there a sub-type (using OpCode.CPUI_PTRSUB)
        private bool isSubtype;
        /// Set to \b true if the whole expression can be transformed
        private bool valid;
        /// Set to \b true if pointer to unitsize or smaller
        private bool isDegenerate;

        /// Look for evidence of an array in a sub-component
        /// Even if the current base data-type is not an array, the pointer expression may incorporate
        /// an array access for a sub component.  This manifests as a non-constant non-multiple terms in
        /// the tree.  If this term is itself defined by a OpCode.CPUI_INT_MULT with a constant, the constant
        /// indicates a likely element size. Return a non-zero value, the likely element size, if there
        /// is evidence of a non-constant non-multiple term. Return zero otherwise.
        /// \return a non-zero value indicating likely element size, or zero
        private uint findArrayHint()
        {
            uint res = 0;
            for (int i = 0; i < nonmult.size(); ++i)
            {
                Varnode vn = nonmult[i];
                if (vn.isConstant()) continue;
                uint vncoeff = 1;
                if (vn.isWritten())
                {
                    PcodeOp op = vn.getDef();
                    if (op.code() == OpCode.CPUI_INT_MULT)
                    {
                        Varnode vnconst = op.getIn(1);
                        if (vnconst.isConstant())
                        {
                            long sval = vnconst.getOffset();
                            Globals.sign_extend(sval, vnconst.getSize() * 8 - 1);
                            vncoeff = (sval < 0) ? (uint) - sval : (uint)sval;
                        }
                    }
                }
                if (vncoeff > res)
                    res = vncoeff;
            }
            return res;
        }

        /// \brief Given an offset into the base data-type and array hints find sub-component being referenced
        ///
        /// An explicit offset should target a specific sub data-type,
        /// but array indexing may confuse things.  This method passes
        /// back the offset of the best matching component, searching among components
        /// that are \e nearby the given offset, preferring a matching array element size
        /// and a component start that is nearer to the offset.
        /// \param off is the given offset into the data-type
        /// \param arrayHint if non-zero indicates array access, where the value is the element size
        /// \param newoff is used to pass back the actual offset of the selected component
        /// \return \b true if a good component match was found
        private bool hasMatchingSubType(ulong off, uint arrayHint, ulong newoff)
        {
            if (arrayHint == 0)
                return (baseType.getSubType(off, newoff) != (Datatype)null);

            int elSizeBefore;
            ulong offBefore;
            Datatype* typeBefore = baseType.nearestArrayedComponentBackward(off, &offBefore, &elSizeBefore);
            if (typeBefore != (Datatype)null)
            {
                if (arrayHint == 1 || elSizeBefore == arrayHint)
                {
                    int sizeAddr = AddrSpace::byteToAddressInt(typeBefore.getSize(), ct.getWordSize());
                    if (offBefore < sizeAddr)
                    {
                        // If the offset is \e inside a component with a compatible array, return it.
                        *newoff = offBefore;
                        return true;
                    }
                }
            }
            int elSizeAfter;
            ulong offAfter;
            Datatype* typeAfter = baseType.nearestArrayedComponentForward(off, &offAfter, &elSizeAfter);
            if (typeBefore == (Datatype)null && typeAfter == (Datatype)null)
                return (baseType.getSubType(off, newoff) != (Datatype)null);
            if (typeBefore == (Datatype)null)
            {
                *newoff = offAfter;
                return true;
            }
            if (typeAfter == (Datatype)null)
            {
                *newoff = offBefore;
                return true;
            }

            ulong distBefore = offBefore;
            ulong distAfter = -offAfter;
            if (arrayHint != 1)
            {
                if (elSizeBefore != arrayHint)
                    distBefore += 0x1000;
                if (elSizeAfter != arrayHint)
                    distAfter += 0x1000;
            }
            *newoff = (distAfter < distBefore) ? offAfter : offBefore;
            return true;
        }

        /// Accumulate details of INT_MULT term and continue traversal if appropriate
        /// Examine a OpCode.CPUI_INT_MULT element in the middle of the add tree. Determine if we treat
        /// the output simply as a leaf, or if the multiply needs to be distributed to an
        /// additive subtree.  If the Varnode is a leaf of the tree, return \b true if
        /// it is considered a multiple of the base data-type size. If the Varnode is the
        /// root of another additive sub-tree, return \b true if no sub-node is a multiple.
        /// \param vn is the output Varnode of the operation
        /// \param op is the OpCode.CPUI_INT_MULT operation
        /// \param treeCoeff is constant multiple being applied to the node
        /// \return \b true if there are no multiples of the base data-type size discovered
        private bool checkMultTerm(Varnode vn, PcodeOp op, ulong treeCoeff)
        {
            Varnode vnconst = op.getIn(1);
            Varnode vnterm = op.getIn(0);
            ulong val;

            if (vnterm.isFree())
            {
                valid = false;
                return false;
            }
            if (vnconst.isConstant())
            {
                val = (vnconst.getOffset() * treeCoeff) & ptrmask;
                long sval = (long)val;
                Globals.sign_extend(sval, vn.getSize() * 8 - 1);
                long rem = (size == 0) ? sval : sval % size;
                if (rem != 0)
                {
                    if ((val > size) && (size != 0))
                    {
                        valid = false; // Size is too big: pointer type must be wrong
                        return false;
                    }
                    if (!preventDistribution)
                    {
                        if (vnterm.isWritten() && vnterm.getDef().code() == OpCode.CPUI_INT_ADD)
                        {
                            if (distributeOp == (PcodeOp)null)
                                distributeOp = op;
                            return spanAddTree(vnterm.getDef(), val);
                        }
                    }
                    return true;
                }
                else
                {
                    if (treeCoeff != 1)
                        isDistributeUsed = true;
                    multiple.Add(vnterm);
                    coeff.Add(sval);
                    return false;
                }
            }
            return true;
        }

        /// Accumulate details of given term and continue tree traversal
        /// If the given Varnode is a constant or multiplicative term, update
        /// totals. If the Varnode is additive, traverse its sub-terms.
        /// \param vn is the given Varnode term
        /// \param treeCoeff is a constant multiple applied to the entire sub-tree
        /// \return \b true if the sub-tree rooted at the given Varnode contains no multiples
        private bool checkTerm(Varnode vn, ulong treeCoeff)
        {
            ulong val;
            PcodeOp def;

            if (vn == ptr) return false;
            if (vn.isConstant())
            {
                val = vn.getOffset() * treeCoeff;
                long sval = (long)val;
                Globals.sign_extend(sval, vn.getSize() * 8 - 1);
                long rem = (size == 0) ? sval : (sval % size);
                if (rem != 0)
                {       // constant is not multiple of size
                    if (treeCoeff != 1)
                    {
                        // An offset "into" the base data-type makes little sense unless is has subcomponents
                        if (baseType.getMetatype() == type_metatype.TYPE_ARRAY || baseType.getMetatype() == type_metatype.TYPE_STRUCT)
                            isDistributeUsed = true;
                    }
                    nonmultsum += val;
                    nonmultsum &= ptrmask;
                    return true;
                }
                if (treeCoeff != 1)
                    isDistributeUsed = true;
                multsum += val;     // Add multiples of size into multsum
                multsum &= ptrmask;
                return false;
            }
            if (vn.isWritten())
            {
                def = vn.getDef();
                if (def.code() == OpCode.CPUI_INT_ADD) // Recurse
                    return spanAddTree(def, treeCoeff);
                if (def.code() == OpCode.CPUI_COPY)
                { // Not finished reducing yet
                    valid = false;
                    return false;
                }
                if (def.code() == OpCode.CPUI_INT_MULT)   // Check for constant coeff indicating size
                    return checkMultTerm(vn, def, treeCoeff);
            }
            else if (vn.isFree())
            {
                valid = false;
                return false;
            }
            return true;
        }

        /// Walk the given sub-tree accumulating details
        /// Recursively walk the sub-tree from the given root.
        /// Terms that are a \e multiple of the base data-type size are accumulated either in
        /// the the sum of constant multiples or the container of non-constant multiples.
        /// Terms that are a \e non-multiple are accumulated either in the sum of constant
        /// non-multiples or the container of non-constant non-multiples. The constant
        /// non-multiples are counted twice, once in the sum, and once in the container.
        /// This routine returns \b true if no node of the sub-tree is considered a multiple
        /// of the base data-type size (or \b false if any node is considered a multiple).
        /// \param op is the root of the sub-expression to traverse
        /// \param treeCoeff is a constant multiple applied to the entire additive tree
        /// \return \b true if the given sub-tree contains no multiple nodes
        private bool spanAddTree(PcodeOp op, ulong treeCoeff)
        {
            bool one_is_non, two_is_non;

            one_is_non = checkTerm(op.getIn(0), treeCoeff);
            if (!valid) return false;
            two_is_non = checkTerm(op.getIn(1), treeCoeff);
            if (!valid) return false;

            if (pRelType != (TypePointerRel*)0) {
                if (multsum != 0 || nonmultsum >= size || !multiple.empty())
                {
                    valid = false;
                    return false;
                }
            }
            if (one_is_non && two_is_non) return true;
            if (one_is_non)
                nonmult.Add(op.getIn(0));
            if (two_is_non)
                nonmult.Add(op.getIn(1));
            return false;       // At least one of the sides contains multiples
        }

        /// Calculate final sub-type offset
        /// Make final calcultions to determine if a pointer to a sub data-type of the base
        /// data-type is being calculated, which will result in a OpCode.CPUI_PTRSUB being generated.
        private void calcSubtype()
        {
            if (size == 0 || nonmultsum < size)
                offset = nonmultsum;
            else
            {
                // For a sum that falls completely outside the data-type, there is presumably some
                // type of constant term added to an array index either at the current level or lower.
                // If we knew here whether an array of the baseType was possible we could make a slightly
                // better decision.
                long snonmult = (long)nonmultsum;
                Globals.sign_extend(snonmult, ptrsize * 8 - 1);
                snonmult = snonmult % size;
                if (snonmult >= 0)
                    // We assume the sum is big enough it represents an array index at this level
                    offset = (ulong)snonmult;
                else
                {
                    // For a negative sum, if the baseType is a structure and there is array hints,
                    // we assume the sum is an array index at a lower level
                    if (baseType.getMetatype() == type_metatype.TYPE_STRUCT && findArrayHint() != 0)
                        offset = nonmultsum;
                    else
                        offset = (ulong)(snonmult + size);
                }
            }
            correct = nonmultsum - offset;
            nonmultsum = offset;
            multsum = (multsum + correct) & ptrmask;    // Some extra multiples of size
            if (nonmult.empty())
            {
                if ((multsum == 0) && multiple.empty())
                {   // Is there anything at all
                    valid = false;
                    return;
                }
                isSubtype = false;      // There are no offsets INTO the pointer
            }
            else if (baseType.getMetatype() == type_metatype.TYPE_SPACEBASE)
            {
                ulong nonmultbytes = AddrSpace.addressToByte(nonmultsum, ct.getWordSize()); // Convert to bytes
                ulong extra;
                uint arrayHint = findArrayHint();
                // Get offset into mapped variable
                if (!hasMatchingSubType(nonmultbytes, arrayHint, &extra))
                {
                    valid = false;      // Cannot find mapped variable but nonmult is non-empty
                    return;
                }
                extra = AddrSpace.byteToAddress(extra, ct.getWordSize()); // Convert back to address units
                offset = (nonmultsum - extra) & ptrmask;
                isSubtype = true;
            }
            else if (baseType.getMetatype() == type_metatype.TYPE_STRUCT)
            {
                ulong nonmultbytes = AddrSpace.addressToByte(nonmultsum, ct.getWordSize()); // Convert to bytes
                ulong extra;
                uint arrayHint = findArrayHint();
                // Get offset into field in structure
                if (!hasMatchingSubType(nonmultbytes, arrayHint, &extra))
                {
                    if (nonmultbytes >= baseType.getSize())
                    {   // Compare as bytes! not address units
                        valid = false; // Out of structure's bounds
                        return;
                    }
                    extra = 0;  // No field, but pretend there is something there
                }
                extra = AddrSpace.byteToAddress(extra, ct.getWordSize()); // Convert back to address units
                offset = (nonmultsum - extra) & ptrmask;
                if (pRelType != (TypePointerRel*)0 && offset == pRelType.getPointerOffset())
                {
                    // offset falls within basic ptrto
                    if (!pRelType.evaluateThruParent(0))
                    {   // If we are not representing offset 0 through parent
                        valid = false;              // Use basic (alternate) form
                        return;
                    }
                }
                isSubtype = true;
            }
            else if (baseType.getMetatype() == type_metatype.TYPE_ARRAY)
            {
                isSubtype = true;
                offset = 0;
            }
            else
            {
                // No struct or array, but nonmult is non-empty
                valid = false;          // There is substructure we don't know about
            }
        }

        /// Build part of tree that is multiple of base size
        /// Construct part of the tree that sums to a multiple of the base data-type size.
        /// This value will be added to the base pointer as a OpCode.CPUI_PTRADD. The final Varnode produced
        /// by the sum is returned.  If there are no multiples, null is returned.
        /// \return the output Varnode of the multiple tree or null
        private Varnode buildMultiples()
        {
            Varnode resNode;

            // Be sure to preserve sign in division below
            // Calc size-relative constant PTR addition
            long smultsum = (long)multsum;
            Globals.sign_extend(smultsum, ptrsize * 8 - 1);
            ulong constCoeff = (size == 0) ? (ulong)0 : (smultsum / size) & ptrmask;
            if (constCoeff == 0)
                resNode = (Varnode)null;
            else
                resNode = data.newConstant(ptrsize, constCoeff);
            for (int i = 0; i < multiple.size(); ++i)
            {
                ulong finalCoeff = (size == 0) ? (ulong)0 : (coeff[i] / size) & ptrmask;
                Varnode vn = multiple[i];
                if (finalCoeff != 1)
                {
                    PcodeOp op = data.newOpBefore(baseOp, OpCode.CPUI_INT_MULT, vn, data.newConstant(ptrsize, finalCoeff));
                    vn = op.getOut();
                }
                if (resNode == (Varnode)null)
                    resNode = vn;
                else
                {
                    PcodeOp op = data.newOpBefore(baseOp, OpCode.CPUI_INT_ADD, vn, resNode);
                    resNode = op.getOut();
                }
            }
            return resNode;
        }

        /// Build part of tree not accounted for by multiples or \e offset
        /// Create a subtree summing all the elements that aren't multiples of the base data-type size.
        /// Correct for any double counting of non-multiple constants.
        /// Return the final Varnode holding the sum or null if there are no terms.
        /// \return the final Varnode or null
        private Varnode buildExtra()
        {
            correct = correct + offset; // Total correction that needs to be made
            Varnode resNode = (Varnode)null;
            for (int i = 0; i < nonmult.size(); ++i)
            {
                Varnode vn = nonmult[i];
                if (vn.isConstant())
                {
                    correct -= vn.getOffset();
                    continue;
                }
                if (resNode == (Varnode)null)
                    resNode = vn;
                else
                {
                    PcodeOp op = data.newOpBefore(baseOp, OpCode.CPUI_INT_ADD, vn, resNode);
                    resNode = op.getOut();
                }
            }
            correct &= ptrmask;
            if (correct != 0)
            {
                Varnode vn = data.newConstant(ptrsize, Globals.uintb_negate(correct - 1, ptrsize));
                if (resNode == (Varnode)null)
                    resNode = vn;
                else
                {
                    PcodeOp op = data.newOpBefore(baseOp, OpCode.CPUI_INT_ADD, vn, resNode);
                    resNode = op.getOut();
                }
            }
            return resNode;
        }

        /// Transform ADD into degenerate PTRADD
        /// The base data-type being pointed to is unit sized (or smaller).  Everything is a multiple, so an ADD
        /// is always converted into a PTRADD.
        /// \return \b true if the degenerate transform was applied
        private bool buildDegenerate()
        {
            if (baseType.getSize() < ct.getWordSize())
                // If the size is really less than scale, there is
                // probably some sort of padding going on
                return false;   // Don't transform at all
            if (baseOp.getOut().getTypeDefFacing().getMetatype() != type_metatype.TYPE_PTR)    // Make sure pointer propagates thru INT_ADD
                return false;
            List<Varnode> newparams;
            int slot = baseOp.getSlot(ptr);
            newparams.Add(ptr);
            newparams.Add(baseOp.getIn(1 - slot));
            newparams.Add(data.newConstant(ct.getSize(), 1));
            data.opSetAllInput(baseOp, newparams);
            data.opSetOpcode(baseOp, OpCode.CPUI_PTRADD);
            return true;
        }

        /// Build the transformed ADD tree
        /// The original ADD tree has been successfully split into \e multiple and
        /// \e non-multiple pieces.  Rewrite the tree as a pointer expression, putting
        /// any \e multiple pieces into a PTRADD operation, creating a PTRSUB if a sub
        /// data-type offset has been calculated, and preserving and remaining terms.
        private void buildTree()
        {
            if (pRelType != (TypePointerRel*)0) {
                int ptrOff = ((TypePointerRel*)ct).getPointerOffset();
                offset -= ptrOff;
                offset &= ptrmask;
            }
            Varnode multNode = buildMultiples();
            Varnode extraNode = buildExtra();
            PcodeOp newop = (PcodeOp)null;

            // Create PTRADD portion of operation
            if (multNode != (Varnode)null)
            {
                newop = data.newOpBefore(baseOp, OpCode.CPUI_PTRADD, ptr, multNode, data.newConstant(ptrsize, size));
                if (ptr.getType().needsResolution())
                    data.inheritResolution(ptr.getType(), newop, 0, baseOp, baseSlot);
                multNode = newop.getOut();
            }
            else
                multNode = ptr;     // Zero multiple terms

            // Create PTRSUB portion of operation
            if (isSubtype)
            {
                newop = data.newOpBefore(baseOp, OpCode.CPUI_PTRSUB, multNode, data.newConstant(ptrsize, offset));
                if (multNode.getType().needsResolution())
                    data.inheritResolution(multNode.getType(), newop, 0, baseOp, baseSlot);
                if (size != 0)
                    newop.setStopTypePropagation();
                multNode = newop.getOut();
            }

            // Add back in any remaining terms
            if (extraNode != (Varnode)null)
                newop = data.newOpBefore(baseOp, OpCode.CPUI_INT_ADD, multNode, extraNode);

            if (newop == (PcodeOp)null)
            {
                // This should never happen
                data.warning("ptrarith problems", baseOp.getAddr());
                return;
            }
            data.opSetOutput(newop, baseOp.getOut());
            data.opDestroy(baseOp);
        }

        /// Reset for a new ADD tree traversal
        private void clear()
        {
            multsum = 0;
            nonmultsum = 0;
            if (pRelType != (TypePointerRel*)0) {
                nonmultsum = ((TypePointerRel*)ct).getPointerOffset();
                nonmultsum &= ptrmask;
            }
            multiple.clear();
            coeff.clear();
            nonmult.clear();
            correct = 0;
            offset = 0;
            valid = true;
            isDistributeUsed = false;
            isSubtype = false;
            distributeOp = (PcodeOp)null;
        }

        /// Construct given root of ADD tree and pointer
        public AddTreeState(Funcdata d, PcodeOp op, int slot)
        {
            data = d;
            baseOp = op;
            baseSlot = slot;
            ptr = op.getIn(slot);
            ct = (TypePointer)ptr.getTypeReadFacing(op);
            ptrsize = ptr.getSize();
            ptrmask = Globals.calc_mask(ptrsize);
            baseType = ct.getPtrTo();
            multsum = 0;        // Sums start out as zero
            nonmultsum = 0;
            pRelType = (TypePointerRel*)0;
            if (ct.isFormalPointerRel())
            {
                pRelType = (TypePointerRel*)ct;
                baseType = pRelType.getParent();
                nonmultsum = pRelType.getPointerOffset();
                nonmultsum &= ptrmask;
            }
            if (baseType.isVariableLength())
                size = 0;       // Open-ended size being pointed to, there will be no "multiples" component
            else
                size = AddrSpace::byteToAddressInt(baseType.getSize(), ct.getWordSize());
            correct = 0;
            offset = 0;
            valid = true;       // Valid until proven otherwise
            preventDistribution = false;
            isDistributeUsed = false;
            isSubtype = false;
            distributeOp = (PcodeOp)null;
            int unitsize = AddrSpace.addressToByteInt(1, ct.getWordSize());
            isDegenerate = (baseType.getSize() <= unitsize && baseType.getSize() > 0);
        }

        /// Attempt to transform the pointer expression
        /// \return \b true if a transform was applied
        public bool apply()
        {
            if (isDegenerate)
                return buildDegenerate();
            spanAddTree(baseOp, 1);
            if (!valid) return false;       // Were there any show stoppers
            if (distributeOp != (PcodeOp)null && !isDistributeUsed)
            {
                clear();
                preventDistribution = true;
                spanAddTree(baseOp, 1);
            }
            calcSubtype();
            if (!valid) return false;
            while (valid && distributeOp != (PcodeOp)null)
            {
                if (!data.distributeIntMultAdd(distributeOp))
                {
                    valid = false;
                    break;
                }
                // Collapse any z = (x * #c) * #d  expressions produced by the distribute
                data.collapseIntMultMult(distributeOp.getIn(0));
                data.collapseIntMultMult(distributeOp.getIn(1));
                clear();
                spanAddTree(baseOp, 1);
                if (distributeOp != (PcodeOp)null && !isDistributeUsed)
                {
                    clear();
                    preventDistribution = true;
                    spanAddTree(baseOp, 1);
                }
                calcSubtype();
            }
            if (!valid)
            {
                // Distribution transforms were made
                ostringstream s;
                s << "Problems distributing in pointer arithmetic at ";
                baseOp.getAddr().printRaw(s);
                data.warningHeader(s.str());
                return true;
            }
            buildTree();
            return true;
        }

        /// Prepare analysis if there is an alternate form of the base pointer
        /// For some forms of pointer (TypePointerRel), the pointer can be interpreted as having two versions
        /// of the data-type being pointed to.  This method initializes analysis for the second version, assuming
        /// analysis of the first version has failed.
        /// \return \b true if there is a second version that can still be analyzed
        public bool initAlternateForm()
        {
            if (pRelType == (TypePointerRel*)0)
                return false;

            pRelType = (TypePointerRel*)0;
            baseType = ct.getPtrTo();
            if (baseType.isVariableLength())
                size = 0;       // Open-ended size being pointed to, there will be no "multiples" component
            else
                size = AddrSpace::byteToAddressInt(baseType.getSize(), ct.getWordSize());
            int unitsize = AddrSpace.addressToByteInt(1, ct.getWordSize());
            isDegenerate = (baseType.getSize() <= unitsize && baseType.getSize() > 0);
            preventDistribution = false;
            clear();
            return true;
        }
    }
}
