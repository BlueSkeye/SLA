using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
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
        internal static readonly ElementId ELEM_COMMENT = new ElementId("comment", 86);
        internal static readonly ElementId ELEM_COMMENTDB = new ElementId("commentdb", 87);
        internal static readonly ElementId ELEM_TEXT = new ElementId("text", 88);
        internal static readonly ElementId ELEM_BHEAD = new ElementId("bhead", 102);
        internal static readonly ElementId ELEM_BLOCK = new ElementId("block", 103);
        internal static readonly ElementId ELEM_BLOCKEDGE = new ElementId("blockedge", 104);
        internal static readonly ElementId ELEM_EDGE = new ElementId("edge", 105);
        internal static readonly ElementId ELEM_CONTEXT_DATA = new ElementId("context_data", 120);
        internal static readonly ElementId ELEM_CONTEXT_POINTS = new ElementId("context_points", 121);
        internal static readonly ElementId ELEM_CONTEXT_POINTSET = new ElementId("context_pointset", 122);
        internal static readonly ElementId ELEM_CONTEXT_SET = new ElementId("context_set", 123);
        internal static readonly ElementId ELEM_SET = new ElementId("set", 124);
        internal static readonly ElementId ELEM_TRACKED_POINTSET = new ElementId("tracked_pointset", 125);
        internal static readonly ElementId ELEM_TRACKED_SET = new ElementId("tracked_set", 126);
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
        // Number serves as next open index
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
