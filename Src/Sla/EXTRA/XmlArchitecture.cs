using Sla.CORE;

namespace Sla.EXTRA
{
    /// \brief An Architecture that loads executables using an XML format
    internal class XmlArchitecture : SleighArchitecture
    {
        private long adjustvma;                 ///< The amount to adjust the virtual memory address
        
        protected override void buildLoader(DocumentStorage store)
        {
            collectSpecFiles(errorstream);
            Element? el = store.getTag("binaryimage");
            if (el == (Element)null) {
                Document doc = store.openDocument(getFilename());
                store.registerTag(doc.getRoot());
                el = store.getTag("binaryimage");
            }
            if (el == (Element)null)
                throw new CORE.LowlevelError("Could not find binaryimage tag");
            loader = new LoadImageXml(getFilename(), el);
        }

        // Inherit SleighArchitecture's version
        // virtual void resolveArchitecture(void);

        /// Read in image information (which uses translator)
        protected override void postSpecFile()
        {
            Architecture.postSpecFile();
            ((LoadImageXml)loader).open(translate);
            if (adjustvma != 0)
                loader.adjustVma((ulong)adjustvma);
        }

        /// Prepend extra stuff to specify binary file and spec
        /// \param encoder is the stream encoder
        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_XML_SAVEFILE);
            encodeHeader(encoder);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_ADJUSTVMA, (ulong)adjustvma);
            ((LoadImageXml)loader).encode(encoder); // Save the LoadImage
            types.encodeCoreTypes(encoder);
            // Save the rest of the state
            base.encode(encoder);
            encoder.closeElement(ElementId.ELEM_XML_SAVEFILE);
        }

        public override void restoreXml(DocumentStorage store)
        {
            Element? el = store.getTag("xml_savefile");
            if (el == (Element)null)
                throw new CORE.LowlevelError("Could not find xml_savefile tag");

            restoreXmlHeader(el);
            TextReader s = new StringReader(el.getAttributeValue("adjustvma"));
            // s.unsetf(ios::dec | ios::hex | ios::oct);
            adjustvma = long.Parse(s.ReadString());
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();

            if (iter.MoveNext()) {
                if (iter.Current.getName() == "binaryimage") {
                    store.registerTag(iter.Current);
                }
            }
            if (iter.MoveNext()) {
                if (iter.Current.getName() == "specextensions") {
                    store.registerTag(iter.Current);
                }
            }
            if (iter.MoveNext()) {
                if (iter.Current.getName() == "coretypes") {
                    store.registerTag(iter.Current);
                }
            }
            // Load the image and configure
            init(store);
            if (iter.MoveNext()) {
                store.registerTag(iter.Current);
                base.restoreXml(store);
            }
        }

        /// This just wraps the base constructor
        /// \param fname is the path to the executable file (containing XML)
        /// \param targ is the (optional) language id
        /// \param estream is the stream to use for the error console
        public XmlArchitecture(string fname, string targ, TextWriter estream)
            : base(fname, targ, estream)

        {
            adjustvma = 0;
        }

        ~XmlArchitecture()
        {
        }
    }
}
