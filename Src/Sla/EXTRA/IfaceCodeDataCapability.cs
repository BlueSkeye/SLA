
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

        public override void registerCommands(IfaceStatus status)
        {
            status.registerCom(new IfcCodeDataInit(), "codedata", "init");
#if BFD_SUPPORTED
            status.registerCom(new IfcCodeDataTarget(), "codedata", "target");
#endif
            status.registerCom(new IfcCodeDataRun(), "codedata", "run");
            status.registerCom(new IfcCodeDataDumpModelHits(), "codedata", "dump", "hits");
            status.registerCom(new IfcCodeDataDumpCrossRefs(), "codedata", "dump", "crossrefs");
            status.registerCom(new IfcCodeDataDumpStarts(), "codedata", "dump", "starts");
            status.registerCom(new IfcCodeDataDumpUnlinked(), "codedata", "dump", "unlinked");
            status.registerCom(new IfcCodeDataDumpTargetHits(), "codedata", "dump", "targethits");
        }
    }
}
