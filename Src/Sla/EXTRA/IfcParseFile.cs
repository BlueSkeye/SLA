using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcParseFile : IfaceDecompCommand
    {
        /// \class IfcParseFile
        /// \brief Parse a file with C declarations: `parse file <filename>`
        ///
        /// The file must contain C syntax data-type and function declarations.
        /// Data-types become part of the program, and function declarations,
        /// if the symbol already exists, associate the prototype with the symbol.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            s.ReadSpaces();
            string filename = s.ReadString();
            if (filename.empty())
                throw new IfaceParseError("Missing filename");

            FileStream fs;

            try { fs = File.OpenRead(filename); }
            catch {
                throw new IfaceExecutionError("Unable to open file: " + filename);
            }

            try {
                // Try to parse the file
                Grammar.parse_C(dcp.conf, fs);
            }
            catch (ParseError err) {
                status.optr.WriteLine($"Error in C syntax: {err.ToString()}");
                throw new IfaceExecutionError("Bad C syntax");
            }
            finally { if (fs != null) fs.Close(); }
        }
    }
}
