using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief A dummy command used during parsing
    internal class IfaceCommandDummy : IfaceCommand
    {
        public override void setData(IfaceStatus root, IfaceData data)
        {
        }

        public override void execute(TextReader s)
        {
        }

        public override string getModule() => "dummy";

        public override IfaceData createData() => (IfaceData*)0;
    }
}
