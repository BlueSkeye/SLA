using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A data-type \e resolved from an associated TypeUnion or TypeStruct
    ///
    /// A \b parent refers to either:
    ///   1) A union
    ///   2) A structure that is an effective union (1 field filling the entire structure) OR
    ///   3) A pointer to a union/structure
    ///
    /// This object represents a data-type that is resolved via analysis from the \b parent data-type.
    /// The resolved data-type can be either:
    ///   1) A specific field of the parent (if the parent is not a pointer)
    ///   2) A pointer to a specific field of the underlying union/structure (if the parent is a pointer)
    ///   3) The parent data-type itself (either a pointer or not)
    /// The \b fieldNum (if non-negative) selects a particular field of the underlying union/structure.
    /// If the parent is a pointer, the resolution is a pointer to the field.
    /// If the parent is not a pointer, the resolution is the field itself.
    /// A \b fieldNum of -1 indicates that the parent data-type itself is the resolution.
    internal class ResolvedUnion
    {
        // friend class ScoreUnionFields;
        /// The resolved data-type
        private Datatype resolve;
        /// Union or Structure being resolved
        private Datatype baseType;
        /// Index of field referenced by \b resolve
        private int4 fieldNum;
        /// If \b true, resolution cannot be overridden
        private bool @lock;

        /// Construct a data-type that resolves to itself
        /// The original parent must either be a union, a partial union, a structure with a single field,
        /// an array with a single element, or a pointer to one of these data-types.
        /// The object is set up initially to resolve to the parent.
        /// \param parent is the original parent data-type
        public ResolvedUnion(Datatype parent)
        {
            baseType = parent;
            if (baseType->getMetatype() == TYPE_PTR)
                baseType = ((TypePointer*)baseType)->getPtrTo();
            resolve = parent;
            fieldNum = -1;
            @lock = false;
        }

        /// Construct a reference to a field
        /// The original parent must be a union or structure.
        /// \param parent is the original parent
        /// \param fldNum is the index of the particular field to resolve to (or -1 to resolve to parent)
        /// \param typegrp is a TypeFactory used to construct the resolved data-type of the field
        public ResolvedUnion(Datatype parent, int4 fldNum, TypeFactory typegrp)
        {
            if (parent->getMetatype() == TYPE_PARTIALUNION)
                parent = ((TypePartialUnion*)parent)->getParentUnion();
            baseType = parent;
            fieldNum = fldNum;
            lock = false;
            if (fldNum < 0)
                resolve = parent;
            else
            {
                if (parent->getMetatype() == TYPE_PTR)
                {
                    TypePointer* pointer = (TypePointer*)parent;
                    Datatype* field = pointer->getPtrTo()->getDepend(fldNum);
                    resolve = typegrp.getTypePointer(parent->getSize(), field, pointer->getWordSize());
                }
                else
                    resolve = parent->getDepend(fldNum);
            }
        }

        /// Get the resolved data-type
        public Datatype getDatatype() => resolve;

        /// Get the union or structure being referenced
        public Datatype getBase() => baseType;

        /// Get the index of the resolved field or -1
        public int4 getFieldNum() => fieldNum;

        /// Is \b this locked against overrides
        public bool isLocked() => @lock;

        /// Set whether \b this resolution is locked against overrides
        public void setLock(bool val)
        {
            @lock = val;
        }
    }
}
