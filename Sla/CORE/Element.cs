using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using _List = System.Collections.Generic.List<ghidra.Element>;

namespace ghidra
{
    /// \brief An XML element.  A node in the DOM tree.
    /// This is the main node for the in-memory representation of the XML (DOM) tree.
    public class Element
    {
        /// The (local) name of the element
        private string name;
        /// Character content of the element
        private string content;
        /// A list of attribute names for \b this element
        private List<string> attr;
        /// a (corresponding) list of attribute values for \b this element
        private List<string> value;

        /// The parent Element (or null)
        protected Element? parent;
        /// A list of child Element objects
        protected _List? children;

        /// Constructor given a parent Element
        public Element(Element? par)
        {
            parent = par;
        }

        /// Destructor
        //~Element()
        //{
        //    if (null != children) {
        //        foreach (Element deletedElement in children) {
        //            delete deletedElement;
        //        }
        //    }
        //}

        /// Set the local name of the element
        public void setName(string nm)
        {
            name = nm;
        }

        /// \brief Append new character content to \b this element
        /// \param str is an array of character data
        /// \param start is the index of the first character to append
        /// \param length is the number of characters to append
        public unsafe void addContent(char* str, int start, int length)
        {
            //    for(int i=0;i<length;++i) content += str[start+i]; }
            content += new string(str, start, length);
        }

        /// \brief Add a new child Element to the model, with \b this as the parent
        /// \param child is the new child Element
        public void addChild(Element child)
        {
            children.Add(child);
        }

        /// \brief Add a new name/value attribute pair to \b this element
        /// \param nm is the name of the attribute
        /// \param vl is the value of the attribute
        public void addAttribute(string nm, string vl)
        {
            attr.Add(nm);
            value.Add(vl);
        }

        /// Get the parent Element
        public Element? getParent()
        {
            return parent;
        }

        /// Get the local name of \b this element
        public ref string getName()
        {
            return ref name;
        }

        /// Get the list of child elements
        public ref _List? getChildren()
        {
            return ref children;
        }

        /// Get the character content of \b this element
        public ref string getContent()
        {
            return ref content;
        }

        /// \brief Get an attribute value by name
        /// Look up the value for the given attribute name and return it. An exception is
        /// thrown if the attribute does not exist.
        /// \param nm is the name of the attribute
        /// \return the corresponding attribute value
        public string getAttributeValue(string nm)
        {
            for (int i = 0; i < attr.Count; ++i) {
                if (attr[i] == nm) {
                    return value[i];
                }
            }
            throw new DecoderError($"Unknown attribute: {nm}");
        }

        /// Get the number of attributes for \b this element
        public int getNumAttributes()
        {
            return attr.Count;
        }

        /// Get the name of the i-th attribute
        public string getAttributeName(int i)
        {
            return attr[i];
        }

        /// Get the value of the i-th attribute
        public string getAttributeValue(int i)
        {
            return value[i];
        }
    }
}
