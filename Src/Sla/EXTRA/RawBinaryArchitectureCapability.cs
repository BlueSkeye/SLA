using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief Extension point for building an Architecture that reads in raw images
    internal class RawBinaryArchitectureCapability : ArchitectureCapability
    {
        ///< The singleton instance
        private static RawBinaryArchitectureCapability rawBinaryArchitectureCapability =
            new RawBinaryArchitectureCapability();

        private RawBinaryArchitectureCapability()
        {
            name = "raw";
        }

        // private RawBinaryArchitectureCapability(RawBinaryArchitectureCapability op2);	///< Not implemented

        // private RawBinaryArchitectureCapability &operator=(RawBinaryArchitectureCapability &op2);	///< Not implemented

        ~RawBinaryArchitectureCapability()
        {
            SleighArchitecture.shutdown();
        }

        public override Architecture buildArchitecture(string filename, string target, TextWriter estream)
            => new RawBinaryArchitecture(filename, target, estream);

        // File can always be opened as raw binary
        public override bool isFileMatch(string filename) => true;

        public override bool isXmlMatch(Document doc) => doc->getRoot()->getName() == "raw_savefile";
    }
}
