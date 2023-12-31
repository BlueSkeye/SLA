﻿using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCallGraphBuild : IfaceDecompCommand
    {
        /// Set to \b true if a quick analysis is desired
        protected bool quick;

        /// \class IfcCallGraphBuild
        /// \brief Build the call-graph for the architecture/program: `callgraph build`
        ///
        /// Build, or rebuild, the call-graph with nodes for all existing functions.
        /// Functions are to decompiled to recover destinations of indirect calls.
        /// Going forward, the graph is held in memory and is accessible by other commands.
        public override void execute(TextReader s)
        {
            dcp.allocateCallGraph();

            dcp.cgraph.buildAllNodes();       // Build a node in the graph for existing symbols
            quick = false;
            iterateFunctionsAddrOrder();
            status.optr.WriteLine("Successfully built callgraph");
        }

        public override void iterationCallback(Funcdata fd)
        {
            DateTime start_time, end_time;

            if (fd.hasNoCode()) {
                status.optr.WriteLine($"No code for {fd.getName()}");
                return;
            }
            if (quick) {
                dcp.fd = fd;
                dcp.followFlow(status.optr, 0);
            }
            else {
                try {
                    dcp.conf.clearAnalysis(fd); // Clear any old analysis
                    dcp.conf.allacts.getCurrent().reset(fd);
                    start_time = DateTime.UtcNow();
                    dcp.conf.allacts.getCurrent().perform(fd);
                    end_time = DateTime.UtcNow();
                    status.optr.Write($"Decompiled {fd.getName()}");
                    //	  *status.optr << ": " << hex << fd.getAddress().getOffset();
                    status.optr.Write($"({fd.getSize()})");
                    TimeSpan duration = (end_time - start_time);
                    status.optr.WriteLine($" time={(int)duration.TotalMilliseconds} ms");
                }
                catch (LowlevelError err) {
                    status.optr.WriteLine($"Skipping {fd.getName()}: {err.ToString()}");
                }
            }
            dcp.cgraph.buildEdges(fd);
            dcp.conf.clearAnalysis(fd);
        }
    }
}
