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
            LoadImageBfd* ldr;

            collectSpecFiles(*errorstream);
            if (getTarget().find("binary") == 0)
                ldr = new LoadImageBfd(getFilename(), "binary");
            else if (getTarget().find("default") == 0)
                ldr = new LoadImageBfd(getFilename(), "default");
            else
                ldr = new LoadImageBfd(getFilename(), getTarget());
            ldr->open();
            if (adjustvma != 0)
                ldr->adjustVma(adjustvma);
            loader = ldr;
        }

        protected override void resolveArchitecture()
        {
            archid = getTarget();
            if (archid.find(':') == string::npos)
            {
                archid = loader->getArchType();
                // kludge to distinguish windows binaries from linux/gcc
                if (archid.find("efi-app-ia32") != string::npos)
                    archid = "x86:LE:32:default:windows";
                else if (archid.find("pe-i386") != string::npos)
                    archid = "x86:LE:32:default:windows";
                else if (archid.find("pei-i386") != string::npos)
                    archid = "x86:LE:32:default:windows";
                else if (archid.find("pei-x86-64") != string::npos)
                    archid = "x86:LE:64:default:windows";
                else if (archid.find("sparc") != string::npos)
                    archid = "sparc:BE:32:default:default";
                else if (archid.find("elf64") != string::npos)
                    archid = "x86:LE:64:default:gcc";
                else if (archid.find("elf") != string::npos)
                    archid = "x86:LE:32:default:gcc";
                else if (archid.find("mach-o") != string::npos)
                    archid = "PowerPC:BE:32:default:macosx";
                else
                    throw LowlevelError("Cannot convert bfd target to sleigh target: " + archid);
            }
            SleighArchitecture::resolveArchitecture();
        }

        protected void postSpecFile()
        { // Attach default space to loader
            Architecture::postSpecFile();
            ((LoadImageBfd*)loader)->attachToSpace(getDefaultCodeSpace());
        }

        public override void encode(Encoder encoder)
        {               // prepend extra stuff to specify binary file and spec
            encoder.openElement(ELEM_BFD_SAVEFILE);
            encodeHeader(encoder);
            encoder.writeUnsignedInteger(ATTRIB_ADJUSTVMA, adjustvma);
            types->encodeCoreTypes(encoder);
            SleighArchitecture::encode(encoder); // Save the rest of the state
            encoder.closeElement(ELEM_BFD_SAVEFILE);
        }

        public override void restoreXml(DocumentStorage store)
        {
            Element* el = store.getTag("bfd_savefile");
            if (el == (Element*)0)
                throw LowlevelError("Could not find bfd_savefile tag");

            restoreXmlHeader(el);
            {
                istringstream s(el->getAttributeValue("adjustvma"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> adjustvma;
            }
            List &list(el->getChildren());
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
            init(store); // Load the image and configure

            if (iter != list.end())
            {
                store.registerTag(*iter);
                SleighArchitecture::restoreXml(store);
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
