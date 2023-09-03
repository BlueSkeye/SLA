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
    /// \brief A data-type that holds \e part of a TypeStruct or TypeArray
    internal class TypePartialStruct : Datatype
    {
        // friend class TypeFactory;
        /// The \e undefined data-type to use if a formal data-type is required.
        private Datatype stripped;
        /// Parent structure or array of which \b this is a part
        private Datatype container;
        /// Byte offset within the parent where \b this starts
        private int offset;

        /// Construct from another TypePartialStruct
        public TypePartialStruct(TypePartialStruct op)
            : base(op)
        {
            stripped = op.stripped;
            container = op.container;
            offset = op.offset;
        }

        internal TypePartialStruct(Datatype contain, int off, int sz, Datatype strip)
            : base(sz, type_metatype.TYPE_PARTIALSTRUCT)
        {
#if CPUI_DEBUG
            if (contain.getMetatype() != type_metatype.TYPE_STRUCT && contain.getMetatype() != type_metatype.TYPE_ARRAY)
                throw new LowlevelError("Parent of partial struct is not a struture or array");
#endif
            flags |= Properties.has_stripped;
            stripped = strip;
            container = contain;
            offset = off;
        }

        /// Get the data-type containing \b this piece
        internal Datatype getParent() => container;

        public override void printRaw(TextWriter s)
        {
            container.printRaw(s);
            s.Write($"[off={offset},sz={size}]");
        }

        public override Datatype? getSubType(ulong off, out ulong newoff)
        {
            int sizeLeft = (size - (int)off);
            off += (uint)offset;
            Datatype? ct = container;
            do {
                ct = ct.getSubType(off, out newoff);
                if (ct == (Datatype)null)
                    break;
                off = newoff;
                // Component can extend beyond range of this partial, in which case we go down another level
            } while (ct.getSize() - (int)off > sizeLeft);
            return ct;
        }

        public override int getHoleSize(int off)
        {
            int sizeLeft = size - off;
            off += offset;
            int res = container.getHoleSize(off);
            if (res > sizeLeft)
                res = sizeLeft;
            return res;
        }

        public override int compare(Datatype op, int level)
        {
            int res = base.compare(op, level);
            if (res != 0) return res;
            // Both must be partial
            TypePartialStruct tp = (TypePartialStruct)op;
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
            // Both must be partial
            TypePartialStruct tp = (TypePartialStruct)op;
            if (container != tp.container)
                // Compare absolute pointers
                return container.CompareTo(tp.container);
            return (offset != tp.offset) 
                ? (offset < tp.offset) ? -1 : 1
                : (op.getSize() - size);
        }

        internal override Datatype clone() => new TypePartialStruct(this);

        public override Datatype getStripped() => stripped;
    }
}
