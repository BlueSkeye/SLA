using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfaceCodeDataCommand : IfaceCommand
    {
        protected IfaceStatus status;
        protected IfaceDecompData dcp;
        protected CodeDataAnalysis codedata;
        
        public override void setData(IfaceStatus root, IfaceData data)
        {
            status = root;
            codedata = (CodeDataAnalysis*)data;
            dcp = (IfaceDecompData*)status.getData("decompile");
        }

        public override string getModule() => "codedata";

        public override IfaceData createData() => new CodeDataAnalysis();
    }
}
