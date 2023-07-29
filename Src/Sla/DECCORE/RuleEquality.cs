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
    internal class RuleEquality : Rule
    {
        public RuleEquality(string g)
            : base(g, 0, "equality")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleEquality(getGroup());
        }

        /// \class RuleEquality
        /// \brief Collapse INT_EQUAL and INT_NOTEQUAL:  `f(V,W) == f(V,W)  =>  true`
        ///
        /// If both inputs to an INT_EQUAL or INT_NOTEQUAL op are functionally equivalent,
        /// the op can be collapsed to a COPY of a \b true or \b false.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_EQUAL);
            oplist.push_back(CPUI_INT_NOTEQUAL);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn;
            if (!functionalEquality(op.getIn(0), op.getIn(1)))
                return 0;

            data.opSetOpcode(op, CPUI_COPY);
            data.opRemoveInput(op, 1);
            vn = data.newConstant(1, (op.code() == CPUI_INT_EQUAL) ? 1 : 0);
            data.opSetInput(op, vn, 0);
            return 1;
        }
    }
}
