using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcGlobalify : IfaceDecompCommand
    {
        /// \class IfcGlobalify
        /// \brief Treat all normal memory as discoverable global variables: `global spaces`
        ///
        /// This has the drastic effect that the decompiler will treat all registers and stack
        /// locations as global variables.
        public override void execute(TextReader s)
        {
            if (dcp->conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");
            dcp->conf->globalify();
            *status->optr << "Successfully made all registers/memory locations global" << endl;
        }
    }
}
