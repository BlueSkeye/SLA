/* ###
 * IP: GHIDRA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
//#include "translate.hh"

using System.Numerics;
using System;
using System.Text;
using ghidra;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;
using System.ComponentModel;
using System.Runtime.Intrinsics;
using System.Diagnostics;
using System.Drawing;
using static System.Formats.Asn1.AsnWriter;
using System.Xml.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Sla.CORE {

    /// \brief Exception for encountering unimplemented pcode
    /// This error is thrown when a particular machine instruction
    /// cannot be translated into pcode. This particular error
    /// means that the particular instruction being decoded was valid,
    /// but the system doesn't know how to represent it in pcode.
    public class UnimplError : LowlevelError
    {
        ///< Number of bytes in the unimplemented instruction
        private int instruction_length;

        /// \brief Constructor
        /// \param s is a more verbose description of the error
        /// \param l is the length (in bytes) of the unimplemented instruction
        public UnimplError(string s, int l)
            : base(s)
        {
            instruction_length = l;
        }

        public UnimplError(string s, int l, Exception innerException)
            : base(s, innerException)
        {
            instruction_length = l;
        }
    }


    /// \brief Exception for bad instruction data
    /// This error is thrown when the system cannot decode data
    /// for a particular instruction.  This usually means that the
    /// data is not really a machine instruction, but may indicate
    /// that the system is unaware of the particular instruction.
    public class BadDataError : LowlevelError
    {
        /// \brief Constructor
        /// \param s is a more verbose description of the error
        public BadDataError(ref string s)
            : base(s)
        {
        }
    }

    /// \brief Object for describing how a space should be truncated
    ///
    /// This can turn up in various XML configuration files and essentially acts
    /// as a command to override the size of an address space as defined by the architecture
    public class TruncationTag
    {
        ///< Name of space to be truncated
        private string spaceName;
        ///< Size truncated addresses into the space
        private uint size;

        /// Parse a \<truncate_space> element to configure \b this object
        /// \param decoder is the stream decoder
        ///< Restore \b this from a stream
        public void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_TRUNCATE_SPACE);
            spaceName = decoder.readString(AttributeId.ATTRIB_SPACE);
            size = (uint)decoder.readUnsignedInteger(AttributeId.ATTRIB_SIZE);
            decoder.closeElement(elemId);
        }

        ///< Get name of address space being truncated
        public string getName() => spaceName;

        ///< Size (of pointers) for new truncated space
        public uint getSize() => size;
    }

    /// \brief Abstract class for emitting pcode to an application
    /// Translation engines pass back the generated pcode for an
    /// instruction to the application using this class.
    public abstract class PcodeEmit
    {
        ///< Virtual destructor
        ~PcodeEmit()
        {
        }

        /// \brief The main pcode emit method.
        /// A single pcode instruction is returned to the application via this method.
        ///  Particular applications override it to tailor how the operations are used.
        /// \param addr is the Address of the machine instruction
        /// \param opc is the opcode of the particular pcode instruction
        /// \param outvar if not \e null is a pointer to data about the output varnode
        /// \param vars is a pointer to an array of VarnodeData for each input varnode
        /// \param isize is the number of input varnodes
        public abstract void dump(Address addr, OpCode opc, VarnodeData? outvar,
            VarnodeData[] vars, int isize);

        /// Emit pcode directly from an \<op> element
        /// A convenience method for passing around p-code operations via stream.
        /// A single p-code operation is parsed from an \<op> element and
        /// returned to the application via the PcodeEmit::dump method.
        /// \param addr is the address (of the instruction) to associate with the p-code op
        /// \param decoder is the stream decoder
        public void decodeOp(ref Address addr, ref Decoder decoder)
        {
            OpCode opcode;
            int isize;
            // VarnodeData outvar = new VarnodeData();
            VarnodeData[] invar = new VarnodeData[16];
            VarnodeData? outptr; //  = ref outvar;

            uint elemId = decoder.openElement(ElementId.ELEM_OP);
            isize = (int)decoder.readSignedInteger(AttributeId.ATTRIB_SIZE);
            if (isize <= 16) {
                opcode = PcodeOpRaw.decode(decoder, isize, invar, out outptr);
            }
            else {
                List<VarnodeData> varStorage = new List<VarnodeData>(isize);
                for(int index = 0; index < isize; index++) {
                    varStorage[index] = new VarnodeData();
                }
                opcode = PcodeOpRaw.decode(decoder, isize, varStorage.ToArray(),
                    out outptr);
            }
            decoder.closeElement(elemId);
            dump(addr, opcode, outptr, invar, isize);
        }
    }

    /// \brief Abstract class for emitting disassembly to an application
    ///
    /// Translation engines pass back the disassembly character data
    /// for decoded machine instructions to an application using this class.
    public abstract class AssemblyEmit
    {
        ///< Virtual destructor
        ~AssemblyEmit()
        {
        }

        /// \brief The main disassembly emitting method.
        /// The disassembly strings for a single machine instruction
        /// are passed back to an application through this method.
        /// Particular applications can tailor the use of the disassembly
        /// by overriding this method.
        /// \param addr is the Address of the machine instruction
        /// \param mnem is the decoded instruction mnemonic
        /// \param body is the decode body (or operands) of the instruction
        public abstract void dump(ref Address addr, ref string mnem, ref string body);
    }

    /// \brief Abstract class for converting native constants to addresses
    /// This class is used if there is a special calculation to get from a constant embedded
    /// in the code being analyzed to the actual Address being referred to.  This is used especially
    /// in the case of a segmented architecture, where "near" pointers must be extended to a full address
    /// with implied segment information.
    public abstract class AddressResolver
    {
        ///// Virtual destructor
        //~AddressResolver()
        //{
        //}

        /// \brief The main resolver method.
        /// Given a native constant in a specific context, resolve what address is being referred to.
        /// The constant can be a partially encoded pointer, in which case the full pointer encoding
        /// is recovered as well as the address.  Whether or not a pointer is partially encoded or not
        /// is determined by the \e sz parameter, indicating the number of bytes in the pointer. A value
        /// of -1 here indicates that the pointer is known to be a full encoding.
        /// \param val is constant to be resolved to an address
        /// \param sz is the size of \e val in context (or -1).
        /// \param point is the address at which this constant is being used
        /// \param fullEncoding is used to hold the full pointer encoding if \b val is a partial encoding
        /// \return the resolved Address
        public abstract Address resolve(ulong val, int sz, Address point,
            out ulong fullEncoding);
    }

    /// \brief A virtual space \e stack space
    ///
    /// In a lot of analysis situations it is convenient to extend
    /// the notion of an address space to mean bytes that are indexed
    /// relative to some base register.  The canonical example of this
    /// is the \b stack space, which models the concept of local
    /// variables stored on the stack.  An address of (\b stack, 8)
    /// might model the address of a function parameter on the stack
    /// for instance, and (\b stack, 0xfffffff4) might be the address
    /// of a local variable.  A space like this is inherently \e virtual
    /// and contained within whatever space is being indexed into.
    public class SpacebaseSpace : AddrSpace
    {
        // friend class AddrSpaceManager;
        ///< Containing space
        private AddrSpace contain;
        ///< true if a base register has been attached
        private bool hasbaseregister;
        ///< true if stack grows in negative direction
        private bool isNegativeStack;
        ///< location data of the base register
        private VarnodeData baseloc;
        ///< Original base register before any truncation
        private VarnodeData baseOrig;

        ///< Set the base register at time space is created
        /// This routine sets the base register associated with this \b virtual space
        /// It will throw an exception if something tries to set two (different) base registers
        /// \param data is the location data for the base register
        /// \param truncSize is the size of the space covered by the register
        /// \param stackGrowth is \b true if the stack which this register manages grows in a negative direction
        internal void setBaseRegister(VarnodeData data, int truncSize, bool stackGrowth)
        {
            if (hasbaseregister) {
                if ((baseloc != data) || (isNegativeStack != stackGrowth)) {
                    throw new LowlevelError(
                        $"Attempt to assign more than one base register to space: {getName()}");
                }
            }
            hasbaseregister = true;
            isNegativeStack = stackGrowth;
            baseOrig = data;
            baseloc = data;
            if (truncSize != baseloc.size) {
                if (baseloc.space.isBigEndian()) {
                    baseloc.offset += (baseloc.size - (uint)truncSize);
                }
                baseloc.size = (uint)truncSize;
            }
        }

        /// Construct a virtual space.  This is usually used for the stack
        /// space, which is indicated by the \b isFormal parameters, but multiple such spaces are allowed.
        /// \param m is the manager for this \b program \b specific address space
        /// \param t is associated processor translator
        /// \param nm is the name of the space
        /// \param ind is the integer identifier
        /// \param sz is the size of the space
        /// \param base is the containing space
        /// \param dl is the heritage delay
        /// \param isFormal is the formal stack space indicator
        public SpacebaseSpace(AddrSpaceManager m, Translate t, string nm, int ind, int sz,
            AddrSpace @base, int dl,bool isFormal)
            : base(m, t, spacetype.IPTR_SPACEBASE, nm, (uint)sz, @base.getWordSize(), ind,0, dl)
        {
            contain = @base;
            // No base register assigned yet
            hasbaseregister = false;
            // default stack growth
            isNegativeStack = true;
            if (isFormal) {
                setFlags(Properties.formal_stackspace);
            }
        }

        /// For use with decode
        /// This is a partial constructor, which must be followed up
        /// with decode in order to fillin the rest of the spaces
        /// attributes
        /// \param m is the associated address space manager
        /// \param t is the associated processor translator
        public SpacebaseSpace(AddrSpaceManager m, Translate t)
            : base(m, t, spacetype.IPTR_SPACEBASE)
        {
            hasbaseregister = false;
            isNegativeStack = true;
            setFlags(Properties.programspecific);
        }

        public virtual int numSpacebase()
        {
            return hasbaseregister ? 1 : 0;
        }

        public virtual ref VarnodeData getSpacebase(int i)
        {
            if ((!hasbaseregister) || (i != 0)) {
                throw new LowlevelError($"No base register specified for space: {getName()}");
            }
            return ref baseloc;
        }

        public virtual ref VarnodeData getSpacebaseFull(int i)
        {
            if ((!hasbaseregister) || (i != 0)) {
                throw new LowlevelError("No base register specified for space: {getName()}");
            }
            return ref baseOrig;
        }

        public override bool stackGrowsNegative()
        {
            return isNegativeStack;
        }

        /// Return containing space
        public override AddrSpace? getContain()
        {
            return contain;
        }
        
        public override void saveXml(StreamWriter s)
        {
            s.Write("<space_base");
            saveBasicAttributes(s);
            Xml.a_v(s, "contain", contain.getName());
            s.WriteLine("/>");
        }

        public virtual void decode(ref Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_SPACE_BASE);
            decodeBasicAttributes(decoder);
            contain = decoder.readSpace(AttributeId.ATTRIB_CONTAIN);
            decoder.closeElement(elemId);
        }
    }

    /// \brief A record describing how logical values are split
    /// The decompiler can describe a logical value that is stored split across multiple
    /// physical memory locations.  This record describes such a split. The pieces must be listed
    /// from \e most \e significant to \e least \e significant.
    public class JoinRecord
    {
        // friend class AddrSpaceManager;
        /// All the physical pieces of the symbol
        internal List<VarnodeData> pieces;
        /// Special entry representing entire symbol in one chunk
        internal VarnodeData unified;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) {
                GC.SuppressFinalize(this);
            }
            pieces = null;
            unified = null;
        }

        /// Get number of pieces in this record
        public int numPieces()
        {
            return pieces.Count;
        }

        /// Does this record extend a float varnode
        public bool isFloatExtension()
        {
            return (pieces.Count == 1);
        }

        /// Get the i-th piece
        public VarnodeData getPiece(uint i)
        {
            return pieces[(int)i];
        }

        /// Get the Varnode whole
        public ref VarnodeData getUnified()
        {
            return ref unified;
        }

        /// Given offset in \e join space, get equivalent address of piece
        /// The \e join space range maps to the underlying pieces in a natural endian aware way.
        /// Given an offset in the range, figure out what address it is mapping to.
        /// The particular piece is passed back as an index, and the Address is returned.
        /// \param offset is the offset within \b this range to map
        /// \param pos will hold the passed back piece index
        /// \return the Address mapped to
        public Address getEquivalentAddress(ulong offset, out int pos)
        {
            if (offset < unified.offset) {
                // offset comes before this range
                pos = 0;
                return new Address();
            }
            int smallOff = (int)(offset - unified.offset);
            if (pieces[0].space.isBigEndian()) {
                for (pos = 0; pos < pieces.Count; ++pos) {
                    int pieceSize = (int)pieces[pos].size;
                    if (smallOff < pieceSize) {
                        break;
                    }
                    smallOff -= pieceSize;
                }
                if (pos == pieces.Count) {
                    // offset comes after this range
                    return new Address();
                }
            }
            else {
                for (pos = pieces.Count - 1; pos >= 0; --pos) {
                    int pieceSize = (int)pieces[pos].size;
                    if (smallOff < pieceSize) {
                        break;
                    }
                    smallOff -= pieceSize;
                }
                if (pos < 0) {
                    // offset comes after this range
                    return new Address();
                }
            }
            if (0 > smallOff) {
                throw new BugException();
            }
            return new Address(pieces[pos].space, pieces[pos].offset + (uint)smallOff);
        }

        /// Compare records lexigraphically by pieces
        /// Allow sorting on JoinRecords so that a collection of pieces can be quickly mapped to
        /// its logical whole, specified with a join address
        public static bool operator <(JoinRecord op1, JoinRecord op2)
        {
            // Some joins may have same piece but different unified size  (floating point)
            if (op1.unified.size != op2.unified.size) {
                // Compare size first
                return (op1.unified.size < op2.unified.size);
            }
            // Lexigraphic sort on pieces
            int i = 0;
            while(true) {
                if (op1.pieces.Count == i) {
                    // If more pieces in op2, it is bigger (return true), if same number this==op2, return false
                    return (op2.pieces.Count > i);
                }
                if (op2.pieces.Count == i) {
                    // More pieces in -this-, so it is bigger, return false
                    return false;
                }
                if (op1.pieces[i] != op2.pieces[i]) {
                    return (op1.pieces[i] < op2.pieces[i]);
                }
                i += 1;
            }
        }

        public static bool operator >(JoinRecord op1, JoinRecord op2)
        {
            throw new NotImplementedException();
        }
    }

        /// \brief Comparator for JoinRecord objects
        public class JoinRecordCompare : IEqualityComparer<JoinRecord>
    {
        internal static readonly JoinRecordCompare Singleton = new JoinRecordCompare();

        private JoinRecordCompare()
        {
        }

        /////< Compare to JoinRecords using their built-in comparison
        //public bool operator()(JoinRecord a, JoinRecord b)
        //{
        //    return *a< *b;
        //}
        public bool Equals(JoinRecord? x, JoinRecord? y)
        {
            // REMARK Transformed the < comparison in an == one.
            return (x ?? throw new BugException()) == (y ?? throw new BugException());
        }

        public int GetHashCode([DisallowNull] JoinRecord obj)
        {
            throw new NotImplementedException();
        }
    }

    /// \brief A manager for different address spaces
    /// Allow creation, lookup by name, lookup by shortcut, lookup by name, and iteration
    /// over address spaces
    public class AddrSpaceManager
    {
        /// Every space we know about for this architecture
        private List<AddrSpace> baselist;
        /// Special constant resolvers
        private List<AddressResolver?> resolvelist;
        /// Map from name . space
        private SortedDictionary<string, AddrSpace> name2Space;
        /// Map from shortcut . space
        private SortedDictionary<int, AddrSpace> shortcut2Space;
        /// Quick reference to constant space
        private AddrSpace constantspace;
        /// Default space where code lives, generally main RAM
        private AddrSpace defaultcodespace;
        /// Default space where data lives
        private AddrSpace defaultdataspace;
        /// Space for internal pcode op pointers
        private AddrSpace iopspace;
        /// Space for internal callspec pointers
        private AddrSpace fspecspace;
        /// Space for unifying split variables
        private AddrSpace joinspace;
        /// Stack space associated with processor
        private AddrSpace stackspace;
        /// Temporary space associated with processor
        private AddrSpace uniqspace;
        /// Next offset to be allocated in join space
        private ulong joinallocate;
        /// Different splits that have been defined in join space
        private HashSet<JoinRecord> splitset =
            new HashSet<JoinRecord>(JoinRecordCompare.Singleton);
        /// JoinRecords indexed by join address
        private List<JoinRecord> splitlist;

        /// Add a space to the model based an on XML tag
        /// The initialization of address spaces is the same across all
        /// variants of the Translate object.  This routine initializes
        /// a single address space from a decoder element.  It knows
        /// which class derived from AddrSpace to instantiate based on
        /// the ElementId.
        /// \param decoder is the stream decoder
        /// \param trans is the translator object to be associated with the new space
        /// \return a pointer to the initialized AddrSpace
        protected AddrSpace decodeSpace(Decoder decoder, Translate trans)
        {
            uint elemId = decoder.peekElement();
            AddrSpace res;
            if (elemId == ElementId.ELEM_SPACE_BASE) {
                res = new SpacebaseSpace(this, trans);
            }
            else if (elemId == ElementId.ELEM_SPACE_UNIQUE) {
                res = new UniqueSpace(this, trans);
            }
            else if (elemId == ElementId.ELEM_SPACE_OTHER) {
                res = new OtherSpace(this, trans);
            }
            else if (elemId == ElementId.ELEM_SPACE_OVERLAY) {
                res = new OverlaySpace(this, trans);
            }
            else {
                res = new AddrSpace(this, trans, spacetype.IPTR_PROCESSOR);
            }
            res.decode(decoder);
            return res;
        }

        ///< Restore address spaces in the model from an XML tag
        /// This routine initializes (almost) all the address spaces used
        /// for a particular processor by using a \b \<spaces\> element,
        /// which contains child elements for the specific address spaces.
        /// This also instantiates the builtin \e constant space. It
        /// should probably also instantiate the \b iop, \b fspec, and \b join
        /// spaces, but this is currently done by the Architecture class.
        /// \param decoder is the stream decoder
        /// \param trans is the processor translator to be associated with the spaces
        protected void decodeSpaces(Decoder decoder, Translate trans)
        {
            // The first space should always be the constant space
            insertSpace(new ConstantSpace(this, trans));

            uint elemId = decoder.openElement(ElementId.ELEM_SPACES);
            string defname = decoder.readString(AttributeId.ATTRIB_DEFAULTSPACE);
            while (decoder.peekElement() != 0) {
                insertSpace(decodeSpace(decoder, trans));
            }
            decoder.closeElement(elemId);
            AddrSpace spc = getSpaceByName(defname);
            if (spc == null) {
                throw new LowlevelError($"Bad 'defaultspace' attribute: {defname}");
            }
            setDefaultCodeSpace(spc.getIndex());
        }

        ///< Set the default address space (for code)
        /// Once all the address spaces have been initialized, this routine
        /// should be called once to establish the official \e default
        /// space for the processor, via its index. Should only be
        /// called during initialization.
        /// \param index is the index of the desired default space
        protected void setDefaultCodeSpace(int index)
        {
            if (defaultcodespace != null) {
                throw new LowlevelError("Default space set multiple times");
            }
            if (baselist.Count <= index || baselist[index] == null) {
                throw new LowlevelError("Bad index for default space");
            }
            defaultcodespace = baselist[index];
            // By default the default data space is the same
            defaultdataspace = defaultcodespace;
        }

        ///< Set the default address space for data
        /// If the architecture has different code and data spaces, this routine can be called
        /// to set the \e data space after the \e code space has been set.
        /// \param index is the index of the desired default space
        protected void setDefaultDataSpace(int index)
        {
            if (defaultcodespace == null) {
                throw new LowlevelError("Default data space must be set after the code space");
            }
            if (baselist.Count <= index || baselist[index] == null) {
                throw new LowlevelError("Bad index for default data space");
            }
            defaultdataspace = baselist[index];
        }

        ///< Set reverse justified property on this space
        /// For spaces with alignment restrictions, the address of a small variable must be justified
        /// within a larger aligned memory word, usually either to the left boundary for little endian encoding
        /// or to the right boundary for big endian encoding.  Some compilers justify small variables to
        /// the opposite side of the one indicated by the endianness. Setting this property on a space
        /// causes the decompiler to use this justification
        protected void setReverseJustified(AddrSpace spc)
        {
            spc.setFlags(AddrSpace.Properties.reverse_justification);
        }

        ///< Select a shortcut character for a new space
        /// Assign a \e shortcut character to the given address space.
        /// This routine makes use of the desired type of the new space
        /// and info about shortcuts for spaces that already exist to
        /// pick a unique and consistent character.  This method also builds
        /// up a map from short to AddrSpace object.
        /// \param spc is the given AddrSpace
        protected void assignShortcut(AddrSpace spc)
        {
            if (spc.shortcut != ' ') {
                // If the shortcut is already assigned
                shortcut2Space.Add(spc.shortcut, spc);
                return;
            }
            char shortcut;
            switch (spc.getType()) {
                case spacetype.IPTR_CONSTANT:
                    shortcut = '#';
                    break;
                case spacetype.IPTR_PROCESSOR:
                    if (spc.getName() == "register")
                        shortcut = '%';
                    else
                        shortcut = spc.getName()[0];
                    break;
                case spacetype.IPTR_SPACEBASE:
                    shortcut = 's';
                    break;
                case spacetype.IPTR_INTERNAL:
                    shortcut = 'u';
                    break;
                case spacetype.IPTR_FSPEC:
                    shortcut = 'f';
                    break;
                case spacetype.IPTR_JOIN:
                    shortcut = 'j';
                    break;
                case spacetype.IPTR_IOP:
                    shortcut = 'i';
                    break;
                default:
                    shortcut = 'x';
                    break;
            }

            if (shortcut >= 'A' && shortcut <= 'Z') {
                shortcut = (char)(shortcut + 0x20);
            }

            int collisionCount = 0;
            while (!shortcut2Space.TryAdd(shortcut, spc)) {
                collisionCount += 1;
                if (collisionCount > 26) {
                    // Could not find a unique shortcut, but we just re-use 'z' as we
                    // can always use the long form to specify the address if there are really so many
                    // spaces that need to be distinguishable (in the console mode)
                    spc.shortcut = 'z';
                    return;
                }
                shortcut = (char)(shortcut + 1);
                if (shortcut < 'a' || shortcut > 'z') {
                    shortcut = 'a';
                }
            }
            spc.shortcut = (char)shortcut;
        }

        /// Mark that given space can be accessed with near pointers
        /// \param spc is the AddrSpace to mark
        /// \param size is the (minimum) size of a near pointer in bytes
        protected void markNearPointers(AddrSpace spc, int size)
        {
            spc.setFlags(AddrSpace.Properties.has_nearpointers);
            if (spc.minimumPointerSize == 0 && spc.addressSize != size) {
                spc.minimumPointerSize = size;
            }
        }

        /// Add a new address space to the model
        /// This adds a previously instantiated address space (AddrSpace) to the model
        /// for this processor. It checks a set of indexing and naming conventions for
        /// the space and throws an exception if the conventions are violated. Should
        /// only be called during initialization.
        /// \todo This really shouldn't be public. Need to move the allocation of \b iop,
        /// \b fspec, and \b join out of Architecture
        /// \param spc the address space to insert
        protected void insertSpace(AddrSpace spc)
        {
            bool nameTypeMismatch = false;
            bool duplicateName = false;
            bool duplicateId = false;
            switch (spc.getType()) {
                case spacetype.IPTR_CONSTANT:
                    if (spc.getName() != ConstantSpace.NAME) {
                        nameTypeMismatch = true;
                    }
                    if (spc.index != ConstantSpace.INDEX) {
                        throw new LowlevelError("const space must be assigned index 0");
                    }
                    constantspace = spc;
                    break;
                case spacetype.IPTR_INTERNAL:
                    if (spc.getName() != UniqueSpace.NAME) {
                        nameTypeMismatch = true;
                    }
                    if (uniqspace != null) {
                        duplicateName = true;
                    }
                    uniqspace = spc;
                    break;
                case spacetype.IPTR_FSPEC:
                    if (spc.getName() != "fspec") {
                        nameTypeMismatch = true;
                    }
                    if (fspecspace != null) {
                        duplicateName = true;
                    }
                    fspecspace = spc;
                    break;
                case spacetype.IPTR_JOIN:
                    if (spc.getName() != JoinSpace.NAME) {
                        nameTypeMismatch = true;
                    }
                    if (joinspace != null) {
                        duplicateName = true;
                    }
                    joinspace = spc;
                    break;
                case spacetype.IPTR_IOP:
                    if (spc.getName() != "iop") {
                        nameTypeMismatch = true;
                    }
                    if (iopspace != null) {
                        duplicateName = true;
                    }
                    iopspace = spc;
                    break;
                case spacetype.IPTR_SPACEBASE:
                    if (spc.getName() == "stack") {
                        if (stackspace != null) {
                            duplicateName = true;
                        }
                        stackspace = spc;
                    }
                    // fallthru
                    goto case spacetype.IPTR_PROCESSOR;
                case spacetype.IPTR_PROCESSOR:
                    if (spc.isOverlay()) {
                        // If this is a new overlay space
                        // Mark the base as being overlayed
                        (spc.getContain() ?? throw new BugException())
                            .setFlags(AddrSpace.Properties.overlaybase);
                    }
                    else if (spc.isOtherSpace()) {
                        if (spc.index != OtherSpace.INDEX) {
                            throw new LowlevelError("OTHER space must be assigned index 1");
                        }
                    }
                    break;
            }

            while(baselist.Count <= spc.index) {
                baselist.Add(null);
            }
            duplicateId = baselist[spc.index] != null;
            if (!nameTypeMismatch && !duplicateName && !duplicateId) {
                duplicateName = !name2Space.TryAdd(spc.getName(), spc);
            }
            if (nameTypeMismatch || duplicateName || duplicateId) {
                string errMsg = "Space " + spc.getName();
                if (nameTypeMismatch) {
                    errMsg = errMsg + " was initialized with wrong type";
                }
                if (duplicateName) {
                    errMsg = errMsg + " was initialized more than once";
                }
                if (duplicateId) {
                    errMsg = errMsg + " was assigned as id duplicating: " + baselist[spc.index].getName();
                }
                if (0 == spc.refcount) {
                    spc.Dispose();
                }
                spc = null;
                throw new LowlevelError(errMsg);
            }
            baselist[spc.index] = spc;
            spc.refcount += 1;
            assignShortcut(spc);
        }

        ///< Copy spaces from another manager
        /// Different managers may need to share the same spaces. I.e. if different programs being
        /// analyzed share the same processor. This routine pulls in a reference of every space in -op2-
        /// in order to manage it from within -this-
        /// \param op2 is a pointer to space manager being copied
        protected void copySpaces(AddrSpaceManager op2)
        {
            // Insert every space in -op2- into -this- manager
            for (int i = 0; i < op2.baselist.Count; ++i) {
                AddrSpace spc = op2.baselist[i];
                if (spc != null)
                    insertSpace(spc);
            }
            setDefaultCodeSpace(op2.getDefaultCodeSpace().getIndex());
            setDefaultDataSpace(op2.getDefaultDataSpace().getIndex());
        }

        ///< Set the base register of a spacebase space
        /// Perform the \e privileged act of associating a base register with an existing \e virtual space
        /// \param basespace is the virtual space
        /// \param ptrdata is the location data for the base register
        /// \param truncSize is the size of the space covered by the base register
        /// \param stackGrowth is true if the stack grows "normally" towards address 0
        protected void addSpacebasePointer(SpacebaseSpace basespace, VarnodeData ptrdata,
            int truncSize, bool stackGrowth)
        {
            basespace.setBaseRegister(ptrdata, truncSize, stackGrowth);
        }

        ///< Override the base resolver for a space
        /// Provide a new specialized resolver for a specific AddrSpace.  The manager takes ownership of resolver.
        /// \param spc is the space to which the resolver is associated
        /// \param rsolv is the new resolver object
        protected void insertResolver(AddrSpace spc, AddressResolver rsolv)
        {
            int ind = spc.getIndex();
            while (resolvelist.Count <= ind) {
                resolvelist.Add(null);
            }
            //if (resolvelist[ind] != null) {
            //    delete resolvelist[ind];
            //}
            resolvelist[ind] = rsolv;
        }

        ///< Set the range of addresses that can be inferred as pointers
        /// This method establishes for a single address space, what range of constants are checked
        /// as possible symbol starts, when it is not known apriori that a constant is a pointer.
        /// \param range is the range of values for a single address space
        protected void setInferPtrBounds(Sla.CORE.Range range)
        {
            range.getSpace().pointerLowerBound = range.getFirst();
            range.getSpace().pointerUpperBound = range.getLast();
        }

        ///< Find JoinRecord for \e offset in the join space
        /// Given a specific \e offset into the \e join address space, recover the JoinRecord that
        /// contains the offset, as a range in the \e join address space.  If there is no existing
        /// record, null is returned.
        /// \param offset is an offset into the join space
        /// \return the JoinRecord containing that offset or null
        protected JoinRecord findJoinInternal(ulong offset)
        {
            int min = 0;
            int max = splitlist.Count - 1;
            while (min <= max) {
                // Binary search
                int mid = (min + max) / 2;
                JoinRecord rec = splitlist[mid];
                ulong val = rec.unified.offset;
                if (val + rec.unified.size <= offset)
                    min = mid + 1;
                else if (val > offset)
                    max = mid - 1;
                else
                    return rec;
            }
            return null;
        }

        ///< Construct an empty address space manager
        /// Initialize manager containing no address spaces. All the cached space slots are set to null
        public AddrSpaceManager()
        {
            defaultcodespace = null;
            defaultdataspace = null;
            constantspace = null;
            iopspace = null;
            fspecspace = null;
            joinspace = null;
            stackspace = null;
            uniqspace = null;
            joinallocate = 0;
        }

        ///< Destroy the manager
        ////// Base destructor class, cleans up AddrSpace pointers which
        /// must be explicited created via \e new
        ~AddrSpaceManager()
        {
            List<AddrSpace> deletedSpaces = new List<AddrSpace>();
            foreach (AddrSpace spc in baselist) {
                if (spc == null) {
                    continue;
                }
                if (spc.refcount > 1) {
                    spc.refcount -= 1;
                }
                else {
                    deletedSpaces.Add(spc);
                }
            }
            foreach(AddrSpace deletedSpace in deletedSpaces) {
                deletedSpace.Dispose();
            }
            //foreach (AddressResolver? scannedResolver in resolvelist) {
            //    if (null != scannedResolver) {
            //        delete scannedResolver;
            //    }
            //}
            foreach (JoinRecord scannedRecord in splitlist) {
                // Delete any join records
                scannedRecord.Dispose();
            }
        }

        ///< Get size of addresses for the default space
        /// Return the size of addresses for the processor's official
        /// default space. This space is usually the main RAM databus.
        /// \return the size of an address in bytes
        public uint getDefaultSize()
        {
            return defaultcodespace.getAddrSize();
        }

        ///< Get address space by name
        /// All address spaces have a unique name associated with them.
        /// This routine retrieves the AddrSpace object based on the
        /// desired name.
        /// \param nm is the name of the address space
        /// \return a pointer to the AddrSpace object
        public AddrSpace? getSpaceByName(string nm)
        {
            AddrSpace? result = null;
            return name2Space.TryGetValue(nm, out result)
                ? result
                : null;
        }

        ///< Get address space from its shortcut
        /// All address spaces have a unique shortcut (ASCII) character
        /// assigned to them. This routine retrieves an AddrSpace object
        /// given a specific shortcut.
        /// \param sc is the shortcut character
        /// \return a pointer to an AddrSpace
        public AddrSpace? getSpaceByShortcut(char sc)
        {
            AddrSpace? result;
            return shortcut2Space.TryGetValue(sc, out result)
                ? result
                : null;
        }

        ///< Get the internal pcode op space
        /// There is a special address space reserved for encoding pointers
        /// to pcode operations as addresses.  This allows a direct pointer
        /// to be \e hidden within an operation, when manipulating pcode
        /// internally. (See IopSpace)
        /// \return a pointer to the address space
        public AddrSpace getIopSpace()
        {
            return iopspace;
        }

        ///< Get the internal callspec space
        /// There is a special address space reserved for encoding pointers
        /// to the FuncCallSpecs object as addresses. This allows direct
        /// pointers to be \e hidden within an operation, when manipulating
        /// pcode internally. (See FspecSpace)
        /// \return a pointer to the address space
        public AddrSpace getFspecSpace()
        {
            return fspecspace;
        }

        ///< Get the joining space
        /// There is a special address space reserved for providing a 
        /// logical contiguous memory location for variables that are
        /// really split between two physical locations.  This allows the
        /// the decompiler to work with the logical value. (See JoinSpace)
        /// \return a pointer to the address space
        public AddrSpace getJoinSpace()
        {
            return joinspace;
        }

        ///< Get the stack space for this processor
        /// Most processors have registers and instructions that are
        /// reserved for implementing a stack. In the pcode translation,
        /// these are translated into locations and operations on a
        /// dedicated \b stack address space. (See SpacebaseSpace)
        /// \return a pointer to the \b stack space
        public AddrSpace getStackSpace()
        {
            return stackspace;
        }

        ///< Get the temporary register space for this processor
        /// Both the pcode translation process and the simplification
        /// process need access to a pool of temporary registers that
        /// can be used for moving data around without affecting the
        /// address spaces used to formally model the processor's RAM
        /// and registers.  These temporary locations are all allocated
        /// from a dedicated address space, referred to as the \b unique
        /// space. (See UniqueSpace)
        /// \return a pointer to the \b unique space
        public AddrSpace getUniqueSpace()
        {
            return uniqspace;
        }

        ///< Get the default address space of this processor
        /// Most processors have a main address bus, on which the bulk
        /// of the processor's RAM is mapped. This matches SLEIGH's notion
        /// of the \e default space. For Harvard architectures, this is the
        /// space where code exists (as opposed to data).
        /// \return a pointer to the \e default code space
        public AddrSpace getDefaultCodeSpace()
        {
            return defaultcodespace;
        }

        ///< Get the default address space where data is stored
        /// Return the default address space for holding data. For most processors, this
        /// is just the main RAM space and is the same as the default \e code space.
        /// For Harvard architectures, this is the space where data is stored
        /// (as opposed to code).
        /// \return a pointer to the \e default data space
        public AddrSpace getDefaultDataSpace()
        {
            return defaultdataspace;
        }

        ///< Get the constant space
        /// Pcode represents constant values within an operation as offsets within a
        /// special \e constant address space. 
        /// (See ConstantSpace)
        /// \return a pointer to the \b constant space
        public AddrSpace getConstantSpace()
        {
            return constantspace;
        }

        ///< Get a constant encoded as an Address
        /// This routine encodes a specific value as a \e constant
        /// address. I.e. the address space of the resulting Address
        /// will be the \b constant space, and the offset will be the
        /// value.
        /// \param val is the constant value to encode
        /// \return the \e constant address
        public Address getConstant(ulong val)
        {
            return new Address(constantspace, val);
        }

        /// Create a constant address encoding an address space
        /// This routine is used to encode a pointer to an address space as a \e constant
        /// Address, for use in \b LOAD and \b STORE operations. This is used internally
        /// and is slightly more efficient than storing the formal index of the space
        /// param spc is the space pointer to be encoded
        /// \return the encoded Address
        public Address createConstFromSpace(AddrSpace spc)
        {
            if (spacetype.IPTR_CONSTANT != spc.type) {
                throw new InvalidOperationException();
            }
            ConstantSpace? constantSpace = spc as ConstantSpace;
            if (null == constantSpace) {
                throw new InvalidOperationException();
            }
            return new Address(constantspace, constantSpace._uniqueId);
        }

        /// \brief Resolve a native constant into an Address
        ///
        /// If there is a special resolver for the AddrSpace, this is invoked, otherwise
        /// basic wordsize conversion and wrapping is performed. If the address encoding is
        /// partial (as in a \e near pointer) and the full encoding can be recovered, it is passed back.
        /// The \e sz parameter indicates the number of bytes in constant and is used to determine if
        /// the constant is a partial or full pointer encoding. A value of -1 indicates the value is
        /// known to be a full encoding.
        /// \param spc is the space to generate the address from
        /// \param val is the constant encoding of the address
        /// \param sz is the size of the constant encoding (or -1)
        /// \param point is the context address (for recovering full encoding info if necessary)
        /// \param fullEncoding is used to pass back the recovered full encoding of the pointer
        /// \return the formal Address associated with the encoding
        public Address resolveConstant(AddrSpace spc, ulong val, int sz, ref Address point,
            out ulong fullEncoding)
        {
            int ind = spc.getIndex();
            if (ind < resolvelist.Count) {
                AddressResolver? resolve = resolvelist[ind];
                if (null != resolve) {
                    return resolve.resolve(val, sz, point, out fullEncoding);
                }
            }
            fullEncoding = val;
            val = AddrSpace.addressToByte(val, spc.getWordSize());
            val = spc.wrapOffset(val);
            return new Address(spc, val);
        }

        ///< Get the number of address spaces for this processor
        /// This returns the total number of address spaces used by the
        /// processor, including all special spaces, like the \b constant
        /// space and the \b iop space. 
        /// \return the number of spaces
        public int numSpaces()
        {
            return baselist.Count;
        }

        ///< Get an address space via its index
        /// This retrieves a specific address space via its formal index.
        /// All spaces have an index, and in conjunction with the numSpaces
        /// method, this method can be used to iterate over all spaces.
        /// \param i is the index of the address space
        /// \return a pointer to the desired space
        public AddrSpace getSpace(int i)
        {
            return baselist[i];
        }

        ///< Get the next \e contiguous address space
        /// Get the next space in the absolute order of addresses.
        /// This ordering is determined by the AddrSpace index.
        /// \param spc is the pointer to the space being queried
        /// \return the pointer to the next space in absolute order
        public AddrSpace? getNextSpaceInOrder(AddrSpace spc)
        {
            if (spc == null) {
                return baselist[0];
            }
            if (spc.IsMaxAddressSpace) {
                return null;
            }
            int index = spc.getIndex() + 1;
            while (index < baselist.Count) {
                AddrSpace res = baselist[index];
                if (res != null) {
                    return res;
                }
                index += 1;
            }
            return AddrSpace.MaxAddressSpace;
        }

        /// Get (or create) JoinRecord for \e pieces
        /// Given a list of memory locations, the \e pieces, either find a pre-existing JoinRecord or
        /// create a JoinRecord that represents the logical joining of the pieces.
        /// \param pieces if the list memory locations to be joined
        /// \param logicalsize of a \e single \e piece join, or zero
        /// \return a pointer to the JoinRecord
        public JoinRecord findAddJoin(List<VarnodeData> pieces, uint logicalsize)
        {
            // Find a pre-existing split record, or create a new one corresponding to the input -pieces-
            // If -logicalsize- is 0, calculate logical size as sum of pieces
            if (pieces.Count == 0) {
                throw new LowlevelError("Cannot create a join without pieces");
            }
            if ((pieces.Count == 1) && (logicalsize == 0)) {
                throw new LowlevelError("Cannot create a single piece join without a logical size");
            }
            uint totalsize;
            if (logicalsize != 0) {
                if (pieces.Count != 1) {
                    throw new LowlevelError("Cannot specify logical size for multiple piece join");
                }
                totalsize = logicalsize;
            }
            else {
                totalsize = 0;
                for (int i = 0; i < pieces.Count; ++i) {
                    // Calculate sum of the sizes of all pieces
                    totalsize += pieces[i].size;
                }
                if (totalsize == 0) {
                    throw new LowlevelError("Cannot create a zero size join");
                }
            }
            JoinRecord testnode = new JoinRecord();
            testnode.pieces = pieces;
            testnode.unified.size = totalsize;
            if (splitset.Contains(testnode)) {
                // If already in the set
                return testnode;
            }
            JoinRecord newjoin = new JoinRecord();
            newjoin.pieces = pieces;

            // Next biggest multiple of 16
            uint roundsize = (totalsize + 15) & ~((uint)0xf);

            newjoin.unified.space = joinspace;
            newjoin.unified.offset = joinallocate;
            joinallocate += roundsize;
            newjoin.unified.size = totalsize;
            splitset.Add(newjoin);
            splitlist.Add(newjoin);
            return newjoin;
        }

        /// Find JoinRecord for \e offset in the join space
        /// Given a specific \e offset into the \e join address space, recover the JoinRecord that
        /// lists the pieces corresponding to that offset.  The offset must originally have come from
        /// a JoinRecord returned by \b findAddJoin, otherwise this method throws an exception.
        /// \param offset is an offset into the join space
        /// \return the JoinRecord for that offset
        public JoinRecord findJoin(ulong offset)
        {
            int min = 0;
            int max = splitlist.Count - 1;
            while (min <= max) {        // Binary search
                int mid = (min + max) / 2;
                JoinRecord rec = splitlist[mid];
                ulong val = rec.unified.offset;
                if (val == offset) return rec;
                if (val < offset)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            throw new LowlevelError("Unlinked join address");
        }

        ///< Set the deadcodedelay for a specific space
        /// Set the number of passes for a specific AddrSpace before deadcode removal is allowed
        /// for that space.
        /// \param spc is the AddrSpace to change
        /// \param delaydelta is the number of rounds to the delay should be set to
        public void setDeadcodeDelay(AddrSpace spc, int delaydelta)
        {
            spc.deadcodedelay = delaydelta;
        }

        ///< Mark a space as truncated from its original size
        /// Mark the named space as truncated from its original size
        /// \param tag is a description of the space and how it should be truncated
        public void truncateSpace(ref TruncationTag tag)
        {
            AddrSpace spc = getSpaceByName(tag.getName());
            if (spc == null) {
                throw new LowlevelError("Unknown space in <truncate_space> command: " + tag.getName());
            }
            spc.truncateSpace(tag.getSize());
        }

        /// \brief Build a logically lower precision storage location for a bigger floating point register
        /// This handles the situation where we need to find a logical address to hold the lower
        /// precision floating-point value that is stored in a bigger register
        /// If the logicalsize (precision) requested matches the -realsize- of the register
        /// just return the real address.  Otherwise construct a join address to hold the logical value
        /// \param realaddr is the address of the real floating-point register
        /// \param realsize is the size of the real floating-point register
        /// \param logicalsize is the size (lower precision) size of the logical value
        public Address constructFloatExtensionAddress(ref Address realaddr, int realsize,
            int logicalsize)
        {
            if (logicalsize == realsize) {
                return realaddr;
            }
            List<VarnodeData> pieces = new List<VarnodeData>();
            pieces.Add(new VarnodeData() {
                space = realaddr.getSpace(),
                offset = realaddr.getOffset(),
                size = (uint)realsize
            });
            JoinRecord join = findAddJoin(pieces, (uint)logicalsize);
            return join.getUnified().getAddr();
        }

        /// \brief Build a logical whole from register pairs
        /// This handles the common case, of trying to find a join address given a high location and a low
        /// location. This may not return an address in the \e join address space.  It checks for the case
        /// where the two pieces are contiguous locations in a mappable space, in which case it just returns
        /// the containing address
        /// \param translate is the Translate object used to find registers
        /// \param hiaddr is the address of the most significant piece to be joined
        /// \param hisz is the size of the most significant piece
        /// \param loaddr is the address of the least significant piece
        /// \param losz is the size of the least significant piece
        /// \return an address representing the start of the joined range
        public Address constructJoinAddress(Translate translate, ref Address hiaddr, int hisz,
            ref Address loaddr, int losz)
        {
            spacetype hitp = hiaddr.getSpace().getType();
            spacetype lotp = loaddr.getSpace().getType();
            bool usejoinspace = true;
            if (   (   (hitp != spacetype.IPTR_SPACEBASE)
                    && (hitp != spacetype.IPTR_PROCESSOR))
                || (   (lotp != spacetype.IPTR_SPACEBASE)
                    && (lotp != spacetype.IPTR_PROCESSOR)))
            {
                throw new LowlevelError("Trying to join in appropriate locations");
            }
            if (   (hitp == spacetype.IPTR_SPACEBASE)
                || (lotp == spacetype.IPTR_SPACEBASE)
                || (hiaddr.getSpace() == getDefaultCodeSpace())
                || (loaddr.getSpace() == getDefaultCodeSpace()))
            {
                usejoinspace = false;
            }
            if (hiaddr.isContiguous(hisz, loaddr, losz)) {
                // If we are contiguous
                if (!usejoinspace) {
                    // and in a mappable space, just return the earliest address
                    return hiaddr.isBigEndian() ? hiaddr : loaddr;
                }
                else {
                    // If we are in a non-mappable (register) space, check to see if
                    // a parent register exists
                    if (hiaddr.isBigEndian()) {
                        if (translate.getRegisterName(hiaddr.getSpace(),
                            hiaddr.getOffset(), (hisz + losz)).Length != 0)
                        {
                            return hiaddr;
                        }
                    }
                    else {
                        if (translate.getRegisterName(loaddr.getSpace(),
                            loaddr.getOffset(), (hisz + losz)).Length != 0)
                        {
                            return loaddr;
                        }
                    }
                }
            }
            // Otherwise construct a formal JoinRecord
            List<VarnodeData> pieces = new List<VarnodeData>();
            pieces.Add(new VarnodeData() {
                space = hiaddr.getSpace(),
                offset = hiaddr.getOffset(),
                size = (uint)hisz
            });
            pieces.Add(new VarnodeData() {
                space = loaddr.getSpace(),
                offset = loaddr.getOffset(),
                size = (uint)losz
            });
            JoinRecord join = findAddJoin(pieces, 0);
            return join.getUnified().getAddr();
        }

        /// \brief Make sure a possibly offset \e join address has a proper JoinRecord
        /// If an Address in the \e join AddressSpace is shifted from its original offset, it may no
        /// longer have a valid JoinRecord.  The shift or size change may even make the address of
        /// one of the pieces a more natural representation.  Given a new Address and size, this method
        /// decides if there is a matching JoinRecord. If not it either constructs a new JoinRecord or
        /// computes the address within the containing piece.  The given Address is changed if necessary
        /// either to the offset corresponding to the new JoinRecord or to a normal \e non-join Address.
        /// \param addr is the given Address
        /// \param size is the size of the range in bytes
        public void renormalizeJoinAddress(Address addr, int size)
        {
            JoinRecord joinRecord = findJoinInternal(addr.getOffset());
            if (joinRecord == null) {
                throw new LowlevelError("Join address not covered by a JoinRecord");
            }
            if (   (addr.getOffset() == joinRecord.unified.offset)
                && (size == joinRecord.unified.size))
            {
                // JoinRecord matches perfectly, no change necessary
                return;
            }
            int pos1;
            Address addr1 = joinRecord.getEquivalentAddress(addr.getOffset(), out pos1);
            int pos2;
            if (1 > size) {
                throw new BugException();
            }
            Address addr2 = joinRecord.getEquivalentAddress(addr.getOffset() + (uint)(size - 1),
                out pos2);
            if (addr2.isInvalid()) {
                throw new LowlevelError("Join address range not covered");
            }
            if (pos1 == pos2) {
                addr = addr1;
                return;
            }
            List<VarnodeData> newPieces = new List<VarnodeData>();
            int sizeTrunc1 = (int)(addr1.getOffset() - joinRecord.pieces[pos1].offset);
            int sizeTrunc2 = (int)(joinRecord.pieces[pos2].size - (int)(addr2.getOffset() - joinRecord.pieces[pos2].offset) - 1);
            VarnodeData firstNode;
            if (pos2 < pos1) {
                // Little endian
                newPieces.Add(firstNode = joinRecord.pieces[pos2]);
                pos2 += 1;
                while (pos2 <= pos1) {
                    newPieces.Add(joinRecord.pieces[pos2]);
                    pos2 += 1;
                }
                VarnodeData lastNode = newPieces[newPieces.Count - 1];
                lastNode.offset = addr1.getOffset();
                lastNode.size -= (uint)sizeTrunc1;
                firstNode.size -= (uint)sizeTrunc2;
            }
            else {
                newPieces.Add(firstNode = joinRecord.pieces[pos1]);
                pos1 += 1;
                while (pos1 <= pos2) {
                    newPieces.Add(joinRecord.pieces[pos1]);
                    pos1 += 1;
                }
                firstNode.offset = addr1.getOffset();
                firstNode.size -= (uint)sizeTrunc1;
                VarnodeData lastNode = newPieces[newPieces.Count - 1];
                lastNode.size -= (uint)sizeTrunc2;
            }
            JoinRecord newJoinRecord = findAddJoin(newPieces, 0);
            Address newAddress = new Address(newJoinRecord.unified.space, newJoinRecord.unified.offset);
            // Mimic Assignment operator behavior.
            addr.@base = newAddress.@base;
            addr.offset = newAddress.offset;
        }

        /// \brief Parse a string with just an \e address \e space name and a hex offset
        /// The string \e must contain a hexadecimal offset.  The offset may be optionally prepended with "0x".
        /// The string may optionally start with the name of the address space to associate with the offset, followed
        /// by ':' to separate it from the offset.  If the name is not present, the default data space is assumed.
        /// \param val is the string to parse
        /// \return the parsed address
        public Address parseAddressSimple(string val)
        {
            int col = val.IndexOf(':');
            AddrSpace spc;
            if (-1 == col) {
                spc = getDefaultDataSpace();
                col = 0;
            }
            else {
                string spcName = val.Substring(0, col);
                spc = getSpaceByName(spcName);
                if (null == spc) {
                    throw new LowlevelError($"Unknown address space: {spcName}");
                }
                col += 1;
            }
            if (col + 2 <= val.Length) {
                if (val[col] == '0' && val[col + 1] == 'x') {
                    col += 2;
                }
            }
            StreamReader s = new StreamReader(val.Substring(col));
            ulong off = s.ReadDecimalUnsignedLongInteger();
            return new Address(spc, AddrSpace.addressToByte(off, spc.getWordSize()));
        }
    }

    /// \brief The interface to a translation engine for a processor.
    /// This interface performs translations of instruction data
    /// for a particular processor.  It has two main functions
    ///     - Disassemble single machine instructions
    ///     - %Translate single machine instructions into \e pcode.
    /// It is also the repository for information about the exact
    /// configuration of the reverse engineering model associated
    /// with the processor. In particular, it knows about all the
    /// address spaces, registers, and spacebases for the processor.
    public abstract class Translate : AddrSpaceManager
    {
        /// Tagged addresses in the \e unique address space
        public enum UniqueLayout
        {
            /// Location of the runtime temporary for boolean inversion
            RUNTIME_BOOLEAN_INVERT = 0,
            /// Location of the runtime temporary storing the return value
            RUNTIME_RETURN_LOCATION = 0x80,
            /// Location of the runtime temporary for storing an effective address
            RUNTIME_BITRANGE_EA = 0x100,
            /// Range of temporaries for use in compiling p-code snippets
            INJECT = 0x200,
            /// Range of temporaries for use during decompiler analysis
            ANALYSIS = 0x10000000
        }

        /// \b true if the general endianness of the process is big endian
        private bool target_isbigendian;
        /// Starting offset into unique space
        private uint unique_base;
        /// Byte modulo on which instructions are aligned
        protected int alignment;
        /// Floating point formats utilized by the processor
        protected List<FloatFormat> floatformats;

        ///< Set general endianness to \b big if val is \b true
        /// Although endianness is usually specified on the space, most languages set an endianness
        /// across the entire processor.  This routine sets the endianness to \b big if the -val-
        /// is passed in as \b true. Otherwise, the endianness is set to \b small.
        /// \param val is \b true if the endianness should be set to \b big
        protected void setBigEndian(bool val)
        {
            target_isbigendian = val;
        }

        ///< Set the base offset for new temporary registers
        /// The \e unique address space, for allocating temporary registers,
        /// is used for both registers needed by the pcode translation
        /// engine and, later, by the simplification engine.  This routine
        /// sets the boundary of the portion of the space allocated
        /// for the pcode engine, and sets the base offset where registers
        /// created by the simplification process can start being allocated.
        /// \param val is the boundary offset
        protected void setUniqueBase(uint val)
        {
            if (val > unique_base) unique_base = val;
        }

        ///< Constructor for the translator
        /// This constructs only a shell for the Translate object.  It
        /// won't be usable until it is initialized for a specific processor
        /// The main entry point for this is the Translate::initialize method,
        /// which must be overridden by a derived class
        public Translate()
        {
            target_isbigendian = false;
            unique_base = 0;
            alignment = 1;
        }

        ///< If no explicit float formats, set up default formats
        /// If no floating-point format objects were registered by the \b initialize method, this
        /// method will fill in some suitable default formats.  These defaults are based on
        /// the 4-byte and 8-byte encoding specified by the IEEE 754 standard.
        public void setDefaultFloatFormats()
        {
            if (0 == floatformats.Count) {
                // Default IEEE 754 float formats
                floatformats.Add(new FloatFormat(4));
                floatformats.Add(new FloatFormat(8));
            }
        }

        ///< Is the processor big endian?
        /// Processors can usually be described as using a big endian
        /// encoding or a little endian encoding. This routine returns
        /// \b true if the processor globally uses big endian encoding.
        /// \return \b true if big endian
        public bool isBigEndian()
        {
            return target_isbigendian;
        }

        ///< Get format for a particular floating point encoding
        /// The pcode model for floating point encoding assumes that a
        /// consistent encoding is used for all values of a given size.
        /// This routine fetches the FloatFormat object given the size,
        /// in bytes, of the desired encoding.
        /// \param size is the size of the floating-point value in bytes
        /// \return a pointer to the floating-point format
        public FloatFormat? getFloatFormat(int size)
        {
            foreach (FloatFormat scannedFormat in floatformats) {
                if (scannedFormat.getSize() == size) {
                    return scannedFormat;
                }
            }
            return null;
        }

        ///< Get the instruction alignment for the processor
        /// If machine instructions need to have a specific alignment
        /// for this processor, this routine returns it. I.e. a return
        /// value of 4, means that the address of all instructions
        /// must be a multiple of 4. If there is no
        /// specific alignment requirement, this routine returns 1.
        /// \return the instruction alignment
        public int getAlignment()
        {
            return alignment;
        }

        ///< Get the base offset for new temporary registers
        /// Return the first offset within the \e unique space after the range statically reserved by Translate.
        /// This is generally the starting offset where dynamic temporary registers can start to be allocated.
        /// \return the first allocatable offset
        public uint getUniqueBase()
        {
            return unique_base;
        }

        ///< Get a tagged address within the \e unique space
        /// Regions of the \e unique space are reserved for specific uses. We select the start of a specific
        /// region based on the given tag.
        /// \param layout is the given tag
        /// \return the absolute offset into the \e unique space
        public uint getUniqueStart(UniqueLayout layout)
        {
            return (layout != UniqueLayout.ANALYSIS)
                ? (uint)layout + unique_base
                : (uint)layout;
        }

        /// \brief Initialize the translator given XML configuration documents
        /// A translator gets initialized once, possibly using XML documents
        /// to configure it.
        /// \param store is a set of configuration documents
        public abstract void initialize(DocumentStorage store);

        /// \brief Add a new context variable to the model for this processor
        /// Add the name of a context register used by the processor and
        /// how that register is packed into the context state. This
        /// information is used by a ContextDatabase to associate names
        /// with context information and to pack context into a single
        /// state variable for the translation engine.
        /// \param name is the name of the new context variable
        /// \param sbit is the first bit of the variable in the packed state
        /// \param ebit is the last bit of the variable in the packed state
        public virtual void registerContext(ref string name, int sbit, int ebit)
        {
        }

        /// \brief Set the default value for a particular context variable
        /// Set the value to be returned for a context variable when
        /// there are no explicit address ranges specifying a value
        /// for the variable.
        /// \param name is the name of the context variable
        /// \param val is the value to be considered default
        public virtual void setContextDefault(ref string name, uint val)
        {
        }

        /// \brief Toggle whether disassembly is allowed to affect context
        /// By default the disassembly/pcode translation engine can change
        /// the global context, thereby affecting later disassembly.  Context
        /// may be getting determined by something other than control flow in,
        /// the disassembly, in which case this function can turn off changes
        /// made by the disassembly
        /// \param val is \b true to allow context changes, \b false prevents changes
        public virtual void allowContextSet(bool val)
        {
        }

        /// \brief Get a register as VarnodeData given its name
        /// Retrieve the location and size of a register given its name
        /// \param nm is the name of the register
        /// \return the VarnodeData for the register
        public abstract ref VarnodeData getRegister(string nm);

        /// \brief Get the name of a register given its location
        /// Generic references to locations in a \e register space can
        /// be translated into the associated register \e name.  If the
        /// location doesn't match a register \e exactly, an empty string
        /// is returned.
        /// \param base is the address space containing the location
        /// \param off is the offset of the location
        /// \param size is the size of the location
        /// \return the name of the register, or an empty string
        public abstract string getRegisterName(AddrSpace @base, ulong off, int size);

        /// \brief Get a list of all register names and the corresponding location
        /// Most processors have a list of named registers and possibly other memory locations
        /// that are specific to it.  This function populates a map from the location information
        /// to the name, for every named location known by the translator
        /// \param reglist is the map which will be populated by the call
        public abstract void getAllRegisters(Dictionary<VarnodeData, string> reglist);

        /// \brief Get a list of all \e user-defined pcode ops
        /// The pcode model allows processors to define new pcode
        /// instructions that are specific to that processor. These
        /// \e user-defined instructions are all identified by a name
        /// and an index.  This method returns a list of these ops
        /// in index order.
        /// \param res is the resulting List of user op names
        public abstract void getUserOpNames(ref List<string> res);

        /// \brief Get the length of a machine instruction
        /// This method decodes an instruction at a specific address
        /// just enough to find the number of bytes it uses within the
        /// instruction stream.
        /// \param baseaddr is the Address of the instruction
        /// \return the number of bytes in the instruction
        public abstract int instructionLength(ref Address baseaddr);

        /// \brief Transform a single machine instruction into pcode
        /// This is the main interface to the pcode translation engine.
        /// The \e dump method in the \e emit object is invoked exactly
        /// once for each pcode operation in the translation for the
        /// machine instruction at the given address.
        /// This routine can throw either
        ///     - UnimplError or
        ///     - BadDataError
        /// \param emit is the tailored pcode emitting object
        /// \param baseaddr is the Address of the machine instruction
        /// \return the number of bytes in the machine instruction
        public abstract int oneInstruction(ref PcodeEmit emit, ref Address baseaddr);

        /// \brief Disassemble a single machine instruction
        /// This is the main interface to the disassembler for the
        /// processor.  It disassembles a single instruction and
        /// returns the result to the application via the \e dump
        /// method in the \e emit object.
        /// \param emit is the disassembly emitting object
        /// \param baseaddr is the address of the machine instruction to disassemble
        public abstract int printAssembly(AssemblyEmit emit, ref Address baseaddr);
    }
}
