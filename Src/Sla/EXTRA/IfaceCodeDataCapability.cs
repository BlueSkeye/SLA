using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.EXTRA
{
    internal class IfaceCodeDataCapability : IfaceCapability
    {
        // Singleton instance
        private static IfaceCodeDataCapability ifaceCodeDataCapability =
            new IfaceCodeDataCapability();
        
        private IfaceCodeDataCapability()
        {
            name = "codedata";
        }

        // private IfaceCodeDataCapability(IfaceCodeDataCapability op2);	// Not implemented

        // private IfaceCodeDataCapability operator=(IfaceCodeDataCapability op2);	// Not implemented

        public virtual void registerCommands(IfaceStatus status)
        {
            status.registerCom(new IfcCodeDataInit(), "codedata", "init");
            status.registerCom(new IfcCodeDataTarget(), "codedata", "target");
            status.registerCom(new IfcCodeDataRun(), "codedata", "run");
            status.registerCom(new IfcCodeDataDumpModelHits(), "codedata", "dump", "hits");
            status.registerCom(new IfcCodeDataDumpCrossRefs(), "codedata", "dump", "crossrefs");
            status.registerCom(new IfcCodeDataDumpStarts(), "codedata", "dump", "starts");
            status.registerCom(new IfcCodeDataDumpUnlinked(), "codedata", "dump", "unlinked");
            status.registerCom(new IfcCodeDataDumpTargetHits(), "codedata", "dump", "targethits");
        }
    }
}
