using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionIndentIncrement : ArchOption
    {
        public OptionIndentIncrement()
        {
            name = "indentincrement";
        }

        /// \class OptionIndentIncrement
        /// \brief Set the number of characters to indent per nested scope.
        ///
        /// The first parameter is the integer value specifying how many characters to indent.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            istringstream s = new istringstream(p1);
            s.unsetf(ios::dec | ios::hex | ios::oct);
            int val = -1;
            s >> val;
            if (val == -1)
                throw ParseError("Must specify integer increment");
            glb.print.setIndentIncrement(val);
            return "Characters per indent level set to " + p1;
        }
    }
}
