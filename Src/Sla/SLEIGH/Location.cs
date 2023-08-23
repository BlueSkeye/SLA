
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
            TextWriter s = new StringWriter();
            s.Write($"{filename}:{lineno}");
            return s.ToString();
        }
    }
}
