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
            RawLoadImage ldr;

            collectSpecFiles(errorstream);
            ldr = new RawLoadImage(getFilename());
            ldr.open();
            if (adjustvma != 0)
                ldr.adjustVma(adjustvma);
            loader = ldr;
        }

        protected override void resolveArchitecture()
        {
            archid = getTarget();
            // Nothing to derive from the image itself, we just copy in the passed in target
            base.resolveArchitecture();
        }

        protected override void postSpecFile()
        {
            base.postSpecFile();
            // Attach default space to loader
            ((RawLoadImage)loader).attachToSpace(getDefaultCodeSpace());
        }

        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_RAW_SAVEFILE);
            encodeHeader(encoder);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_ADJUSTVMA, (ulong)adjustvma);
            types.encodeCoreTypes(encoder);
            base.encode(encoder);
            encoder.closeElement(ElementId.ELEM_RAW_SAVEFILE);
        }

        public override void restoreXml(DocumentStorage store)
        {
            Element? el = store.getTag("raw_savefile");
            if (el == (Element)null)
                throw new CORE.LowlevelError("Could not find raw_savefile tag");

            restoreXmlHeader(el); {
                TextReader s = new StringReader(el.getAttributeValue("adjustvma"));
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                adjustvma = long.Parse(s.ReadString());
            }
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            bool eolReached = false;

            if (iter.MoveNext()) {
                if (iter.Current.getName() == "coretypes") {
                    store.registerTag(iter.Current);
                    eolReached = !iter.MoveNext();
                }
            }
            else eolReached = true;
            // Load the image and configure
            init(store);

            if (!eolReached) {
                store.registerTag(iter.Current);
                base.restoreXml(store);
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
