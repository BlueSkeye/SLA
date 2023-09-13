using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class BfdArchitectureCapability : ArchitectureCapability
    {
        ///< The singleton instance
        private static BfdArchitectureCapability bfdArchitectureCapability =
            new BfdArchitectureCapability();

        ///< Singleton constructor
        private BfdArchitectureCapability()
        {
            name = "bfd";
        }

        /// Not implemented
        // private BfdArchitectureCapability(BfdArchitectureCapability op2);

        /// Not implemented
        // private static BfdArchitectureCapability operator=(BfdArchitectureCapability op2);

        ~BfdArchitectureCapability()
        {
            SleighArchitecture.shutdown();
        }

        public override Architecture buildArchitecture(string filename, string target,
            TextWriter estream)
        {
            return new BfdArchitecture(filename, target, estream);
        }

        public override bool isFileMatch(string filename)
        {
            StreamReader s;

            try { s = new StreamReader(File.OpenRead(filename)); }
            catch { return false; }
            s.ReadSpaces();
            char val1 = s.ReadMandatoryCharacter();
            char val2 = s.ReadMandatoryCharacter();
            char val3 = s.ReadMandatoryCharacter();
            s.Close();
            if ((val1 == '<') && (val2 == 'b') && (val3 == 'i'))
                return false;       // Probably XML, not BFD
            return true;
        }

        public override bool isXmlMatch(Document doc)
        {
            return (doc.getRoot().getName() == "bfd_savefile");
        }
    }
}
