using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionNamespaceStrategy : ArchOption
    {
        public OptionNamespaceStrategy()
        {
            name = "namespacestrategy";
        }

        /// \class OptionNamespaceStrategy
        /// \brief How should namespace tokens be displayed
        ///
        /// The first parameter gives the strategy identifier, mapping to PrintLanguage::namespace_strategy.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            PrintLanguage::namespace_strategy strategy;
            if (p1 == "minimal")
                strategy = PrintLanguage::MINIMAL_NAMESPACES;
            else if (p1 == "all")
                strategy = PrintLanguage::ALL_NAMESPACES;
            else if (p1 == "none")
                strategy = PrintLanguage::NO_NAMESPACES;
            else
                throw ParseError("Must specify a valid strategy");
            glb.print.setNamespaceStrategy(strategy);
            return "Namespace strategy set";
        }
    }
}
