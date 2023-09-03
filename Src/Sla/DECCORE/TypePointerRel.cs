using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Relative pointer: A pointer with a fixed offset into a specific structure or other data-type
    ///
    /// The other data-type, the \b container, is typically a TypeStruct or TypeArray.  Even though \b this pointer
    /// does not point directly to the start of the container, it is possible to access the container through \b this,
    /// as the distance (the \b offset) to the start of the container is explicitly known.
    internal class TypePointerRel : TypePointer
    {
        /// Same data-type with container info stripped
        protected TypePointer stripped;
        /// Parent structure or array which \b this is pointing into
        protected Datatype parent;
        /// Byte offset within the parent where \b this points to
        protected int offset;

        /// \brief Mark \b this as an ephemeral data-type, to be replaced in the final output
        ///
        /// A \e base data-type is cached, which is a stripped version of the relative pointer, leaving
        /// just a plain TypePointer object with the same underlying \b ptrto.  The base data-type
        /// replaces \b this relative pointer for formal variable declarations in source code output.
        /// This TypePointerRel is not considered a formal data-type but is only used to provide extra
        /// context for the pointer during propagation.
        /// \param typegrp is the factory from which to fetch the base pointer
        internal void markEphemeral(TypeFactory typegrp)
        {
            stripped = typegrp.getTypePointer(size, ptrto, wordsize);
            flags |= Properties.has_stripped;
            // An ephemeral relative pointer that points to something unknown, propagates slightly
            // differently than a formal relative pointer
            if (ptrto.getMetatype() == type_metatype.TYPE_UNKNOWN)
                submeta = sub_metatype.SUB_PTRREL_UNK;
        }

        /// Restore \b this relative pointer data-type from a stream
        /// Parse a \<type> element with children describing the data-type being pointed to
        /// and the parent data-type.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        internal override void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            //  uint elemId = decoder.openElement();
            flags |= Properties.is_ptrrel;
            decodeBasic(decoder);
            metatype = type_metatype.TYPE_PTR;        // Don't use type_metatype.TYPE_PTRREL internally
            decoder.rewindAttributes();
            while(true) {
                AttributeId attrib = decoder.getNextAttributeId();
                if (attrib == 0) break;
                if (attrib == AttributeId.ATTRIB_WORDSIZE) {
                    wordsize = (uint)decoder.readUnsignedInteger();
                }
                else if (attrib == AttributeId.ATTRIB_SPACE) {
                    spaceid = decoder.readSpace();
                }
            }
            ptrto = typegrp.decodeType(decoder);
            parent = typegrp.decodeType(decoder);
            uint subId = decoder.openElement(ElementId.ELEM_OFF);
            offset = (int)decoder.readSignedInteger(AttributeId.ATTRIB_CONTENT);
            decoder.closeElement(subId);
            if (offset == 0)
                throw new LowlevelError("For metatype=\"ptrstruct\", <off> tag must not be zero");
            submeta = sub_metatype.SUB_PTRREL;
            if (name.Length == 0)       // If the data-type is not named
                markEphemeral(typegrp); // it is considered ephemeral
                                        //  decoder.closeElement(elemId);
        }

        /// Internal constructor for decode
        internal TypePointerRel()
            : base()
        {
            offset = 0;
            parent = (Datatype)null;
            stripped = (TypePointer)null;
            submeta = sub_metatype.SUB_PTRREL;
        }

        /// Construct from another TypePointerRel
        ///< Restore \b this relative pointer data-type from a stream
        public TypePointerRel(TypePointerRel op)
            : base((TypePointer)op)
        {
            offset = op.offset;
            parent = op.parent;
            stripped = op.stripped;
        }
        
        /// Construct given a size, pointed-to type, parent, and offset
        public TypePointerRel(int sz, Datatype pt, uint ws, Datatype par, int off)
            : base(sz, pt, ws)
        {
            parent = par; 
            offset = off;
            stripped = (TypePointer)null;
            flags |= Properties.is_ptrrel;
            submeta = sub_metatype.SUB_PTRREL;
        }

        /// Get the parent data-type to which \b this pointer is offset
        public Datatype getParent() => parent;

        /// Do we display given address offset as coming from the parent data-type
        /// For a variable that is a relative pointer, constant offsets relative to the variable can be
        /// displayed either as coming from the variable itself or from the parent object.
        /// \param addrOff is the given offset in address units
        /// \return \b true if the variable should be displayed as coming from the parent
        public bool evaluateThruParent(ulong addrOff)
        {
            ulong byteOff = AddrSpace.addressToByte(addrOff, wordsize);
            if (ptrto.getMetatype() == type_metatype.TYPE_STRUCT && byteOff < ptrto.getSize())
                return false;
            byteOff = (byteOff + offset) & Globals.calc_mask(size);
            return (byteOff < parent.getSize());
        }

        /// \brief Get offset of \b this pointer relative to start of the containing data-type
        ///
        /// \return the offset value in \e address \e units
        public int getPointerOffset() => AddrSpace.byteToAddressInt(offset, wordsize);

        public override void printRaw(TextWriter s)
        {
            ptrto.printRaw(s);
            s.Write($" *+{offset}[");
            parent.printRaw(s);
            s.Write(']');
        }

        public override int compare(Datatype op, int level)
        {
            int res = base.compare(op, level); // Compare as plain pointers first
            if (res != 0) return res;
            // Both must be relative pointers
            TypePointerRel tp = (TypePointerRel)op;
            // Its possible a formal relative pointer gets compared to its equivalent ephemeral version.
            // In which case, we prefer the formal version.
            if (stripped == (TypePointer)null) {
                if (tp.stripped != (TypePointer)null)
                    return -1;
            }
            else {
                if (tp.stripped == (TypePointer)null)
                    return 1;
            }
            return 0;
        }

        public override int compareDependency(Datatype op)
        {
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypePointerRel tp = (TypePointerRel)op;  // Both must be TypePointerRel
            if (ptrto != tp.ptrto) return (ptrto < tp.ptrto) ? -1 : 1;    // Compare absolute pointers
            if (offset != tp.offset) return (offset < tp.offset) ? -1 : 1;
            if (parent != tp.parent) return (parent < tp.parent) ? -1 : 1;

            if (wordsize != tp.wordsize) return (wordsize < tp.wordsize) ? -1 : 1;
            return (op.getSize() - size);
        }

        internal override Datatype clone() => new TypePointerRel(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(type_metatype.TYPE_PTRREL, encoder);  // Override the metatype for XML
            if (wordsize != 1)
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_WORDSIZE, wordsize);
            ptrto.encode(encoder);
            parent.encodeRef(encoder);
            encoder.openElement(ElementId.ELEM_OFF);
            encoder.writeSignedInteger(AttributeId.ATTRIB_CONTENT, offset);
            encoder.closeElement(ElementId.ELEM_OFF);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }

        public override TypePointer? downChain(ulong off, out TypePointer? par, out ulong parOff,
            bool allowArrayWrap, TypeFactory typegrp)
        {
            type_metatype ptrtoMeta = ptrto.getMetatype();
            if (   off < (uint)ptrto.getSize()
                && (ptrtoMeta == type_metatype.TYPE_STRUCT || ptrtoMeta == type_metatype.TYPE_ARRAY))
            {
                return base.downChain(off, out par, out parOff, allowArrayWrap, typegrp);
            }
            // Convert off to be relative to the parent container
            ulong relOff = (off + (uint)offset) & Globals.calc_mask((uint)size);
            if (relOff >= (uint)parent.getSize()) {
                par = null;
                parOff = 0;
                // Don't let pointer shift beyond original container
                return (TypePointer)null;
            }
            TypePointer origPointer = typegrp.getTypePointer(size, parent, wordsize);
            off = relOff;
            // Recovering the start of the parent is still downchaining, even though the parent may be the container
            par = null;
            parOff = 0;
            return (relOff == 0 && offset != 0)
                // So we return the pointer to the parent and don't drill down to field at offset 0
                ? origPointer
                : origPointer.downChain(off, out par, out parOff, allowArrayWrap, typegrp);
        }

        public override bool isPtrsubMatching(ulong off)
        {
            if (stripped != (TypePointer)null)
                return base.isPtrsubMatching(off);
            int iOff = AddrSpace.addressToByteInt((int)off, wordsize);
            iOff += offset;
            return (iOff >= 0 && iOff <= parent.getSize());
        }

        /// Get the plain form of the pointer
        public override Datatype getStripped() => stripped;

        /// \brief Given a containing data-type and offset, find the "pointed to" data-type suitable for a TypePointerRel
        ///
        /// The biggest contained data-type that starts at the exact offset is returned. If the offset is negative
        /// or the is no data-type starting exactly there, an \b xunknown1 data-type is returned.
        /// \param base is the given container data-type
        /// \param off is the offset relative to the start of the container
        /// \param typegrp is the factory owning the data-types
        /// \return the "pointed to" data-type
        public static Datatype getPtrToFromParent(Datatype @base, int off, TypeFactory typegrp)
        {
            if (off > 0) {
                ulong curoff = (ulong)off;
                do {
                    @base = @base.getSubType(curoff, out curoff);
                } while (curoff != 0 && @base != (Datatype)null);
                if (@base == (Datatype)null)
                    @base = typegrp.getBase(1, type_metatype.TYPE_UNKNOWN);
            }
            else
                @base = typegrp.getBase(1, type_metatype.TYPE_UNKNOWN);
            return @base;
        }
    }
}
