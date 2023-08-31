
namespace Sla.EXTRA
{
    internal class IfcCodeDataRun : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            codedata.runModel();
        }
    }
}
