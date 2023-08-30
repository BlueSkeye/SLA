using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Factory and static initializer for the "c-language" back-end to the decompiler
    ///
    /// The singleton adds itself to the list of possible back-end languages for the decompiler
    /// and it acts as a factory for producing the PrintC object for emitting c-language tokens.
    internal class PrintCCapability : PrintLanguageCapability
    {
        /// The singleton instance
        private static PrintCCapability printCCapability = new PrintCCapability();

        /// Initialize the singleton
        private PrintCCapability()
        {
            name = "c-language";
            isdefault = true;
        }

        ///// Not implemented
        //private PrintCCapability(PrintCCapability op2)
        //{
        //}


        //// Not implemented
        //private static PrintCCapability operator=(PrintCCapability op);

        public override PrintLanguage buildLanguage(Architecture glb)
        {
            return new PrintC(glb, name);
        }
    }
}
