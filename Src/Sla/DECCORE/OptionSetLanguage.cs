using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionSetLanguage : ArchOption
    {
        public OptionSetLanguage()
        {
            name = "setlanguage";
        }

        /// \class OptionSetLanguage
        /// \brief Set the current language emitted by the decompiler
        ///
        /// The first specifies the name of the language to emit: "c-language", "java-language", etc.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            string res;

            glb->setPrintLanguage(p1);
            res = "Decompiler produces " + p1;
            return res;
        }
    }
}
