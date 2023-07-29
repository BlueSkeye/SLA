using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal struct PreferSplitRecord
    {
        internal VarnodeData storage;
        // Number of initial bytes (in address order) to split into first piece
        internal int4 splitoffset;

        internal static bool operator <(PreferSplitRecord op1, PreferSplitRecord op2)
        {
            if (op1.storage.space != op2.storage.space)
                return (op1.storage.space.getIndex() < op2.storage.space.getIndex());
            if (op1.storage.size != op2.storage.size)
                return (op1.storage.size > op2.storage.size); // Bigger sizes come first
            return op1.storage.offset < op2.storage.offset;
        }
    }
}
