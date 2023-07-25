using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        
        protected virtual uint4 allocateTemp() => compiler->getUniqueAddr();

        protected virtual Location getLocation(SleighSymbol sym) => compiler->getLocation(sym);

        protected virtual void reportError(Location loc, string msg) => compiler->reportError(loc, msg);

        protected virtual void reportWarning(Location loc, string msg) => compiler->reportWarning(loc, msg);

        protected virtual void addSymbol(SleighSymbol sym) => compiler->addSymbol(sym);

        public SleighPcode()
            : base()
        {
            compiler = (SleighCompile*)0;
        }

        /// Hook in the main parser
        public void setCompiler(SleighCompile comp)
        {
            compiler = comp;
        }
    }
}
