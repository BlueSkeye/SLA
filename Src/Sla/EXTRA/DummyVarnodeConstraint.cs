﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class DummyVarnodeConstraint : UnifyConstraint
    {
        private int varindex;
        
        public DummyVarnodeConstraint(int ind)
        {
            maxnum = varindex = ind;
        }
        
        public override UnifyConstraint clone() => (new DummyVarnodeConstraint(varindex)).copyid(this);

        public override bool step(UnifyState state) => true;

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[varindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override int getBaseIndex() => varindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
        }

        public override bool isDummy() => true;
    }
}
