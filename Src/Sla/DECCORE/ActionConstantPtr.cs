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
    /// \brief Check for constants, with pointer type, that correspond to global symbols
    internal class ActionConstantPtr
    {
        private int localcount;        ///< Number of passes made for this function

        /// \brief Search for address space annotations in the path of a pointer constant.
        ///
        /// From a constant, search forward in its data-flow either for a LOAD or STORE operation where we can
        /// see the address space being accessed, or search for a pointer data-type with an address space attribute.
        /// We make a limited traversal through the op reading the constant, through INT_ADD, INDIRECT, COPY,
        /// and MULTIEQUAL until we hit a LOAD or STORE.
        /// \param vn is the constant we are searching from
        /// \param op is the PcodeOp reading the constant
        /// \return the discovered AddrSpace or null
        private static AddrSpace searchForSpaceAttribute(Varnode vn, PcodeOp op)
        {
            for (int i = 0; i < 3; ++i)
            {
                Datatype* dt = vn.getType();
                if (dt.getMetatype() == TYPE_PTR)
                {
                    AddrSpace* spc = ((TypePointer*)dt).getSpace();
                    if (spc != (AddrSpace)null && spc.getAddrSize() == vn.getSize())    // If provided a pointer with space attribute
                        return spc;     // use that
                }
                switch (op.code())
                {
                    case CPUI_INT_ADD:
                    case CPUI_COPY:
                    case CPUI_INDIRECT:
                    case CPUI_MULTIEQUAL:
                        vn = op.getOut();
                        op = vn.loneDescend();
                        break;
                    case CPUI_LOAD:
                        return op.getIn(0).getSpaceFromConst();
                    case CPUI_STORE:
                        if (op.getIn(1) == vn)
                            return op.getIn(0).getSpaceFromConst();
                        return (AddrSpace)null;
                    default:
                        return (AddrSpace)null;
                }
                if (op == (PcodeOp)null) break;
            }
            for (list<PcodeOp*>::const_iterator iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
            {
                op = *iter;
                OpCode opc = op.code();
                if (opc == CPUI_LOAD)
                    return op.getIn(0).getSpaceFromConst();
                else if (opc == CPUI_STORE && op.getIn(1) == vn)
                    return op.getIn(0).getSpaceFromConst();
            }
            return (AddrSpace)null;
        }

        /// \brief Select the AddrSpace in which we infer with the given constant is a pointer
        ///
        /// The constant must match the AddrSpace address size. If there is more than one possible match,
        /// search for more information in the syntax tree.
        /// \param vn is the given constant Varnode
        /// \param op is the PcodeOp which uses the constant
        /// \param spaceList is the list of address spaces to select from
        /// \return the selected address space or null
        private static AddrSpace selectInferSpace(Varnode* vn, PcodeOp* op,
            List<AddrSpace*> &spaceList)
        {
            AddrSpace* resSpace = (AddrSpace)null;
            if (vn.getType().getMetatype() == TYPE_PTR)
            {
                AddrSpace* spc = ((TypePointer*)vn.getType()).getSpace();
                if (spc != (AddrSpace)null && spc.getAddrSize() == vn.getSize())
                    return spc;
            }
            for (int i = 0; i < spaceList.size(); ++i)
            {
                AddrSpace* spc = spaceList[i];
                int minSize = spc.getMinimumPtrSize();
                if (minSize == 0)
                {
                    if (vn.getSize() != spc.getAddrSize())
                        continue;
                }
                else if (vn.getSize() < minSize)
                    continue;
                if (resSpace != (AddrSpace)null)
                {
                    AddrSpace* searchSpc = searchForSpaceAttribute(vn, op);
                    if (searchSpc != (AddrSpace)null)
                        resSpace = searchSpc;
                    break;
                }
                resSpace = spc;
            }
            return resSpace;
        }

        /// \brief Determine if given Varnode might be a pointer constant.
        ///
        /// If it is a pointer, return the symbol it points to, or NULL otherwise. If it is determined
        /// that the Varnode is a pointer to a specific symbol, the encoding of the full pointer is passed back.
        /// Usually this is just the constant value of the Varnode, but in this case of partial pointers
        /// (like \e near pointers) the full pointer may contain additional information.
        /// \param spc is the address space being pointed to
        /// \param vn is the given Varnode
        /// \param op is the lone descendant of the Varnode
        /// \param slot is the slot index of the Varnode
        /// \param rampoint will hold the Address of the resolved symbol
        /// \param fullEncoding will hold the full pointer encoding being passed back
        /// \param data is the function being analyzed
        /// \return the recovered symbol or NULL
        private static SymbolEntry isPointer(AddrSpace spc, Varnode vn, PcodeOp* op, int slot,
                Address &rampoint, ulong &fullEncoding, Funcdata &data)
        {
            bool needexacthit;
            Architecture* glb = data.getArch();
            Varnode* outvn;
            if (vn.getTypeReadFacing(op).getMetatype() == TYPE_PTR)
            { // Are we explicitly marked as a pointer
                rampoint = glb.resolveConstant(spc, vn.getOffset(), vn.getSize(), op.getAddr(), fullEncoding);
                needexacthit = false;
            }
            else
            {
                if (vn.isTypeLock()) return (SymbolEntry)null; // Locked as NOT a pointer
                needexacthit = true;
                // Check if the constant is involved in a potential pointer expression
                // as the base
                switch (op.code())
                {
                    case CPUI_RETURN:
                    case CPUI_CALL:
                    case CPUI_CALLIND:
                        // A constant parameter or return value could be a pointer
                        if (!glb.infer_pointers)
                            return (SymbolEntry)null;
                        if (slot == 0)
                            return (SymbolEntry)null;
                        break;
                    case CPUI_PIECE:
                    // Pointers get concatenated in structures
                    case CPUI_COPY:
                    case CPUI_INT_EQUAL:
                    case CPUI_INT_NOTEQUAL:
                    case CPUI_INT_LESS:
                    case CPUI_INT_LESSEQUAL:
                        // A comparison with a constant could be a pointer
                        break;
                    case CPUI_INT_ADD:
                        outvn = op.getOut();
                        if (outvn.getTypeDefFacing().getMetatype() == TYPE_PTR)
                        {
                            // Is there another pointer base in this expression
                            if (op.getIn(1 - slot).getTypeReadFacing(op).getMetatype() == TYPE_PTR)
                                return (SymbolEntry)null; // If so, we are not a pointer
                                                        // FIXME: need to fully explore additive tree
                            needexacthit = false;
                        }
                        else if (!glb.infer_pointers)
                            return (SymbolEntry)null;
                        break;
                    case CPUI_STORE:
                        if (slot != 2)
                            return (SymbolEntry)null;
                        break;
                    default:
                        return (SymbolEntry)null;
                }
                // Make sure the constant is in the expected range for a pointer
                if (spc.getPointerLowerBound() > vn.getOffset())
                    return (SymbolEntry)null;
                if (spc.getPointerUpperBound() < vn.getOffset())
                    return (SymbolEntry)null;
                // Check if the constant looks like a single bit or mask
                if (bit_transitions(vn.getOffset(), vn.getSize()) < 3)
                    return (SymbolEntry)null;
                rampoint = glb.resolveConstant(spc, vn.getOffset(), vn.getSize(), op.getAddr(), fullEncoding);
            }

            if (rampoint.isInvalid()) return (SymbolEntry)null;
            // Since we are looking for a global address
            // Assume it is address tied and use empty usepoint
            SymbolEntry* entry = data.getScopeLocal().getParent().queryContainer(rampoint, 1, Address());
            if (entry != (SymbolEntry)null)
            {
                Datatype* ptrType = entry.getSymbol().getType();
                if (ptrType.getMetatype() == TYPE_ARRAY)
                {
                    Datatype* ct = ((TypeArray*)ptrType).getBase();
                    // In the special case of strings (character arrays) we allow the constant pointer to
                    // refer to the middle of the string
                    if (ct.isCharPrint())
                        needexacthit = false;
                }
                if (needexacthit && entry.getAddr() != rampoint)
                    return (SymbolEntry)null;
            }
            return entry;
        }

        /// Constructor
        public ActionConstantPtr(string g)
            : base(0,"constantptr", g)
        {
        }
        
        public override void reset(Funcdata data)
        {
            localcount = 0;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionConstantPtr(getGroup());
        }

        public override int apply(Funcdata data)
        {
            if (!data.hasTypeRecoveryStarted()) return 0;

            if (localcount >= 4)        // At most 4 passes (once type recovery starts)
                return 0;
            localcount += 1;

            VarnodeLocSet::const_iterator begiter, enditer;
            Architecture* glb = data.getArch();
            AddrSpace* cspc = glb.getConstantSpace();
            SymbolEntry* entry;
            Varnode* vn;

            begiter = data.beginLoc(cspc);
            enditer = data.endLoc(cspc);

            while (begiter != enditer)
            {
                vn = *begiter++;
                if (!vn.isConstant()) break; // New varnodes may get inserted between begiter and enditer
                if (vn.getOffset() == 0) continue; // Never make constant 0 into spacebase
                if (vn.isPtrCheck()) continue; // Have we checked this variable before
                if (vn.hasNoDescend()) continue;
                if (vn.isSpacebase()) continue; // Don't use constant 0 which is already spacebase
                                                 //    if (vn.getSize() != rspc.getAddrSize()) continue; // Must be size of pointer

                PcodeOp* op = vn.loneDescend();
                if (op == (PcodeOp)null) continue;
                AddrSpace* rspc = selectInferSpace(vn, op, glb.inferPtrSpaces);
                if (rspc == (AddrSpace)null) continue;
                int slot = op.getSlot(vn);
                OpCode opc = op.code();
                if (opc == CPUI_INT_ADD)
                {
                    if (op.getIn(1 - slot).isSpacebase()) continue; // Make sure other side is not a spacebase already
                }
                else if ((opc == CPUI_PTRSUB) || (opc == CPUI_PTRADD))
                    continue;
                Address rampoint;
                ulong fullEncoding;
                entry = isPointer(rspc, vn, op, slot, rampoint, fullEncoding, data);
                vn.setPtrCheck();      // Set check flag AFTER searching for symbol
                if (entry != (SymbolEntry)null)
                {
                    data.spacebaseConstant(op, slot, entry, rampoint, fullEncoding, vn.getSize());
                    if ((opc == CPUI_INT_ADD) && (slot == 1))
                        data.opSwapInput(op, 0, 1);
                    count += 1;
                }
            }
            return 0;
        }
    }
}
