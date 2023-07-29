using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class Token
    {
        // A multiple-byte sized chunk of pattern in a bitstream
        private string name;
        private int4 size;          // Number of bytes in token;
        private int4 index;         // Index of this token, for resolving offsets
        private bool bigendian;
        
        public Token(string ,int4 sz,bool be, int4 ind)
        {
            name = nm;
            size = sz;
            bigendian = be;
            index = ind;
        }
        
        public int4 getSize() => size;

        public bool isBigEndian() => bigendian;

        public int4 getIndex() => index;

        public string getName() => name;
    }
}
