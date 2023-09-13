
namespace Sla.EXTRA
{
#if BFD_SUPPORTED
    internal class IfcCodeDataTarget : IfaceCodeDataCommand
    {
        public override void execute(TextReader s)
        {
            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("Missing system call name");
            string token;
            s >> token;
            List<ImportRecord> irec = new List<ImportRecord>();
            LoadImageBfd loadbfd = (LoadImageBfd)dcp.conf.loader;
            loadbfd.getImportTable(irec);
            int i;
            for (i = 0; i < irec.size(); ++i) {
                if (irec[i].funcname == token) break;
            }
            if (i == irec.size())
                status.fileoptr.WriteLine($"Unable to find reference to call {token}");
            else {
                codedata.addTarget(irec[i].funcname, irec[i].thunkaddress, (uint)1);
            }
        }
    }
#endif
}
