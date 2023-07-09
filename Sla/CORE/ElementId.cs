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
        internal static readonly ElementId ELEM_CONTEXT_DATA = new ElementId("context_data", 120);
        internal static readonly ElementId ELEM_CONTEXT_POINTS = new ElementId("context_points", 121);
        internal static readonly ElementId ELEM_CONTEXT_POINTSET = new ElementId("context_pointset", 122);
        internal static readonly ElementId ELEM_CONTEXT_SET = new ElementId("context_set", 123);
        internal static readonly ElementId ELEM_SET = new ElementId("set", 124);
        internal static readonly ElementId ELEM_TRACKED_POINTSET = new ElementId("tracked_pointset", 125);
        internal static readonly ElementId ELEM_TRACKED_SET = new ElementId("tracked_set", 126);
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
