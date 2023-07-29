using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.EXTRA
{
    /// \brief Architecture that reads its binary as a raw file
    internal class RawBinaryArchitecture : SleighArchitecture
    {
        /// What address byte 0 of the raw file gets treated as
        private long adjustvma;

        protected override void buildLoader(DocumentStorage store)
        {
            RawLoadImage* ldr;

            collectSpecFiles(*errorstream);
            ldr = new RawLoadImage(getFilename());
            ldr->open();
            if (adjustvma != 0)
                ldr->adjustVma(adjustvma);
            loader = ldr;
        }

        protected override void resolveArchitecture()
        {
            archid = getTarget();   // Nothing to derive from the image itself, we just copy in the passed in target
            SleighArchitecture::resolveArchitecture();
        }

        protected override void postSpecFile()
        {
            Architecture::postSpecFile();
            ((RawLoadImage*)loader)->attachToSpace(getDefaultCodeSpace());   // Attach default space to loader
        }

        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_RAW_SAVEFILE);
            encodeHeader(encoder);
            encoder.writeUnsignedInteger(ATTRIB_ADJUSTVMA, adjustvma);
            types->encodeCoreTypes(encoder);
            SleighArchitecture::encode(encoder);
            encoder.closeElement(ELEM_RAW_SAVEFILE);
        }

        public override void restoreXml(DocumentStorage store)
        {
            const Element* el = store.getTag("raw_savefile");
            if (el == (const Element*)0)
    throw LowlevelError("Could not find raw_savefile tag");

            restoreXmlHeader(el);
            {
                istringstream s(el->getAttributeValue("adjustvma"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> adjustvma;
            }
            const List &list(el->getChildren());
            List::const_iterator iter;

            iter = list.begin();
            if (iter != list.end())
            {
                if ((*iter)->getName() == "coretypes")
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

        public RawBinaryArchitecture(string fname, string targ, TextWriter estream)
            : base(fname, targ, estream)
        {
            adjustvma = 0;
        }

        ~RawBinaryArchitecture()
        {
        }
    }
}
