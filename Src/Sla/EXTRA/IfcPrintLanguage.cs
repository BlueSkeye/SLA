using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintLanguage : IfaceDecompCommand
    {
        /// \class IfcPrintLanguage
        /// \brief Print current output using a specific language: `print language <langname>`
        ///
        /// The current function must already be decompiled.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("No print language specified");
            string langroot = s.ReadString();
            langroot = langroot + "-language";

            string curlangname = dcp.conf.print.getName();
            dcp.conf.setPrintLanguage(langroot);
            dcp.conf.print.setOutputStream(status.fileoptr);
            dcp.conf.print.docFunction(dcp.fd);
            dcp.conf.setPrintLanguage(curlangname); // Reset to original language
        }
    }
}
