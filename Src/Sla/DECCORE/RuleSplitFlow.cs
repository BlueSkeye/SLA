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
    internal class RuleSplitFlow : Rule
    {
        public RuleSplitFlow(string g)
            : base(g, 0, "splitflow")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSplitFlow(getGroup());
        }

        /// \class RuleSplitFlow
        /// \brief Try to detect and split artificially joined Varnodes
        ///
        /// Look for SUBPIECE coming from a PIECE that has come through INDIRECTs and/or MULTIEQUAL
        /// Then: check if the input to SUBPIECE can be viewed as two independent pieces
        /// If so:  split the pieces into independent data-flows
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_SUBPIECE);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            int4 loSize = (int4)op.getIn(1).getOffset();
            if (loSize == 0)            // Make sure SUBPIECE doesn't take least significant part
                return 0;
            Varnode* vn = op.getIn(0);
            if (!vn.isWritten())
                return 0;
            if (vn.isPrecisLo() || vn.isPrecisHi())
                return 0;
            if (op.getOut().getSize() + loSize != vn.getSize())
                return 0;               // Make sure SUBPIECE is taking most significant part
            PcodeOp* concatOp = (PcodeOp*)0;
            PcodeOp* multiOp = vn.getDef();
            while (multiOp.code() == CPUI_INDIRECT)
            {   // PIECE may come through INDIRECT
                Varnode* tmpvn = multiOp.getIn(0);
                if (!tmpvn.isWritten()) return 0;
                multiOp = tmpvn.getDef();
            }
            if (multiOp.code() == CPUI_PIECE)
            {
                if (vn.getDef() != multiOp)
                    concatOp = multiOp;
            }
            else if (multiOp.code() == CPUI_MULTIEQUAL)
            {   // Otherwise PIECE comes through MULTIEQUAL
                for (int4 i = 0; i < multiOp.numInput(); ++i)
                {
                    Varnode* invn = multiOp.getIn(i);
                    if (!invn.isWritten()) continue;
                    PcodeOp* tmpOp = invn.getDef();
                    if (tmpOp.code() == CPUI_PIECE)
                    {
                        concatOp = tmpOp;
                        break;
                    }
                }
            }
            if (concatOp == (PcodeOp*)0)            // Didn't find the concatenate
                return 0;
            if (concatOp.getIn(1).getSize() != loSize)
                return 0;
            SplitFlow splitFlow(&data,vn,loSize);
            if (!splitFlow.doTrace()) return 0;
            splitFlow.apply();
            return 1;
        }
    }
}
