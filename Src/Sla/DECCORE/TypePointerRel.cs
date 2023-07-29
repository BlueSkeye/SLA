using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static ghidra.FuncCallSpecs;

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
        protected int4 offset;

        /// \brief Mark \b this as an ephemeral data-type, to be replaced in the final output
        ///
        /// A \e base data-type is cached, which is a stripped version of the relative pointer, leaving
        /// just a plain TypePointer object with the same underlying \b ptrto.  The base data-type
        /// replaces \b this relative pointer for formal variable declarations in source code output.
        /// This TypePointerRel is not considered a formal data-type but is only used to provide extra
        /// context for the pointer during propagation.
        /// \param typegrp is the factory from which to fetch the base pointer
        protected void markEphemeral(TypeFactory typegrp)
        {
            stripped = typegrp.getTypePointer(size, ptrto, wordsize);
            flags |= has_stripped;
            // An ephemeral relative pointer that points to something unknown, propagates slightly
            // differently than a formal relative pointer
            if (ptrto.getMetatype() == TYPE_UNKNOWN)
                submeta = SUB_PTRREL_UNK;
        }

        /// Restore \b this relative pointer data-type from a stream
        /// Parse a \<type> element with children describing the data-type being pointed to
        /// and the parent data-type.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        protected void decode(Decoder decoder, TypeFactory typegrp)
        {
            //  uint4 elemId = decoder.openElement();
            flags |= is_ptrrel;
            decodeBasic(decoder);
            metatype = TYPE_PTR;        // Don't use TYPE_PTRREL internally
            decoder.rewindAttributes();
            for (; ; )
            {
                uint4 attrib = decoder.getNextAttributeId();
                if (attrib == 0) break;
                if (attrib == ATTRIB_WORDSIZE)
                {
                    wordsize = decoder.readUnsignedInteger();
                }
                else if (attrib == ATTRIB_SPACE)
                {
                    spaceid = decoder.readSpace();
                }
            }
            ptrto = typegrp.decodeType(decoder);
            parent = typegrp.decodeType(decoder);
            uint4 subId = decoder.openElement(ELEM_OFF);
            offset = decoder.readSignedInteger(ATTRIB_CONTENT);
            decoder.closeElement(subId);
            if (offset == 0)
                throw new LowlevelError("For metatype=\"ptrstruct\", <off> tag must not be zero");
            submeta = SUB_PTRREL;
            if (name.size() == 0)       // If the data-type is not named
                markEphemeral(typegrp); // it is considered ephemeral
                                        //  decoder.closeElement(elemId);
        }

        /// Internal constructor for decode
        protected TypePointerRel()
            : base()
        {
            offset = 0;
            parent = (Datatype*)0;
            stripped = (TypePointer*)0;
            submeta = SUB_PTRREL;
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
        public TypePointerRel(int4 sz, Datatype pt, uint4 ws, Datatype par, int4 off)
            : base(sz, pt, ws)
        {
            parent = par; 
            offset = off;
            stripped = (TypePointer*)0;
            flags |= is_ptrrel;
            submeta = SUB_PTRREL;
        }

        /// Get the parent data-type to which \b this pointer is offset
        public Datatype getParent() => parent;

        /// Do we display given address offset as coming from the parent data-type
        /// For a variable that is a relative pointer, constant offsets relative to the variable can be
        /// displayed either as coming from the variable itself or from the parent object.
        /// \param addrOff is the given offset in address units
        /// \return \b true if the variable should be displayed as coming from the parent
        public bool evaluateThruParent(uintb addrOff)
        {
            uintb byteOff = AddrSpace::addressToByte(addrOff, wordsize);
            if (ptrto.getMetatype() == TYPE_STRUCT && byteOff < ptrto.getSize())
                return false;
            byteOff = (byteOff + offset) & calc_mask(size);
            return (byteOff < parent.getSize());
        }

        /// \brief Get offset of \b this pointer relative to start of the containing data-type
        ///
        /// \return the offset value in \e address \e units
        public int4 getPointerOffset() => AddrSpace::byteToAddressInt(offset, wordsize);

        public override void printRaw(TextWriter s)
        {
            ptrto.printRaw(s);
            s << " *+";
            s << dec << offset;
            s << '[';
            parent.printRaw(s);
            s << ']';
        }

        public override int4 compare(Datatype op, int4 level)
        {
            int4 res = TypePointer::compare(op, level); // Compare as plain pointers first
            if (res != 0) return res;
            // Both must be relative pointers
            TypePointerRel* tp = (TypePointerRel*)&op;
            // Its possible a formal relative pointer gets compared to its equivalent ephemeral version.
            // In which case, we prefer the formal version.
            if (stripped == (TypePointer*)0)
            {
                if (tp.stripped != (TypePointer*)0)
                    return -1;
            }
            else
            {
                if (tp.stripped == (TypePointer*)0)
                    return 1;
            }
            return 0;
        }

        public override int4 compareDependency(Datatype op)
        {
            if (submeta != op.getSubMeta()) return (submeta < op.getSubMeta()) ? -1 : 1;
            TypePointerRel tp = (TypePointerRel*)&op;  // Both must be TypePointerRel
            if (ptrto != tp.ptrto) return (ptrto < tp.ptrto) ? -1 : 1;    // Compare absolute pointers
            if (offset != tp.offset) return (offset < tp.offset) ? -1 : 1;
            if (parent != tp.parent) return (parent < tp.parent) ? -1 : 1;

            if (wordsize != tp.wordsize) return (wordsize < tp.wordsize) ? -1 : 1;
            return (op.getSize() - size);
        }

        public override Datatype clone() => new TypePointerRel(this);

        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_TYPE);
            encodeBasic(TYPE_PTRREL, encoder);  // Override the metatype for XML
            if (wordsize != 1)
                encoder.writeUnsignedInteger(ATTRIB_WORDSIZE, wordsize);
            ptrto.encode(encoder);
            parent.encodeRef(encoder);
            encoder.openElement(ELEM_OFF);
            encoder.writeSignedInteger(ATTRIB_CONTENT, offset);
            encoder.closeElement(ELEM_OFF);
            encoder.closeElement(ELEM_TYPE);
        }

        public override TypePointer downChain(uintb off, TypePointer par, uintb parOff, bool allowArrayWrap,
            TypeFactory typegrp)
        {
            type_metatype ptrtoMeta = ptrto.getMetatype();
            if (off < ptrto.getSize() && (ptrtoMeta == TYPE_STRUCT || ptrtoMeta == TYPE_ARRAY))
            {
                return TypePointer::downChain(off, par, parOff, allowArrayWrap, typegrp);
            }
            uintb relOff = (off + offset) & calc_mask(size);        // Convert off to be relative to the parent container
            if (relOff >= parent.getSize())
                return (TypePointer*)0;         // Don't let pointer shift beyond original container

            TypePointer* origPointer = typegrp.getTypePointer(size, parent, wordsize);
            off = relOff;
            if (relOff == 0 && offset != 0) // Recovering the start of the parent is still downchaining, even though the parent may be the container
                return origPointer; // So we return the pointer to the parent and don't drill down to field at offset 0
            return origPointer.downChain(off, par, parOff, allowArrayWrap, typegrp);
        }

        public override bool isPtrsubMatching(uintb off)
        {
            if (stripped != (TypePointer*)0)
                return TypePointer::isPtrsubMatching(off);
            int4 iOff = AddrSpace::addressToByteInt((int4)off, wordsize);
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
        public static Datatype getPtrToFromParent(Datatype @base, int4 off, TypeFactory typegrp)
        {
            if (off > 0)
            {
                uintb curoff = off;
                do
                {
                    @base = @base.getSubType(curoff, &curoff);
                } while (curoff != 0 && @base != (Datatype*)0);
                if (@base == (Datatype*)0)
                    @base = typegrp.getBase(1, TYPE_UNKNOWN);
            }
            else
                @base = typegrp.getBase(1, TYPE_UNKNOWN);
            return @base;
        }
    }
}
