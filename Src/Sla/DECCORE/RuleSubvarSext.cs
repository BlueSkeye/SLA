﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSubvarSext : Rule
    {
        /// Is it guaranteed the root is a sub-variable needing to be trimmed
        private int4 isaggressive;
        
        public RuleSubvarSext(string g)
            : base(g, 0, "subvar_sext")
        {
            isaggressive = false;
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSubvarSext(getGroup());
        }

        /// \class RuleSubvarSext
        /// \brief Perform SubvariableFlow analysis triggered by INT_SEXT
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_SEXT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op->getOut();
            Varnode* invn = op->getIn(0);
            uintb mask = calc_mask(invn->getSize());

            SubvariableFlow subflow(&data,vn,mask,isaggressive,true,false);
            if (!subflow.doTrace()) return 0;
            subflow.doReplacement();
            return 1;
        }

        public override void reset(Funcdata data)
        {
            isaggressive = data.getArch()->aggressive_ext_trim;
        }
    }
}