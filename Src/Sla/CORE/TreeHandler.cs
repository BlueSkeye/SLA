using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Locator = object;

namespace Sla.CORE
{
    /// \brief A SAX interface implementation for constructing an in-memory DOM model.
    /// This implementation builds a DOM model of the XML stream being parsed, creating an
    /// Element object for each XML element tag in the stream.  This handler is initialized with
    /// a root Element object, which after parsing is complete will own all parsed elements.
    public class TreeHandler : ContentHandler
    {
        ///< The root XML element being processed by \b this handler
        private Element root;
        ///< The \e current XML element being processed by \b this handler
        private Element cur;
        /// The last error condition returned by the parser (if not empty)
        private string? error;

        ///< Constructor given root Element
        public TreeHandler(Element rt)
        {
            root = rt;
            cur = root;
        }

        ~TreeHandler()
        {
        }

        public override void setDocumentLocator(Locator locator)
        {
        }

        public override void startDocument()
        {
        }

        public override void endDocument()
        {
        }

        public override void startPrefixMapping(ref string prefix, ref string uri)
        {
        }

        public override void endPrefixMapping(ref string prefix)
        {
        }

        public override void startElement(string namespaceURI, string localName,
            string qualifiedName, Attributes atts)
        {
            Element newel = new Element(cur);
            cur.addChild(newel);
            cur = newel;
            newel.setName(localName);
            for (int i = 0; i < atts.getLength(); ++i)
            {
                newel.addAttribute(atts.getLocalName(i), atts.getValue(i));
            }
        }

        public override void endElement(string namespaceURI, string localName,
            string qualifiedName)
        {
            cur = cur.getParent();
        }

        public unsafe override void characters(char* text, int start, int length)
        {
            cur.addContent(text, start, length);
        }

        public unsafe override void ignorableWhitespace(char* text, int start, int length)
        {
        }

        public override void processingInstruction(ref string target, ref string data)
        {
        }

        public override void setVersion(string val)
        {
        }

        public override void setEncoding(string val)
        {
        }

        public override void skippedEntity(ref string name)
        {
        }

        public override void setError(string errmsg)
        {
            error = errmsg;
        }

        /// Get the current error message
        public virtual string? getError()
        {
            return error;
        }
    }
}
