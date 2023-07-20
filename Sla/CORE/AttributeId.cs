using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief An annotation for a data element to being transferred to/from a stream
    /// This class parallels the XML concept of an \b attribute on an element. An AttributeId describes
    /// a particular piece of data associated with an ElementId.  The defining characteristic of the AttributeId is
    /// its name.  Internally this name is associated with an integer id.  The name (and id) uniquely determine
    /// the data being labeled, within the context of a specific ElementId.  Within this context, an AttributeId labels either
    ///   - An unsigned integer
    ///   - A signed integer
    ///   - A boolean value
    ///   - A string
    /// The same AttributeId can be used to label a different type of data when associated with a different ElementId.
    public class AttributeId
    {
        /// The name of the attribute
        private string name;
        /// The (internal) id of the attribute
        private uint id;

        // Common attributes.  Attributes with multiple uses
        internal static readonly AttributeId ATTRIB_CONTENT = new AttributeId("XMLcontent", 1);
        internal static readonly AttributeId ATTRIB_ALIGN = new AttributeId("align", 2);
        internal static readonly AttributeId ATTRIB_BIGENDIAN = new AttributeId("bigendian", 3);
        internal static readonly AttributeId ATTRIB_CONSTRUCTOR = new AttributeId("constructor", 4);
        internal static readonly AttributeId ATTRIB_DESTRUCTOR = new AttributeId("destructor", 5);
        internal static readonly AttributeId ATTRIB_EXTRAPOP = new AttributeId("extrapop", 6);
        internal static readonly AttributeId ATTRIB_FORMAT = new AttributeId("format", 7);
        internal static readonly AttributeId ATTRIB_HIDDENRETPARM = new AttributeId("hiddenretparm", 8);
        internal static readonly AttributeId ATTRIB_ID = new AttributeId("id", 9);
        internal static readonly AttributeId ATTRIB_INDEX = new AttributeId("index", 10);
        internal static readonly AttributeId ATTRIB_INDIRECTSTORAGE = new AttributeId("indirectstorage", 11);
        internal static readonly AttributeId ATTRIB_METATYPE = new AttributeId("metatype", 12);
        internal static readonly AttributeId ATTRIB_MODEL = new AttributeId("model", 13);
        internal static readonly AttributeId ATTRIB_NAME = new AttributeId("name", 14);
        internal static readonly AttributeId ATTRIB_NAMELOCK = new AttributeId("namelock", 15);
        internal static readonly AttributeId ATTRIB_OFFSET = new AttributeId("offset", 16);
        internal static readonly AttributeId ATTRIB_READONLY = new AttributeId("readonly", 17);
        internal static readonly AttributeId ATTRIB_REF = new AttributeId("ref", 18);
        internal static readonly AttributeId ATTRIB_SIZE = new AttributeId("size", 19);
        internal static readonly AttributeId ATTRIB_SPACE = new AttributeId("space", 20);
        internal static readonly AttributeId ATTRIB_THISPTR = new AttributeId("thisptr", 21);
        internal static readonly AttributeId ATTRIB_TYPE = new AttributeId("type", 22);
        internal static readonly AttributeId ATTRIB_TYPELOCK = new AttributeId("typelock", 23);
        internal static readonly AttributeId ATTRIB_VAL = new AttributeId("val", 24);
        internal static readonly AttributeId ATTRIB_VALUE = new AttributeId("value", 25);
        internal static readonly AttributeId ATTRIB_WORDSIZE = new AttributeId("wordsize", 26);
        internal static readonly AttributeId ATTRIB_FIRST = new AttributeId("first", 27);
        internal static readonly AttributeId ATTRIB_LAST = new AttributeId("last", 28);
        internal static readonly AttributeId ATTRIB_UNIQ = new AttributeId("uniq", 29);
        internal static readonly AttributeId ATTRIB_CODE = new AttributeId("code", 43);
        internal static readonly AttributeId ATTRIB_CONTAIN = new AttributeId("contain", 44);
        internal static readonly AttributeId ATTRIB_DEFAULTSPACE = new AttributeId("defaultspace", 45);
        internal static readonly AttributeId ATTRIB_UNIQBASE = new AttributeId("uniqbase", 46);
        internal static readonly AttributeId ATTRIB_CAT = new AttributeId("cat", 61);
        internal static readonly AttributeId ATTRIB_FIELD = new AttributeId("field", 62);
        internal static readonly AttributeId ATTRIB_MERGE = new AttributeId("merge", 63);
        internal static readonly AttributeId ATTRIB_SCOPEIDBYNAME = new AttributeId("scopeidbyname", 64);
        internal static readonly AttributeId ATTRIB_VOLATILE = new AttributeId("volatile", 65);
        internal static readonly AttributeId ATTRIB_DYNAMIC = new AttributeId("dynamic", 70);
        internal static readonly AttributeId ATTRIB_INCIDENTALCOPY = new AttributeId("incidentalcopy", 71);
        internal static readonly AttributeId ATTRIB_INJECT = new AttributeId("inject", 72);
        internal static readonly AttributeId ATTRIB_PARAMSHIFT = new AttributeId("paramshift", 73);
        internal static readonly AttributeId ATTRIB_TARGETOP = new AttributeId("targetop", 74);
        internal static readonly AttributeId ATTRIB_ALTINDEX = new AttributeId("altindex", 75);
        internal static readonly AttributeId ATTRIB_DEPTH = new AttributeId("depth", 76);
        internal static readonly AttributeId ATTRIB_END = new AttributeId("end", 77);
        internal static readonly AttributeId ATTRIB_OPCODE = new AttributeId("opcode", 78);
        internal static readonly AttributeId ATTRIB_REV = new AttributeId("rev", 79);
        internal static readonly AttributeId ATTRIB_A = new AttributeId("a", 80);
        internal static readonly AttributeId ATTRIB_B = new AttributeId("b", 81);
        internal static readonly AttributeId ATTRIB_LENGTH = new AttributeId("length", 82);
        internal static readonly AttributeId ATTRIB_TAG = new AttributeId("tag", 83);
        internal static readonly AttributeId ATTRIB_NOCODE = new AttributeId("nocode", 84);
        internal static readonly AttributeId ATTRIB_BASE = new AttributeId("base", 89);
        internal static readonly AttributeId ATTRIB_DEADCODEDELAY = new AttributeId("deadcodedelay", 90);
        internal static readonly AttributeId ATTRIB_DELAY = new AttributeId("delay", 91);
        internal static readonly AttributeId ATTRIB_LOGICALSIZE = new AttributeId("logicalsize", 92);
        internal static readonly AttributeId ATTRIB_PHYSICAL = new AttributeId("physical", 93);
        internal static readonly AttributeId ATTRIB_ADJUSTVMA = new AttributeId("adjustvma", 103);
        internal static readonly AttributeId ATTRIB_ENABLE = new AttributeId("enable", 104);
        internal static readonly AttributeId ATTRIB_GROUP = new AttributeId("group", 105);
        internal static readonly AttributeId ATTRIB_GROWTH = new AttributeId("growth", 106);
        internal static readonly AttributeId ATTRIB_KEY = new AttributeId("key", 107);
        internal static readonly AttributeId ATTRIB_LOADERSYMBOLS = new AttributeId("loadersymbols", 108);
        internal static readonly AttributeId ATTRIB_PARENT = new AttributeId("parent", 109);
        internal static readonly AttributeId ATTRIB_REGISTER = new AttributeId("register", 110);
        internal static readonly AttributeId ATTRIB_REVERSEJUSTIFY = new AttributeId("reversejustify", 111);
        internal static readonly AttributeId ATTRIB_SIGNEXT = new AttributeId("signext", 112);
        internal static readonly AttributeId ATTRIB_STYLE = new AttributeId("style", 113);
        internal static readonly AttributeId ATTRIB_CUSTOM = new AttributeId("custom", 114);
        internal static readonly AttributeId ATTRIB_DOTDOTDOT = new AttributeId("dotdotdot", 115);
        internal static readonly AttributeId ATTRIB_EXTENSION = new AttributeId("extension", 116);
        internal static readonly AttributeId ATTRIB_HASTHIS = new AttributeId("hasthis", 117);
        internal static readonly AttributeId ATTRIB_INLINE = new AttributeId("inline", 118);
        internal static readonly AttributeId ATTRIB_KILLEDBYCALL = new AttributeId("killedbycall", 119);
        internal static readonly AttributeId ATTRIB_MAXSIZE = new AttributeId("maxsize", 120);
        internal static readonly AttributeId ATTRIB_MINSIZE = new AttributeId("minsize", 121);
        internal static readonly AttributeId ATTRIB_MODELLOCK = new AttributeId("modellock", 122);
        internal static readonly AttributeId ATTRIB_NORETURN = new AttributeId("noreturn", 123);
        internal static readonly AttributeId ATTRIB_POINTERMAX = new AttributeId("pointermax", 124);
        internal static readonly AttributeId ATTRIB_SEPARATEFLOAT = new AttributeId("separatefloat", 125);
        internal static readonly AttributeId ATTRIB_STACKSHIFT = new AttributeId("stackshift", 126);
        internal static readonly AttributeId ATTRIB_STRATEGY = new AttributeId("strategy", 127);
        internal static readonly AttributeId ATTRIB_THISBEFORERETPOINTER = new AttributeId("thisbeforeretpointer", 128);
        internal static readonly AttributeId ATTRIB_VOIDLOCK = new AttributeId("voidlock", 129);
        internal static readonly AttributeId ATTRIB_LABEL = new AttributeId("label", 131);
        internal static readonly AttributeId ATTRIB_NUM = new AttributeId("num", 132);
        internal static readonly AttributeId ATTRIB_ADDRESS = new AttributeId("address", 148);

        // ATTRIB_PIECE is a special attribute for supporting the legacy attributes "piece1", "piece2", ..., "piece9",
        // It is effectively a sequence of indexed attributes for use with Encoder::writeStringIndexed.
        // The index starts at the ids reserved for "piece1" thru "piece9" but can extend farther.
        internal static readonly AttributeId ATTRIB_PIECE = new AttributeId("piece", 94);
        // Open slots 94-102
        // Common attributes.  Attributes with multiple uses
        
        // Number serves as next open index
        internal static readonly AttributeId ATTRIB_UNKNOWN = new AttributeId("XMLunknown", 149);

        /// A map of AttributeId names to their associated id
        private static Dictionary<string, uint> lookupAttributeId;

        private static List<AttributeId>? _thelist;

        /// Retrieve the list of static AttributeId
        /// Access static vector of AttributeId objects that are registered during static initialization
        /// The list itself is created once on the first call to this method.
        /// \return a reference to the vector
        private static ref List<AttributeId> getList()
        {
            if (null == _thelist) {
                _thelist = new List<AttributeId>();
            }
            return ref _thelist;
        }

        /// Construct given a name and id
        /// This constructor should only be invoked for static objects.  It registers the attribute for inclusion
        /// in the global hashtable.
        /// \param nm is the name of the attribute
        /// \param i is an id to associate with the attribute
        public AttributeId(string nm, uint i)
        {
            name = nm;
            id = i;
            getList().Add(this);
        }

        /// Get the attribute's name
        public ref string getName()
        {
            return ref name;
        }

        /// Get the attribute's id
        public uint getId()
        {
            return id;
        }

        /// Test equality with another AttributeId
        public static bool operator==(AttributeId op1, AttributeId op2)
        {
            return (op1.id == op2.id);
        }

        public static bool operator !=(AttributeId op1, AttributeId op2)
        {
            return !(op1 == op2);
        }

        // static uint find(const string &nm);          ///< Find the id associated with a specific attribute name

        ///< Populate a hashtable with all AttributeId objects
        /// Fill the hashtable mapping attribute names to their id, from registered attribute objects
        public static void initialize()
        {
            ref List<AttributeId> thelist = ref getList();
            for (int i = 0; i < thelist.Count; ++i) {
                AttributeId attrib = thelist[i];
#if CPUI_DEBUG
                if (lookupAttributeId.find(attrib->name) != lookupAttributeId.end()) {
                    throw DecoderError(attrib->name + " attribute registered more than once");
                }
#endif
                lookupAttributeId[attrib.name] = attrib.id;
            }
            thelist.Clear();
            thelist.TrimExcess();
        }

        /// Test equality of a raw integer id with an AttributeId
        public static bool operator ==(uint id, AttributeId op2)
        {
            return (id == op2.id);
        }

        public static bool operator !=(uint id, AttributeId op2)
        {
            return !(id == op2.id);
        }

        /// Test equality of an AttributeId with a raw integer id
        public static bool operator ==(AttributeId op1, uint id)
        {
            return (op1.id == id);
        }

        public static bool operator !=(AttributeId op1, uint id)
        {
            return !(op1 == id);
        }

        /// The name is looked up in the global list of all attributes.  If the attribute is not in the list, a special
        /// placeholder attribute, ATTRIB_UNKNOWN, is returned as a placeholder for attributes with unrecognized names.
        /// \param nm is the name of the attribute
        /// \return the associated id
        public static uint find(string nm)
        {
            uint result;
            return lookupAttributeId.TryGetValue(nm, out result) ? result : ATTRIB_UNKNOWN.id;
        }
    }
}
