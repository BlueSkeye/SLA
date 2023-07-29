using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCodeDataTarget : IfaceCodeDataCommand
    {
        public override void execute(istream s)
        {
            string token;

            s >> ws;
            if (s.eof())
                throw new IfaceParseError("Missing system call name");

            s >> token;
            List<ImportRecord> irec;
            LoadImageBfd* loadbfd = (LoadImageBfd*)dcp.conf.loader;
            loadbfd.getImportTable(irec);
            int i;
            for (i = 0; i < irec.size(); ++i)
            {
                if (irec[i].funcname == token) break;
            }
            if (i == irec.size())
                *status.fileoptr << "Unable to find reference to call " << token << endl;
            else
            {
                codedata.addTarget(irec[i].funcname, irec[i].thunkaddress, (uint)1);
            }
        }
    }
}
