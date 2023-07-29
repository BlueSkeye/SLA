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
    internal class DummyOpConstraint : UnifyConstraint
    {
        private int4 opindex;
        
        public DummyOpConstraint(int4 ind)
        {
            maxnum = opindex = ind;
        }
        
        public override UnifyConstraint clone() => (new DummyOpConstraint(opindex))->copyid(this);

        public override bool step(UnifyState state) => true;

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
        }

        public override int4 getBaseIndex() => opindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
        }

        public override bool isDummy() => true;
    }
}
