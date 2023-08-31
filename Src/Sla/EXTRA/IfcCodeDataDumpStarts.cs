
namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpStarts : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            codedata.dumpFunctionStarts(status.fileoptr);
        }
    }
}
