using Sla.SLEIGH;

namespace Sla.SLACOMP
{
    /// \brief Parsing for the semantic section of Constructors
    ///
    /// This is just the base p-code compiler for building OpTpl and VarnodeTpl.
    /// Symbols, locations, and error/warning messages are tied into to the main
    /// parser.
    internal class SleighPcode : PcodeCompile
    {
        private SleighCompile compiler;            ///< The main SLEIGH parser
        
        protected override uint allocateTemp() => compiler.getUniqueAddr();

        public override Location? getLocation(SleighSymbol sym) => compiler.getLocation(sym);

        public override void reportError(Location loc, string msg) => compiler.reportError(loc, msg);

        public override void reportWarning(Location loc, string msg) => compiler.reportWarning(loc, msg);

        protected override void addSymbol(SleighSymbol sym) => compiler.addSymbol(sym);

        public SleighPcode()
            : base()
        {
            compiler = (SleighCompile)null;
        }

        /// Hook in the main parser
        public void setCompiler(SleighCompile comp)
        {
            compiler = comp;
        }
    }
}
