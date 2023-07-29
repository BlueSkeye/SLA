using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.EXTRA
{
    internal class IfcCallFixup : IfaceDecompCommand
    {
        /// \brief Scan a single-line p-code snippet declaration from the given stream
        ///
        /// A declarator is scanned first, providing a name to associate with the snippet, as well
        /// as potential names of the formal \e output Varnode and \e input Varnodes.
        /// The body of the snippet is then surrounded by '{' and '}'  The snippet name,
        /// input/output names, and the body are passed back to the caller.
        /// \param s is the given stream to scan
        /// \param name passes back the name of the snippet
        /// \param outname passes back the formal output parameter name, or is empty
        /// \param inname passes back an array of the formal input parameter names
        /// \param pcodestring passes back the snippet body
        public static void readPcodeSnippet(TextReader s, string name, string outname, List<string> inname,
            string pcodestring)
        {
            char bracket;
            s >> outname;
            parse_toseparator(s, name);
            s >> bracket;
            if (outname == "void")
                outname = "";
            if (bracket != '(')
                throw IfaceParseError("Missing '('");
            while (bracket != ')')
            {
                string param;
                parse_toseparator(s, param);
                s >> bracket;
                if (param.size() != 0)
                    inname.push_back(param);
            }
            s >> ws >> bracket;
            if (bracket != '{')
                throw IfaceParseError("Missing '{'");
            getline(s, pcodestring, '}');
        }

        /// \class IfcCallFixup
        /// \brief Add a new call fix-up to the program: `fixup call ...`
        ///
        /// Create a new call fixup-up for the architecture/program, suitable for
        /// replacing called functions.  The fix-up is specified as a function-style declarator,
        /// which also provides the formal name of the fix-up.
        /// A "void" return-type and empty parameter list must be given.
        /// \code
        ///   fixup call void myfixup1() { EAX = 0; RBX = RCX + RDX + 1; }
        /// \endcode
        public override void execute(TextReader s)
        {
            string name, outname, pcodestring;
            vector<string> inname;

            readPcodeSnippet(s, name, outname, inname, pcodestring);
            int4 id = -1;
            try
            {
                id = dcp.conf.pcodeinjectlib.manualCallFixup(name, pcodestring);
            }
            catch (LowlevelError err)
            {
                *status.optr << "Error compiling pcode: " << err.ToString() << endl;
                return;
            }
            InjectPayload* payload = dcp.conf.pcodeinjectlib.getPayload(id);
            payload.printTemplate(*status.optr);
        }
    }
}
