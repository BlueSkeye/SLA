using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class Location
    {
        private string filename;
        private int lineno;
        
        public Location()
        {
        }

        public Location(string fname, int line)
        {
            filename = fname;
            lineno = line;
        }

        public string getFilename() => filename;

        public int getLineno() => lineno;

        public string format()
        {
            ostringstream s;
            s << filename << ":" << dec << lineno;
            return s.str();
        }
    }
}
