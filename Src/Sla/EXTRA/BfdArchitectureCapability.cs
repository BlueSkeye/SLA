using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        // private BfdArchitectureCapability(BfdArchitectureCapability op2);   ///< Not implemented

        // private static BfdArchitectureCapability operator=(BfdArchitectureCapability op2);	///< Not implemented

        ~BfdArchitectureCapability()
        {
            SleighArchitecture::shutdown();
        }

        public override Architecture buildArchitecture(string filename, string target,
            TextWriter estream)
        {
            return new BfdArchitecture(filename, target, estream);
        }

        public override bool isFileMatch(string filename)
        {
            ifstream s(filename.c_str());
            if (!s)
                return false;
            int val1, val2, val3;
            s >> ws;
            val1 = s.get();
            val2 = s.get();
            val3 = s.get();
            s.close();
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
