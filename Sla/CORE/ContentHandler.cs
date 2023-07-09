using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Locator = object;

namespace ghidra
{
    /// \brief The SAX interface for parsing XML documents
    /// This is the formal interface for handling the low-level string pieces of an XML document as
    /// they are scanned by the parser.
    public abstract class ContentHandler
    {
        /// Destructor
        ~ContentHandler()
        {
        }

        /// Set the Locator object for documents
        public abstract void setDocumentLocator(Locator locator);

        /// Start processing a new XML document
        public abstract void startDocument();

        /// End processing for the current XML document
        public abstract void endDocument();

        /// Start a new prefix to namespace URI mapping
        public abstract void startPrefixMapping(ref string prefix, ref string uri);

        /// Finish the current prefix
        public abstract void endPrefixMapping(ref string prefix);

        /// \brief Callback indicating a new XML element has started.
        /// \param namespaceURI is the namespace to which the new element belongs
        /// \param localName is the local name of the new element
        /// \param qualifiedName is the fully qualified name of the new element
        /// \param atts is the set of (previously parsed) attributes to attach to the new element
        public abstract void startElement(string namespaceURI, string localName,
            string qualifiedName, Attributes atts);

        /// \brief Callback indicating parsing of the current XML element is finished.
        ///
        /// \param namespaceURI is the namespace to which the element belongs
        /// \param localName is the local name of the new element
        /// \param qualifiedName is the fully qualified name of the element.
        public abstract void endElement(string namespaceURI, string localName,
              string qualifiedName);

        /// \brief Callback with raw characters to be inserted in the current XML element
        /// \param text is an array of character data being inserted.
        /// \param start is the first character within the array to insert.
        /// \param length is the number of characters to insert.
        public unsafe abstract void characters(char* text, int start, int length);

        /// \brief Callback with whitespace character data for the current XML element
        /// \param text is an array of character data that can be inserted.
        /// \param start is the first character within the array to insert.
        /// \param length is the number of characters to insert.
        public unsafe abstract void ignorableWhitespace(char* text, int start, int length);

        /// \brief Set the XML version as specified by the current document
        /// \param version is the parsed version string
        public abstract void setVersion(string version);

        /// \brief Set the character encoding as specified by the current document
        /// \param encoding is the parsed encoding string
        public abstract void setEncoding(string encoding);

        /// \brief Callback for a formal \e processing \e instruction seen in the current document
        /// \param target is the target instruction to process
        /// \param data is (optional) character data for the instruction
        public abstract void processingInstruction(ref string target, ref string data);

        /// \brief Callback for an XML entity skipped by the parser
        /// \param name is the name of the entity being skipped
        public abstract void skippedEntity(ref string name);

        /// \brief Callback for handling an error condition during XML parsing
        /// \param errmsg is a message describing the error condition
        public abstract void setError(string errmsg);
    }
}
