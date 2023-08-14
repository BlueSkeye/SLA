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
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image");
            if (dcp.cgraph == (CallGraph)null)
                throw new IfaceExecutionError("Callgraph has not been built");
            if (dcp.conf.evalfp_current == (ProtoModel)null) {
                status.optr.WriteLine("Always using default prototype");
                return;
            }

            if (!dcp.conf.evalfp_current.isMerged()) {
                status.optr.WriteLine($"Always using prototype {dcp.conf.evalfp_current.getName()}");
                return;
            }
            ProtoModelMerged model = (ProtoModelMerged)dcp.conf.evalfp_current;
            status.optr.WriteLine("Trying to distinguish between prototypes:");
            for (int i = 0; i < model.numModels(); ++i)
                status.optr.WriteLine($"  {model.getModel(i).getName()}");
            iterateFunctionsLeafOrder();
        }

        public override void iterationCallback(Funcdata fd)
        {
            DateTime start_time, end_time;

            status.optr.Write($"{fd.getName()} ");
            if (fd.hasNoCode()) {
                status.optr.WriteLine("has no code");
                return;
            }
            if (fd.getFuncProto().isInputLocked()) {
                status.optr.WriteLine("has locked prototype");
                return;
            }
            try {
                dcp.conf.clearAnalysis(fd); // Clear any old analysis
                dcp.conf.allacts.getCurrent().reset(fd);
                start_time = DateTime.UtcNow;
                dcp.conf.allacts.getCurrent().perform(fd);
                end_time = DateTime.UtcNow;
                //    *status.optr << "Decompiled " << fd.getName();
                //    *status.optr << '(' << dec << fd.getSize() << ')';
                status.optr.WriteLine($"proto={fd.getFuncProto().getModelName()}");
                fd.getFuncProto().setModelLock(true);
                TimeSpan duration = (end_time - start_time);
                duration *= 1000.0;
                status.optr.WriteLine($" time={(int)duration.TotalMilliseconds} ms");
            }
            catch (CORE.LowlevelError err) {
                status.optr.WriteLine($"Skipping {fd.getName()}: {err.ToString()}");
            }
            dcp.conf.clearAnalysis(fd);
        }
    }
}
