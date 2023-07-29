using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcListprototypes : IfaceDecompCommand
    {
        /// \class IfcListprototypes
        /// \brief List known prototype models: `list prototypes`
        ///
        /// All prototype models are listed with markup indicating the
        /// \e default, the evaluation model for the active function, and
        /// the evaluation model for called functions.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");

            map<string, ProtoModel*>::const_iterator iter;
            for (iter = dcp.conf.protoModels.begin(); iter != dcp.conf.protoModels.end(); ++iter)
            {
                ProtoModel* model = (*iter).second;
                *status.optr << model.getName();
                if (model == dcp.conf.defaultfp)
                    *status.optr << " default";
                else if (model == dcp.conf.evalfp_called)
                    *status.optr << " eval called";
                else if (model == dcp.conf.evalfp_current)
                    *status.optr << " eval current";
                *status.optr << endl;
            }
        }
    }
}
