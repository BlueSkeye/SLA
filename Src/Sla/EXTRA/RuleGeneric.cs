﻿using Sla.DECCORE;
using Sla.EXTRA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.EXTRA
{
#if CPUI_RULECOMPILE
    internal class RuleGeneric : Rule
    {
        // A user configurable rule, (a rule read in from a file)
        private List<OpCode> starterops;
        private int opinit;            // Index of initialized op
        private ConstraintGroup constraint;
        private UnifyState state;
        
        public RuleGeneric(string g, string nm, List<OpCode> sops,int opi, ConstraintGroup c)
            : base(g,0, nm)
        {
            state = new UnifyState(c);
            starterops = sops;
            opinit = opi;
            constraint = c;
        }

        ~RuleGeneric()
        {
            delete constraint;
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleGeneric(getGroup(), getName(), starterops, opinit,
                (ConstraintGroup*)constraint.clone());
        }

        public override void getOpList(List<OpCode> oplist)
        {
            for (int i = 0; i < starterops.size(); ++i)
                oplist.Add((uint)starterops[i]);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            state.setFunction(&data);
            state.initialize(opinit, op);
            constraint.initialize(state);
            return constraint.step(state);
        }

        public static RuleGeneric build(string nm, string gp, string content)
        {
            RuleCompile compiler;
            istringstream s = new istringstream(content);
            compiler.run(s, false);
            if (compiler.numErrors() != 0)
                throw new LowlevelError("Unable to parse dynamic rule: " + nm);

            List<OpCode> opcodelist;
            int opinit = compiler.postProcessRule(opcodelist);
            RuleGeneric* res = new RuleGeneric(gp, nm, opcodelist, opinit, compiler.releaseRule());
            return res;
        }
    }
#endif
}
