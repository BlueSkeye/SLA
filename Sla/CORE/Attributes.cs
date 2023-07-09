using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief The \e attributes for a single XML element
    /// A container for name/value pairs (of strings) for the formal attributes, as collected during parsing.
    /// This object is used to initialize the Element object but is not part of the final, in memory, DOM model.
    /// This also holds other properties of the element that are unused in this implementation,
    /// including the \e namespace URI.
    public class Attributes : IDisposable
    {
        /// A placeholder for the namespace URI that should be attached to the element
        private const string bogus_uri = "http://unused.uri";
        //  static string prefix;
        /// The name of the XML element
        private string elementname;
        /// List of names for each formal XML attribute
        private List<string> name;
        /// List of values for each formal XML attribute
        private List<string> value;

        /// Construct from element name string
        public Attributes(string el)
        {
            elementname = el;
        }

        ///< Destructor
        ~Attributes()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing) {
                GC.SuppressFinalize(this);
            }
            //for (int i = 0; i < name.Count; ++i) {
            //    delete name[i];
            //    delete value[i];
            //}
            // delete elementname;
        }

        /// Get the namespace URI associated with this element
        public string getelemURI()
        {
            return bogus_uri;
        }

        /// Get the name of this element
        public ref string getelemName()
        {
            return ref elementname;
        }

        /// Add a formal attribute
        public void add_attribute(string nm, string vl)
        {
            if (null == name) {
                name = new List<string>();
            }
            if (null == value) {
                value = new List<string>();
            }
            name.Add(nm);
            value.Add(vl);
        }

        /// The official SAX interface
        /// Get the number of attributes associated with the element
        public int getLength()
        {
            return name.Count;
        }

        /// Get the namespace URI associated with the i-th attribute
        public string getURI(int i)
        {
            return bogus_uri;
        }

        /// Get the local name of the i-th attribute
        public string getLocalName(int i)
        {
            return name[i];
        }

        /// Get the qualified name of the i-th attribute
        public string getQName(int i)
        {
            return name[i];
        }

        //  int getIndex(const string &uri,const string &localName) const;
        //  int getIndex(const string &qualifiedName) const;
        //  const string &getType(int index) const;
        //  const string &getType(const string &uri,const string &localName) const;
        //  const string &getType(const string &qualifiedName) const;

        /// Get the value of the i-th attribute
        public string getValue(int i)
        {
            return value[i];
        }

        //const string &getValue(const string &uri,const string &localName) const;

        /// \brief Get the value of the attribute with the given qualified name
        public string getValue(ref string qualifiedName)
        {
            for (int i = 0; i < name.Count; ++i) {
                if (name[i] == qualifiedName) {
                    return value[i];
                }
            }
            return bogus_uri;
        }
    }
}
