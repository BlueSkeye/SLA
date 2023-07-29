using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCommentInstr : IfaceDecompCommand
    {
        /// \class IfcCommentInstr
        /// \brief Attach a comment to an address: `comment <address> comment text...`
        ///
        /// Add a comment to the database, suitable for integration into decompiler output
        /// for the \e current function.  The command-line takes the address of the
        /// machine instruction which the comment will be attached to and is followed by
        /// the text of the comment.
        public override void execute(TextReader s)
        { // Comment on a particular address within current function
            if (dcp.conf == (Architecture*)0)
                throw new IfaceExecutionError("Decompile action not loaded");

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            int size;
            Address addr = parse_machaddr(s, size, *dcp.conf.types);
            s >> ws;
            string comment;
            char tok;
            s.get(tok);
            while (!s.eof())
            {
                comment += tok;
                s.get(tok);
            }
            uint type = dcp.conf.print.getInstructionComment();
            dcp.conf.commentdb.addComment(type,
                            dcp.fd.getAddress(), addr, comment);
        }
    }
}
