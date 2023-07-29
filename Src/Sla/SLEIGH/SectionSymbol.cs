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
        private int4 templateid;        // Index into the ConstructTpl array
        private int4 define_count;      // Number of definitions of this named section
        private int4 ref_count;     // Number of references to this named section
        
        public SectionSymbol(string nm,int4 id)
            : base(nm)
        {
            templateid = id;
            define_count = 0;
            ref_count = 0;
        }
        
        public int4 getTemplateId() => templateid;

        public void incrementDefineCount()
        {
            define_count += 1;
        }
    
        public void incrementRefCount()
        {
            ref_count += 1;
        }
    
        public int4 getDefineCount() => define_count;

        public int4 getRefCount() => ref_count;

        public override symbol_type getType() => section_symbol;
    }
}
