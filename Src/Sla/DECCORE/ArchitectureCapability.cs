using Sla.CORE;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Abstract extension point for building Architecture objects
    /// Decompilation hinges on initially recognizing the format of code then
    /// bootstrapping into discovering the processor etc.  This is the base class
    /// for the different extensions that perform this process.  Each extension
    /// implements the buildArchitecture() method as the formal entry point
    /// for the bootstrapping process.
    internal abstract class ArchitectureCapability
    {
        /// Current major version of decompiler
        private const uint majorversion = 5;
        /// Current minor version of decompiler
        private const uint minorversion = 0;
        /// The list of registered extensions
        private static List<ArchitectureCapability> thelist =
            new List<ArchitectureCapability>();

        /// Identifier for this capability
        protected string name;

        /// Get the capability identifier
        public string getName() => name;

        /// Do specialized initialization
        public virtual void initialize()
        {
            thelist.Add(this);
        }

        /// \brief Build an Architecture given a raw file or data
        /// This is implemented by each separate extension. The method is handed
        /// a \e filename and possibly external target information and must build
        /// the Architecture object, initializing all the major subcomponents, using just this info.
        /// \param filename is the path to the executable file to examine
        /// \param target if non-empty is a language id string
        /// \param estream is an output stream for error messages
        public abstract Architecture buildArchitecture(string filename, string target, TextWriter estream);

        /// \brief Determine if this extension can handle this file
        /// \param filename is the name of the file to examine
        /// \return \b true is \b this extension is suitable for analyzing the file
        public abstract bool isFileMatch(string filename);

        /// \brief Determine is this extension can handle this XML document
        /// If a file to analyze is XML based, this method examines the XML parse
        /// to determine if \b this extension can understand the document
        /// \param doc is the parsed XML document
        /// \return \b true if \b this extension understands the XML
        public abstract bool isXmlMatch(Document doc);

        /// Find an extension to process a file
        /// Given a specific file, find an ArchitectureCapability that can handle it.
        /// \param filename is the path to the file
        /// \return an ArchitectureCapability that can handle it or NULL
        public static ArchitectureCapability? findCapability(string filename)
        {
            for (int i = 0; i < thelist.Count; ++i) {
                ArchitectureCapability capa = thelist[i];
                if (capa.isFileMatch(filename)) {
                    return capa;
                }
            }
            return null;
        }

        /// Find an extension to process an XML document
        /// Given a parsed XML document, find an ArchitectureCapability that can handle it.
        /// \param doc is the parsed XML document
        /// \return an ArchitectureCapability that can handle it or NULL
        public static ArchitectureCapability? findCapability(Document doc)
        {
            for (int i = 0; i < thelist.Count; ++i) {
                ArchitectureCapability capa = thelist[i];
                if (capa.isXmlMatch(doc)) {
                    return capa;
                }
            }
            return null;
        }

        /// Get a capability by name
        /// Return the ArchitectureCapability object with the matching name
        /// \param name is the name to match
        /// \return the ArchitectureCapability or null if no match is found
        public static ArchitectureCapability? getCapability(string name)
        {
            for (int i = 0; i < thelist.Count; ++i) {
                ArchitectureCapability res = thelist[i];
                if (res.getName() == name) {
                    return res;
                }
            }
            return null;
        }

        /// Sort extensions
        /// Modify order that extensions are searched, to effect which gets a chance
        /// to run first.
        /// Right now all we need to do is make sure the raw architecture comes last
        public static void sortCapabilities()
        {
            int i;
            for (i = 0; i < thelist.Count; ++i) {
                if (thelist[i].getName() == "raw") {
                    break;
                }
            }
            if (i == thelist.Count) {
                return;

            }
            ArchitectureCapability capa = thelist[i];
            for (int j = i + 1; j < thelist.Count; ++j) {
                thelist[j - 1] = thelist[j];
            }
            thelist[thelist.Count - 1] = capa;
        }

        /// Get \e major decompiler version
        public static uint getMajorVersion() => majorversion;

        /// Get \e minor decompiler version
        public static uint getMinorVersion() => minorversion;
    }
}
