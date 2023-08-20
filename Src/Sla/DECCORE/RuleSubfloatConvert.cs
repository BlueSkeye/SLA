using Sla.CORE;
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
    internal class RuleSubfloatConvert : Rule
    {
        public RuleSubfloatConvert(string g)
            : base(g, 0, "subfloat_convert")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubfloatConvert(getGroup());
        }

        /// \class RuleSubfloatConvert
        /// \brief Perform SubfloatFlow analysis triggered by FLOAT_FLOAT2FLOAT
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_FLOAT_FLOAT2FLOAT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode invn = op.getIn(0);
            Varnode outvn = op.getOut();
            int insize = invn.getSize();
            int outsize = outvn.getSize();
            if (outsize > insize)
            {
                SubfloatFlow subflow = new SubfloatFlow(&data,outvn,insize);
                if (!subflow.doTrace()) return 0;
                subflow.apply();
            }
            else
            {
                SubfloatFlow subflow = new SubfloatFlow(&data,invn,outsize);
                if (!subflow.doTrace()) return 0;
                subflow.apply();
            }
            return 1;
        }
    }
}
