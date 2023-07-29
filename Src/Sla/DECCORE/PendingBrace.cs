using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Set of print commands for displaying an open brace '{' and setting a new indent level
    ///
    /// These are the print commands sent to the emitter prior to printing and \e else block.
    /// The open brace can be canceled if the block decides it wants to use "else if" syntax.
    internal class PendingBrace : PendPrint
    {
        private int4 indentId;      ///< Id associated with the new indent level
        
        public PendingBrace()
        {
            indentId = -1;
        }

        ///< If commands have been issued, returns the new indent level id.
        public int4 getIndentId() => indentId; 
        
        public override void callback(Emit emit)
        {
            emit->print(PrintC::OPEN_CURLY);
            indentId = emit->startIndent();
        }
    }
}
