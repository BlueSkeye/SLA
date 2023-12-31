﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief An internal data-type for holding information about a variable's relative position within a union data-type
    ///
    /// This is a data-type that can be assigned to a Varnode offset into a Symbol, where either the Symbol itself or
    /// a sub-field is a TypeUnion. In these cases, we know the Varnode is properly contained within a TypeUnion,
    /// but the lack of context prevents us from deciding which field of the TypeUnion applies (and possibly
    /// the sub-field of the field).
    internal class TypePartialUnion : Datatype
    {
        // friend class TypeFactory;
        /// The \e undefined data-type to use if a formal data-type is required.
        protected Datatype stripped;
        /// Union data-type containing \b this partial data-type
        protected TypeUnion container;
        /// Offset (in bytes) into the \e container union
        protected int offset;

        /// Construct from another TypePartialUnion
        public TypePartialUnion(TypePartialUnion op)
            : base(op)
        {
            stripped = op.stripped;
            container = op.container;
            offset = op.offset;
        }

        public TypePartialUnion(TypeUnion contain, int off, int sz, Datatype strip)
            : base(sz, type_metatype.TYPE_PARTIALUNION)
        {
            flags |= (Properties.needs_resolution | Properties.has_stripped);
            stripped = strip;
            container = contain;
            offset = off;
        }

        /// Get the union which \b this is part of
        public TypeUnion getParentUnion() => container;

        public override void printRaw(TextWriter s)
        {
            container.printRaw(s);
            s.Write($"[off={offset},sz={size}]");
        }

        public override TypeField? findTruncation(int off, int sz, PcodeOp op, int slot,
            out int newoff)
        {
            return container.findTruncation(off + offset, sz, op, slot, out newoff);
        }

        public override int numDepend() => container.numDepend();

        public override Datatype getDepend(int index)
        {
            // Treat dependents as coming from the underlying union
            Datatype res = container.getDepend(index);
            if (res.getSize() != size) // But if the size doesn't match
                return stripped;        // Return the stripped data-type
            return res;
        }

        public override int compare(Datatype op, int level)
        {
            int res = base.compare(op, level);
            if (res != 0) return res;
            // Both must be partial unions
            TypePartialUnion tp = (TypePartialUnion)op;
            if (offset != tp.offset) return (offset < tp.offset) ? -1 : 1;
            level -= 1;
            if (level < 0) {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            return container.compare(tp.container, level); // Compare the underlying union
        }

        public override int compareDependency(Datatype op)
        {
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypePartialUnion tp = (TypePartialUnion)op;  // Both must be partial unions
            // Compare absolute pointers
            if (container != tp.container) return (container < tp.container) ? -1 : 1;
            if (offset != tp.offset) return (offset < tp.offset) ? -1 : 1;
            return (op.getSize() - size);
        }

        internal override Datatype clone() => new TypePartialUnion(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeSignedInteger(AttributeId.ATTRIB_OFFSET, offset);
            container.encodeRef(encoder);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }

        public override Datatype getStripped() => stripped;

        public override Datatype resolveInFlow(PcodeOp op, int slot)
        {
            Datatype curType = container;
            int curOff = offset;
            while (curType != (Datatype)null && curType.getSize() > size) {
                if (curType.getMetatype() == type_metatype.TYPE_UNION) {
                    TypeField field = curType.resolveTruncation(curOff, op, slot, out curOff);
                    curType = (field == (TypeField)null) ? (Datatype)null : field.type;
                }
                else {
                    ulong newOff;
                    curType = curType.getSubType((ulong)curOff, out newOff);
                    curOff = (int)newOff;
                }
            }
            if (curType != (Datatype)null && curType.getSize() == size)
                return curType;
            return stripped;
        }

        public override Datatype findResolve(PcodeOp op, int slot)
        {
            Datatype curType = container;
            int curOff = offset;
            while (curType != (Datatype)null && curType.getSize() > size) {
                if (curType.getMetatype() == type_metatype.TYPE_UNION) {
                    Datatype newType = curType.findResolve(op, slot);
                    curType = (newType == curType) ? (Datatype)null : newType;
                }
                else {
                    ulong newOff;
                    curType = curType.getSubType((ulong)curOff, out newOff);
                    curOff = (int)newOff;
                }
            }
            if (curType != (Datatype)null && curType.getSize() == size)
                return curType;
            return stripped;
        }

        public override int findCompatibleResolve(Datatype ct)
            => container.findCompatibleResolve(ct);

        public override TypeField? resolveTruncation(int off, PcodeOp op, int slot, out int newoff)
            => container.resolveTruncation(off + offset, op, slot, out newoff);
    }
}
