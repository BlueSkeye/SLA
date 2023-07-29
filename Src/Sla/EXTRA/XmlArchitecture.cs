using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.EXTRA
{
    /// \brief An Architecture that loads executables using an XML format
    internal class XmlArchitecture : SleighArchitecture
    {
        private long adjustvma;                 ///< The amount to adjust the virtual memory address
        
        protected override void buildLoader(DocumentStorage store)
        {
            collectSpecFiles(*errorstream);
            Element* el = store.getTag("binaryimage");
            if (el == (Element)null) {
                Document* doc = store.openDocument(getFilename());
                store.registerTag(doc.getRoot());
                el = store.getTag("binaryimage");
            }
            if (el == (Element)null)
                throw new LowlevelError("Could not find binaryimage tag");
            loader = new LoadImageXml(getFilename(), el);
        }

        // virtual void resolveArchitecture(void);   		///< Inherit SleighArchitecture's version

        /// Read in image information (which uses translator)
        protected override void postSpecFile()
        {
            Architecture::postSpecFile();
            ((LoadImageXml*)loader).open(translate);
            if (adjustvma != 0)
                loader.adjustVma(adjustvma);
        }

        /// Prepend extra stuff to specify binary file and spec
        /// \param encoder is the stream encoder
        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_XML_SAVEFILE);
            encodeHeader(encoder);
            encoder.writeUnsignedInteger(ATTRIB_ADJUSTVMA, adjustvma);
            ((LoadImageXml*)loader).encode(encoder); // Save the LoadImage
            types.encodeCoreTypes(encoder);
            SleighArchitecture::encode(encoder); // Save the rest of the state
            encoder.closeElement(ELEM_XML_SAVEFILE);
        }

        public override void restoreXml(DocumentStorage store)
        {
            Element el = store.getTag("xml_savefile");
            if (el == (Element)null)
                throw new LowlevelError("Could not find xml_savefile tag");

            restoreXmlHeader(el);
            {
                istringstream s = new istringstream(el.getAttributeValue("adjustvma"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> adjustvma;
            }
            List list = el.getChildren();
            List::const_iterator iter;

            iter = list.begin();
            if (iter != list.end())
            {
                if ((*iter).getName() == "binaryimage")
                {
                    store.registerTag(*iter);
                    ++iter;
                }
            }
            if (iter != list.end())
            {
                if ((*iter).getName() == "specextensions")
                {
                    store.registerTag(*iter);
                    ++iter;
                }
            }
            if (iter != list.end())
            {
                if ((*iter).getName() == "coretypes")
                {
                    store.registerTag(*iter);
                    ++iter;
                }
            }
            init(store);            // Load the image and configure

            if (iter != list.end())
            {
                store.registerTag(*iter);
                SleighArchitecture::restoreXml(store);
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
