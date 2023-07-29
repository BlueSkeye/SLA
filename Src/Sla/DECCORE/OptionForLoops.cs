using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionForLoops : ArchOption
    {
        public OptionForLoops()
        {
            name = "analyzeforloops";
        }

        /// \class OptionForLoops
        /// \brief Toggle whether the decompiler attempts to recover \e for-loop variables
        ///
        /// Setting the first parameter to "on" causes the decompiler to search for a suitable loop variable
        /// controlling iteration of a \e while-do block.  The \e for-loop displays the following on a single line:
        ///    - loop variable initializer (optional)
        ///    - loop condition
        ///    - loop variable incrementer
        ///
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            glb.analyze_for_loops = onOrOff(p1);

            string res = "Recovery of for-loops is " + p1;
            return res;
        }
    }
}
