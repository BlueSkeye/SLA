using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionErrorReinterpreted : ArchOption
    {
        public OptionErrorReinterpreted()
        {
            name = "errorreinterpreted";
        }

        /// \class OptionErrorReinterpreted
        /// \brief Toggle whether off-cut reinterpretation of an instruction is a fatal error
        ///
        /// If the first parameter is "on", interpreting the same code bytes at two or more different
        /// \e cuts, during disassembly, is considered a fatal error.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);

            string res;
            if (val)
            {
                res = "Instruction reinterpretation is now a fatal error";
                glb.flowoptions |= FlowInfo.FlowFlag.error_reinterpreted;
            }
            else
            {
                res = "Instruction reinterpretation is now NOT a fatal error";
                glb.flowoptions &= ~((uint)FlowInfo.FlowFlag.error_reinterpreted);
            }

            return res;
        }
    }
}
