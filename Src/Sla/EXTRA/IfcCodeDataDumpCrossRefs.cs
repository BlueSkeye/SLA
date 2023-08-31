
namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpCrossRefs : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            codedata.dumpCrossRefs(status.fileoptr);
        }
    }
}
