
namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpModelHits : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            codedata.dumpModelHits(status.fileoptr);
        }
    }
}
