using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            : base(sz, TYPE_PARTIALUNION)
        {
            flags |= (needs_resolution | has_stripped);
            stripped = strip;
            container = contain;
            offset = off;
        }

        /// Get the union which \b this is part of
        public TypeUnion getParentUnion() => container;

        public override void printRaw(TextWriter s)
        {
            container.printRaw(s);
            s << "[off=" << dec << offset << ",sz=" << size << ']';
        }

        public override TypeField findTruncation(int off, int sz, PcodeOp op, int slot, int newoff)
        {
            return container.findTruncation(off + offset, sz, op, slot, newoff);
        }

        public override int numDepend() => container.numDepend();

        public override Datatype getDepend(int index)
        {
            // Treat dependents as coming from the underlying union
            Datatype* res = container.getDepend(index);
            if (res.getSize() != size) // But if the size doesn't match
                return stripped;        // Return the stripped data-type
            return res;
        }

        public override int compare(Datatype op, int level)
        {
            int res = Datatype::compare(op, level);
            if (res != 0) return res;
            // Both must be partial unions
            TypePartialUnion* tp = (TypePartialUnion*)&op;
            if (offset != tp.offset) return (offset < tp.offset) ? -1 : 1;
            level -= 1;
            if (level < 0)
            {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            return container.compare(*tp.container, level); // Compare the underlying union
        }

        public override int compareDependency(Datatype op)
        {
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypePartialUnion* tp = (TypePartialUnion*)&op;  // Both must be partial unions
            if (container != tp.container) return (container < tp.container) ? -1 : 1;    // Compare absolute pointers
            if (offset != tp.offset) return (offset < tp.offset) ? -1 : 1;
            return (op.getSize() - size);
        }

        public override Datatype clone() => new TypePartialUnion(this);

        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeSignedInteger(ATTRIB_OFFSET, offset);
            container.encodeRef(encoder);
            encoder.closeElement(ELEM_TYPE);
        }

        public override Datatype getStripped() => stripped;

        public override Datatype resolveInFlow(PcodeOp op, int slot)
        {
            Datatype* curType = container;
            int curOff = offset;
            while (curType != (Datatype*)0 && curType.getSize() > size)
            {
                if (curType.getMetatype() == TYPE_UNION)
                {
                    TypeField field = curType.resolveTruncation(curOff, op, slot, curOff);
                    curType = (field == (TypeField*)0) ? (Datatype*)0 : field.type;
                }
                else
                {
                    ulong newOff;
                    curType = curType.getSubType(curOff, &newOff);
                    curOff = newOff;
                }
            }
            if (curType != (Datatype*)0 && curType.getSize() == size)
                return curType;
            return stripped;
        }

        public override Datatype findResolve(PcodeOp op, int slot)
        {
            Datatype* curType = container;
            int curOff = offset;
            while (curType != (Datatype*)0 && curType.getSize() > size)
            {
                if (curType.getMetatype() == TYPE_UNION)
                {
                    Datatype* newType = curType.findResolve(op, slot);
                    curType = (newType == curType) ? (Datatype*)0 : newType;
                }
                else
                {
                    ulong newOff;
                    curType = curType.getSubType(curOff, &newOff);
                    curOff = newOff;
                }
            }
            if (curType != (Datatype*)0 && curType.getSize() == size)
                return curType;
            return stripped;
        }

        public override int findCompatibleResolve(Datatype ct)
            => container.findCompatibleResolve(ct);

        public override TypeField resolveTruncation(int off, PcodeOp op, int slot, int newoff)
            => container.resolveTruncation(off + offset, op, slot, newoff);
    }
}
