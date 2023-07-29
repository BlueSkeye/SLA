using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSelectCse : Rule
    {
        public RuleSelectCse(string g)
            : base(g,0,"selectcse")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSelectCse(getGroup());
        }

        /// \class RuleSelectCse
        /// \brief Look for common sub-expressions (built out of a restricted set of ops)
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_SUBPIECE);
            oplist.push_back(CPUI_INT_SRIGHT); // For division optimization corrections
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op.getIn(0);
            list<PcodeOp*>::const_iterator iter;
            OpCode opc = op.code();
            PcodeOp* otherop;
            uint hash;
            List<pair<uint, PcodeOp*>> list;
            List<Varnode*> vlist;

            for (iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
            {
                otherop = *iter;
                if (otherop.code() != opc) continue;
                hash = otherop.getCseHash();
                if (hash == 0) continue;
                list.push_back(pair<uint, PcodeOp*>(hash, otherop));
            }
            if (list.size() <= 1) return 0;
            cseEliminateList(data, list, vlist);
            if (vlist.empty()) return 0;
            return 1;
        }
    }
}
