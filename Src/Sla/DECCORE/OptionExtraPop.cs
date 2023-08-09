using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionExtraPop : ArchOption
    {
        public OptionExtraPop()
        {
            name = "extrapop";
        }

        /// \class OptionExtraPop
        /// \brief Set the \b extrapop parameter used by the (default) prototype model.
        ///
        /// The \b extrapop for a function is the number of bytes popped from the stack that
        /// a calling function can assume when this function is called.
        ///
        /// The first parameter is the integer value to use as the \e extrapop, or the special
        /// value "unknown" which triggers the \e extrapop recovery analysis.
        ///
        /// The second parameter, if present, indicates a specific function to modify. Otherwise,
        /// the default prototype model is modified.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            int expop = -300;
            string res;
            if (p1 == "unknown")
                expop = ProtoModel.extrapop_unknown;
            else
            {
                istringstream s1(p1);
                s1.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
                s1 >> expop;
            }
            if (expop == -300)
                throw ParseError("Bad extrapop adjustment parameter");
            if (p2.size() != 0)
            {
                Funcdata* fd;
                fd = glb.symboltab.getGlobalScope().queryFunction(p2);
                if (fd == (Funcdata)null)
                    throw RecovError("Unknown function name: " + p2);
                fd.getFuncProto().setExtraPop(expop);
                res = "ExtraPop set for function " + p2;
            }
            else
            {
                glb.defaultfp.setExtraPop(expop);
                if (glb.evalfp_current != (ProtoModel)null)
                    glb.evalfp_current.setExtraPop(expop);
                if (glb.evalfp_called != (ProtoModel)null)
                    glb.evalfp_called.setExtraPop(expop);
                res = "Global extrapop set";
            }
            return res;
        }
    }
}
