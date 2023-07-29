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
        private int size;          // Number of bytes in token;
        private int index;         // Index of this token, for resolving offsets
        private bool bigendian;
        
        public Token(string ,int sz,bool be, int ind)
        {
            name = nm;
            size = sz;
            bigendian = be;
            index = ind;
        }
        
        public int getSize() => size;

        public bool isBigEndian() => bigendian;

        public int getIndex() => index;

        public string getName() => name;
    }
}
