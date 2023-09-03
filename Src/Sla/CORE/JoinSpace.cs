using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief The pool of logically joined variables
    /// Some logical variables are split across non-contiguous regions of memory. This space
    /// creates a virtual place for these logical variables to exist.  Any memory location within this
    /// space is backed by 2 or more memory locations in other spaces that physically hold the pieces
    /// of the logical value. The database controlling symbols is responsible for keeping track of
    /// mapping the logical address in this space to its physical pieces.  Offsets into this space do not
    /// have an absolute meaning, the database may vary what offset is assigned to what set of pieces.
    public class JoinSpace : AddrSpace
    {
        ///< Maximum number of pieces that can be marshaled in one \e join address
        public const int MAX_PIECES = 64;
        ///< Reserved name for the join space
        public const string NAME = "join";

        /// This is the constructor for the \b join space, which is automatically constructed by the
        /// analysis engine, and constructed only once. The name should always be \b join.
        /// \param m is the associated address space manager
        /// \param t is the associated processor translator
        /// \param ind is the integer identifier
        public JoinSpace(AddrSpaceManager m, Translate t, int ind)
            : base(m, t, spacetype.IPTR_JOIN, NAME, sizeof(uint), 1, ind, 0, 0)
        {
            // This is a virtual space
            // setFlags(hasphysical);
            // This space is never heritaged, but does dead-code analysis
            clearFlags(Properties.heritaged);
        }

        // virtual int overlapJoin(ulong offset, int size, AddrSpace* pointSpace, ulong pointOff, int pointSkip) const;

        /// Encode a \e join address to the stream.  This method in the interface only
        /// outputs attributes for a single element, so we are forced to encode what should probably
        /// be recursive elements into an attribute.
        /// \param encoder is the stream encoder
        /// \param offset is the offset within the address space to encode
        public override void encodeAttributes(Encoder encoder, ulong offset)
        {
            // Record must already exist
            JoinRecord rec = getManager().findJoin(offset);
            encoder.writeSpace(AttributeId.ATTRIB_SPACE, this);
            int num = rec.numPieces();
            if (num > MAX_PIECES) {
                throw new LowlevelError("Exceeded maximum pieces in one join address");
            }
            for (uint i = 0; i < num; ++i) {
                VarnodeData vdata = rec.getPiece(i);
                StringBuilder t = new StringBuilder();
                t.Append(vdata.space.getName());
                t.AppendFormat(":0x{0:X}:{1}", vdata.offset, vdata.size);
                encoder.writeStringIndexed(AttributeId.ATTRIB_PIECE, i, t.ToString());
            }
            if (num == 1) {
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_LOGICALSIZE, rec.getUnified().size);
            }
        }

        /// Encode a \e join address to the stream.  This method in the interface only
        /// outputs attributes for a single element, so we are forced to encode what should probably
        /// be recursive elements into an attribute.
        /// \param encoder is the stream encoder
        /// \param offset is the offset within the address space to encode
        /// \param size is the size of the memory location being encoded
        public override void encodeAttributes(Encoder encoder, ulong offset, int size)
        {
            encodeAttributes(encoder, offset);    // Ignore size
        }

        /// Parse a join address the current element.  Pieces of the join are encoded as a sequence
        /// of attributes.  The Translate::findAddJoin method is used to construct a logical
        /// address within the join space.
        /// \param decoder is the stream decoder
        /// \param size is a reference to be filled in as the size encoded by the tag
        /// \return the offset of the final address encoded by the tag
        public override ulong decodeAttributes(Decoder decoder, out uint size)
        {
            List<VarnodeData> pieces = new List<VarnodeData>();
            uint sizesum = 0;
            uint logicalsize = 0;
            while(true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_LOGICALSIZE) {
                    logicalsize = (uint)decoder.readUnsignedInteger();
                    continue;
                }
                else if (attribId == AttributeId.ATTRIB_UNKNOWN) {
                    attribId = decoder.getIndexedAttributeId(AttributeId.ATTRIB_PIECE);
                }
                if (attribId < AttributeId.ATTRIB_PIECE.getId()) {
                    continue;
                }
                int pos = (int)(attribId - AttributeId.ATTRIB_PIECE.getId());
                if (pos > MAX_PIECES) {
                    continue;
                }
                VarnodeData vdat = pieces[pos];
                while (pieces.Count <= pos) {
                    pieces.Add(vdat = new VarnodeData());
                }

                string attrVal = decoder.readString();
                int offpos = attrVal.IndexOf(':');
                if (-1 == offpos) {
                    Translate tr = getTrans();
                    VarnodeData point = tr.getRegister(attrVal);
                    vdat = point;
                }
                else {
                    int szpos = attrVal.IndexOf(':', offpos + 1);
                    if (-1 == szpos) {
                        throw new LowlevelError("join address piece attribute is malformed");
                    }
                    string spcname = attrVal.Substring(0, offpos);
                    vdat.space = getManager().getSpaceByName(spcname)
                        ?? throw new BugException();
                    StreamReader s1 = new StreamReader(attrVal.Substring(offpos + 1, szpos));
                    // s1.unsetf(ios::dec | ios::hex | ios::oct);
                    vdat.offset = s1.ReadDecimalUnsignedLongInteger();
                    StreamReader s2 = new StreamReader(attrVal.Substring(szpos + 1));
                    // s2.unsetf(ios::dec | ios::hex | ios::oct);
                    vdat.size = s2.ReadDecimalUnsignedInteger();
                }
                sizesum += vdat.size;
            }
            JoinRecord rec = getManager().findAddJoin(pieces, logicalsize);
            size = rec.getUnified().size;
            return rec.getUnified().offset;
        }

        public override int overlapJoin(ulong offset, int size, AddrSpace pointSpace,
            ulong pointOffset, int pointSkip)
        {
            if (this == pointSpace) {
                // If the point is in the join space, translate the point into the piece address space
                JoinRecord pieceRecord = getManager().findJoin(pointOffset);
                int pos;
                if (0 > pointSkip) {
                    throw new BugException();
                }
                Address addr = pieceRecord.getEquivalentAddress(pointOffset + (uint)pointSkip,
                    out pos);
                pointSpace = addr.getSpace();
                pointOffset = addr.getOffset();
            }
            else {
                if (pointSpace.getType() == spacetype.IPTR_CONSTANT) {
                    return -1;
                }
                if (0 > pointSkip) {
                    throw new BugException();
                }
                pointOffset = pointSpace.wrapOffset(pointOffset + (uint)pointSkip);
            }
            JoinRecord joinRecord = getManager().findJoin(offset);
            // Set up so we traverse pieces in data order
            int startPiece, endPiece, dir;
            if (isBigEndian()) {
                startPiece = 0;
                endPiece = joinRecord.numPieces();
                dir = 1;
            }
            else {
                startPiece = joinRecord.numPieces() - 1;
                endPiece = -1;
                dir = -1;
            }
            int bytesAccum = 0;
            for (int i = startPiece; i != endPiece; i += dir) {
                VarnodeData vData = joinRecord.getPiece((uint)i);
                if (vData.space == pointSpace
                    && pointOffset >= vData.offset
                    && pointOffset <= vData.offset + (vData.size - 1))
                {
                    int res = (int)(pointOffset - vData.offset) + bytesAccum;
                    return (res >= size) ? -1 : res;
                }
                bytesAccum += (int)vData.size;
            }
            return -1;
        }

        public override void printRaw(TextWriter s, ulong offset)
        {
            JoinRecord rec = getManager().findJoin(offset);
            int szsum = 0;
            int num = rec.numPieces();
            s.Write('{');
            for (uint i = 0; i < num; ++i) {
                VarnodeData vdat = rec.getPiece(i);
                szsum += (int)vdat.size;
                if (i != 0) {
                    s.Write(',');
                }
                vdat.space.printRaw(s, vdat.offset);
            }
            if (num == 1) {
                szsum = (int)rec.getUnified().size;
                s.Write(':');
                s.Write(szsum);
            }
            s.Write('}');
        }

        public override ulong read(string s, out int size)
        {
            List<VarnodeData> pieces = new List<VarnodeData>();
            int szsum = 0;
            int i = 0;
            while (i < s.Length) {
                // Prepare to read next VarnodeData
                pieces.Add(new VarnodeData());
                string token = string.Empty;
                while ((i < s.Length) && (s[i] != ',')) {
                    token += s[i];
                    i += 1;
                }
                // Skip the comma
                i += 1;
                VarnodeData lastnode;
                try {
                    pieces.Add(lastnode = getTrans().getRegister(token));
                }
                catch (LowlevelError) {
                    // Name doesn't exist
                    char tryShortcut = token[0];
                    AddrSpace spc = getManager().getSpaceByShortcut(tryShortcut)
                        ?? throw new BugException();
                    if (spc == null) {
                        throw new LowlevelError("Could not parse join string");
                    }
                    int subsize;
                    lastnode = pieces[pieces.Count - 1];
                    lastnode.space = spc;
                    lastnode.offset = spc.read(token.Substring(1), out subsize);
                    lastnode.size = (uint)subsize;
                }
                szsum += (int)lastnode.size;
            }
            JoinRecord rec = getManager().findAddJoin(pieces, 0);
            size = szsum;
            return rec.getUnified().offset;
        }

        public override void saveXml(TextWriter s)
        {
            throw new LowlevelError("Should never save join space to XML");
        }

        public virtual void decode(ref Decoder decoder)
        {
            throw new LowlevelError("Should never decode join space");
        }
    }
}
