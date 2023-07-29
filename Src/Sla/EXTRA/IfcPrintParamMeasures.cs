using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintParamMeasures : IfaceDecompCommand
    {
        /// \class IfcPrintParamMeasures
        /// \brief Perform parameter-id analysis on the \e current function: `print parammeasures`
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            ParamIDAnalysis pidanalysis = new ParamIDAnalysis(dcp.fd, false);
            pidanalysis.savePretty(*status.fileoptr, true);
            *status.fileoptr << "\n";
        }
    }
}
