using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if CPUI_RULECOMPILE
    internal class MyLoadImage : LoadImage
    {
        public MyLoadImage()
            : base("nofile")
        {
        }
        
        public override void loadFill(byte[] ptr, int size, Address addr)
        {
            for(int i=0;i<size;++i) ptr[i] = 0;
        }

        public override string getArchType() => "myload";

        public override void adjustVma(long adjust)
        {
        }
    }
#endif
}
