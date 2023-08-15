using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about how a HighVariable fits into a larger group or Symbol
    ///
    /// This is an extension to a HighVariable object that is assigned if the HighVariable is part of a
    /// group of mutually overlapping HighVariables. It describes the overlaps and how they affect the HighVariable Cover.
    internal class VariablePiece
    {
        // friend class VariableGroup;
        /// Group to which \b this piece belongs
        internal VariableGroup group;
        /// HighVariable owning \b this piece
        private HighVariable high;
        /// Byte offset of \b this piece within the group
        private int groupOffset;
        /// Number of bytes in \b this piece
        private int size;

        /// List of VariablePieces \b this piece intersects with
        private /*mutable*/ List<VariablePiece> intersection;
        /// Extended cover for the piece, taking into account intersections
        private /*mutable*/ Cover cover;

        /// Construct piece given a HighVariable and its position within the whole.
        /// If \b this is the first piece in the group, allocate a new VariableGroup object.
        /// \param h is the given HighVariable to treat as a piece
        /// \param offset is the byte offset of the piece within the whole
        /// \param grp is another HighVariable in the whole, or null if \b this is the first piece
        public VariablePiece(HighVariable h, int offset, HighVariable grp = (HighVariable)null)
        {
            high = h;
            groupOffset = offset;
            size = h.getInstance(0).getSize();
            if (grp != (HighVariable)null)
                group = grp.piece.getGroup();
            else
                group = new VariableGroup();
            group.addPiece(this);
        }

        ~VariablePiece()
        {
            group.removePiece(this);
            if (group.empty()) {
                // delete group;
            }
            else
                markIntersectionDirty();
        }

        /// Get the HighVariable associate with \b this piece
        public HighVariable getHigh() => high;

        /// Get the central group
        public VariableGroup getGroup() => group;

        /// Get the offset of \b this within its group
        public int getOffset() => groupOffset;

        /// Return the number of bytes in \b this piece.
        public int getSize() => size;

        /// Get the cover associated with \b this piece.
        public Cover getCover() => cover;

        /// Get number of pieces \b this intersects with
        public int numIntersection() => intersection.size();

        /// Get i-th piece \b this intersects with
        public VariablePiece getIntersection(int i) => intersection[i];

        /// Mark all pieces as needing intersection recalculation
        public void markIntersectionDirty()
        {
            foreach (VariablePiece piece in group.pieceSet)
                piece.high.highflags |=
                    (HighVariable.DirtinessFlags.intersectdirty | HighVariable.DirtinessFlags.extendcoverdirty);
        }

        /// Mark all intersecting pieces as having a dirty extended cover
        public void markExtendCoverDirty()
        {
            if ((high.highflags & HighVariable.DirtinessFlags.intersectdirty) != 0)
                return; // intersection list itself is dirty, extended covers will be recomputed anyway
            for (int i = 0; i < intersection.size(); ++i) {
                intersection[i].high.highflags |= HighVariable.DirtinessFlags.extendcoverdirty;
            }
            high.highflags |= HighVariable.DirtinessFlags.extendcoverdirty;
        }

        /// Calculate intersections with other pieces in the group
        /// Compute list of exactly the HighVariable pieces that intersect with \b this.
        public void updateIntersections()
        {
            if ((high.highflags & HighVariable.DirtinessFlags.intersectdirty) == 0) return;

            int endOffset = groupOffset + size;
            intersection.Clear();
            foreach (VariablePiece otherPiece in group.pieceSet) {
                if (otherPiece == this) continue;
                if (endOffset <= otherPiece.groupOffset) continue;
                int otherEndOffset = otherPiece.groupOffset + otherPiece.size;
                if (groupOffset >= otherEndOffset) continue;
                intersection.Add(otherPiece);
            }
            high.highflags &= ~HighVariable.DirtinessFlags.intersectdirty;
        }

        /// Calculate extended cover based on intersections
        /// Union internal covers of all pieces intersecting with \b this.
        public void updateCover()
        {
            if ((high.highflags & (HighVariable.DirtinessFlags.coverdirty | HighVariable.DirtinessFlags.extendcoverdirty)) == 0) return;
            high.updateInternalCover();
            cover = high.internalCover;
            for (int i = 0; i < intersection.size(); ++i) {
                HighVariable high = intersection[i].high;
                high.updateInternalCover();
                cover.merge(high.internalCover);
            }
            high.highflags &= ~HighVariable.DirtinessFlags.extendcoverdirty;
        }

        /// Transfer \b this piece to another VariableGroup
        /// If there are no remaining references to the old VariableGroup it is deleted.
        /// \param newGroup is the new VariableGroup to transfer \b this to
        public void transferGroup(VariableGroup newGroup)
        {
            group.removePiece(this);
            //if (group.empty())
            //    delete group;
            newGroup.addPiece(this);
        }

        /// Move ownership of \b this to another HighVariable
        public void setHigh(HighVariable newHigh)
        {
            high = newHigh;
        }

        /// Combine two VariableGroups
        /// Combine the VariableGroup associated \b this and the given other VariablePiece into one group.
        /// Offsets are adjusted so that \b this and the other VariablePiece have the same offset.
        /// Combining in this way requires pieces of the same size and offset to be merged. This
        /// method does not do the merging but passes back a list of HighVariable pairs that need to be merged.
        /// The first element in the pair will have its VariablePiece in the new group, and the second element
        /// will have its VariablePiece freed in preparation for the merge.
        /// \param op2 is the given other VariablePiece
        /// \param mergePairs passes back the collection of HighVariable pairs that must be merged
        public void mergeGroups(VariablePiece op2, List<HighVariable> mergePairs)
        {
            int diff = groupOffset - op2.groupOffset; // Add to op2, or subtract from this
            if (diff > 0)
                op2.group.adjustOffsets(diff);
            else if (diff < 0)
                group.adjustOffsets(-diff);
            foreach (VariablePiece piece in op2.group.pieceSet) {
                VariablePiece? variablePiece;
                if (group.pieceSet.TryGetValue(piece, out variablePiece)) {
                    mergePairs.Add(variablePiece.high);
                    mergePairs.Add(piece.high);
                    piece.high.piece = (VariablePiece)null; // Detach HighVariable from its original VariablePiece
                    // delete piece;
                }
                else
                    piece.transferGroup(group);
            }
        }
    }
}
