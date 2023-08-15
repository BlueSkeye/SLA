using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    /// \brief Extension for building an XML format capable Architecture
    internal class XmlArchitectureCapability : ArchitectureCapability
    {
        /// The singleton instance
        private static XmlArchitectureCapability xmlArchitectureCapability =
            new XmlArchitectureCapability();
        
        private XmlArchitectureCapability()
        {
            name = "xml";
        }

        // private XmlArchitectureCapability(XmlArchitectureCapability op2);	///< Not implemented
        // XmlArchitectureCapability &operator=(XmlArchitectureCapability op2);	///< Not implemented

        ~XmlArchitectureCapability()
        {
            SleighArchitecture.shutdown();
        }

        public override Architecture buildArchitecture(string filename, string target, TextWriter estream)
            => new XmlArchitecture(filename, target, estream);

        public override bool isFileMatch(string filename)
        {
            TextReader s;
            try { s = new StreamReader(File.OpenRead(filename)); }
            catch { return false; }
            int val1, val2, val3;
            s >> ws;
            val1 = s.get();
            val2 = s.get();
            val3 = s.get();
            s.Close();
            if ((val1 == '<') && (val2 == 'b') && (val3 == 'i')) // Probably <binaryimage> tag
                return true;
            return false;
        }

        public override bool isXmlMatch(Document doc)
        {
            return (doc.getRoot().getName() == "xml_savefile");
        }
    }
}
