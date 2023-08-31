
namespace Sla.EXTRA
{
    internal class IfcCodeDataInit : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            codedata.init(dcp.conf);
        }
    }
}
