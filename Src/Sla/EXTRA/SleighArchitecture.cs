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
        private static Dictionary<int, Sleigh> translators = new Dictionary<int, Sleigh>();      ///< Map from language index to instantiated translators
        private static List<LanguageDescription> description = new List<LanguageDescription>(); ///< List of languages we know about
        private int languageindex;                 ///< Index (within LanguageDescription array) of the active language
        private string filename;                    ///< Name of active load-image file
        private string target;                  ///< The \e language \e id of the active load-image

        /// \brief Read a SLEIGH .ldefs file
        ///
        /// Any \<language> tags are added to the LanguageDescription array
        /// \param specfile is the filename of the .ldefs file
        /// \param errs is an output stream for printing error messages
        private static void loadLanguageDescription(string specfile, TextWriter errs)
        {
            StreamReader s;
            
            try { s = new StreamReader(File.OpenRead(specfile)); }
            catch {
                return;
            }
            XmlDecode decoder = new XmlDecode((AddrSpaceManager)null);
            try {
                decoder.ingestStream(s);
            }
            catch (DecoderError) {
                errs.Write($"WARNING: Unable to parse sleigh specfile: {specfile}");
                return;
            }

            uint elemId = decoder.openElement(ElementId.ELEM_LANGUAGE_DEFINITIONS);
            while (true) {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_LANGUAGE) {
                    LanguageDescription newDescription = new LanguageDescription();
                    newDescription.decode(decoder);
                    description.Add(newDescription);
                }
                else {
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
        private bool isTranslateReused() => translators.ContainsKey(languageindex);

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

            List<string> testspecs = new List<string>();
            specpaths.matchList(testspecs, ".ldefs", true);
            foreach (string candidate in testspecs)
                loadLanguageDescription(candidate, errs);
        }

        protected override Translate buildTranslator(DocumentStorage store)
        {
            // Build a sleigh translator
            Sleigh? sleigh;
            if (translators.TryGetValue(languageindex, out sleigh)) {
                sleigh.reset(loader, context);
                return sleigh;
            }
            sleigh = new Sleigh(loader, context);
            translators[languageindex] = sleigh;
            return sleigh;
        }

        protected override PcodeInjectLibrary buildPcodeInjectLibrary()
        {
            // Build the pcode injector based on sleigh
            return new PcodeInjectLibrarySleigh(this);
        }

        protected override void buildTypegrp(DocumentStorage store)
        {
            Element? el = store.getTag("coretypes");
            types = new TypeFactory(this); // Initialize the object
            if (el != (Element)null) {
                XmlDecode decoder = new XmlDecode(this, el);
                types.decodeCoreTypes(decoder);
            }
            else {
                // Put in the core types
                types.setCoreType("void", 1, type_metatype.TYPE_VOID, false);
                types.setCoreType("bool", 1, type_metatype.TYPE_BOOL, false);
                types.setCoreType("byte", 1, type_metatype.TYPE_UINT, false);
                types.setCoreType("ushort", 2, type_metatype.TYPE_UINT, false);
                types.setCoreType("uint", 4, type_metatype.TYPE_UINT, false);
                types.setCoreType("ulong", 8, type_metatype.TYPE_UINT, false);
                types.setCoreType("int1", 1, type_metatype.TYPE_INT, false);
                types.setCoreType("short", 2, type_metatype.TYPE_INT, false);
                types.setCoreType("int", 4, type_metatype.TYPE_INT, false);
                types.setCoreType("long", 8, type_metatype.TYPE_INT, false);
                types.setCoreType("float4", 4, type_metatype.TYPE_FLOAT, false);
                types.setCoreType("float8", 8, type_metatype.TYPE_FLOAT, false);
                types.setCoreType("float10", 10, type_metatype.TYPE_FLOAT, false);
                types.setCoreType("float16", 16, type_metatype.TYPE_FLOAT, false);
                types.setCoreType("xunknown1", 1, type_metatype.TYPE_UNKNOWN, false);
                types.setCoreType("xunknown2", 2, type_metatype.TYPE_UNKNOWN, false);
                types.setCoreType("xunknown4", 4, type_metatype.TYPE_UNKNOWN, false);
                types.setCoreType("xunknown8", 8, type_metatype.TYPE_UNKNOWN, false);
                types.setCoreType("code", 1, type_metatype.TYPE_CODE, false);
                types.setCoreType("char", 1, type_metatype.TYPE_INT, true);
                types.setCoreType("wchar2", 2, type_metatype.TYPE_INT, true);
                types.setCoreType("wchar4", 4, type_metatype.TYPE_INT, true);
                types.cacheCoreTypes();
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
            Element? symtag = store.getTag(ElementId.ELEM_DEFAULT_SYMBOLS.getName());
            if (symtag == (Element)null) return;
            XmlDecode decoder = new XmlDecode(this, symtag);
            uint el = decoder.openElement(ElementId.ELEM_DEFAULT_SYMBOLS);
            while (decoder.peekElement() != 0)
            {
                uint subel = decoder.openElement(ElementId.ELEM_SYMBOL);
                Address? addr = null;
                string? name = null;
                int size = 0;
                int volatileState = -1;
                while (true) {
                    uint attribId = decoder.getNextAttributeId();
                    if (attribId == 0) break;
                    if (attribId == AttributeId.ATTRIB_NAME)
                        name = decoder.readString();
                    else if (attribId == AttributeId.ATTRIB_ADDRESS) {
                        addr = parseAddressSimple(decoder.readString());
                    }
                    else if (attribId == AttributeId.ATTRIB_VOLATILE) {
                        volatileState = decoder.readBool() ? 1 : 0;
                    }
                    else if (attribId == AttributeId.ATTRIB_SIZE)
                        size = (int)decoder.readSignedInteger();
                }
                decoder.closeElement(subel);
                if (null == name) throw new BugException();
                if (null == addr) throw new BugException();
                if (name.Length == 0)
                    throw new CORE.LowlevelError("Missing name attribute in <symbol> element");
                if (addr.isInvalid())
                    throw new CORE.LowlevelError("Missing address attribute in <symbol> element");
                if (size == 0)
                    size = (int)addr.getSpace().getWordSize();
                if (volatileState >= 0)
                {
                    CORE.Range range = new CORE.Range(addr.getSpace(), addr.getOffset(),
                        (ulong)((int)addr.getOffset() + (size - 1)));
                    if (volatileState == 0)
                        symboltab.clearPropertyRange(Varnode.varnode_flags.volatil, range);
                    else
                        symboltab.setPropertyRange(Varnode.varnode_flags.volatil, range);
                }
                Datatype ct = types.getBase(size, type_metatype.TYPE_UNKNOWN);
                Address usepoint = new Address();
                symboltab.getGlobalScope().addSymbol(name, ct, addr, usepoint);
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
                store.registerTag(doc.getRoot());
            }
            catch (DecoderError err) {
                ostringstream serr;
                serr << "XML error parsing processor specification: " << processorfile;
                serr << "\n " << err.ToString();
                throw new SleighError(serr.str());
            }
            catch (CORE.LowlevelError err) {
                ostringstream serr;
                serr << "Error reading processor specification: " << processorfile;
                serr << "\n " << err.ToString();
                throw new SleighError(serr.str());
            }

            try
            {
                Document* doc = store.openDocument(compilerfile);
                store.registerTag(doc.getRoot());
            }
            catch (DecoderError err) {
                ostringstream serr;
                serr << "XML error parsing compiler specification: " << compilerfile;
                serr << "\n " << err.ToString();
                throw new SleighError(serr.str());
            }
            catch (CORE.LowlevelError err) {
                ostringstream serr;
                serr << "Error reading compiler specification: " << compilerfile;
                serr << "\n " << err.ToString();
                throw new SleighError(serr.str());
            }

            if (!language_reuse)
            {
                try
                {
                    Document* doc = store.openDocument(slafile);
                    store.registerTag(doc.getRoot());
                }
                catch (DecoderError err) {
                    ostringstream serr;
                    serr << "XML error parsing SLEIGH file: " << slafile;
                    serr << "\n " << err.ToString();
                    throw new SleighError(serr.str());
                }
                catch (CORE.LowlevelError err) {
                    ostringstream serr;
                    serr << "Error reading SLEIGH file: " << slafile;
                    serr << "\n " << err.ToString();
                    throw new SleighError(serr.str());
                }
            }
        }

        protected override void modifySpaces(Translate trans)
        {
            LanguageDescription language = description[languageindex];
            for (int i = 0; i < language.numTruncations(); ++i)
            {
                trans.truncateSpace(language.getTruncation(i));
            }
        }

        protected override void resolveArchitecture()
        { // Find best architecture
            if (archid.Length == 0)
            {
                if ((target.Length == 0) || (target == "default"))
                    archid = loader.getArchType();
                else
                    archid = target;
            }
            if (archid.find("binary-") == 0)
                archid.erase(0, 7);
            else if (archid.find("default-") == 0)
                archid.erase(0, 8);

            archid = normalizeArchitecture(archid);
            string baseid = archid.substr(0, archid.rfind(':'));
            int i;
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
                throw new CORE.LowlevelError("No sleigh specification for " + baseid);
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
            encoder.writeString(AttributeId.ATTRIB_NAME, filename);
            encoder.writeString(AttributeId.ATTRIB_TARGET, target);
        }

        /// Restore from basic attributes of an executable
        /// \param el is the root XML element
        public void restoreXmlHeader(Element el)
        {
            filename = el.getAttributeValue("name");
            target = el.getAttributeValue("target");
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
            int i;
            string::size_type curpos = 0;
            for (i = 0; i < 4; ++i)
            {
                curpos = nm.find(':', curpos + 1);
                if (curpos == string::npos) break;
                pos[i] = curpos;
            }
            if ((i != 3) && (i != 4))
                throw new CORE.LowlevelError("Architecture string does not look like sleigh id: " + nm);
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
            List<string> ghidradir;
            List<string> procdir;
            List<string> procdir2;
            List<string> languagesubdirs;

            FileManage::scanDirectoryRecursive(ghidradir, "Ghidra", rootpath, 2);
            for (uint i = 0; i < ghidradir.size(); ++i)
            {
                FileManage::scanDirectoryRecursive(procdir, "Processors", ghidradir[i], 1); // Look for Processors structure
                FileManage::scanDirectoryRecursive(procdir, "contrib", ghidradir[i], 1);
            }
            if (procdir.size() != 0)
            {
                for (uint i = 0; i < procdir.size(); ++i)
                    FileManage::directoryList(procdir2, procdir[i]);

                List<string> datadirs;
                for (uint i = 0; i < procdir2.size(); ++i)
                    FileManage::scanDirectoryRecursive(datadirs, "data", procdir2[i], 1);

                List<string> languagedirs;
                for (uint i = 0; i < datadirs.size(); ++i)
                    FileManage::scanDirectoryRecursive(languagedirs, "languages", datadirs[i], 1);

                for (uint i = 0; i < languagedirs.size(); ++i)
                    languagesubdirs.Add(languagedirs[i]);

                // In the old version we have to go down one more level to get to the ldefs
                for (uint i = 0; i < languagedirs.size(); ++i)
                    FileManage::directoryList(languagesubdirs, languagedirs[i]);
            }
            // If we haven't matched this directory structure, just use the rootpath as the directory containing
            // the ldef
            if (languagesubdirs.size() == 0)
                languagesubdirs.Add(rootpath);

            for (uint i = 0; i < languagesubdirs.size(); ++i)
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
                throw new CORE.LowlevelError(s.str());
            return description;
        }

        /// Shutdown all Translate objects and free global resources.
        public static void shutdown()
        {
            if (translators.empty()) return;    // Already cleared
            for (Dictionary<int, Sleigh*>::const_iterator iter = translators.begin(); iter != translators.end(); ++iter)
                delete(*iter).second;
            translators.clear();
            // description.clear();  // static List is destroyed by the normal exit handler
        }

        /// Known directories that contain .ldefs files.
        public static FileManage specpaths;
    }
}
