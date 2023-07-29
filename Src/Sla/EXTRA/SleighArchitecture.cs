using Sla.CORE;
using Sla.DECCORE;
using Sla.EXTRA;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.EXTRA
{
    /// \brief An Architecture that uses the decompiler's native SLEIGH translation engine
    ///
    /// Any Architecture derived from \b this knows how to natively read in:
    ///   - a compiled SLEIGH specification (.sla)
    ///   - a processor specification file (.pspec), and
    ///   - a compiler specification file (.cspec)
    ///
    /// Generally a \e language \e id (i.e. x86:LE:64:default) is provided, then this
    /// object is able to automatically load in configuration and construct the Translate object.
    internal class SleighArchitecture : Architecture
    {
        private static Dictionary<int4, Sleigh> translators = new Dictionary<int4, Sleigh>();      ///< Map from language index to instantiated translators
        private static List<LanguageDescription> description = new List<LanguageDescription>(); ///< List of languages we know about
        private int4 languageindex;                 ///< Index (within LanguageDescription array) of the active language
        private string filename;                    ///< Name of active load-image file
        private string target;                  ///< The \e language \e id of the active load-image

        /// \brief Read a SLEIGH .ldefs file
        ///
        /// Any \<language> tags are added to the LanguageDescription array
        /// \param specfile is the filename of the .ldefs file
        /// \param errs is an output stream for printing error messages
        private static void loadLanguageDescription(string specfile, TextWriter errs)
        {
            ifstream s = new ifstream(specfile.c_str());
            if (!s) return;

            XmlDecode decoder = new XmlDecode((AddrSpaceManager*)0);
            try
            {
                decoder.ingestStream(s);
            }
            catch (DecoderError err) {
                errs << "WARNING: Unable to parse sleigh specfile: " << specfile;
                return;
            }

            uint4 elemId = decoder.openElement(ELEM_LANGUAGE_DEFINITIONS);
            for (; ; )
            {
                uint4 subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ELEM_LANGUAGE)
                {
                    description.emplace_back();
                    description.back().decode(decoder);
                }
                else
                {
                    decoder.openElement();
                    decoder.closeElementSkipping(subId);
                }
            }
            decoder.closeElement(elemId);
        }

        /// Test if last Translate object can be reused
        /// If the current \b languageindex matches the \b last_languageindex,
        /// try to reuse the previous Sleigh object, so we don't reload
        /// the .sla file.
        /// \return \b true if it can be reused
        private bool isTranslateReused() => (translators.find(languageindex) != translators.end());

        /// Error stream associated with \b this SleighArchitecture
        protected TextWriter errorstream;

        // buildLoader must be filled in by derived class
        /// Gather specification files in normal locations
        /// This is run once when spinning up the decompiler.
        /// Look for the root .ldefs files within the normal directories and parse them.
        /// Use these to populate the list of \e language \e ids that are supported.
        /// \param errs is an output stream for writing error messages
        protected static void collectSpecFiles(TextWriter errs)
        {
            if (!description.empty()) return; // Have we already collected before

            vector<string> testspecs;
            vector<string>::iterator iter;
            specpaths.matchList(testspecs, ".ldefs", true);
            for (iter = testspecs.begin(); iter != testspecs.end(); ++iter)
                loadLanguageDescription(*iter, errs);
        }

        protected override Translate buildTranslator(DocumentStorage store)
        {               // Build a sleigh translator
            map<int4, Sleigh*>::const_iterator iter;
            Sleigh* sleigh;
            iter = translators.find(languageindex);
            if (iter != translators.end())
            {
                sleigh = (*iter).second;
                sleigh->reset(loader, context);
                return sleigh;
            }
            sleigh = new Sleigh(loader, context);
            translators[languageindex] = sleigh;
            return sleigh;
        }

        protected override PcodeInjectLibrary buildPcodeInjectLibrary()
        { // Build the pcode injector based on sleigh
            PcodeInjectLibrary* res;

            res = new PcodeInjectLibrarySleigh(this);
            return res;
        }

        protected override void buildTypegrp(DocumentStorage store)
        {
            Element* el = store.getTag("coretypes");
            types = new TypeFactory(this); // Initialize the object
            if (el != (Element*)0) {
                XmlDecode decoder = new XmlDecode(this, el);
                types->decodeCoreTypes(decoder);
            }
            else
            {
                // Put in the core types
                types->setCoreType("void", 1, TYPE_VOID, false);
                types->setCoreType("bool", 1, TYPE_BOOL, false);
                types->setCoreType("uint1", 1, TYPE_UINT, false);
                types->setCoreType("uint2", 2, TYPE_UINT, false);
                types->setCoreType("uint4", 4, TYPE_UINT, false);
                types->setCoreType("uint8", 8, TYPE_UINT, false);
                types->setCoreType("int1", 1, TYPE_INT, false);
                types->setCoreType("int2", 2, TYPE_INT, false);
                types->setCoreType("int4", 4, TYPE_INT, false);
                types->setCoreType("int8", 8, TYPE_INT, false);
                types->setCoreType("float4", 4, TYPE_FLOAT, false);
                types->setCoreType("float8", 8, TYPE_FLOAT, false);
                types->setCoreType("float10", 10, TYPE_FLOAT, false);
                types->setCoreType("float16", 16, TYPE_FLOAT, false);
                types->setCoreType("xunknown1", 1, TYPE_UNKNOWN, false);
                types->setCoreType("xunknown2", 2, TYPE_UNKNOWN, false);
                types->setCoreType("xunknown4", 4, TYPE_UNKNOWN, false);
                types->setCoreType("xunknown8", 8, TYPE_UNKNOWN, false);
                types->setCoreType("code", 1, TYPE_CODE, false);
                types->setCoreType("char", 1, TYPE_INT, true);
                types->setCoreType("wchar2", 2, TYPE_INT, true);
                types->setCoreType("wchar4", 4, TYPE_INT, true);
                types->cacheCoreTypes();
            }
        }

        protected override void buildCommentDB(DocumentStorage store)
        {
            commentdb = new CommentDatabaseInternal();
        }

        protected override void buildStringManager(DocumentStorage store)
        {
            stringManager = new StringManagerUnicode(this, 2048);
        }

        protected override void buildConstantPool(DocumentStorage store)
        {
            cpool = new ConstantPoolInternal();
        }

        protected override void buildContext(DocumentStorage store)
        {
            context = new ContextInternal();
        }

        protected override void buildSymbols(DocumentStorage store)
        {
            Element* symtag = store.getTag(ELEM_DEFAULT_SYMBOLS.getName());
            if (symtag == (Element*)0) return;
            XmlDecode decoder = new XmlDecode(this, symtag);
            uint4 el = decoder.openElement(ELEM_DEFAULT_SYMBOLS);
            while (decoder.peekElement() != 0)
            {
                uint4 subel = decoder.openElement(ELEM_SYMBOL);
                Address addr;
                string name;
                int4 size = 0;
                int4 volatileState = -1;
                for (; ; )
                {
                    uint4 attribId = decoder.getNextAttributeId();
                    if (attribId == 0) break;
                    if (attribId == ATTRIB_NAME)
                        name = decoder.readString();
                    else if (attribId == ATTRIB_ADDRESS)
                    {
                        addr = parseAddressSimple(decoder.readString());
                    }
                    else if (attribId == ATTRIB_VOLATILE)
                    {
                        volatileState = decoder.readBool() ? 1 : 0;
                    }
                    else if (attribId == ATTRIB_SIZE)
                        size = decoder.readSignedInteger();
                }
                decoder.closeElement(subel);
                if (name.Length == 0)
                    throw new LowlevelError("Missing name attribute in <symbol> element");
                if (addr.isInvalid())
                    throw new LowlevelError("Missing address attribute in <symbol> element");
                if (size == 0)
                    size = addr.getSpace()->getWordSize();
                if (volatileState >= 0)
                {
                    Sla.CORE.Range range(addr.getSpace(), addr.getOffset(), addr.getOffset() +(size - 1));
                    if (volatileState == 0)
                        symboltab->clearPropertyRange(Varnode::volatil, range);
                    else
                        symboltab->setPropertyRange(Varnode::volatil, range);
                }
                Datatype* ct = types->getBase(size, TYPE_UNKNOWN);
                Address usepoint;
                symboltab->getGlobalScope()->addSymbol(name, ct, addr, usepoint);
            }
            decoder.closeElement(el);
        }

        protected override void buildSpecFile(DocumentStorage store)
        { // Given a specific language, make sure relevant spec files are loaded
            bool language_reuse = isTranslateReused();
            LanguageDescription language = description[languageindex];
            string compiler = archid.substr(archid.rfind(':') + 1);
            CompilerTag compilertag = language.getCompiler(compiler);

            string processorfile;
            string compilerfile;
            string slafile;

            specpaths.findFile(processorfile, language.getProcessorSpec());
            specpaths.findFile(compilerfile, compilertag.getSpec());
            if (!language_reuse)
                specpaths.findFile(slafile, language.getSlaFile());

            try
            {
                Document* doc = store.openDocument(processorfile);
                store.registerTag(doc->getRoot());
            }
            catch (DecoderError err) {
                ostringstream serr;
                serr << "XML error parsing processor specification: " << processorfile;
                serr << "\n " << err.ToString();
                throw SleighError(serr.str());
            }
            catch (LowlevelError err) {
                ostringstream serr;
                serr << "Error reading processor specification: " << processorfile;
                serr << "\n " << err.ToString();
                throw SleighError(serr.str());
            }

            try
            {
                Document* doc = store.openDocument(compilerfile);
                store.registerTag(doc->getRoot());
            }
            catch (DecoderError err) {
                ostringstream serr;
                serr << "XML error parsing compiler specification: " << compilerfile;
                serr << "\n " << err.ToString();
                throw SleighError(serr.str());
            }
            catch (LowlevelError err) {
                ostringstream serr;
                serr << "Error reading compiler specification: " << compilerfile;
                serr << "\n " << err.ToString();
                throw SleighError(serr.str());
            }

            if (!language_reuse)
            {
                try
                {
                    Document* doc = store.openDocument(slafile);
                    store.registerTag(doc->getRoot());
                }
                catch (DecoderError err) {
                    ostringstream serr;
                    serr << "XML error parsing SLEIGH file: " << slafile;
                    serr << "\n " << err.ToString();
                    throw SleighError(serr.str());
                }
                catch (LowlevelError err) {
                    ostringstream serr;
                    serr << "Error reading SLEIGH file: " << slafile;
                    serr << "\n " << err.ToString();
                    throw SleighError(serr.str());
                }
            }
        }

        protected override void modifySpaces(Translate trans)
        {
            LanguageDescription language = description[languageindex];
            for (int4 i = 0; i < language.numTruncations(); ++i)
            {
                trans->truncateSpace(language.getTruncation(i));
            }
        }

        protected override void resolveArchitecture()
        { // Find best architecture
            if (archid.Length == 0)
            {
                if ((target.Length == 0) || (target == "default"))
                    archid = loader->getArchType();
                else
                    archid = target;
            }
            if (archid.find("binary-") == 0)
                archid.erase(0, 7);
            else if (archid.find("default-") == 0)
                archid.erase(0, 8);

            archid = normalizeArchitecture(archid);
            string baseid = archid.substr(0, archid.rfind(':'));
            int4 i;
            languageindex = -1;
            for (i = 0; i < description.size(); ++i)
            {
                if (description[i].getId() == baseid)
                {
                    languageindex = i;
                    if (description[i].isDeprecated())
                        printMessage("WARNING: Language " + baseid + " is deprecated");
                    break;
                }
            }

            if (languageindex == -1)
                throw new LowlevelError("No sleigh specification for " + baseid);
        }

        /// Construct given executable file
        /// Prepare \b this SleighArchitecture for analyzing the given executable image.
        /// Full initialization, including creation of the Translate object, still must be
        /// performed by calling the init() method.
        /// \param fname is the filename of the given executable image
        /// \param targ is the optional \e language \e id or other target information
        /// \param estream is a pointer to an output stream for writing error messages
        public SleighArchitecture(string fname, string targ, TextWriter estream)
            : base()
        {
            filename = fname;
            target = targ;
            errorstream = estream;
        }

        /// Get the executable filename
        public string getFilename() => filename;

        /// Get the \e language \e id of the active processor
        public string getTarget() => target;

        /// Encode basic attributes of the active executable
        /// \param encoder is the stream encoder
        public void encodeHeader(Encoder encoder)
        {
            encoder.writeString(ATTRIB_NAME, filename);
            encoder.writeString(ATTRIB_TARGET, target);
        }

        /// Restore from basic attributes of an executable
        /// \param el is the root XML element
        public void restoreXmlHeader(Element el)
        {
            filename = el->getAttributeValue("name");
            target = el->getAttributeValue("target");
        }

        public override void printMessage(string message)
        {
            *errorstream << message << endl;
        }

        ~SleighArchitecture()
        {
            translate = (Translate*)0;
        }

        public override string getDescription() => description[languageindex].getDescription();

        /// Try to recover a \e language \e id processor field
        /// Given an architecture target string try to recover an
        /// appropriate processor name for use in a normalized \e language \e id.
        /// \param nm is the given target string
        /// \return the processor field
        public static string normalizeProcessor(string nm)
        {
            if (nm.find("386") != string::npos)
                return "x86";
            return nm;
        }

        /// Try to recover a \e language \e id endianess field
        /// Given an architecture target string try to recover an
        /// appropriate endianness string for use in a normalized \e language \e id.
        /// \param nm is the given target string
        /// \return the endianness field
        public static string normalizeEndian(string nm)
        {
            if (nm.find("big") != string::npos)
                return "BE";
            if (nm.find("little") != string::npos)
                return "LE";
            return nm;
        }

        /// Try to recover a \e language \e id size field
        /// Given an architecture target string try to recover an
        /// appropriate size string for use in a normalized \e language \e id.
        /// \param nm is the given target string
        /// \return the size field
        public static string normalizeSize(string nm)
        {
            string res = nm;
            string::size_type pos;

            pos = res.find("bit");
            if (pos != string::npos)
                res.erase(pos, 3);
            pos = res.find('-');
            if (pos != string::npos)
                res.erase(pos, 1);
            return res;
        }

        /// Try to recover a \e language \e id string
        /// Try to normalize the target string into a valid \e language \e id.
        /// In general the target string must already look like a \e language \e id,
        /// but it can drop the compiler field and be a little sloppier in its format.
        /// \param nm is the given target string
        /// \return the normalized \e language \e id
        public static string normalizeArchitecture(string nm)
        {
            string processor;
            string endian;
            string size;
            string variant;
            string compile;

            string::size_type pos[4];
            int4 i;
            string::size_type curpos = 0;
            for (i = 0; i < 4; ++i)
            {
                curpos = nm.find(':', curpos + 1);
                if (curpos == string::npos) break;
                pos[i] = curpos;
            }
            if ((i != 3) && (i != 4))
                throw new LowlevelError("Architecture string does not look like sleigh id: " + nm);
            processor = nm.substr(0, pos[0]);
            endian = nm.substr(pos[0] + 1, pos[1] - pos[0] - 1);
            size = nm.substr(pos[1] + 1, pos[2] - pos[1] - 1);

            if (i == 4)
            {
                variant = nm.substr(pos[2] + 1, pos[3] - pos[2] - 1);
                compile = nm.substr(pos[3] + 1);
            }
            else
            {
                variant = nm.substr(pos[2] + 1);
                compile = "default";
            }

            processor = normalizeProcessor(processor);
            endian = normalizeEndian(endian);
            size = normalizeSize(size);
            return processor + ':' + endian + ':' + size + ':' + variant + ':' + compile;
        }

        /// \brief Scan directories for SLEIGH specification files
        ///
        /// This assumes a standard "Ghidra/Processors/*/data/languages" layout.  It
        /// scans for all matching directories and prepares for reading .ldefs files.
        /// \param rootpath is the root path of the Ghidra installation
        public static void scanForSleighDirectories(string rootpath)
        {
            vector<string> ghidradir;
            vector<string> procdir;
            vector<string> procdir2;
            vector<string> languagesubdirs;

            FileManage::scanDirectoryRecursive(ghidradir, "Ghidra", rootpath, 2);
            for (uint4 i = 0; i < ghidradir.size(); ++i)
            {
                FileManage::scanDirectoryRecursive(procdir, "Processors", ghidradir[i], 1); // Look for Processors structure
                FileManage::scanDirectoryRecursive(procdir, "contrib", ghidradir[i], 1);
            }
            if (procdir.size() != 0)
            {
                for (uint4 i = 0; i < procdir.size(); ++i)
                    FileManage::directoryList(procdir2, procdir[i]);

                vector<string> datadirs;
                for (uint4 i = 0; i < procdir2.size(); ++i)
                    FileManage::scanDirectoryRecursive(datadirs, "data", procdir2[i], 1);

                vector<string> languagedirs;
                for (uint4 i = 0; i < datadirs.size(); ++i)
                    FileManage::scanDirectoryRecursive(languagedirs, "languages", datadirs[i], 1);

                for (uint4 i = 0; i < languagedirs.size(); ++i)
                    languagesubdirs.push_back(languagedirs[i]);

                // In the old version we have to go down one more level to get to the ldefs
                for (uint4 i = 0; i < languagedirs.size(); ++i)
                    FileManage::directoryList(languagesubdirs, languagedirs[i]);
            }
            // If we haven't matched this directory structure, just use the rootpath as the directory containing
            // the ldef
            if (languagesubdirs.size() == 0)
                languagesubdirs.push_back(rootpath);

            for (uint4 i = 0; i < languagesubdirs.size(); ++i)
                specpaths.addDir2Path(languagesubdirs[i]);
        }

        /// Get list of all known language descriptions
        /// Parse all .ldef files and a return the list of all LanguageDescription objects
        /// If there are any parse errors in the .ldef files, an exception is thrown
        /// \return the list of LanguageDescription objects
        public static List<LanguageDescription> getDescriptions()
        {
            ostringstream s;
            collectSpecFiles(s);
            if (!s.str().empty())
                throw new LowlevelError(s.str());
            return description;
        }

        /// Shutdown all Translate objects and free global resources.
        public static void shutdown()
        {
            if (translators.empty()) return;    // Already cleared
            for (map<int4, Sleigh*>::const_iterator iter = translators.begin(); iter != translators.end(); ++iter)
                delete(*iter).second;
            translators.clear();
            // description.clear();  // static vector is destroyed by the normal exit handler
        }

        /// Known directories that contain .ldefs files.
        public static FileManage specpaths;
    }
}
