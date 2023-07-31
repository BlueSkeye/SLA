using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A container for parsed XML documents
    /// This holds multiple XML documents that have already been parsed. Documents
    /// can be put in this container, either by handing it a stream via parseDocument()
    /// or a filename via openDocument().  If they are explicitly registered, specific
    /// XML Elements can be looked up by name via getTag().
    public class DocumentStorage
    {
        /// The list of documents held by this container
        private List<Document> doclist = new List<Document>();
        /// The map from name to registered XML elements
        private Dictionary<string, Element> tagmap = new Dictionary<string, Element>();

        /// Destructor
        ~DocumentStorage()
        {
            foreach(Document? scannedDocument in doclist) {
                if (scannedDocument != null) {
                    scannedDocument.Dispose();
                }
            }
        }

        /// \brief Parse an XML document from the given stream
        /// Parsing starts immediately on the stream, attempting to make an in-memory DOM tree.
        /// An XmlException is thrown for any parsing error.
        /// \param s is the given stream to parse
        /// \return the in-memory DOM tree
        Document parseDocument(StreamReader s)
        {
            Document result = Xml.xml_tree(s);
            doclist.Add(result);
            return result;
        }

        /// \brief Open and parse an XML file
        /// The given filename is opened on the local filesystem and an attempt is made to parse
        /// its contents into an in-memory DOM tree. An XmlException is thrown for any parsing error.
        /// \param filename is the name of the XML document file
        /// \return the in-memory DOM tree
        public Document openDocument(ref string filename)
        {
            try {
                using (FileStream inputStream = File.OpenRead(filename)) {
                    using (StreamReader s = new StreamReader(inputStream)) {
                        return parseDocument(s);
                    }
                }
            }
            catch {
                throw new DecoderError($"Unable to open xml document {filename}");
            }
        }


        /// \brief Register the given XML Element object under its tag name
        /// Only one Element can be stored on \b this object per tag name.
        /// \param el is the given XML element
        public void registerTag(Element el)
        {
            tagmap[el.getName()] = el;
        }

        /// \brief Retrieve a registered XML Element by name
        /// \param nm is the XML tag name
        /// \return the matching registered Element or null
        public Element? getTag(string nm)
        {
            Element? result;

            return tagmap.TryGetValue(nm, out result) ? result : null;
        }
    }
}
