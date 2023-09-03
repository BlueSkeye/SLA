
namespace Sla.CORE
{
    /// \brief An XML based decoder
    /// The underlying transfer encoding is an XML document.  The decoder can either be initialized with an
    /// existing Element as the root of the data to transfer, or the ingestStream() method can be invoked
    /// to read the XML document from an input stream, in which case the decoder manages the Document object.
    public class XmlDecode : Sla.CORE.Decoder
    {
        /// An ingested XML document, owned by \b this decoder
        private Document? document;
        /// The root XML element to be decoded
        private Element? rootElement;
        /// Stack of currently \e open elements
        private Stack<Element> elStack = new Stack<Element>();
        /// Index of next child for each \e open element
        private Stack<IEnumerator<Element>?> iterStack =
            new Stack<IEnumerator<Element>?>();
        /// Position of \e current attribute to parse (in \e current element)
        private int attributeIndex;

        /// \brief Find the attribute index, within the given element, for the given name
        /// Run through the attributes of the element until we find the one matching the name,
        /// or throw an exception otherwise.
        /// \param el is the given element to search
        /// \param attribName is the attribute name to search for
        /// \return the matching attribute index
        private int findMatchingAttribute(Element el, string attribName)
        {
            for (int i = 0; i < el.getNumAttributes(); ++i) {
                if (el.getAttributeName(i) == attribName) {
                    return i;
                }
            }
            throw new DecoderError($"Attribute missing: {attribName}");
        }

        ///< Constructor with preparsed root
        public XmlDecode(AddrSpaceManager spc, Element root)
            : base(spc)
        {
            document = null;
            rootElement = root;
            attributeIndex = -1;
        }

        ///< Constructor for use with ingestStream
        public XmlDecode(AddrSpaceManager spc)
            : base(spc)
        {
            document = null;
            rootElement = null;
            attributeIndex = -1;
        }

        /// Get pointer to underlying XML element object
        internal Element getCurrentXmlElement()
        {
            return elStack.Peek();
        }

        ~XmlDecode()
        {
            if (null != document) {
                document.Dispose();
                document = null;
            }
        }

        public override void ingestStream(StreamReader s)
        {
            document = Xml.xml_tree(s);
            rootElement = document.getRoot();
        }

        public override uint peekElement()
        {
            Element el;
            if (0 == elStack.Count) {
                if (null == rootElement) {
                    return 0;
                }
                el = rootElement;
            }
            else {
                el = elStack.Peek();
                IEnumerator<Element>? iter = iterStack.Peek();
                if (null == iter) {
                    return 0;
                }
                el = iter.Current;
            }
            return ElementId.find(el.getName());
        }

        public override uint openElement()
        {
            Element el;
            if (0 == elStack.Count) {
                if (rootElement == null) {
                    // Document already traversed
                    return 0;
                }
                el = rootElement;
                // Only open once
                rootElement = null;
            }
            else {
                el = elStack.Peek();
                IEnumerator<Element>? iter = iterStack.Peek();
                if (null == iter) {
                    // Element already fully traversed
                    return 0;
                }
                el = iter.Current;
                if (!iter.MoveNext()) {
                    iterStack.Pop();
                    iterStack.Push(null);
                }
            }
            elStack.Push(el);
            List<Element>? children = el.getChildren();
            iterStack.Push((null == children) ? null : children.GetEnumerator());
            attributeIndex = -1;
            return ElementId.find(el.getName());
        }

        public override uint openElement(ElementId elemId)
        {
            Element el;
            if (0 == elStack.Count) {
                if (rootElement == null) {
                    throw new DecoderError(
                        $"Expecting <{elemId.getName()}> but reached end of document");
                }
                el = rootElement;
                // Only open document once
                rootElement = null;
            }
            else {
                el = elStack.Peek();
                IEnumerator<Element>? iter = iterStack.Peek();
                if (null != iter) {
                    el = iter.Current;
                    if (!iter.MoveNext()){
                        iterStack.Pop();
                        iterStack.Push(null);
                    }
                }
                else {
                    throw new DecoderError($"Expecting <{elemId.getName()}> but no remaining children in current element");
                }
            }
            if (el.getName() != elemId.getName()) {
                throw new DecoderError($"Expecting <{elemId.getName()}> but got <{el.getName()}>");
            }
            elStack.Push(el);
            List<Element>? children = el.getChildren();
            iterStack.Push((null == children) ? null : children.GetEnumerator());
            attributeIndex = -1;
            return elemId.getId();
        }

        public override void closeElement(uint id)
        {
#if CPUI_DEBUG
            Element el = elStack.GetLastItem();
            if (iterStack.GetLastItem() != el.getChildren().end())
                throw DecoderError("Closing element <" + el.getName() + "> with additional children");
            if (ElementId::find(el.getName()) != id)
                throw DecoderError("Trying to close <" + el.getName() + "> with mismatching id");
#endif
            elStack.Pop();
            iterStack.Pop();
            // Cannot read any additional attributes
            attributeIndex = 1000;
        }

        public override void closeElementSkipping(uint id)
        {
#if CPUI_DEBUG
            Element el = elStack.GetLastItem();
            if (ElementId::find(el.getName()) != id)
                throw DecoderError("Trying to close <" + el.getName() + "> with mismatching id");
#endif
            elStack.Pop();
            iterStack.Pop();
            // We could check that id matches current element
            attributeIndex = 1000;
        }

        public override void rewindAttributes()
        {
            attributeIndex = -1;
        }

        public override uint getNextAttributeId()
        {
            Element el = elStack.Peek();
            int nextIndex = attributeIndex + 1;
            if (nextIndex < el.getNumAttributes()) {
                attributeIndex = nextIndex;
                return AttributeId.find(el.getAttributeName(attributeIndex));
            }
            return 0;
        }

        public override uint getIndexedAttributeId(AttributeId attribId)
        {
            Element el = elStack.Peek();
            if (attributeIndex< 0 || (attributeIndex >= el.getNumAttributes())) {
                return AttributeId.ATTRIB_UNKNOWN.getId();
            }
            // For XML, the index is encoded directly in the attribute name
            string attribName = new string(el.getAttributeName(attributeIndex));
            // Does the name start with desired attribute base name?
            if (!attribName.StartsWith(attribId.getName())) {
                return AttributeId.ATTRIB_UNKNOWN.getId();
            }
            uint val = 0;
            // Strip off the base name
            StreamReader s = new StreamReader(attribName.Substring(attribId.getName().Length));
            // Decode the remaining decimal integer (starting at 1)
            val = s.ReadDecimalUnsignedInteger();
            if (0 == val) {
                throw new LowlevelError($"Bad indexed attribute: {attribId.getName()}");
            }
            return attribId.getId() + (val-1);
        }

        public override bool readBool()
        {
            Element el = elStack.Peek();
            return Xml.xml_readbool(el.getAttributeValue(attributeIndex));
        }

        public override bool readBool(AttributeId attribId)
        {
            Element el = elStack.Peek();
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                return Xml.xml_readbool(el.getContent());
            }
            int index = findMatchingAttribute(el, attribId.getName());
            return Xml.xml_readbool(el.getAttributeValue(index));
        }

        public override long readSignedInteger()
        {
            Element el = elStack.Peek();
            long res = 0;
            StreamReader s2 = new StreamReader(el.getAttributeValue(attributeIndex));
            // s2.unsetf(ios::dec | ios::hex | ios::oct);
            return s2.ReadDecimalLong();
        }

        public override long readSignedInteger(AttributeId attribId)
        {
            Element el = elStack.Peek();
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                return new StreamReader(el.getContent()).ReadDecimalLong();
            }
            int index = findMatchingAttribute(el, attribId.getName());
            StreamReader s = new StreamReader(el.getAttributeValue(index));
            // s.unsetf(ios::dec | ios::hex | ios::oct);
            return s.ReadDecimalLong();
        }

        public override long readSignedIntegerExpectString(string expect, long expectval)
        {
            Element el = elStack.Peek();
            string value = el.getAttributeValue(attributeIndex);
            if (value == expect) {
                return expectval;
            }
            StreamReader s2 = new StreamReader(value);
            // s2.unsetf(ios::dec | ios::hex | ios::oct);
            return s2.ReadDecimalLong();
        }

        public override long readSignedIntegerExpectString(ref AttributeId attribId,
            ref string expect, long expectval)
        {
            string value = readString(attribId);
            if (value == expect) {
                return expectval;
            }
            StreamReader s2 = new StreamReader(value);
            // s2.unsetf(ios::dec | ios::hex | ios::oct);
            return s2.ReadDecimalLong();
        }

        public override ulong readUnsignedInteger()
        {
            Element el = elStack.Peek();
            StreamReader s2 = new StreamReader(el.getAttributeValue(attributeIndex));
            // s2.unsetf(ios::dec | ios::hex | ios::oct);
            return s2.ReadDecimalUnsignedLongInteger();
        }

        public override ulong readUnsignedInteger(AttributeId attribId)
        {
            Element el = elStack.Peek();
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                return new StreamReader(el.getContent()).ReadDecimalUnsignedLongInteger();
            }
            int index = findMatchingAttribute(el, attribId.getName());
            // s.unsetf(ios::dec | ios::hex | ios::oct);
            return new StreamReader(el.getAttributeValue(index)).ReadDecimalUnsignedLongInteger();
        }

        public override string readString()
        {
            return elStack.Peek().getAttributeValue(attributeIndex);
        }

        public override string readString(AttributeId attribId)
        {
            Element el = elStack.Peek();
            return (attribId == AttributeId.ATTRIB_CONTENT)
                ? el.getContent()
                : el.getAttributeValue(findMatchingAttribute(el, attribId.getName()));
        }

        public override AddrSpace readSpace()
        {
            Element el = elStack.Peek();
            string nm = el.getAttributeValue(attributeIndex);
            return spcManager.getSpaceByName(nm)
                ?? throw new DecoderError($"Unknown address space name: {nm}");
        }

        public override AddrSpace readSpace(AttributeId attribId)
        {
            Element el = elStack.Peek();
            string nm;
            if (attribId == AttributeId.ATTRIB_CONTENT) {
                nm = el.getContent();
            }
            else {
                int index = findMatchingAttribute(el, attribId.getName());
                nm = el.getAttributeValue(index);
            }
            return spcManager.getSpaceByName(nm)
                ?? throw new DecoderError($"Unknown address space name: {nm}");
        }
    }
}
