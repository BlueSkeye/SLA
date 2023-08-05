using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.FuncCallSpecs;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RulePieceStructure : Rule
    {
        /// \brief Markup for Varnodes pieced together into structure/array
        /// \brief Find the base structure or array data-type that the given Varnode is part of
        ///
        /// If the Varnode's data-type is already a structure or array, return that data-type.
        /// If the Varnode is part of a known symbol, use that data-type.
        /// The starting byte offset of the given Varnode within the structure or array is passed back.
        /// \param vn is the given Varnode
        /// \param baseOffset is used to pass back the starting offset
        /// \return the structure or array data-type, or null otherwise
        private static Datatype determineDatatype(Varnode vn, int baseOffset)
        {
            Datatype* ct = vn.getStructuredType();
            if (ct == (Datatype)null)
                return ct;

            if (ct.getSize() != vn.getSize())
            {           // vn is a partial
                SymbolEntry* entry = vn.getSymbolEntry();
                baseOffset = vn.getAddr().overlap(0, entry.getAddr(), ct.getSize());
                if (baseOffset < 0)
                    return (Datatype)null;
                baseOffset += entry.getOffset();
                // Find concrete sub-type that matches the size of the Varnode
                Datatype* subType = ct;
                ulong subOffset = baseOffset;
                while (subType != (Datatype)null && subType.getSize() > vn.getSize())
                {
                    subType = subType.getSubType(subOffset, &subOffset);
                }
                if (subType != (Datatype)null && subType.getSize() == vn.getSize() && subOffset == 0)
                {
                    // If there is a concrete sub-type
                    if (!subType.isPieceStructured())  // and the concrete sub-type is not a structured type itself
                        return (Datatype)null;    // don't split out CONCAT forming the sub-type
                }
            }
            else
            {
                baseOffset = 0;
            }
            return ct;
        }

        /// \brief For a structured data-type, determine if the given range spans multiple elements
        ///
        /// Return true unless the range falls within a single non-structured element.
        /// \param ct is the structured data-type
        /// \param offset is the start of the given range
        /// \param size is the number of bytes in the range
        /// \return \b true if the range spans multiple elements
        private static bool spanningRange(Datatype ct, int off, int size)
        {
            if (offset + size > ct.getSize()) return false;
            ulong newOff = offset;
            for (; ; )
            {
                ct = ct.getSubType(newOff, &newOff);
                if (ct == (Datatype)null) return true;    // Don't know what it spans, assume multiple
                if ((int)newOff + size > ct.getSize()) return true;   // Spans more than 1
                if (!ct.isPieceStructured()) break;
            }
            return false;
        }

        /// \brief Convert an INT_ZEXT operation to a PIECE with a zero constant as the first parameter
        ///
        /// The caller provides a parent data-type and an offset into it corresponding to the \e output of the INT_ZEXT.
        /// The op is converted to a PIECE with a 0 Varnode, which will be assigned a data-type based on
        /// the parent data-type and a computed offset.
        /// \param zext is the INT_ZEXT operation
        /// \param ct is the parent data-type
        /// \param offset is the byte offset of the \e output within the parent data-type
        /// \param data is the function containing the operation
        /// \return true if the INT_ZEXT was successfully converted
        private static bool convertZextToPiece(PcodeOp zext, Datatype structuredType, int offset,
            Funcdata data)
        {
            Varnode* outvn = zext.getOut();
            Varnode* invn = zext.getIn(0);
            if (invn.isConstant()) return false;
            int sz = outvn.getSize() - invn.getSize();
            if (sz > sizeof(ulong)) return false;
            offset += outvn.getSpace().isBigEndian() ? 0 : invn.getSize();
            ulong newOff = offset;
            while (ct != (Datatype)null && ct.getSize() > sz)
            {
                ct = ct.getSubType(newOff, &newOff);
            }
            Varnode* zerovn = data.newConstant(sz, 0);
            if (ct != (Datatype)null && ct.getSize() == sz)
                zerovn.updateType(ct, false, false);
            data.opSetOpcode(zext, OpCode.CPUI_PIECE);
            data.opInsertInput(zext, zerovn, 0);
            if (invn.getType().needsResolution())
                data.inheritResolution(invn.getType(), zext, 1, zext, 0);  // Transfer invn's resolution to slot 1
            return true;
        }

        /// \brief Search for leaves in the CONCAT tree defined by an INT_ZEXT operation and convert them to PIECE
        ///
        /// The CONCAT tree can be extended through an INT_ZEXT, if the extensions output crosses multiple fields of
        /// the parent data-type.  We check this and replace the INT_ZEXT with PIECE if appropriate.
        /// \param stack is the node container for the CONCAT tree
        /// \param structuredType is the parent data-type for the tree
        /// \param data is the function containing the tree
        /// \return \b true if any INT_ZEXT replacement was performed
        private static bool findReplaceZext(List<PieceNode> &stack, Datatype structuredType, Funcdata data)
        {
            bool change = false;
            for (int i = 0; i < stack.size(); ++i)
            {
                PieceNode & node(stack[i]);
                if (!node.isLeaf()) continue;
                Varnode* vn = node.getVarnode();
                if (!vn.isWritten()) continue;
                PcodeOp* op = vn.getDef();
                if (op.code() != OpCode.CPUI_INT_ZEXT) continue;
                if (!spanningRange(structuredType, node.getTypeOffset(), vn.getSize())) continue;
                if (convertZextToPiece(op, structuredType, node.getTypeOffset(), data))
                    change = true;
            }
            return change;
        }

        /// \brief Return \b true if the two given \b root and \b leaf should be part of different symbols
        ///
        /// A leaf in a CONCAT tree can be in a separate from the root if it is a parameter or a separate root.
        /// \param root is the root of the CONCAT tree
        /// \param leaf is the given leaf Varnode
        /// \return \b true if the two Varnodes should be in different symbols
        private static bool separateSymbol(Varnode root, Varnode leaf)
        {
            if (root.getSymbolEntry() != leaf.getSymbolEntry()) return true;  // Forced to be different symbols
            if (root.isAddrTied()) return false;
            if (!leaf.isWritten()) return true;    // Assume to be different symbols
            if (leaf.isProtoPartial()) return true;    // Already in another tree
            PcodeOp* op = leaf.getDef();
            if (op.isMarker()) return true;    // Leaf is not defined locally
            if (op.code() != OpCode.CPUI_PIECE) return false;
            if (leaf.getType().isPieceStructured()) return true;  // Would be a separate root

            return false;
        }

        public RulePieceStructure(string g)
            : base(g, 0, "piecestructure")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RulePieceStructure(getGroup());
        }

        /// \class RulePieceStructure
        /// \brief Concatenating structure pieces gets printed as explicit write statements
        ///
        /// Set properties so that a CONCAT expression like `v = CONCAT(CONCAT(v1,v2),CONCAT(v3,v4))` gets
        /// rendered as a sequence of separate write statements. `v.field1 = v1; v.field2 = v2; v.field3 = v3; v.field4 = v4;`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_PIECE);
            oplist.Add(CPUI_INT_ZEXT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (op.isPartialRoot()) return 0;      // Check if CONCAT tree already been visited
            Varnode* outvn = op.getOut();
            int baseOffset;
            Datatype* ct = determineDatatype(outvn, baseOffset);
            if (ct == (Datatype)null) return 0;

            if (op.code() == OpCode.CPUI_INT_ZEXT)
            {
                if (convertZextToPiece(op, outvn.getType(), 0, data))
                    return 1;
                return 0;
            }
            // Check if outvn is really the root of the tree
            PcodeOp* zext = outvn.loneDescend();
            if (zext != (PcodeOp)null)
            {
                if (zext.code() == OpCode.CPUI_PIECE)
                    return 0;       // More PIECEs below us, not a root
                if (zext.code() == OpCode.CPUI_INT_ZEXT)
                {
                    // Extension of a structured data-type,  convert extension to PIECE first
                    if (convertZextToPiece(zext, zext.getOut().getType(), 0, data))
                        return 1;
                    return 0;
                }
            }

            List<PieceNode> stack;
            for (; ; )
            {
                PieceNode::gatherPieces(stack, outvn, op, baseOffset);
                if (!findReplaceZext(stack, ct, data))  // Check for INT_ZEXT leaves that need to be converted to PIECEs
                    break;
                stack.clear();  // If we found some, regenerate the tree
            }

            op.setPartialRoot();
            bool anyAddrTied = outvn.isAddrTied();
            Address baseAddr = outvn.getAddr() - baseOffset;
            for (int i = 0; i < stack.size(); ++i)
            {
                PieceNode & node(stack[i]);
                Varnode* vn = node.getVarnode();
                Address addr = baseAddr + node.getTypeOffset();
                addr.renormalize(vn.getSize());        // Allow for possible join address
                if (vn.getAddr() == addr)
                {
                    if (!node.isLeaf() || !separateSymbol(outvn, vn))
                    {
                        // Varnode already has correct address and will be part of the same symbol as root
                        // so we don't need to change the storage or insert a COPY
                        if (!vn.isAddrTied() && !vn.isProtoPartial())
                        {
                            vn.setProtoPartial();
                        }
                        anyAddrTied = anyAddrTied || vn.isAddrTied();
                        continue;
                    }
                }
                if (node.isLeaf())
                {
                    PcodeOp* copyOp = data.newOp(1, node.getOp().getAddr());
                    Varnode* newVn = data.newVarnodeOut(vn.getSize(), addr, copyOp);
                    anyAddrTied = anyAddrTied || newVn.isAddrTied();   // Its possible newVn is addrtied, even if vn isn't
                    Datatype* newType = data.getArch().types.getExactPiece(ct, node.getTypeOffset(), vn.getSize());
                    if (newType == (Datatype)null)
                        newType = vn.getType();
                    newVn.updateType(newType, false, false);
                    data.opSetOpcode(copyOp, OpCode.CPUI_COPY);
                    data.opSetInput(copyOp, vn, 0);
                    data.opSetInput(node.getOp(), newVn, node.getSlot());
                    data.opInsertBefore(copyOp, node.getOp());
                    if (vn.getType().needsResolution())
                    {
                        // Inherit PIECE's read resolution for COPY's read
                        data.inheritResolution(vn.getType(), copyOp, 0, node.getOp(), node.getSlot());
                    }
                    if (newType.needsResolution())
                    {
                        newType.resolveInFlow(copyOp, -1); // If the piece represents part of a union, resolve it
                    }
                    if (!newVn.isAddrTied())
                        newVn.setProtoPartial();
                }
                else
                {
                    // Reaching here we know vn is NOT addrtied and has a lone descendant
                    // We completely replace the Varnode with one having the correct storage
                    PcodeOp* defOp = vn.getDef();
                    PcodeOp* loneOp = vn.loneDescend();
                    int slot = loneOp.getSlot(vn);
                    Varnode* newVn = data.newVarnode(vn.getSize(), addr, vn.getType());
                    data.opSetOutput(defOp, newVn);
                    data.opSetInput(loneOp, newVn, slot);
                    data.deleteVarnode(vn);
                    if (!newVn.isAddrTied())
                        newVn.setProtoPartial();
                }
            }
            if (!anyAddrTied)
                data.getMerge().registerProtoPartialRoot(outvn);
            return 1;
        }
    }
}
