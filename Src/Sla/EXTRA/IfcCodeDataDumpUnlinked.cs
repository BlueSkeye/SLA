
namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpUnlinked : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            codedata.dumpUnlinked(status.fileoptr);
        }
    }
}
