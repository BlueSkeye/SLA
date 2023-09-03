using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.CORE.XmlScan;

namespace Sla.EXTRA
{
    internal class TargetHit
    {
        public Address funcstart;        // Starting address of function making target call
        public Address codeaddr;       // Address of instruction refering to target call
        public Address thunkaddr;      // The target call
        public uint mask;         // Mask associated with this target

        public TargetHit(Address func, Address code, Address thunk,uint m)
        {
            funcstart = new Address(ref func);
            codeaddr = new Address(ref code);
            thunkaddr = new Address(ref thunk);

            mask = m;
        }

        public static bool operator <(TargetHit op1, TargetHit op2)
            => (op1.funcstart<op2.funcstart);

        public static bool operator >(TargetHit op1, TargetHit op2)
            => (op1.funcstart > op2.funcstart);
    }
}
