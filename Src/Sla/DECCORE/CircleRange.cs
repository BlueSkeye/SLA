﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A class for manipulating integer value ranges.
    ///
    /// The idea is to have a representation of common sets of
    /// values that a varnode might take on in analysis so that
    /// the representation can be manipulated symbolically to
    /// some extent.  The representation is a circular range
    /// (determined by a half-open interval [left,right)), over
    /// the integers mod 2^n,  where mask = 2^n-1.
    /// The range can support a step, if some of the
    /// least significant bits of the mask are set to zero.
    ///
    /// The class then can
    ///   - Generate ranges based on a pcode condition:
    ///      -    x < 2      =>   left=0  right=2  mask=sizeof(x)
    ///      -    5 >= x     =>   left=5  right=0  mask=sizeof(x)
    ///
    ///   - Intersect and union ranges, if the result is another range
    ///   - Pull-back a range through a transformation operation
    ///   - Iterate
    ///
    ///   \code
    ///     val = range.getMin();
    ///     do {
    ///     } while(range.getNext(val));
    ///   \endcode
    internal sealed class CircleRange
    {
        // Left boundary of the open range [left,right)
        private ulong left;
        // Right boundary of the open range [left,right)
        private ulong right;
        // Bit mask defining the size (modulus) and stop of the range
        private ulong mask;
        // \b true if set is empty
        private bool isempty;
        // Explicit step size
        private int step;
        // Map from raw overlaps to normalized overlap code
        private const string arrange = "gcgbegdagggggggeggggcgbggggggggcdfgggggggegdggggbgggfggggcgbegda";

        // ADDED : Handle *this = other.
        private void CopyFrom(CircleRange other)
        {
            this.left = other.left;
            this.right = other.right;
            this.mask = other.mask;
            this.isempty = other.isempty;
            this.step = other.step;
        }

        // Normalize the representation of full sets
        // All the instantiations where left == right represent the same set. We
        // normalize the representation so we can compare sets easily.
        private void normalize()
        {
            if (left == right) {
                left = (step != 1) ? left % (uint)step : 0;
                right = left;
            }
        }

        /// Set \b this to the complement of itself
        /// This method \b only works if \b step is 1
        private void complement()
        {
            if (isempty) {
                left = 0;
                right = 0;
                isempty = false;
                return;
            }
            if (left == right) {
                isempty = true;
                return;
            }
            ulong tmp = left;
            left = right;
            right = tmp;
        }

        // Convert \b this to boolean.
        // If the original range contained
        //   - 0 and 1   => the new range is [0,2)
        //   - 0 only    => the new range is [0,1)
        //   - 1 only    => the new range is [1,2)
        //   - neither 0 or 1  =>  the new range is empty
        // \return \b true if the range contains both 0 and 1
        private bool convertToBoolean()
        {
            if (isempty) return false;
            bool contains_zero = contains(0);
            bool contains_one = contains(1);
            mask = 0xff;
            step = 1;
            if (contains_zero && contains_one) {
                left = 0;
                right = 2;
                isempty = false;
                return true;
            }
            else if (contains_zero) {
                left = 0;
                right = 1;
                isempty = false;
            }
            else if (contains_one) {
                left = 1;
                right = 2;
                isempty = false;
            }
            else {
                isempty = true;
            }
            return false;
        }

        /// \brief  Recalculate range based on new stride
        ///
        /// Restrict a left/right specified range to a new stride, given the step and
        /// remainder it needs to match. This assumes the specified range is not empty.
        /// \param mask is the domain mask
        /// \param step is the new stride
        /// \param oldStep is the original step (always smaller)
        /// \param rem is the given remainder to match
        /// \param myleft is a reference to the left boundary of the specified range
        /// \param myright is a reference to the right boundary of the specified range
        /// \return \b true if result is empty
        private static bool newStride(ulong mask, int step, int oldStep, uint rem, ulong myleft,
            ulong myright)
        {
            if (oldStep != 1) {
                uint oldRem = (uint)(myleft % (uint)oldStep);
                if (oldRem != (rem % oldStep))
                    return true;            // Step is completely off
            }
            bool origOrder = (myleft < myright);
            uint leftRem = (uint)(myleft % (uint)step);
            uint rightRem = (uint)(myright % (uint)step);
            myleft += (ulong)((leftRem > rem) ? rem + step - leftRem : rem - leftRem);
            myright += (ulong)((rightRem > rem) ? rem + step - rightRem : rem - rightRem);
            myleft &= mask;
            myright &= mask;
            bool newOrder = (myleft < myright);
            return (origOrder != newOrder);
        }

        /// \brief Make \b this range fit in a new domain
        ///
        /// Truncate any part of the range outside of the new domain.
        /// If the original range is completely outside of the new domain,
        /// return \b true (empty). Step information is preserved.
        /// \param newMask is the mask for the new domain
        /// \param newStep is the step associated with the range
        /// \param myleft is a reference to the left edge of the range to fit
        /// \param myright is a reference to the right edge of the range to fit
        /// \return \b true if the truncated domain is empty
        private static bool newDomain(ulong newMask, int newStep, ulong myleft, ulong myright)
        {
            ulong rem = (newStep != 1) ? myleft % (uint)newStep : 0;
            if (myleft > newMask) {
                if (myright > newMask) {
                    // Both bounds out of range of newMask
                    if (myleft < myright) return true; // Old range is completely out of bounds of new mask
                    myleft = rem;
                    myright = rem;      // Old range contained everything in newMask
                    return false;
                }
                myleft = rem;       // Take everything up to left edge of new range
            }
            if (myright > newMask) {
                myright = rem;      // Take everything up to right edge of new range
            }
            if (myleft == myright) {
                myleft = rem;       // Normalize the everything
                myright = rem;
            }
            return false;           // not empty
        }

        /// Calculate overlap code
        /// If two ranges are labeled [l , r) and  [op2.l, op2.r), the
        /// overlap of the ranges can be characterized by listing the four boundary
        /// values  in order, as the circle is traversed in a clock-wise direction.  This characterization can be
        /// further normalized by starting the list at op2.l, unless op2.l is contained in the range [l, r).
        /// In which case, the list should start with l.  You get the following 6 categories
        ///    - a  = (l r op2.l op2.r)
        ///    - b  = (l op2.l r op2.r)
        ///    - c  = (l op2.l op2.r r)
        ///    - d  = (op2.l l r op2.r)
        ///    - e  = (op2.l l op2.r r)
        ///    - f  = (op2.l op2.r l r)
        ///    - g  = (l op2.r op2.l r)
        ///
        /// Given 2 ranges, this method calculates the category code for the overlap.
        /// \param op1left is left boundary of the first range
        /// \param op1right is the right boundary of the first range
        /// \param op2left is the left boundary of the second range
        /// \param op2right is the right boundary of the second range
        /// \return the character code of the normalized overlap category
        private static char encodeRangeOverlaps(ulong op1left, ulong op1right, ulong op2left, ulong op2right)
        {
            int val = (op1left <= op1right) ? 0x20 : 0;
            val |= (op1left <= op2left) ? 0x10 : 0;
            val |= (op1left <= op2right) ? 0x8 : 0;
            val |= (op1right <= op2left) ? 4 : 0;
            val |= (op1right <= op2right) ? 2 : 0;
            val |= (op2left <= op2right) ? 1 : 0;
            return arrange[val];
        }

        /// Construct an empty range
        public CircleRange()
        {
            isempty = true;
        }

        /// Construct given specific boundaries.
        /// Give specific left/right boundaries and step information.
        /// The first element in the set is given left boundary. The sequence
        /// then proceeds by the given \e step up to (but not including) the given
        /// right boundary.  Care should be taken to make sure the remainders of the
        /// left and right boundaries modulo the step are equal.
        /// \param lft is the left boundary of the range
        /// \param rgt is the right boundary of the range
        /// \param size is the domain size in bytes (1,2,4,8,..)
        /// \param stp is the desired step (1,2,4,8,..)
        public CircleRange(ulong lft, ulong rgt, int size, int stp)
        {
            mask = Globals.calc_mask((uint)size);
            step = stp;
            left = lft;
            right = rgt;
            isempty = false;
        }

        /// Construct a boolean range
        /// The range contains only a single integer, 0 or 1, depending on the boolean parameter.
        /// \param val is the boolean parameter
        public CircleRange(bool val)
        {
            mask = 0xff;
            step = 1;
            left = val ? 1UL : 0;
            right = val ? 2UL : 1;
            isempty = false;
        }

        /// Construct range with single value
        /// A size specifies the number of bytes (*8 to get number of bits) in the mask.
        /// The stride is assumed to be 1.
        /// \param val is is the single value
        /// \param size is the size of the mask in bytes
        public CircleRange(ulong val, int size)
        {
            mask = Globals.calc_mask((uint)size);
            step = 1;
            left = val;
            right = (left + 1) & mask;
            isempty = false;
        }

        /// Set directly to a specific range
        /// \param lft is the left boundary of the range
        /// \param rgt is the right boundary of the range
        /// \param size is the size of the range domain in bytes
        /// \param stp is the step/stride of the range
        public void setRange(ulong lft, ulong rgt, int size, int step)
        {
            mask = Globals.calc_mask((uint)size);
            left = lft;
            right = rgt;
            this.step = step;
            isempty = false;
        }

        /// Set range with a single value
        /// A size specifies the number of bytes (*8 to get number of bits) in the mask.
        /// The stride is assumed to be 1.
        /// \param val is is the single value
        /// \param size is the size of the mask in bytes
        public void setRange(ulong val, int size)
        {
            mask = Globals.calc_mask((uint)size);
            step = 1;
            left = val;
            right = (left + 1) & mask;
            isempty = false;
        }

        /// Set a completely full range
        /// Make a range of values that holds everything.
        /// \param size is the size (in bytes) of the range
        public void setFull(int size)
        {
            mask = Globals.calc_mask((uint)size);
            step = 1;
            left = 0;
            right = 0;
            isempty = false;
        }

        /// Return \b true if \b this range is empty
        public bool isEmpty() => isempty;

        /// Return \b true if \b this contains all possible values
        public bool isFull() => ((!isempty) && (step == 1) && (left == right));

        /// Return \b true if \b this contains single value
        public bool isSingle() => (!isempty) && (right == ((left + (uint)step) & mask));

        /// Get the left boundary of the range
        public ulong getMin() => left;

        /// Get the right-most integer contained in the range
        public ulong getMax() => (right - (uint)step) & mask;

        /// Get the right boundary of the range
        public ulong getEnd() => right;

        /// Get the mask
        public ulong getMask() => mask;

        /// Get the size of this range
        /// \return the number of integers contained in this range
        public ulong getSize()
        {
            if (isempty) return 0;
            ulong val;
            if (left < right)
                val = (right - left) / (uint)step;
            else {
                val = (mask - (left - right) + (uint)step) / (uint)step;
                if (val == 0) {
                    // This is an overflow, when all ulong values are in the range
                    val = mask;               // We lie by one, which shouldn't matter for our jumptable application
                    if (step > 1) {
                        val = val / (uint)step;
                        val += 1;
                    }
                }
            }
            return val;
        }

        /// Get the step for \b this range
        public int getStep() => step;

        /// Get maximum information content of range
        /// In this context, the information content of a value is the index (+1) of the
        /// most significant non-zero bit (of the absolute value). This routine returns
        /// the maximum information across all values in the range.
        /// \return the maximum information
        public int getMaxInfo()
        {
            ulong halfPoint = mask ^ (mask >> 1);
            if (contains(halfPoint))
                return 8 * sizeof(ulong) - Globals.count_leading_zeros(halfPoint);
            int sizeLeft, sizeRight;
            if ((halfPoint & left) == 0)
                sizeLeft = Globals.count_leading_zeros(left);
            else
                sizeLeft = Globals.count_leading_zeros(~left & mask);
            if ((halfPoint & right) == 0)
                sizeRight = Globals.count_leading_zeros(right);
            else
                sizeRight = Globals.count_leading_zeros(~right & mask);
            int size1 = 8 * sizeof(ulong) - (sizeRight < sizeLeft ? sizeRight : sizeLeft);
            return size1;
        }

        /// Equals operator
        /// \param op2 is the range to compare \b this to
        /// \return \b true if the two ranges are equal
        public static bool operator ==(CircleRange op1, CircleRange op2)
        {
            if (op1.isempty != op2.isempty) return false;
            if (op1.isempty) return true;
            return (op1.left == op2.left)
                && (op1.right == op2.right)
                && (op1.mask == op2.mask)
                && (op1.step == op2.step);
        }

        public static bool operator !=(CircleRange op1, CircleRange op2)
        {
            return !(op1 == op2);
        }

        /// Advance an integer within the range
            public bool getNext(ulong val)
        {
            val = (val + (uint)step) & mask;
            return (val != right);
        }

        /// Check containment of another range in \b this.
        /// \param op2 is the specific range to test for containment.
        /// \return \b true if \b this contains the interval \b op2
        public bool contains(CircleRange op2)
        {
            if (isempty)
                return op2.isempty;
            if (op2.isempty)
                return true;
            if (step > op2.step)
            {
                // This must have a smaller or equal step to op2 or containment is impossible
                // except in the corner case where op2 consists of a single element (its step is meaningless)
                if (!op2.isSingle())
                    return false;
            }
            if (left == right) return true;
            if (op2.left == op2.right) return false;
            if (left % (uint)step != op2.left % (uint)step) return false;   // Wrong phase
            if (left == op2.left && right == op2.right) return true;

            char overlapCode = encodeRangeOverlaps(left, right, op2.left, op2.right);

            if (overlapCode == 'c')
                return true;
            if (overlapCode == 'b' && (right == op2.right))
                return true;
            return false;

            // Missing one case where op2.step > this.step, and the boundaries don't show containment,
            // but there is containment because the lower step size UP TO right still contains the edge points
        }

        /// Check containment of a specific integer.
        /// Check if a specific integer is a member of \b this range.
        /// \param val is the specific integer
        /// \return \b true if it is contained in \b this
        public bool contains(ulong val)
        {
            if (isempty) return false;
            if (step != 1) {
                if ((left % (uint)step) != (val % (uint)step))
                    return false;   // Phase is wrong
            }
            if (left < right) {
                if (val < left) return false;
                if (right <= val) return false;
            }
            else if (right < left) {
                if (val < right) return true;
                if (val >= left) return true;
                return false;
            }
            return true;
        }

        /// Intersect \b this with another range
        /// Set \b this to the intersection of \b this and \b op2 as a
        /// single interval if possible.
        /// Return 0 if the result is valid
        /// Return 2 if the intersection is two pieces
        /// If result is not zero, \b this is not modified
        /// \param op2 is the second range
        /// \return the intersection code
        public int intersect(CircleRange op2)
        {
            int retval, newStep;
            ulong newMask, myleft, myright, op2left, op2right;

            if (isempty) return 0;  // Intersection with empty is empty
            if (op2.isempty)
            {
                isempty = true;
                return 0;
            }
            myleft = left;
            myright = right;
            op2left = op2.left;
            op2right = op2.right;
            if (step < op2.step) {
                newStep = op2.step;
                uint rem = (uint)(op2left % (uint)newStep);
                if (newStride(mask, newStep, step, rem, myleft, myright)) {
                    // Increase the smaller stride
                    isempty = true;
                    return 0;
                }
            }
            else if (op2.step < step) {
                newStep = step;
                uint rem = (uint)(myleft % (uint)newStep);
                if (newStride(op2.mask, newStep, op2.step, rem, op2left, op2right)) {
                    isempty = true;
                    return 0;
                }
            }
            else
                newStep = step;
            newMask = mask & op2.mask;
            if (mask != newMask) {
                if (newDomain(newMask, newStep, myleft, myright)) {
                    isempty = true;
                    return 0;
                }
            }
            else if (op2.mask != newMask) {
                if (newDomain(newMask, newStep, op2left, op2right)) {
                    isempty = true;
                    return 0;
                }
            }
            if (myleft == myright) {
                // Intersect with this everything
                left = op2left;
                right = op2right;
                retval = 0;
            }
            else if (op2left == op2right) {
                // Intersect with op2 everything
                left = myleft;
                right = myright;
                retval = 0;
            }
            else {
                char overlapCode = encodeRangeOverlaps(myleft, myright, op2left, op2right);
                switch (overlapCode) {
                    case 'a':           // order (l r op2.l op2.r)
                    case 'f':           // order (op2.l op2.r l r)
                        isempty = true;
                        retval = 0;     // empty set
                        break;
                    case 'b':           // order (l op2.l r op2.r)
                        left = op2left;
                        right = myright;
                        if (left == right)
                            isempty = true;
                        retval = 0;
                        break;
                    case 'c':           // order (l op2.l op2.r r)
                        left = op2left;
                        right = op2right;
                        retval = 0;
                        break;
                    case 'd':           // order (op2.l l r op2.r)
                        left = myleft;
                        right = myright;
                        retval = 0;
                        break;
                    case 'e':           // order (op2.l l op2.r r)
                        left = myleft;
                        right = op2right;
                        if (left == right)
                            isempty = true;
                        retval = 0;
                        break;
                    case 'g':           // order (l op2.r op2.l r)
                        if (myleft == op2right) {
                            left = op2left;
                            right = myright;
                            if (left == right)
                                isempty = true;
                            retval = 0;
                        }
                        else if (op2left == myright) {
                            left = myleft;
                            right = op2right;
                            if (left == right)
                                isempty = true;
                            retval = 0;
                        }
                        else
                            retval = 2;         // 2 pieces
                        break;
                    default:
                        retval = 2;     // Will never reach here
                        break;
                }
            }
            if (retval != 0) return retval;
            mask = newMask;
            step = newStep;
            return 0;
        }

        /// Set the range based on a putative mask.
        /// Try to create a range given a value that is not necessarily a valid mask.
        /// If the mask is valid, range is set to all possible values that whose non-zero
        /// bits are contained in the mask. If the mask is invalid, \b this range is  not modified.
        /// \param nzmask is the putative mask
        /// \param size is a maximum size (in bytes) for the mask
        /// \return \b true if the mask is valid
        public bool setNZMask(ulong nzmask, int size)
        {
            int trans = Globals.bit_transitions(nzmask, size);
            if (trans > 2) return false;    // Too many transitions to form a valid range
            bool hasstep = ((nzmask & 1) == 0);
            if ((!hasstep) && (trans == 2)) return false; // Two sections of non-zero bits
            isempty = false;
            if (trans == 0)
            {
                mask = Globals.calc_mask((uint)size);
                if (hasstep)
                {       // All zeros
                    step = 1;
                    left = 0;
                    right = 1;      // Range containing only zero
                }
                else
                {           // All ones
                    step = 1;
                    left = 0;
                    right = 0;      // Everything
                }
                return true;
            }
            int shift = Globals.leastsigbit_set(nzmask);
            step = 1;
            step <<= shift;
            mask = Globals.calc_mask((uint)size);
            left = 0;
            right = (nzmask + (uint)step) & mask;
            return true;
        }

        /// Union two ranges.
        /// Set \b this to the union of \b this and \b op2 as a single interval.
        /// Return 0 if the result is valid.
        /// Return 2 if the union is two pieces.
        /// If result is not zero, \b this is not modified.
        /// \param op2 is the range to union with
        /// \return the result code
        public int circleUnion(CircleRange op2)
        {
            if (op2.isempty) return 0;
            if (isempty) {
                this.CopyFrom(op2);
                return 0;
            }
            if (mask != op2.mask) {
                // Cannot do proper union with different domains
                return 2;
            }
            ulong aRight = right;
            ulong bRight = op2.right;
            int newStep = step;
            if (step < op2.step) {
                if (isSingle()) {
                    newStep = op2.step;
                    aRight = (left + (uint)newStep) & mask;
                }
                else
                    return 2;
            }
            else if (op2.step < step) {
                if (op2.isSingle()) {
                    newStep = step;
                    bRight = (op2.left + (uint)newStep) & mask;
                }
                else
                    return 2;
            }
            ulong rem;
            if (newStep != 1) {
                rem = left % (uint)newStep;
                if (rem != (op2.left % (uint)newStep))
                    return 2;
            }
            else
                rem = 0;
            if ((left == aRight) || (op2.left == bRight)) {
                left = rem;
                right = rem;
                step = newStep;
                return 0;
            }

            char overlapCode = encodeRangeOverlaps(left, aRight, op2.left, bRight);
            switch (overlapCode) {
                case 'a':           // order (l r op2.l op2.r)
                case 'f':           // order (op2.l op2.r l r)
                    if (aRight == op2.left)
                    {
                        right = bRight;
                        step = newStep;
                        return 0;
                    }
                    if (left == bRight)
                    {
                        left = op2.left;
                        right = aRight;
                        step = newStep;
                        return 0;
                    }
                    return 2;           // 2 pieces;
                case 'b':           // order (l op2.l r op2.r)
                    right = bRight;
                    step = newStep;
                    return 0;
                case 'c':           // order (l op2.l op2.r r)
                    right = aRight;
                    step = newStep;
                    return 0;
                case 'd':           // order (op2.l l r op2.r)
                    left = op2.left;
                    right = bRight;
                    step = newStep;
                    return 0;
                case 'e':              // order (op2.l l op2.r r)
                    left = op2.left;
                    right = aRight;
                    step = newStep;
                    return 0;
                case 'g':           // either impossible or covers whole circle
                    left = rem;
                    right = rem;
                    step = newStep;
                    return 0;           // entire circle is covered
            }
            return -1;          // Never reach here
        }

        /// Construct minimal range that contains both \b this and another range
        /// Turn \b this into a range that contains both the original range and
        /// the other given range. The resulting range may contain values that were in neither
        /// of the original ranges (not a strict union). But the number of added values will be
        /// minimal. This method will create a range with step if the input ranges hold single values
        /// and the distance between them is a power of 2 and less or equal than a given bound.
        /// \param op2 is the other given range to combine with \b this
        /// \param maxStep is the step bound that can be induced for a container with two singles
        /// \return \b true if the container is everything (full)
        public bool minimalContainer(CircleRange op2, int maxStep)
        {
            if (isSingle() && op2.isSingle())
            {
                ulong min, max;
                if (getMin() < op2.getMin())
                {
                    min = getMin();
                    max = op2.getMin();
                }
                else
                {
                    min = op2.getMin();
                    max = getMin();
                }
                ulong diff = max - min;
                if (diff > 0 && diff <= (uint)maxStep) {
                    if (Globals.leastsigbit_set(diff) == Globals.mostsigbit_set(diff)) {
                        step = (int)diff;
                        left = min;
                        right = (max + (uint)step) & mask;
                        return false;
                    }
                }
            }

            ulong aRight = right - (uint)step + 1;        // Treat original ranges as having step=1
            ulong bRight = op2.right - (uint)op2.step + 1;
            step = 1;
            mask |= op2.mask;
            ulong vacantSize1, vacantSize2;

            char overlapCode = encodeRangeOverlaps(left, aRight, op2.left, bRight);
            switch (overlapCode) {
                case 'a':           // order (l r op2.l op2.r)
                    vacantSize1 = left + (mask - bRight) + 1;
                    vacantSize2 = op2.left - aRight;
                    if (vacantSize1 < vacantSize2) {
                        left = op2.left;
                        right = aRight;
                    }
                    else {
                        right = bRight;
                    }
                    break;
                case 'f':           // order (op2.l op2.r l r)
                    vacantSize1 = op2.left + (mask - aRight) + 1;
                    vacantSize2 = left - bRight;
                    if (vacantSize1 < vacantSize2) {
                        right = bRight;
                    }
                    else {
                        left = op2.left;
                        right = aRight;
                    }
                    break;
                case 'b':           // order (l op2.l r op2.r)
                    right = bRight;
                    break;
                case 'c':           // order (l op2.l op2.r r)
                    right = aRight;
                    break;
                case 'd':           // order (op2.l l r op2.r)
                    left = op2.left;
                    right = bRight;
                    break;
                case 'e':           // order (op2.l l op2.r r)
                    left = op2.left;
                    right = aRight;
                    break;
                case 'g':           // order (l op2.r op2.l r)
                    left = 0;           // Entire circle is covered
                    right = 0;
                    break;
            }
            normalize();
            return (left == right);
        }

        /// Convert to complementary range
        /// Convert range to its complement.  The step is automatically converted to 1 first.
        /// \return the original step size
        public int invert()
        {
            int res = step;
            step = 1;
            complement();
            return res;
        }

        /// Set a new step on \b this range.
        /// This method changes the step for \b this range, i.e. elements are removed.
        /// The boundaries of the range do not change except for the remainder modulo the new step.
        /// \param newStep is the new step amount
        /// \param rem is the desired phase (remainder of the values modulo the step)
        public void setStride(int newStep, ulong rem)
        {
            bool iseverything = (!isempty) && (left == right);
            if (newStep == step) return;
            ulong aRight = right - (uint)step;
            step = newStep;
            if (step == 1) return;      // No remainder to fill in
            ulong curRem = left % (uint)step;
            left = (left - curRem) + rem;
            curRem = aRight % (uint)step;
            aRight = (aRight - curRem) + rem;
            right = aRight + (uint)step;
            if ((!iseverything) && (left == right))
                isempty = true;
        }

        /// Pull-back \b this through the given unary operator
        /// \param opc is the OpCode to pull the range back through
        /// \param inSize is the storage size in bytes of the resulting input
        /// \param outSize is the storage size in bytes of the range to pull-back
        /// \return \b true if a valid range is formed in the pull-back
        public bool pullBackUnary(OpCode opc, int inSize, int outSize)
        {
            ulong val;
            // If there is nothing in the output set, no input will map to it
            if (isempty) return true;

            switch (opc)
            {
                case OpCode.CPUI_BOOL_NEGATE:
                    if (convertToBoolean())
                        break;          // Both outputs possible => both inputs possible
                    left = left ^ 1;        // Flip the boolean range
                    right = left + 1;
                    break;
                case OpCode.CPUI_COPY:
                    break;          // Identity transform on range
                case OpCode.CPUI_INT_2COMP:
                    val = (~left + 1 + (uint)step) & mask;
                    left = (~right + 1 + (uint)step) & mask;
                    right = val;
                    break;
                case OpCode.CPUI_INT_NEGATE:
                    val = (~left + (uint)step) & mask;
                    left = (~right + (uint)step) & mask;
                    right = val;
                    break;
                case OpCode.CPUI_INT_ZEXT: {
                        // (smaller) input mask
                        val = Globals.calc_mask((uint)inSize);
                        ulong rem = left % (uint)step;
                        CircleRange zextrange = new CircleRange() {
                            left = rem,
                            // Biggest possible range of ZEXT
                            right = val + 1 + rem,
                            mask = mask,
                            // Keep the same stride
                            step = step,
                            isempty = false
                        };
                        if (0 != intersect(zextrange))
                            return false;
                        left &= val;
                        right &= val;
                        // Preserve the stride
                        mask &= val;
                        break;
                    }
                case OpCode.CPUI_INT_SEXT: {
                        // (smaller) input mask
                        val = Globals.calc_mask((uint)inSize);
                        ulong rem = left & (uint)step;
                        ulong leftValue = (val ^ (val >> 1)) + rem;
                        CircleRange sextrange = new CircleRange() {
                            // High order bit for (small) input space
                            left = leftValue,
                            right = Globals.sign_extend(leftValue, inSize, outSize),
                            mask = mask,
                            // Keep the same stride
                            step = step,
                            isempty = false
                        };
                        if (sextrange.intersect(this) != 0)
                            return false;
                        else {
                            if (!sextrange.isEmpty())
                                return false;
                            else {
                                left &= val;
                                right &= val;
                                mask &= val;        // Preserve the stride
                            }
                        }
                        break;
                    }
                default:
                    return false;
            }
            return true;
        }

        /// Pull-back \b this thru binary operator
        /// \param opc is the OpCode to pull the range back through
        /// \param val is the constant value of the other input parameter (if present)
        /// \param slot is the slot of the input variable whose range gets produced
        /// \param inSize is the storage size in bytes of the resulting input
        /// \param outSize is the storage size in bytes of the range to pull-back
        /// \return \b true if a valid range is formed in the pull-back
        public bool pullBackBinary(OpCode opc, ulong val, int slot, int inSize, int outSize)
        {
            bool yescomplement;
            bool bothTrueFalse;

            // If there is nothing in the output set, no input will map to it
            if (isempty) return true;

            switch (opc) {
                case OpCode.CPUI_INT_EQUAL:
                    bothTrueFalse = convertToBoolean();
                    mask = Globals.calc_mask((uint)inSize);
                    if (bothTrueFalse)
                        break;  // All possible outs => all possible ins
                    yescomplement = (left == 0);
                    left = val;
                    right = (val + 1) & mask;
                    if (yescomplement)
                        complement();
                    break;
                case OpCode.CPUI_INT_NOTEQUAL:
                    bothTrueFalse = convertToBoolean();
                    mask = Globals.calc_mask((uint)inSize);
                    if (bothTrueFalse) break;   // All possible outs => all possible ins
                    yescomplement = (left == 0);
                    left = (val + 1) & mask;
                    right = val;
                    if (yescomplement)
                        complement();
                    break;
                case OpCode.CPUI_INT_LESS:
                    bothTrueFalse = convertToBoolean();
                    mask = Globals.calc_mask((uint)inSize);
                    if (bothTrueFalse) break;   // All possible outs => all possible ins
                    yescomplement = (left == 0);
                    if (slot == 0) {
                        if (val == 0)
                            isempty = true;     // X < 0  is always false
                        else {
                            left = 0;
                            right = val;
                        }
                    }
                    else {
                        if (val == mask)
                            isempty = true;     // 0xffff < X  is always false
                        else {
                            left = (val + 1) & mask;
                            right = 0;
                        }
                    }
                    if (yescomplement)
                        complement();
                    break;
                case OpCode.CPUI_INT_LESSEQUAL:
                    bothTrueFalse = convertToBoolean();
                    mask = Globals.calc_mask((uint)inSize);
                    if (bothTrueFalse) break;   // All possible outs => all possible ins
                    yescomplement = (left == 0);
                    if (slot == 0) {
                        left = 0;
                        right = (val + 1) & mask;
                    }
                    else {
                        left = val;
                        right = 0;
                    }
                    if (yescomplement)
                        complement();
                    break;
                case OpCode.CPUI_INT_SLESS:
                    bothTrueFalse = convertToBoolean();
                    mask = Globals.calc_mask((uint)inSize);
                    if (bothTrueFalse) break;   // All possible outs => all possible ins
                    yescomplement = (left == 0);
                    if (slot == 0) {
                        if (val == (mask >> 1) + 1)
                            isempty = true;     // X < -infinity, is always false
                        else {
                            left = (mask >> 1) + 1; // -infinity
                            right = val;
                        }
                    }
                    else {
                        if (val == (mask >> 1))
                            isempty = true;     // infinity < X, is always false
                        else {
                            left = (val + 1) & mask;
                            right = (mask >> 1) + 1;    // -infinity
                        }
                    }
                    if (yescomplement)
                        complement();
                    break;
                case OpCode.CPUI_INT_SLESSEQUAL:
                    bothTrueFalse = convertToBoolean();
                    mask = Globals.calc_mask((uint)inSize);
                    if (bothTrueFalse) break;   // All possible outs => all possible ins
                    yescomplement = (left == 0);
                    if (slot == 0) {
                        left = (mask >> 1) + 1; // -infinity
                        right = (val + 1) & mask;
                    }
                    else {
                        left = val;
                        right = (mask >> 1) + 1;    // -infinity
                    }
                    if (yescomplement)
                        complement();
                    break;
                case OpCode.CPUI_INT_CARRY:
                    bothTrueFalse = convertToBoolean();
                    mask = Globals.calc_mask((uint)inSize);
                    if (bothTrueFalse) break;   // All possible outs => all possible ins
                    yescomplement = (left == 0);
                    if (val == 0)
                        isempty = true;     // Nothing carries adding zero
                    else {
                        left = ((mask - val) + 1) & mask;
                        right = 0;
                    }
                    if (yescomplement)
                        complement();
                    break;
                case OpCode.CPUI_INT_ADD:
                    left = (left - val) & mask;
                    right = (right - val) & mask;
                    break;
                case OpCode.CPUI_INT_SUB:
                    if (slot == 0) {
                        left = (left + val) & mask;
                        right = (right + val) & mask;
                    }
                    else {
                        left = (val - left) & mask;
                        right = (val - right) & mask;
                    }
                    break;
                case OpCode.CPUI_INT_RIGHT: {
                        if (step == 1) {
                            ulong rightBound = (Globals.calc_mask((uint)inSize) >> (int)val) + 1; // The maximal right bound
                            if (((left >= rightBound) && (right >= rightBound) && (left >= right))
                                || ((left == 0) && (right >= rightBound)) || (left == right))
                            {
                                // covers everything in range of shift
                                left = 0;       // So domain is everything
                                right = 0;
                            }
                            else {
                                if (left > rightBound)
                                    left = rightBound;
                                if (right > rightBound)
                                    right = 0;
                                left = (left << (int)val) & mask;
                                right = (right << (int)val) & mask;
                                if (left == right)
                                    isempty = true;
                            }
                        }
                        else
                            return false;
                        break;
                    }
                case OpCode.CPUI_INT_SRIGHT: {
                        if (step == 1) {
                            ulong rightb = Globals.calc_mask((uint)inSize);
                            ulong leftb = rightb >> (int)(val + 1);
                            rightb = leftb ^ rightb; // Smallest negative possible
                            leftb += 1;     // Biggest positive (+1) possible
                            if (((left >= leftb) && (left <= rightb) && (right >= leftb)
                                && (right <= rightb) && (left >= right)) || (left == right))
                            {
                                // covers everything in range of shift
                                left = 0;       // So domain is everything
                                right = 0;
                            }
                            else {
                                if ((left > leftb) && (left < rightb))
                                    left = leftb;
                                if ((right > leftb) && (right < rightb))
                                    right = rightb;
                                left = (left << (int)val) & mask;
                                right = (right << (int)val) & mask;
                                if (left == right)
                                    isempty = true;
                            }
                        }
                        else
                            return false;
                        break;
                    }
                default:
                    return false;
            }
            return true;
        }

        /// Pull-back \b this range through given PcodeOp.
        /// The pull-back is performed through a given p-code \b op and set \b this
        /// to the resulting range (if possible).
        /// If there is a single unknown input, and the set of values
        /// for this input that cause the output of \b op to fall
        /// into \b this form a range, then set \b this to the
        /// range (the "pullBack") and return the unknown varnode.
        /// Return null otherwise.
        ///
        /// We may know something about the input varnode in the form of its NZMASK, which can further
        /// restrict the range we return.  If \b usenzmask is true, and NZMASK forms a range, intersect
        /// \b this with the result.
        ///
        /// If there is Symbol markup on any constant passed into the op, pass that information back.
        /// \param op is the given PcodeOp
        /// \param constMarkup is the reference for passing back the constant relevant to the pull-back
        /// \param usenzmask specifies whether to use the NZMASK
        /// \return the input Varnode or NULL
        public Varnode? pullBack(PcodeOp op, out Varnode? constMarkup, bool usenzmask)
        {
            Varnode res;

            constMarkup = null;
            if (op.numInput() == 1) {
                res = op.getIn(0);
                if (res.isConstant()) return (Varnode)null;
                if (!pullBackUnary(op.code(), res.getSize(), op.getOut().getSize()))
                    return (Varnode)null;
            }
            else if (op.numInput() == 2) {
                Varnode constvn;
                ulong val;
                // Find non-constant varnode input, and slot
                // Make sure second input is constant
                int slot = 0;
                res = op.getIn(slot);
                constvn = op.getIn(1 - slot);
                if (res.isConstant()) {
                    slot = 1;
                    constvn = res;
                    res = op.getIn(slot);
                    if (res.isConstant())
                        return (Varnode)null;
                }
                else if (!constvn.isConstant())
                    return (Varnode)null;
                val = constvn.getOffset();
                OpCode opc = op.code();
                if (!pullBackBinary(opc, val, slot, res.getSize(), op.getOut().getSize())) {
                    if (usenzmask && opc == OpCode.CPUI_SUBPIECE && val == 0) {
                        // If everything we are truncating is known to be zero, we may still have a range
                        int msbset = Globals.mostsigbit_set(res.getNZMask());
                        msbset = (msbset + 8) / 8;
                        if (op.getOut().getSize() < msbset) // Some bytes we are chopping off might not be zero
                            return (Varnode)null;
                        else {
                            mask = Globals.calc_mask((uint)res.getSize()); // Keep the range but make the mask bigger
                                                              // If the range wraps (left>right) then, increasing the mask adds all the new space into
                                                              // the range, and it would be an inaccurate pullback by itself, but with the nzmask intersection
                                                              // all the new space will get intersected away again.
                        }
                    }
                    else
                        return (Varnode)null;
                }
                if (constvn.getSymbolEntry() != (SymbolEntry)null)
                    constMarkup = constvn;
            }
            else    // Neither unary or binary
                return (Varnode)null;

            if (usenzmask) {
                CircleRange nzrange = new CircleRange();
                if (!nzrange.setNZMask(res.getNZMask(), res.getSize()))
                    return res;
                intersect(nzrange);
                // If the intersect does not succeed (i.e. produces 2 pieces) the original range is
                // preserved and we still consider this pullback successful.
            }
            return res;
        }

        /// Push-forward thru given unary operator
        /// Push all values in the given range through a p-code operator.
        /// If the output set of values forms a range, then set \b this to the range and return \b true.
        /// \param opc is the given p-code operator
        /// \param in1 is the given input range
        /// \param inSize is the storage space in bytes for the input
        /// \param outSize is the storage space in bytes for the output
        /// \return \b true if the result is known and forms a range
        public bool pushForwardUnary(OpCode opc, CircleRange in1, int inSize, uint outSize)
        {
            if (in1.isempty) {
                isempty = true;
                return true;
            }
            switch (opc) {
                case OpCode.CPUI_CAST:
                case OpCode.CPUI_COPY:
                    this.CopyFrom(in1);
                    break;
                case OpCode.CPUI_INT_ZEXT:
                    isempty = false;
                    step = in1.step;
                    mask = Globals.calc_mask(outSize);
                    if (in1.left == in1.right) {
                        left = in1.left % (uint)step;
                        right = in1.mask + 1 + left;
                    }
                    else {
                        left = in1.left;
                        right = (in1.right - (uint)in1.step) & in1.mask;
                        if (right < left)
                            // Extending causes 2 pieces
                            return false;
                        // Impossible for it to wrap with bigger mask
                        right += (uint)step;
                    }
                    break;
                case OpCode.CPUI_INT_SEXT:
                    isempty = false;
                    step = in1.step;
                    mask = Globals.calc_mask(outSize);
                    if (in1.left == in1.right) {
                        ulong rem = in1.left % (uint)step;
                        right = Globals.calc_mask((uint)inSize) >> 1;
                        left = (Globals.calc_mask(outSize) ^ right) + rem;
                        right = right + 1 + rem;
                    }
                    else {
                        left = Globals.sign_extend(in1.left, inSize, (int)outSize);
                        right = Globals.sign_extend((in1.right - (uint)in1.step) & in1.mask,
                            inSize, (int)outSize);
                        if ((long)right < (long)left)
                            // Extending causes 2 pieces
                            return false;
                        right = (right + (uint)step) & mask;
                    }
                    break;
                case OpCode.CPUI_INT_2COMP:
                    isempty = false;
                    step = in1.step;
                    mask = in1.mask;
                    right = (~in1.left + 1 + (uint)step) & mask;
                    left = (~in1.right + 1 + (uint)step) & mask;
                    normalize();
                    break;
                case OpCode.CPUI_INT_NEGATE:
                    isempty = false;
                    step = in1.step;
                    mask = in1.mask;
                    left = (~in1.right + (uint)step) & mask;
                    right = (~in1.left + (uint)step) & mask;
                    normalize();
                    break;
                case OpCode.CPUI_BOOL_NEGATE:
                case OpCode.CPUI_FLOAT_NAN:
                    isempty = false;
                    mask = 0xff;
                    step = 1;
                    left = 0;
                    right = 2;
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// \brief Push \b this range forward through a binary operation
        ///
        /// Push all values in the given ranges through a binary p-code operator.
        /// If the output set of values forms a range, then set \b this to the range and return \b true.
        /// \param opc is the given p-code operator
        /// \param in1 is the first given input range
        /// \param in2 is the second given input range
        /// \param inSize is the storage space in bytes for the input
        /// \param outSize is the storage space in bytes for the output
        /// \param maxStep is the maximum to allow step to grow via multiplication
        /// \return \b true if the result is known and forms a range
        public bool pushForwardBinary(OpCode opc, CircleRange in1, CircleRange in2, int inSize, int outSize,
            int maxStep)
        {
            if (in1.isempty || in2.isempty)
            {
                isempty = true;
                return true;
            }
            switch (opc) {
                case OpCode.CPUI_PTRSUB:
                case OpCode.CPUI_INT_ADD:
                    isempty = false;
                    mask = in1.mask | in2.mask;
                    if (in1.left == in1.right || in2.left == in2.right) {
                        step = (in1.step < in2.step) ? in1.step : in2.step; // Smaller step
                        left = (in1.left + in2.left) % (uint)step;
                        right = left;
                    }
                    else if (in2.isSingle()) {
                        step = in1.step;
                        left = (in1.left + in2.left) & mask;
                        right = (in1.right + in2.left) & mask;
                    }
                    else if (in1.isSingle()) {
                        step = in2.step;
                        left = (in2.left + in1.left) & mask;
                        right = (in2.right + in1.left) & mask;
                    }
                    else {
                        step = (in1.step < in2.step) ? in1.step : in2.step; // Smaller step
                        ulong size1 = (in1.left < in1.right)
                            ? (in1.right - in1.left)
                            : (in1.mask - (in1.left - in1.right) + (uint)in1.step);
                        left = (in1.left + in2.left) & mask;
                        right = (in1.right - (uint)in1.step + in2.right - (uint)in2.step + (uint)step) & mask;
                        ulong sizenew = (left < right)
                            ? (right - left)
                            : (mask - (left - right) + (uint)step);
                        if (sizenew < size1) {
                            // Over-flow, we covered everything
                            right = left;
                        }
                        normalize();
                    }
                    break;
                case OpCode.CPUI_INT_MULT: {
                        isempty = false;
                        mask = in1.mask | in2.mask;
                        ulong constVal;
                        if (in1.isSingle())
                        {
                            constVal = in1.getMin();
                            step = in2.step;
                        }
                        else if (in2.isSingle())
                        {
                            constVal = in2.getMin();
                            step = in1.step;
                        }
                        else
                            return false;
                        uint tmp = (uint)constVal;
                        while (step < maxStep) {
                            if ((tmp & 1) != 0) break;
                            step <<= 1;
                            tmp >>= 1;
                        }
                        int wholeSize = 8 * sizeof(ulong) - Globals.count_leading_zeros(mask);
                        if (in1.getMaxInfo() + in2.getMaxInfo() > wholeSize) {
                            left = in1.left;    // Covered everything
                            right = in1.left;
                            normalize();
                            return true;
                        }
                        if ((constVal & (mask ^ (mask >> 1))) != 0) {
                            // Multiplying by negative number
                            left = ((in1.right - (uint)in1.step) * (in2.right - (uint)in2.step)) & mask;
                            right = ((in1.left * in2.left) + (uint)step) & mask;
                        }
                        else {
                            left = (in1.left * in2.left) & mask;
                            right = ((in1.right - (uint)in1.step) * (in2.right - (uint)in2.step) + (uint)step) & mask;
                        }
                        break;
                    }
                case OpCode.CPUI_INT_LEFT:
                    {
                        if (!in2.isSingle()) return false;
                        isempty = false;
                        mask = in1.mask;
                        step = in1.step;
                        uint sa = (uint)in2.getMin();
                        uint tmp = sa;
                        while (step < maxStep && tmp > 0) {
                            step <<= 1;
                            sa -= 1;
                        }
                        left = (in1.left << (int)sa) & mask;
                        right = (in1.right << (int)sa) & mask;
                        int wholeSize = 8 * sizeof(ulong) - Globals.count_leading_zeros(mask);
                        if (in1.getMaxInfo() + sa > wholeSize) {
                            right = left;   // Covered everything
                            normalize();
                            return true;
                        }
                        break;
                    }
                case OpCode.CPUI_SUBPIECE: {
                        if (!in2.isSingle()) return false;
                        isempty = false;
                        int sa = (int)in2.left * 8;
                        mask = Globals.calc_mask((uint)outSize);
                        step = (sa == 0) ? in1.step : 1;

                        left = (in1.left >> sa) & mask;
                        right = (in1.right >> sa) & mask;
                        if ((left & ~mask) != (right & ~mask)) {
                            // Truncated part is different
                            left = right = 0;   // We cover everything
                        }
                        else {
                            left &= mask;
                            right &= mask;
                            normalize();
                        }
                        break;
                    }
                case OpCode.CPUI_INT_RIGHT:
                    {
                        if (!in2.isSingle()) return false;
                        isempty = false;
                        int sa = (int)in2.left;
                        mask = Globals.calc_mask((uint)outSize);
                        step = 1;           // Lose any step
                        if (in1.left < in1.right) {
                            left = in1.left >> sa;
                            right = ((in1.right - (uint)in1.step) >> sa) + 1;
                        }
                        else {
                            left = 0;
                            right = in1.mask >> sa;
                        }
                        if (left == right)  // Don't truncate accidentally to everything
                            right = (left + 1) & mask;
                        break;
                    }
                case OpCode.CPUI_INT_SRIGHT:
                    {
                        if (!in2.isSingle()) return false;
                        isempty = false;
                        int sa = (int)in2.left;
                        mask = Globals.calc_mask((uint)outSize);
                        // Lose any step
                        step = 1;
                        long valLeft = (long)in1.left;
                        long valRight = (long)in1.right;
                        int bitPos = 8 * inSize - 1;
                        Globals.sign_extend(ref valLeft, bitPos);
                        Globals.sign_extend(ref valRight, bitPos);
                        if (valLeft >= valRight) {
                            // Max positive
                            valRight = (long)(mask >> 1);
                            // Min negative
                            valLeft = valRight + 1;
                            Globals.sign_extend(ref valLeft, bitPos);
                        }
                        left = (ulong)(valLeft >> sa) & mask;
                        right = (ulong)(valRight >> sa) & mask;
                        if (left == right)
                            // Don't truncate accidentally to everything
                            right = (left + 1) & mask;
                        break;
                    }
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_INT_NOTEQUAL:
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_INT_CARRY:
                case OpCode.CPUI_INT_SCARRY:
                case OpCode.CPUI_INT_SBORROW:
                case OpCode.CPUI_BOOL_XOR:
                case OpCode.CPUI_BOOL_AND:
                case OpCode.CPUI_BOOL_OR:
                case OpCode.CPUI_FLOAT_EQUAL:
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                case OpCode.CPUI_FLOAT_LESS:
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                    // Ops with boolean outcome.  We don't try to eliminate outcomes here.
                    isempty = false;
                    mask = 0xff;
                    step = 1;
                    left = 0;       // Both true and false are possible
                    right = 2;
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// \brief Push \b this range forward through a trinary operation
        ///
        /// Push all values in the given ranges through a trinary p-code operator (currenly only OpCode.CPUI_PTRADD).
        /// If the output set of values forms a range, then set \b this to the range and return \b true.
        /// \param opc is the given p-code operator
        /// \param in1 is the first given input range
        /// \param in2 is the second given input range
        /// \param in3 is the third given input range
        /// \param inSize is the storage space in bytes for the input
        /// \param outSize is the storage space in bytes for the output
        /// \param maxStep is the maximum to allow step to grow via multiplication
        /// \return \b true if the result is known and forms a range
        public bool pushForwardTrinary(OpCode opc, CircleRange in1, CircleRange in2, CircleRange in3,
            int inSize, int outSize, int maxStep)
        {
            if (opc != OpCode.CPUI_PTRADD) return false;
            CircleRange tmpRange = new CircleRange();
            if (!tmpRange.pushForwardBinary(OpCode.CPUI_INT_MULT, in2, in3, inSize, inSize, maxStep))
                return false;
            return pushForwardBinary(OpCode.CPUI_INT_ADD, in1, tmpRange, inSize, outSize, maxStep);
        }

        /// Widen the unstable bound to match containing range
        /// Widen \b this range so at least one of the boundaries matches with the given
        /// range, which must contain \b this.
        /// \param op2 is the given containing range
        /// \param leftIsStable is \b true if we want to match right boundaries
        public void widen(CircleRange op2, bool leftIsStable)
        {
            if (leftIsStable) {
                ulong lmod = left % (uint)step;
                ulong mod = op2.right % (uint)step;
                if (mod <= lmod)
                    right = op2.right + (lmod - mod);
                else
                    right = op2.right - (mod - lmod);
                right &= mask;
            }
            else {
                left = op2.left & mask;
            }
            normalize();
        }

        /// Translate range to a comparison op
        /// Recover parameters for a comparison PcodeOp, that returns true for
        /// input values exactly in \b this range.
        /// Return:
        ///    - 0 on success
        ///    - 1 if all inputs must return true
        ///    - 2 if this is not possible
        ///    - 3 if no inputs must return true
        /// \param opc will contain the OpCode for the comparison PcodeOp
        /// \param c will contain the constant input to the op
        /// \param cslot will indicate the slot holding the constant
        /// \return the success code
        public int translate2Op(out OpCode? opc, out ulong c, out int cslot)
        {
            opc = null;
            c = 0;
            cslot = 0;
            if (isempty) return 3;
            if (step != 1) return 2;    // Not possible with a stride
            if (right == ((left + 1) & mask)) {
                // Single value
                opc = OpCode.CPUI_INT_EQUAL;
                cslot = 0;
                c = left;
                return 0;
            }
            if (left == ((right + 1) & mask)) {
                // All but one value
                opc = OpCode.CPUI_INT_NOTEQUAL;
                cslot = 0;
                c = right;
                return 0;
            }
            if (left == right) return 1;    // All outputs are possible
            if (left == 0) {
                opc = OpCode.CPUI_INT_LESS;
                cslot = 1;
                c = right;
                return 0;
            }
            if (right == 0) {
                opc = OpCode.CPUI_INT_LESS;
                cslot = 0;
                c = (left - 1) & mask;
                return 0;
            }
            if (left == (mask >> 1) + 1) {
                opc = OpCode.CPUI_INT_SLESS;
                cslot = 1;
                c = right;
                return 0;
            }
            if (right == (mask >> 1) + 1) {
                opc = OpCode.CPUI_INT_SLESS;
                cslot = 0;
                c = (left - 1) & mask;
                return 0;
            }
            return 2;           // Cannot represent
        }

        /// Write a text representation of \b this to stream
        /// \param s is the stream to write to
        public void printRaw(TextWriter s)
        {
            if (isempty) {
                s.Write("(empty)");
                return;
            }
            if (left == right) {
                s.Write("(full");
                if (step != 1)
                    s.Write($",{step}");
                s.Write(')');
            }
            else if (right == ((left + 1) & mask)) {
                s.Write($"[{left:X}]");
            }
            else {
                s.Write($"[{left:X},{right:X}");
                if (step != 1)
                    s.Write($",{step}");
                s.Write(')');
            }
        }
    }
}
