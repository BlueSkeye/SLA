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
    internal class IfcProducePrototypes : IfaceDecompCommand
    {
        /// \class IfcProducePrototypes
        /// \brief Determine the prototype model for all functions: `produce prototypes`
        ///
        /// Functions are walked in leaf order.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw new IfaceExecutionError("No load image");
            if (dcp.cgraph == (CallGraph*)0)
                throw new IfaceExecutionError("Callgraph has not been built");
            if (dcp.conf.evalfp_current == (ProtoModel*)0)
            {
                *status.optr << "Always using default prototype" << endl;
                return;
            }

            if (!dcp.conf.evalfp_current.isMerged())
            {
                *status.optr << "Always using prototype " << dcp.conf.evalfp_current.getName() << endl;
                return;
            }
            ProtoModelMerged* model = (ProtoModelMerged*)dcp.conf.evalfp_current;
            *status.optr << "Trying to distinguish between prototypes:" << endl;
            for (int i = 0; i < model.numModels(); ++i)
                *status.optr << "  " << model.getModel(i).getName() << endl;

            iterateFunctionsLeafOrder();
        }

        public override void iterationCallback(Funcdata fd)
        {
            clock_t start_time, end_time;
            float duration;

            *status.optr << fd.getName() << ' ';
            if (fd.hasNoCode())
            {
                *status.optr << "has no code" << endl;
                return;
            }
            if (fd.getFuncProto().isInputLocked())
            {
                *status.optr << "has locked prototype" << endl;
                return;
            }
            try
            {
                dcp.conf.clearAnalysis(fd); // Clear any old analysis
                dcp.conf.allacts.getCurrent().reset(*fd);
                start_time = clock();
                dcp.conf.allacts.getCurrent().perform(*fd);
                end_time = clock();
                //    *status.optr << "Decompiled " << fd.getName();
                //    *status.optr << '(' << dec << fd.getSize() << ')';
                *status.optr << "proto=" << fd.getFuncProto().getModelName();
                fd.getFuncProto().setModelLock(true);
                duration = ((float)(end_time - start_time)) / CLOCKS_PER_SEC;
                duration *= 1000.0;
                *status.optr << " time=" << fixed << setprecision(0) << duration << " ms" << endl;
            }
            catch (LowlevelError err) {
                *status.optr << "Skipping " << fd.getName() << ": " << err.ToString() << endl;
            }
            dcp.conf.clearAnalysis(fd);
        }
    }
}
