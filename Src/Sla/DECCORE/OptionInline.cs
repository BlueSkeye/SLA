using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionInline : ArchOption
    {
        public OptionInline()
        {
            name = "inline";
        }

        /// \class OptionInline
        /// \brief Mark/unmark a specific function as \e inline
        ///
        /// The first parameter gives the symbol name of a function. The second parameter is
        /// true" to set the \e inline property, "false" to clear.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            Funcdata* infd = glb.symboltab.getGlobalScope().queryFunction(p1);
            if (infd == (Funcdata*)0)
                throw RecovError("Unknown function name: " + p1);
            bool val;
            if (p2.size() == 0)
                val = true;
            else
                val = (p2 == "true");
            infd.getFuncProto().setInline(val);
            string prop;
            if (val)
                prop = "true";
            else
                prop = "false";
            string res = "Inline property for function " + p1 + " = " + prop;
            return res;
        }
    }
}
