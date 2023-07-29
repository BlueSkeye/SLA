using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief An annotation for a specific collection of hierarchical data
    /// This class parallels the XML concept of an \b element.  An ElementId describes a collection of data, where each
    /// piece is annotated by a specific AttributeId.  In addition, each ElementId can contain zero or more \e child
    /// ElementId objects, forming a hierarchy of annotated data.  Each ElementId has a name, which is unique at least
    /// within the context of its parent ElementId. Internally this name is associated with an integer id. A special
    /// AttributeId ATTRIB_CONTENT is used to label the XML element's text content, which is traditionally not labeled
    /// as an attribute.
    public class ElementId
    {
        ///< A map of ElementId names to their associated id
        private static Dictionary<string, uint> lookupElementId;

        internal static readonly ElementId ELEM_DATA = new ElementId("data", 1);
        internal static readonly ElementId ELEM_INPUT = new ElementId("input", 2);
        internal static readonly ElementId ELEM_OFF = new ElementId("off", 3);
        internal static readonly ElementId ELEM_OUTPUT = new ElementId("output", 4);
        internal static readonly ElementId ELEM_RETURNADDRESS = new ElementId("returnaddress", 5);
        internal static readonly ElementId ELEM_SYMBOL = new ElementId("symbol", 6);
        internal static readonly ElementId ELEM_TARGET = new ElementId("target", 7);
        internal static readonly ElementId ELEM_VAL = new ElementId("val", 8);
        internal static readonly ElementId ELEM_VALUE = new ElementId("value", 9);
        internal static readonly ElementId ELEM_VOID = new ElementId("void", 10);
        internal static readonly ElementId ELEM_ADDR = new ElementId("addr", 11);
        internal static readonly ElementId ELEM_RANGE = new ElementId("range", 12);
        internal static readonly ElementId ELEM_RANGELIST = new ElementId("rangelist", 13);
        internal static readonly ElementId ELEM_REGISTER = new ElementId("register", 14);
        internal static readonly ElementId ELEM_SEQNUM = new ElementId("seqnum", 15);
        internal static readonly ElementId ELEM_VARNODE = new ElementId("varnode", 16);
        internal static readonly ElementId ELEM_BREAK = new ElementId("break", 17);
        internal static readonly ElementId ELEM_CLANG_DOCUMENT = new ElementId("clang_document", 18);
        internal static readonly ElementId ELEM_FUNCNAME = new ElementId("funcname", 19);
        internal static readonly ElementId ELEM_FUNCPROTO = new ElementId("funcproto", 20);
        internal static readonly ElementId ELEM_LABEL = new ElementId("label", 21);
        internal static readonly ElementId ELEM_RETURN_TYPE = new ElementId("return_type", 22);
        internal static readonly ElementId ELEM_STATEMENT = new ElementId("statement", 23);
        internal static readonly ElementId ELEM_SYNTAX = new ElementId("syntax", 24);
        internal static readonly ElementId ELEM_VARDECL = new ElementId("vardecl", 25);
        internal static readonly ElementId ELEM_VARIABLE = new ElementId("variable", 26);
        internal static readonly ElementId ELEM_OP = new ElementId("op", 27);
        internal static readonly ElementId ELEM_SLEIGH = new ElementId("sleigh", 28);
        internal static readonly ElementId ELEM_SPACE = new ElementId("space", 29);
        internal static readonly ElementId ELEM_SPACEID = new ElementId("spaceid", 30);
        internal static readonly ElementId ELEM_SPACES = new ElementId("spaces", 31);
        internal static readonly ElementId ELEM_SPACE_BASE = new ElementId("space_base", 32);
        internal static readonly ElementId ELEM_SPACE_OTHER = new ElementId("space_other", 33);
        internal static readonly ElementId ELEM_SPACE_OVERLAY = new ElementId("space_overlay", 34);
        internal static readonly ElementId ELEM_SPACE_UNIQUE = new ElementId("space_unique", 35);
        internal static readonly ElementId ELEM_TRUNCATE_SPACE = new ElementId("truncate_space", 36);
        //ElementId ELEM_ABSOLUTE_MAX_ALIGNMENT = ElementId("absolute_max_alignment", 37);
        //ElementId ELEM_BITFIELD_PACKING = ElementId("bitfield_packing", 38);
        //ElementId ELEM_CHAR_SIZE = ElementId("char_size", 39);
        //ElementId ELEM_CHAR_TYPE = ElementId("char_type", 40);
        internal static readonly ElementId ELEM_CORETYPES = new ElementId("coretypes", 41);
        internal static readonly ElementId ELEM_DATA_ORGANIZATION = new ElementId("data_organization", 42);
        internal static readonly ElementId ELEM_DEF = new ElementId("def", 43);
        //ElementId ELEM_DEFAULT_ALIGNMENT = ElementId("default_alignment", 44);
        //ElementId ELEM_DEFAULT_POINTER_ALIGNMENT = ElementId("default_pointer_alignment", 45);
        //ElementId ELEM_DOUBLE_SIZE = ElementId("double_size", 46);
        internal static readonly ElementId ELEM_ENTRY = new ElementId("entry", 47);
        internal static readonly ElementId ELEM_ENUM = new ElementId("enum", 48);
        internal static readonly ElementId ELEM_FIELD = new ElementId("field", 49);
        //ElementId ELEM_FLOAT_SIZE = ElementId("float_size", 50);
        internal static readonly ElementId ELEM_INTEGER_SIZE = new ElementId("integer_size", 51);
        //ElementId ELEM_LONG_DOUBLE_SIZE = ElementId("long_double_size", 52);
        //ElementId ELEM_LONG_LONG_SIZE = ElementId("long_long_size", 53);
        internal static readonly ElementId ELEM_LONG_SIZE = new ElementId("long_size", 54);
        //ElementId ELEM_MACHINE_ALIGNMENT = ElementId("machine_alignment", 55);
        //ElementId ELEM_POINTER_SHIFT = ElementId("pointer_shift", 56);
        //ElementId ELEM_POINTER_SIZE = ElementId("pointer_size", 57);
        //ElementId ELEM_SHORT_SIZE = ElementId("short_size", 58);
        internal static readonly ElementId ELEM_SIZE_ALIGNMENT_MAP = new ElementId("size_alignment_map", 59);
        internal static readonly ElementId ELEM_TYPE = new ElementId("type", 60);
        //ElementId ELEM_TYPE_ALIGNMENT_ENABLED = ElementId("type_alignment_enabled", 61);
        internal static readonly ElementId ELEM_TYPEGRP = new ElementId("typegrp", 62);
        internal static readonly ElementId ELEM_TYPEREF = new ElementId("typeref", 63);
        //ElementId ELEM_USE_MS_CONVENTION = ElementId("use_MS_convention", 64);
        //ElementId ELEM_WCHAR_SIZE = ElementId("wchar_size", 65);
        //ElementId ELEM_ZERO_LENGTH_BOUNDARY = ElementId("zero_length_boundary", 66);
        internal static readonly ElementId ELEM_COLLISION = new ElementId("collision", 67);
        internal static readonly ElementId ELEM_DB = new ElementId("db", 68);
        internal static readonly ElementId ELEM_EQUATESYMBOL = new ElementId("equatesymbol", 69);
        internal static readonly ElementId ELEM_EXTERNREFSYMBOL = new ElementId("externrefsymbol", 70);
        internal static readonly ElementId ELEM_FACETSYMBOL = new ElementId("facetsymbol", 71);
        internal static readonly ElementId ELEM_FUNCTIONSHELL = new ElementId("functionshell", 72);
        internal static readonly ElementId ELEM_HASH = new ElementId("hash", 73);
        internal static readonly ElementId ELEM_HOLE = new ElementId("hole", 74);
        internal static readonly ElementId ELEM_LABELSYM = new ElementId("labelsym", 75);
        internal static readonly ElementId ELEM_MAPSYM = new ElementId("mapsym", 76);
        internal static readonly ElementId ELEM_PARENT = new ElementId("parent", 77);
        internal static readonly ElementId ELEM_PROPERTY_CHANGEPOINT = new ElementId("property_changepoint", 78);
        internal static readonly ElementId ELEM_RANGEEQUALSSYMBOLS = new ElementId("rangeequalssymbols", 79);
        internal static readonly ElementId ELEM_SCOPE = new ElementId("scope", 80);
        internal static readonly ElementId ELEM_SYMBOLLIST = new ElementId("symbollist", 81);
        internal static readonly ElementId ELEM_HIGH = new ElementId("high", 82);
        internal static readonly ElementId ELEM_BYTES = new ElementId("bytes", 83);
        internal static readonly ElementId ELEM_STRING = new ElementId("string", 84);
        internal static readonly ElementId ELEM_STRINGMANAGE = new ElementId("stringmanage", 85);
        internal static readonly ElementId ELEM_COMMENT = new ElementId("comment", 86);
        internal static readonly ElementId ELEM_COMMENTDB = new ElementId("commentdb", 87);
        internal static readonly ElementId ELEM_TEXT = new ElementId("text", 88);
        internal static readonly ElementId ELEM_ADDR_PCODE = new ElementId("addr_pcode", 89);
        internal static readonly ElementId ELEM_BODY = new ElementId("body", 90);
        internal static readonly ElementId ELEM_CALLFIXUP = new ElementId("callfixup", 91);
        internal static readonly ElementId ELEM_CALLOTHERFIXUP = new ElementId("callotherfixup", 92);
        internal static readonly ElementId ELEM_CASE_PCODE = new ElementId("case_pcode", 93);
        internal static readonly ElementId ELEM_CONTEXT = new ElementId("context", 94);
        internal static readonly ElementId ELEM_DEFAULT_PCODE = new ElementId("default_pcode", 95);
        internal static readonly ElementId ELEM_INJECT = new ElementId("inject", 96);
        internal static readonly ElementId ELEM_INJECTDEBUG = new ElementId("injectdebug", 97);
        internal static readonly ElementId ELEM_INST = new ElementId("inst", 98);
        internal static readonly ElementId ELEM_PAYLOAD = new ElementId("payload", 99);
        internal static readonly ElementId ELEM_PCODE = new ElementId("pcode", 100);
        internal static readonly ElementId ELEM_SIZE_PCODE = new ElementId("size_pcode", 101);
        internal static readonly ElementId ELEM_BHEAD = new ElementId("bhead", 102);
        internal static readonly ElementId ELEM_BLOCK = new ElementId("block", 103);
        internal static readonly ElementId ELEM_BLOCKEDGE = new ElementId("blockedge", 104);
        internal static readonly ElementId ELEM_EDGE = new ElementId("edge", 105);
        internal static readonly ElementId ELEM_PARAMMEASURES = new ElementId("parammeasures", 106);
        internal static readonly ElementId ELEM_PROTO = new ElementId("proto", 107);
        internal static readonly ElementId ELEM_RANK = new ElementId("rank", 108);
        internal static readonly ElementId ELEM_CONSTANTPOOL = new ElementId("constantpool", 109);
        internal static readonly ElementId ELEM_CPOOLREC = new ElementId("cpoolrec", 110);
        internal static readonly ElementId ELEM_REF = new ElementId("ref", 111);
        internal static readonly ElementId ELEM_AST = new ElementId("ast", 115);
        internal static readonly ElementId ELEM_FUNCTION = new ElementId("function", 116);
        internal static readonly ElementId ELEM_HIGHLIST = new ElementId("highlist", 117);
        internal static readonly ElementId ELEM_JUMPTABLELIST = new ElementId("jumptablelist", 118);
        internal static readonly ElementId ELEM_VARNODES = new ElementId("varnodes", 119);
        internal static readonly ElementId ELEM_TOKEN = new ElementId("token", 112);
        internal static readonly ElementId ELEM_IOP = new ElementId("iop", 113);
        internal static readonly ElementId ELEM_UNIMPL = new ElementId("unimpl", 114);
        internal static readonly ElementId ELEM_CONTEXT_DATA = new ElementId("context_data", 120);
        internal static readonly ElementId ELEM_CONTEXT_POINTS = new ElementId("context_points", 121);
        internal static readonly ElementId ELEM_CONTEXT_POINTSET = new ElementId("context_pointset", 122);
        internal static readonly ElementId ELEM_CONTEXT_SET = new ElementId("context_set", 123);
        internal static readonly ElementId ELEM_SET = new ElementId("set", 124);
        internal static readonly ElementId ELEM_TRACKED_POINTSET = new ElementId("tracked_pointset", 125);
        internal static readonly ElementId ELEM_TRACKED_SET = new ElementId("tracked_set", 126);
        internal static readonly ElementId ELEM_CONSTRESOLVE = new ElementId("constresolve", 127);
        internal static readonly ElementId ELEM_JUMPASSIST = new ElementId("jumpassist", 128);
        internal static readonly ElementId ELEM_SEGMENTOP = new ElementId("segmentop", 129);
        internal static readonly ElementId ELEM_ADDRESS_SHIFT_AMOUNT = new ElementId("address_shift_amount", 130);
        internal static readonly ElementId ELEM_AGGRESSIVETRIM = new ElementId("aggressivetrim", 131);
        internal static readonly ElementId ELEM_COMPILER_SPEC = new ElementId("compiler_spec", 132);
        internal static readonly ElementId ELEM_DATA_SPACE = new ElementId("data_space", 133);
        internal static readonly ElementId ELEM_DEFAULT_MEMORY_BLOCKS = new ElementId("default_memory_blocks", 134);
        internal static readonly ElementId ELEM_DEFAULT_PROTO = new ElementId("default_proto", 135);
        internal static readonly ElementId ELEM_DEFAULT_SYMBOLS = new ElementId("default_symbols", 136);
        internal static readonly ElementId ELEM_EVAL_CALLED_PROTOTYPE = new ElementId("eval_called_prototype", 137);
        internal static readonly ElementId ELEM_EVAL_CURRENT_PROTOTYPE = new ElementId("eval_current_prototype", 138);
        internal static readonly ElementId ELEM_EXPERIMENTAL_RULES = new ElementId("experimental_rules", 139);
        internal static readonly ElementId ELEM_FLOWOVERRIDELIST = new ElementId("flowoverridelist", 140);
        internal static readonly ElementId ELEM_FUNCPTR = new ElementId("funcptr", 141);
        internal static readonly ElementId ELEM_GLOBAL = new ElementId("global", 142);
        internal static readonly ElementId ELEM_INCIDENTALCOPY = new ElementId("incidentalcopy", 143);
        internal static readonly ElementId ELEM_INFERPTRBOUNDS = new ElementId("inferptrbounds", 144);
        internal static readonly ElementId ELEM_MODELALIAS = new ElementId("modelalias", 145);
        internal static readonly ElementId ELEM_NOHIGHPTR = new ElementId("nohighptr", 146);
        internal static readonly ElementId ELEM_PROCESSOR_SPEC = new ElementId("processor_spec", 147);
        internal static readonly ElementId ELEM_PROGRAMCOUNTER = new ElementId("programcounter", 148);
        internal static readonly ElementId ELEM_PROPERTIES = new ElementId("properties", 149);
        internal static readonly ElementId ELEM_PROPERTY = new ElementId("property", 150);
        internal static readonly ElementId ELEM_READONLY = new ElementId("readonly", 151);
        internal static readonly ElementId ELEM_REGISTER_DATA = new ElementId("register_data", 152);
        internal static readonly ElementId ELEM_RULE = new ElementId("rule", 153);
        internal static readonly ElementId ELEM_SAVE_STATE = new ElementId("save_state", 154);
        internal static readonly ElementId ELEM_SEGMENTED_ADDRESS = new ElementId("segmented_address", 155);
        internal static readonly ElementId ELEM_SPACEBASE = new ElementId("spacebase", 156);
        internal static readonly ElementId ELEM_SPECEXTENSIONS = new ElementId("specextensions", 157);
        internal static readonly ElementId ELEM_STACKPOINTER = new ElementId("stackpointer", 158);
        internal static readonly ElementId ELEM_VOLATILE = new ElementId("volatile", 159);
        internal static readonly ElementId ELEM_GROUP = new ElementId("group", 160);
        internal static readonly ElementId ELEM_INTERNALLIST = new ElementId("internallist", 161);
        internal static readonly ElementId ELEM_KILLEDBYCALL = new ElementId("killedbycall", 162);
        internal static readonly ElementId ELEM_LIKELYTRASH = new ElementId("likelytrash", 163);
        internal static readonly ElementId ELEM_LOCALRANGE = new ElementId("localrange", 164);
        internal static readonly ElementId ELEM_MODEL = new ElementId("model", 165);
        internal static readonly ElementId ELEM_PARAM = new ElementId("param", 166);
        internal static readonly ElementId ELEM_PARAMRANGE = new ElementId("paramrange", 167);
        internal static readonly ElementId ELEM_PENTRY = new ElementId("pentry", 168);
        internal static readonly ElementId ELEM_PROTOTYPE = new ElementId("prototype", 169);
        internal static readonly ElementId ELEM_RESOLVEPROTOTYPE = new ElementId("resolveprototype", 170);
        internal static readonly ElementId ELEM_RETPARAM = new ElementId("retparam", 171);
        internal static readonly ElementId ELEM_RETURNSYM = new ElementId("returnsym", 172);
        internal static readonly ElementId ELEM_UNAFFECTED = new ElementId("unaffected", 173);        // Number serves as next open index
        internal static readonly ElementId ELEM_ALIASBLOCK = new ElementId("aliasblock", 174);
        internal static readonly ElementId ELEM_ALLOWCONTEXTSET = new ElementId("allowcontextset", 175);
        internal static readonly ElementId ELEM_ANALYZEFORLOOPS = new ElementId("analyzeforloops", 176);
        internal static readonly ElementId ELEM_COMMENTHEADER = new ElementId("commentheader", 177);
        internal static readonly ElementId ELEM_COMMENTINDENT = new ElementId("commentindent", 178);
        internal static readonly ElementId ELEM_COMMENTINSTRUCTION = new ElementId("commentinstruction", 179);
        internal static readonly ElementId ELEM_COMMENTSTYLE = new ElementId("commentstyle", 180);
        internal static readonly ElementId ELEM_CONVENTIONPRINTING = new ElementId("conventionprinting", 181);
        internal static readonly ElementId ELEM_CURRENTACTION = new ElementId("currentaction", 182);
        internal static readonly ElementId ELEM_DEFAULTPROTOTYPE = new ElementId("defaultprototype", 183);
        internal static readonly ElementId ELEM_ERRORREINTERPRETED = new ElementId("errorreinterpreted", 184);
        internal static readonly ElementId ELEM_ERRORTOOMANYINSTRUCTIONS = new ElementId("errortoomanyinstructions", 185);
        internal static readonly ElementId ELEM_ERRORUNIMPLEMENTED = new ElementId("errorunimplemented", 186);
        internal static readonly ElementId ELEM_EXTRAPOP = new ElementId("extrapop", 187);
        internal static readonly ElementId ELEM_IGNOREUNIMPLEMENTED = new ElementId("ignoreunimplemented", 188);
        internal static readonly ElementId ELEM_INDENTINCREMENT = new ElementId("indentincrement", 189);
        internal static readonly ElementId ELEM_INFERCONSTPTR = new ElementId("inferconstptr", 190);
        internal static readonly ElementId ELEM_INLINE = new ElementId("inline", 191);
        internal static readonly ElementId ELEM_INPLACEOPS = new ElementId("inplaceops", 192);
        internal static readonly ElementId ELEM_INTEGERFORMAT = new ElementId("integerformat", 193);
        internal static readonly ElementId ELEM_JUMPLOAD = new ElementId("jumpload", 194);
        internal static readonly ElementId ELEM_MAXINSTRUCTION = new ElementId("maxinstruction", 195);
        internal static readonly ElementId ELEM_MAXLINEWIDTH = new ElementId("maxlinewidth", 196);
        internal static readonly ElementId ELEM_NAMESPACESTRATEGY = new ElementId("namespacestrategy", 197);
        internal static readonly ElementId ELEM_NOCASTPRINTING = new ElementId("nocastprinting", 198);
        internal static readonly ElementId ELEM_NORETURN = new ElementId("noreturn", 199);
        internal static readonly ElementId ELEM_NULLPRINTING = new ElementId("nullprinting", 200);
        internal static readonly ElementId ELEM_OPTIONSLIST = new ElementId("optionslist", 201);
        internal static readonly ElementId ELEM_PARAM1 = new ElementId("param1", 202);
        internal static readonly ElementId ELEM_PARAM2 = new ElementId("param2", 203);
        internal static readonly ElementId ELEM_PARAM3 = new ElementId("param3", 204);
        internal static readonly ElementId ELEM_PROTOEVAL = new ElementId("protoeval", 205);
        internal static readonly ElementId ELEM_SETACTION = new ElementId("setaction", 206);
        internal static readonly ElementId ELEM_SETLANGUAGE = new ElementId("setlanguage", 207);
        internal static readonly ElementId ELEM_SPLITDATATYPE = new ElementId("splitdatatype", 270);
        internal static readonly ElementId ELEM_STRUCTALIGN = new ElementId("structalign", 208);
        internal static readonly ElementId ELEM_TOGGLERULE = new ElementId("togglerule", 209);
        internal static readonly ElementId ELEM_WARNING = new ElementId("warning", 210);
        internal static readonly ElementId ELEM_BASICOVERRIDE = new ElementId("basicoverride", 211);
        internal static readonly ElementId ELEM_DEST = new ElementId("dest", 212);
        internal static readonly ElementId ELEM_JUMPTABLE = new ElementId("jumptable", 213);
        internal static readonly ElementId ELEM_LOADTABLE = new ElementId("loadtable", 214);
        internal static readonly ElementId ELEM_NORMADDR = new ElementId("normaddr", 215);
        internal static readonly ElementId ELEM_NORMHASH = new ElementId("normhash", 216);
        internal static readonly ElementId ELEM_STARTVAL = new ElementId("startval", 217);
        internal static readonly ElementId ELEM_DEADCODEDELAY = new ElementId("deadcodedelay", 218);
        internal static readonly ElementId ELEM_FLOW = new ElementId("flow", 219);
        internal static readonly ElementId ELEM_FORCEGOTO = new ElementId("forcegoto", 220);
        internal static readonly ElementId ELEM_INDIRECTOVERRIDE = new ElementId("indirectoverride", 221);
        internal static readonly ElementId ELEM_MULTISTAGEJUMP = new ElementId("multistagejump", 222);
        internal static readonly ElementId ELEM_OVERRIDE = new ElementId("override", 223);
        internal static readonly ElementId ELEM_PROTOOVERRIDE = new ElementId("protooverride", 224);
        internal static readonly ElementId ELEM_CALLGRAPH = new ElementId("callgraph", 226);
        internal static readonly ElementId ELEM_NODE = new ElementId("node", 227);
        internal static readonly ElementId ELEM_LOCALDB = new ElementId("localdb", 228);
        internal static readonly ElementId ELEM_BINARYIMAGE = new ElementId("binaryimage", 230);
        internal static readonly ElementId ELEM_BYTECHUNK = new ElementId("bytechunk", 231);
        internal static readonly ElementId ELEM_COMPILER = new ElementId("compiler", 232);
        internal static readonly ElementId ELEM_DESCRIPTION = new ElementId("description", 233);
        internal static readonly ElementId ELEM_LANGUAGE = new ElementId("language", 234);
        internal static readonly ElementId ELEM_LANGUAGE_DEFINITIONS = new ElementId("language_definitions", 235);
        internal static readonly ElementId ELEM_XML_SAVEFILE = new ElementId("xml_savefile", 236);
        internal static readonly ElementId ELEM_RAW_SAVEFILE = new ElementId("raw_savefile", 237);
        internal static readonly ElementId ELEM_BFD_SAVEFILE = new ElementId("bfd_savefile", 238);
        internal static readonly ElementId ELEM_JUMPTABLEMAX = new ElementId("jumptablemax", 271);
        internal static readonly ElementId ELEM_UNKNOWN = new ElementId("XMLunknown", 272);

        /// The name of the element
        private string name;
        /// The (internal) id of the attribute
        private uint id;
        private static List<ElementId>? _thelist;

        /// Retrieve the list of static ElementId
        /// Access static vector of ElementId objects that are registered during static initialization
        /// The list itself is created once on the first call to this method.
        /// \return a reference to the vector
        private static ref List<ElementId> getList()
        {
            if (null == _thelist) {
                _thelist = new List<ElementId>();
            }
            return ref _thelist;
        }

        /// Construct given a name and id
        /// This constructor should only be invoked for static objects.  It registers the element for inclusion
        /// in the global hashtable.
        /// \param nm is the name of the element
        /// \param i is an id to associate with the element
        public ElementId(string nm, uint i)
        {
            name = nm;
            id = i;
            getList().Add(this);
        }

        /// Get the element's name
        public ref string getName()
        {
            return ref name;
        }

        /// Get the element's id
        public uint getId()
        {
            return id;
        }

        /// Test equality with another ElementId
        public static bool operator ==(ElementId op1, ElementId op2)
        {
            return (op1.id == op2.id);
        }

        public static bool operator !=(ElementId op1, ElementId op2)
        {
            return !(op1 == op2);
        }

        // static uint find(const string &nm);          ///< Find the id associated with a specific element name

        /// Populate a hashtable with all ElementId objects
        /// Fill the hashtable mapping element names to their id, from registered element objects
        public static void initialize()
        {
            ref List<ElementId> thelist = ref getList();
            for (int i = 0; i < thelist.Count; ++i) {
                ElementId elem = thelist[i];
#if CPUI_DEBUG
                if (lookupElementId.find(elem.name) != lookupElementId.end()) {
                    throw DecoderError($"{elem.name} element registered more than once");
                }
#endif
                lookupElementId[elem.name] = elem.id;
            }
            thelist.Clear();
            thelist.TrimExcess();
        }

        /// Test equality of a raw integer id with an ElementId
        public static bool operator ==(uint id, ElementId op2)
        {
            return (id == op2.id);
        }

        /// Test equality of an ElementId with a raw integer id
        public static bool operator ==(ElementId op1, uint id)
        {
            return (op1.id == id);
        }

        ///< Test inequality of a raw integer id with an ElementId
        public static bool operator !=(uint id, ElementId op2)
        {
            return (id != op2.id);
        }

        ///< Test inequality of an ElementId with a raw integer id
        public static bool operator !=(ElementId op1, uint id)
        {
            return (op1.id != id);
        }

        /// The name is looked up in the global list of all elements.  If the element is not in the list, a special
        /// placeholder element, ELEM_UNKNOWN, is returned as a placeholder for elements with unrecognized names.
        /// \param nm is the name of the element
        /// \return the associated id
        public static uint find(string nm)
        {
            uint result;
            return lookupElementId.TryGetValue(nm, out result) ? result : ELEM_UNKNOWN.id;
        }
    }
}
