
namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpTargetHits : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            codedata.dumpTargetHits(status.fileoptr);
        }
    }
}
