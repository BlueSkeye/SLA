using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcParseLine : IfaceDecompCommand
    {
        /// \class IfcParseLine
        /// \brief Parse a line of C syntax: `parse line ...`
        ///
        /// The line can contain a declaration either a data-type or a function prototype:
        ///    - `parse line typedef int *specialint;`
        ///    - `parse line struct mystruct { int a; int b; }`
        ///    - `parse line extern void myfunc(int a,int b);`
        ///
        /// Data-types go straight into the program.  For a prototype, the function symbol
        /// must already exist.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("No input");

            try {
                // Try to parse the line
                Grammar.parse_C(dcp.conf, s);
            }
            catch (ParseError err) {
                status.optr.WriteLine($"Error in C syntax: {err.ToString()}");
                throw new IfaceExecutionError("Bad C syntax");
            }
        }
    }
}
