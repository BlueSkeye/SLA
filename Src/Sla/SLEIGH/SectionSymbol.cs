using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class SectionSymbol : SleighSymbol
    {
        // Named p-code sections
        private int templateid;        // Index into the ConstructTpl array
        private int define_count;      // Number of definitions of this named section
        private int ref_count;     // Number of references to this named section
        
        public SectionSymbol(string nm,int id)
            : base(nm)
        {
            templateid = id;
            define_count = 0;
            ref_count = 0;
        }
        
        public int getTemplateId() => templateid;

        public void incrementDefineCount()
        {
            define_count += 1;
        }
    
        public void incrementRefCount()
        {
            ref_count += 1;
        }
    
        public int getDefineCount() => define_count;

        public int getRefCount() => ref_count;

        public override symbol_type getType() => SleighSymbol.symbol_type.section_symbol;
    }
}
