using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Factory and static initializer for the "java-language" back-end to the decompiler
    ///
    /// The singleton adds itself to the list of possible back-end languages for the decompiler
    /// and it acts as a factory for producing the PrintJava object for emitting java-language tokens.
    internal class PrintJavaCapability : PrintLanguageCapability
    {
        /// The singleton instance
        internal static PrintJavaCapability printJavaCapability = new PrintJavaCapability();

        /// Singleton constructor
        private PrintJavaCapability()
        {
            name = "java-language";
            isdefault = false;
        }

        ///// Not implemented
        //private PrintJavaCapability(PrintJavaCapability op2)
        //{
        //}

        ///// Not implemented
        //private static PrintJavaCapability operator=(PrintJavaCapability op);

        public override PrintLanguage buildLanguage(Architecture glb)
        {
            return new PrintJava(glb, name);
        }
    }
}
