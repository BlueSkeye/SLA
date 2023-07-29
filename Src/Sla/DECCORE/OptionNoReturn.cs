using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionNoReturn : ArchOption
    {
        public OptionNoReturn()
        {
            name = "noreturn";
        }

        /// \class OptionNoReturn
        /// \brief Mark/unmark a specific function with the \e noreturn property
        ///
        /// The first parameter is the symbol name of the function. The second parameter
        /// is "true" to enable the \e noreturn property, "false" to disable.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            Funcdata* infd = glb.symboltab.getGlobalScope().queryFunction(p1);
            if (infd == (Funcdata)null)
                throw RecovError("Unknown function name: " + p1);
            bool val;
            if (p2.size() == 0)
                val = true;
            else
                val = (p2 == "true");
            infd.getFuncProto().setNoReturn(val);
            string prop;
            if (val)
                prop = "true";
            else
                prop = "false";
            string res = "No return property for function " + p1 + " = " + prop;
            return res;
        }
    }
}
