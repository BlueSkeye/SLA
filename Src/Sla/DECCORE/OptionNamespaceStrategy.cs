using Sla.EXTRA;

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
            PrintLanguage.namespace_strategy strategy;
            if (p1 == "minimal")
                strategy = PrintLanguage.namespace_strategy.MINIMAL_NAMESPACES;
            else if (p1 == "all")
                strategy = PrintLanguage.namespace_strategy.ALL_NAMESPACES;
            else if (p1 == "none")
                strategy = PrintLanguage.namespace_strategy.NO_NAMESPACES;
            else
                throw new ParseError("Must specify a valid strategy");
            glb.print.setNamespaceStrategy(strategy);
            return "Namespace strategy set";
        }
    }
}
