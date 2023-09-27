using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    // \brief Extension for building an XML format capable Architecture
    internal class XmlArchitectureCapability : ArchitectureCapability
    {
        // The singleton instance
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
            s.ReadSpaces();
            int val1 = s.Read();
            int val2 = s.Read();
            int val3 = s.Read();
            s.Close();
            // Probably <binaryimage> tag
            if ((val1 == '<') && (val2 == 'b') && (val3 == 'i'))
                return true;
            return false;
        }

        public override bool isXmlMatch(Document doc)
        {
            return (doc.getRoot().getName() == "xml_savefile");
        }
    }
}
