﻿using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A node in a tree structure of OpCode.CPUI_PIECE operations
    ///
    /// If a group of Varnodes are concatenated into a larger structure, this object is used to explicitly gather
    /// the PcodeOps (and Varnodes) in the data-flow and view them as a unit. In a properly formed tree, for each
    /// OpCode.CPUI_PIECE operation, the addresses of the input Varnodes and the output Varnode align according to the
    /// concatenation. Internal Varnodes can have only one descendant, but the leaf and the root Varnodes
    /// can each have multiple descendants
    internal class PieceNode
    {
        /// OpCode.CPUI_PIECE operation combining this particular Varnode piece
        private PcodeOp pieceOp;
        /// The particular slot of this Varnode within OpCode.CPUI_PIECE
        private int slot;
        /// Byte offset into structure/array
        private int typeOffset;
        /// \b true if this is a leaf of the tree structure
        private bool leaf;
        
        public PieceNode(PcodeOp op, int sl, int off, bool l)
        {
            pieceOp = op;
            slot = sl;
            typeOffset = off;
            leaf = l;
        }

        /// Return \b true if \b this node is a leaf of the tree structure
        public bool isLeaf() => leaf;

        /// Get the byte offset of \b this node into the data-type
        public int getTypeOffset() => typeOffset;

        /// Get the input slot associated with \b this node
        public int getSlot() => slot;

        /// Get the PcodeOp reading \b this piece
        public PcodeOp getOp() => pieceOp;

        /// Get the Varnode representing \b this piece
        public Varnode getVarnode() => pieceOp.getIn(slot);

        /// \brief Determine if a Varnode is a leaf within the CONCAT tree rooted at the given Varnode
        ///
        /// The CONCAT tree is the maximal set of Varnodes that are all inputs to OpCode.CPUI_PIECE operations,
        /// with no other uses, and that all ultimately flow to the root Varnode.  This method tests
        /// whether a Varnode is a leaf of this tree.
        /// \param rootVn is the given root of the CONCAT tree
        /// \param vn is the Varnode to test as a leaf
        /// \param typeOffset is byte offset of the test Varnode within fully concatenated value
        /// \return \b true is the test Varnode is a leaf of the tree
        public static bool isLeaf(Varnode rootVn, Varnode vn, int typeOffset)
        {
            if (vn.isMapped() && rootVn.getSymbolEntry() != vn.getSymbolEntry())
            {
                return true;
            }
            if (!vn.isWritten()) return true;
            PcodeOp def = vn.getDef();
            if (def.code() != OpCode.CPUI_PIECE) return true;
            PcodeOp op = vn.loneDescend();
            if (op == (PcodeOp)null) return true;
            if (vn.isAddrTied())
            {
                Address addr = rootVn.getAddr() + typeOffset;
                if (vn.getAddr() != addr) return true;
            }
            return false;
        }

        /// Find the root of the CONCAT tree of Varnodes marked either isProtoPartial() or isAddrTied().
        /// This will be the maximal Varnode that containing the given Varnode (as storage), with a
        /// backward path to it through PIECE operations. All Varnodes along the path, except the root, will be
        /// marked as isProtoPartial() or isAddrTied().
        /// \return the root of the CONCAT tree
        public static Varnode findRoot(Varnode vn)
        {
            while (vn.isProtoPartial() || vn.isAddrTied()) {
                IEnumerator<PcodeOp> iter = vn.beginDescend();
                PcodeOp? pieceOp = (PcodeOp)null;
                while (iter.MoveNext()) {
                    PcodeOp op = iter.Current;
                    if (op.code() != OpCode.CPUI_PIECE) continue;
                    int slot = op.getSlot(vn);
                    Address addr = op.getOut().getAddr();
                    if (addr.getSpace().isBigEndian() == (slot == 1))
                        addr = addr + op.getIn(1 - slot).getSize();
                    addr.renormalize(vn.getSize());        // Allow for possible join address
                    if (addr == vn.getAddr()) {
                        if (pieceOp != (PcodeOp)null) {
                            // If there is more than one valid PIECE
                            if (0 != op.compareOrder(pieceOp))  // Attach this to earliest one
                                pieceOp = op;
                        }
                        else
                            pieceOp = op;
                    }
                }
                if (pieceOp == (PcodeOp)null)
                    break;
                vn = pieceOp.getOut();
            }
            return vn;
        }

        /// \brief Build the CONCAT tree rooted at the given Varnode
        ///
        /// Recursively walk backwards from the root through OpCode.CPUI_PIECE operations, stopping if a Varnode
        /// is deemed a leaf.  Collect all Varnodes involved in the tree in a list.  For each Varnode in the tree,
        /// record whether it is leaf and also calculate its offset within the data-type attached to the root.
        /// \param stack holds the markup for each node of the tree
        /// \param rootVn is the given root of the tree
        /// \param op is the current PIECE op to explore as part of the tree
        /// \param baseOffset is the offset associated with the output of the current PIECE op
        public static void gatherPieces(List<PieceNode> stack, Varnode rootVn, PcodeOp op, int baseOffset)
        {
            for (int i = 0; i < 2; ++i) {
                Varnode vn = op.getIn(i);
                int offset = (rootVn.getSpace().isBigEndian() == (i == 1)) ? baseOffset + op.getIn(1 - i).getSize() : baseOffset;
                bool res = isLeaf(rootVn, vn, offset);
                stack.Add(new PieceNode(op, i, offset, res));
                if (!res)
                    gatherPieces(stack, rootVn, vn.getDef(), offset);
            }
        }
    }
}
