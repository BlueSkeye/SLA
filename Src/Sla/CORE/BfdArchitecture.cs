using Sla.DECCORE;
using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.CORE
{
    internal class BfdArchitecture : SleighArchitecture
    {
        ///< How much to adjust the virtual memory address
        private long adjustvma;
        
        protected override void buildLoader(DocumentStorage store)
        {
            LoadImageBfd ldr;

            collectSpecFiles(errorstream);
            string target = getTarget();
            if (target.StartsWith("binary"))
                ldr = new LoadImageBfd(getFilename(), "binary");
            else if (target.StartsWith("default"))
                ldr = new LoadImageBfd(getFilename(), "default");
            else
                ldr = new LoadImageBfd(getFilename(), getTarget());
            ldr.open();
            if (adjustvma != 0) {
                ldr.adjustVma(adjustvma);
            }
            loader = ldr;
        }

        protected override void resolveArchitecture()
        {
            archid = getTarget();
            if (-1 == archid.IndexOf(':')) {
                archid = loader.getArchType();
                // kludge to distinguish windows binaries from linux/gcc
                if (-1 != archid.IndexOf("efi-app-ia32"))
                    archid = "x86:LE:32:default:windows";
                else if (-1 != archid.IndexOf("pe-i386"))
                    archid = "x86:LE:32:default:windows";
                else if (-1 != archid.IndexOf("pei-i386"))
                    archid = "x86:LE:32:default:windows";
                else if (-1 != archid.IndexOf("pei-x86-64"))
                    archid = "x86:LE:64:default:windows";
                else if (-1 != archid.IndexOf("sparc"))
                    archid = "sparc:BE:32:default:default";
                else if (-1 != archid.IndexOf("elf64"))
                    archid = "x86:LE:64:default:gcc";
                else if (-1 != archid.IndexOf("elf"))
                    archid = "x86:LE:32:default:gcc";
                else if (-1 != archid.IndexOf("mach-o"))
                    archid = "PowerPC:BE:32:default:macosx";
                else
                    throw new LowlevelError("Cannot convert bfd target to sleigh target: " + archid);
            }
            base.resolveArchitecture();
        }

        protected override void postSpecFile()
        {
            // Attach default space to loader
            base.postSpecFile();
            ((LoadImageBfd)loader).attachToSpace(getDefaultCodeSpace());
        }

        public override void encode(Encoder encoder)
        {
            // prepend extra stuff to specify binary file and spec
            encoder.openElement(ElementId.ELEM_BFD_SAVEFILE);
            encodeHeader(encoder);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_ADJUSTVMA, adjustvma);
            types.encodeCoreTypes(encoder);
            base.encode(encoder); // Save the rest of the state
            encoder.closeElement(ElementId.ELEM_BFD_SAVEFILE);
        }

        public override void restoreXml(DocumentStorage store)
        {
            Element? el = store.getTag("bfd_savefile");
            if (el == (Element)null)
                throw new LowlevelError("Could not find bfd_savefile tag");

            restoreXmlHeader(el);
            adjustvma = long.Parse(el.getAttributeValue("adjustvma"));
            List<Element> list = el.getChildren();
            int childIndex = 0;

            if (0 < list.Count) {
                if (list[0].getName() == "coretypes") {
                    store.registerTag(list[0]);
                    childIndex++;
                }
            }
            // Load the image and configure
            init(store);

            if (childIndex < list.Count) {
                store.registerTag(list[childIndex]);
                base.restoreXml(store);
            }
        }

        /// This just wraps the base class constructor
        /// \param fname is the path to the executable file
        /// \param targ is the (optional) language id to use for the file
        /// \param estream is the stream to use for the error console
        public BfdArchitecture(string fname, string targ, TextWriter estream)
            : base(fname, targ, estream)

        {               // Select architecture from string
            adjustvma = 0;
        }

        ~BfdArchitecture()
        {
        }
    }
}
