using Sla.DECCORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Partial data-type information mapped to a specific range of bytes
    ///
    /// This object gives a hint about the data-type for a sequence of bytes
    /// starting at a specific address offset (typically on the stack). It describes
    /// where the data-type starts, what data-type it might be, and how far it extends
    /// from the start point (possibly as an array).
    internal class RangeHint
    {
        // friend class MapState;
        // friend class ScopeLocal;

        /// \brief The basic categorization of the range
        public enum RangeType
        {
            /// A data-type with a fixed size
            @fixed = 0,
            /// An array with a (possibly unknown) number of elements
            open = 1,
            /// An (artificial) boundary to the range of bytes getting analyzed
            endpoint = 2
        }

        /// Starting offset of \b this range of bytes
        private uintb start;
        /// Number of bytes in a single element of this range
        private int4 size;
        /// A signed version of the starting offset
        private intb sstart;
        /// Putative data-type for a single element of this range
        private Datatype type;
        /// Additional boolean properties of this range
        private uint4 flags;
        /// The type of range
        private RangeType rangeType;
        /// Minimum upper bound on the array index (if \b this is \e open)
        private int4 highind;
    
        public RangeHint()
        {
        }

        public RangeHint(uintb st, int4 sz, intb sst, Datatype ct, uint4 fl, RangeType rt, int4 hi)
        {
            start = st; size = sz; sstart = sst; type = ct; flags = fl; rangeType = rt; highind = hi;
        }

        /// \brief Can the given intersecting RangeHint coexist with \b this at their given offsets
        ///
        /// Determine if the data-type information in the two ranges \e line \e up
        /// properly, in which case the union of the two ranges can exist without
        /// destroying data-type information.
        /// \param b is the range to reconcile with \b this
        /// \return \b true if the data-type information can be reconciled
        public bool reconcile(RangeHint b)
        {
            RangeHint a = this;
            if (a->type->getSize() < b->type->getSize())
            {
                RangeHint tmp = b;
                b = a;          // Make sure b is smallest
                a = tmp;
            }
            intb mod = (b->sstart - a->sstart) % a->type->getSize();
            if (mod < 0)
                mod += a->type->getSize();

            Datatype* sub = a->type;
            uintb umod = mod;
            while ((sub != (Datatype*)0) && (sub->getSize() > b->type->getSize()))
                sub = sub->getSubType(umod, &umod);

            if (sub == (Datatype*)0) return false;
            if (umod != 0) return false;
            if (sub->getSize() == b->type->getSize()) return true;
            if ((b->flags & Varnode::typelock) != 0) return false;
            // If we reach here, component sizes do not match
            // Check for data-types we want to protect more
            type_metatype meta = a->type->getMetatype();
            if (meta != TYPE_STRUCT && meta != TYPE_UNION)
            {
                if (meta != TYPE_ARRAY || ((TypeArray*)(a->type))->getBase()->getMetatype() == TYPE_UNKNOWN)
                    return false;
            }
            // For structures, unions, and arrays, test if b looks like a partial data-type
            meta = b->type->getMetatype();
            if (meta == TYPE_UNKNOWN || meta == TYPE_INT || meta == TYPE_UINT)
            {
                return true;
            }
            return false;
        }

        /// \brief Return \b true if \b this or the given range contains the other.
        ///
        /// We assume \b this range starts at least as early as the given range
        /// and that the two ranges intersect.
        /// \param b is the given range to check for containment with \b this
        /// \return \b true if one contains the other
        public bool contain(RangeHint b)
        {
            if (sstart == b->sstart) return true;
            //  if (sstart==send) return true;
            //  if (b->sstart==b->send) return true;
            if (b->sstart + b->size - 1 <= sstart + size - 1) return true;
            return false;
        }

        /// \brief Return \b true if the \b this range's data-type is preferred over the other given range
        ///
        /// A locked data-type is preferred over unlocked. A \e fixed size over \e open size.
        /// Otherwise data-type ordering is used.
        /// \param b is the other given range
        /// \param reconcile is \b true is the two ranges have \e reconciled data-types
        /// \return \b true if \b this ranges's data-type is preferred
        public bool preferred(RangeHint b, bool reconcile)
        {
            if (start != b->start)
                return true;        // Something must occupy a->start to b->start
                                    // Prefer the locked type
            if ((b->flags & Varnode::typelock) != 0)
            {
                if ((flags & Varnode::typelock) == 0)
                    return false;
            }
            else if ((flags & Varnode::typelock) != 0)
                return true;

            if (!reconcile)
            {       // If the ranges don't reconcile
                if (rangeType == open && b->rangeType != open) // Throw out the open range
                    return false;
                if (b->rangeType == open && rangeType != open)
                    return true;
            }

            return (0 > type->typeOrder(*b->type)); // Prefer the more specific
        }

        /// Try to concatenate another RangeHint onto \b this
        /// If \b this RangeHint is an array and the following RangeHint line up, adjust \b this
        /// so that it \e absorbs the other given RangeHint and return \b true.
        /// The second RangeHint:
        ///   - must have the same element size
        ///   - must have close to the same data-type
        ///   - must line up with the step of the first array
        ///   - must not be a locked data-type
        ///   - must not extend the size of the first array beyond what is known of its limits
        ///
        /// \param b is the other RangeHint to absorb
        /// \return \b true if the other RangeHint was successfully absorbed
        public bool attemptJoin(RangeHint b)
        {
            if (rangeType != open) return false;
            if (highind < 0) return false;
            if (b->rangeType == endpoint) return false;         // Don't merge with bounding range
            Datatype* settype = type;                   // Assume we will keep this data-type
            if (settype->getSize() != b->type->getSize()) return false;
            if (settype != b->type)
            {
                Datatype* aTestType = type;
                Datatype* bTestType = b->type;
                while (aTestType->getMetatype() == TYPE_PTR)
                {
                    if (bTestType->getMetatype() != TYPE_PTR)
                        break;
                    aTestType = ((TypePointer*)aTestType)->getPtrTo();
                    bTestType = ((TypePointer*)bTestType)->getPtrTo();
                }
                if (aTestType->getMetatype() == TYPE_UNKNOWN)
                    settype = b->type;
                else if (bTestType->getMetatype() == TYPE_UNKNOWN)
                {
                }
                else if (aTestType->getMetatype() == TYPE_INT && bTestType->getMetatype() == TYPE_UINT)
                {
                }
                else if (aTestType->getMetatype() == TYPE_UINT && bTestType->getMetatype() == TYPE_INT)
                {
                }
                else if (aTestType != bTestType)    // If they are both not unknown, they must be the same
                    return false;
            }
            if ((flags & Varnode::typelock) != 0) return false;
            if ((b->flags & Varnode::typelock) != 0) return false;
            if (flags != b->flags) return false;
            intb diffsz = b->sstart - sstart;
            if ((diffsz % settype->getSize()) != 0) return false;
            diffsz /= settype->getSize();
            if (diffsz > highind) return false;
            type = settype;
            absorb(b);
            return true;
        }

        /// Absorb the other RangeHint into \b this
        /// Absorb details of the other RangeHint into \b this, except for the data-type.  Inherit an \e open range
        /// type and any indexing information. The data-type for \b this is assumed to be compatible and preferred
        /// over the other data-type and is not changed.
        /// \param b is the other RangeHint to absorb
        public void absorb(RangeHint b)
        {
            if (b->rangeType == open && type->getSize() == b->type->getSize())
            {
                rangeType = open;
                if (0 <= b->highind)
                { // If b has array indexing
                    intb diffsz = b->sstart - sstart;
                    diffsz /= type->getSize();
                    int4 trialhi = b->highind + diffsz;
                    if (highind < trialhi)
                        highind = trialhi;
                }
            }
        }

        /// Try to form the union of \b this with another RangeHint
        /// Given that \b this and the other RangeHint intersect, redefine \b this so that it
        /// becomes the union of the two original ranges.  The union must succeed in some form.
        /// An attempt is made to preserve the data-type information of both the original ranges,
        /// but changes will be made if necessary.  An exception is thrown if the data-types
        /// are locked and cannot be reconciled.
        /// \param b is the other RangeHint to merge with \b this
        /// \param space is the address space holding the ranges
        /// \param typeFactory is a factory for producing data-types
        /// \return \b true if there was an overlap that could be reconciled
        public bool merge(RangeHint b, AddrSpace space, TypeFactory typeFactory)
        {
            bool didReconcile;
            int4 resType;       // 0=this, 1=b, 2=confuse

            if (contain(b))
            {           // Does one range contain the other
                didReconcile = reconcile(b);    // Can the data-type layout be reconciled
                if (!didReconcile && start != b->start)
                    resType = 2;
                else
                    resType = preferred(b, didReconcile) ? 0 : 1;
            }
            else
            {
                didReconcile = false;
                resType = ((flags & Varnode::typelock) != 0) ? 0 : 2;
            }
            // Check for really problematic cases
            if (!didReconcile)
            {
                if ((flags & Varnode::typelock) != 0)
                {
                    if ((b->flags & Varnode::typelock) != 0)
                        throw new LowlevelError("Overlapping forced variable types : " + type->getName() + "   " + b->type->getName());
                    if (start != b->start)
                        return false;       // Discard b entirely
                }
            }

            if (resType == 0)
            {
                if (didReconcile)
                    absorb(b);
            }
            else if (resType == 1)
            {
                RangeHint copyRange = *this;
                type = b->type;
                flags = b->flags;
                rangeType = b->rangeType;
                highind = b->highind;
                size = b->size;
                absorb(&copyRange);
            }
            else if (resType == 2)
            {
                // Concede confusion about types, set unknown type rather than this or b's type
                flags = 0;
                rangeType = fixed;
                int4 diff = (int4)(b->sstart - sstart);
                if (diff + b->size > size)
                    size = diff + b->size;
                if (size != 1 && size != 2 && size != 4 && size != 8)
                {
                    size = 1;
                    rangeType = open;
                }
                type = typeFactory->getBase(size, TYPE_UNKNOWN);
                flags = 0;
                highind = -1;
                return false;
            }
            return false;
        }

        /// Order \b this with another RangeHint
        /// Compare (signed) offset, size, RangeType, type lock, and high index, in that order.
        /// Datatype is \e not compared.
        /// \param op2 is the other RangeHint to compare with \b this
        /// \return -1, 0, or 1 depending on if \b this comes before, is equal to, or comes after
        public int4 compare(RangeHint op2)
        {
            if (sstart != op2.sstart)
                return (sstart < op2.sstart) ? -1 : 1;
            if (size != op2.size)
                return (size < op2.size) ? -1 : 1;      // Small sizes come first
            if (rangeType != op2.rangeType)
                return (rangeType < op2.rangeType) ? -1 : 1;
            uint4 thisLock = flags & Varnode::typelock;
            uint4 op2Lock = op2.flags & Varnode::typelock;
            if (thisLock != op2Lock)
                return (thisLock < op2Lock) ? -1 : 1;
            if (highind != op2.highind)
                return (highind < op2.highind) ? -1 : 1;
            return 0;
        }

        /// Compare two RangeHint pointers
        public static bool compareRanges(RangeHint a, RangeHint b)
        {
            return (a->compare(*b) < 0);
        }
    }
}
