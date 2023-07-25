using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSubvarZext : Rule
    {
        public RuleSubvarZext(string g)
            : base(g, 0, "subvar_zext")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSubvarZext(getGroup());
        }

        /// \class RuleSubvarZext
        /// \brief Perform SubvariableFlow analysis triggered by INT_ZEXT
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_ZEXT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op->getOut();
            Varnode* invn = op->getIn(0);
            uintb mask = calc_mask(invn->getSize());

            SubvariableFlow subflow(&data,vn,mask,invn->isPtrFlow(),false,false);
            if (!subflow.doTrace()) return 0;
            subflow.doReplacement();
            return 1;
        }
    }
}
