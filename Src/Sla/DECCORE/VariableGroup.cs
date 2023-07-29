﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A collection of HighVariable objects that overlap
    ///
    /// A HighVariable represents a variable or partial variable that is manipulated as a unit by the (de)compiler.
    /// A formal Symbol may be manipulated using multiple HighVariables that in principal can overlap. For a set of
    /// HighVariable objects that mutually overlap, a VariableGroup is a central access point for information about
    /// the intersections.  The information is used in particular to extend HighVariable Cover objects to take into
    /// account the intersections.
    internal class VariableGroup
    {
        // friend class VariablePiece;

        /// \brief Compare two VariablePiece pointers by offset then by size
        internal struct PieceCompareByOffset
        {
            /// Comparison operator
            /// Compare by offset within the group, then by size.
            /// \param a is the first piece to compare
            /// \param b is the other piece to compare
            /// \return \b true if \b a should be ordered before the \b b
            internal static bool operator/*()*/(VariablePiece a, VariablePiece b)
            {
              if (a.getOffset() != b.getOffset())
                return (a.getOffset() < b.getOffset());
              return (a.getSize() < b.getSize());
            }
        }

        /// The set of VariablePieces making up \b this group
        private set<VariablePiece, PieceCompareByOffset> pieceSet;
        /// Number of contiguous bytes covered by the whole group
        private int4 size;
        /// Byte offset of \b this group within its containing Symbol
        private int4 symbolOffset;
        
        public VariableGroup()
        {
            size = 0;
            symbolOffset = 0;
        }

        /// Return \b true if \b this group has no pieces
        public bool empty() => pieceSet.empty();

        /// Add a new piece to \b this group
        /// The VariablePiece takes partial ownership of \b this, via refCount.
        /// \param piece is the new piece to add
        public void addPiece(VariablePiece piece)
        {
            piece.group = this;
            if (!pieceSet.insert(piece).second)
                throw new LowlevelError("Duplicate VariablePiece");
            int4 pieceMax = piece.getOffset() + piece.getSize();
            if (pieceMax > size)
                size = pieceMax;
        }

        /// Adjust offset for every piece by the given amount
        /// The adjustment amount must be positive, and this effectively increases the size of the group.
        /// \param amt is the given amount to add to offsets
        public void adjustOffsets(int4 amt)
        {
            set<VariablePiece*, VariableGroup::PieceCompareByOffset>::iterator iter;

            for (iter = pieceSet.begin(); iter != pieceSet.end(); ++iter)
            {
                (*iter).groupOffset += amt;
            }
            size += amt;
        }

        /// Remove a piece from \b this group
        public void removePiece(VariablePiece piece)
        {
            pieceSet.erase(piece);
            // We currently don't adjust size here as removePiece is currently only called during clean up
        }

        /// Get the number of bytes \b this group covers
        public int4 getSize() => size;

        /// Cache the symbol offset for the group
        public void setSymbolOffset(int4 val)
        {
            symbolOffset = val;
        }

        /// Get offset of \b this group within its Symbol
        public int4 getSymbolOffset() => symbolOffset;

        /// Combine given VariableGroup into \b this
        /// Every VariablePiece in the given group is moved into \b this and the VariableGroup object is deleted.
        /// There must be no matching VariablePieces with the same size and offset between the two groups
        /// or a LowlevelError exception is thrown.
        /// \param op2 is the given VariableGroup to merge into \b this
        public void combineGroups(VariableGroup op2)
        {
            set<VariablePiece*, VariableGroup::PieceCompareByOffset>::iterator iter = op2.pieceSet.begin();
            set<VariablePiece*, VariableGroup::PieceCompareByOffset>::iterator enditer = op2.pieceSet.end();

            while (iter != enditer)
            {
                VariablePiece* piece = *iter;
                ++iter;
                piece.transferGroup(this);
            }
        }
    }
}
