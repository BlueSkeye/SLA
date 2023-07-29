using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (dcp->conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");

            string filename;
            ifstream fs;

            s >> ws >> filename;
            if (filename.empty())
                throw IfaceParseError("Missing filename");

            fs.open(filename.c_str());
            if (!fs)
                throw IfaceExecutionError("Unable to open file: " + filename);

            try
            {               // Try to parse the file
                parse_C(dcp->conf, fs);
            }
            catch (ParseError err)
            {
                *status->optr << "Error in C syntax: " << err.ToString() << endl;
                throw IfaceExecutionError("Bad C syntax");
            }
            fs.close();
        }
    }
}
