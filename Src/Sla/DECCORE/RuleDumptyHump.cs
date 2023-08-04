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
    internal class RuleDumptyHump : Rule
    {
        public RuleDumptyHump(string g)
            : base(g, 0, "dumptyhump")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleDumptyHump(getGroup());
        }

        /// \class RuleDumptyHump
        /// \brief Simplify join and break apart: `sub( concat(V,W), c)  =>  sub(W,c)`
        ///
        /// Depending on c, there are other variants:
        ///  - `sub( concat(V,W), 0)  =>  W`
        ///  - `sub( concat(V,W), c)  =>  V`
        ///  - `sub( concat(V,W), c)  =>  sub(V,c)`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {               // If we append something to a varnode
                        // And then take a subpiece that cuts off what
                        // we just appended, treat whole thing as COPY
            Varnode @base;
            Varnode vn;
            Varnode vn1;
            Varnode vn2;
            PcodeOp pieceop;
            int offset, outsize;

            @base = op.getIn(0);
            if (!@base.isWritten()) return 0;
            pieceop = @base.getDef();
            if (pieceop.code() != OpCode.CPUI_PIECE) return 0;
            offset = op.getIn(1).getOffset();
            outsize = op.getOut().getSize();

            vn1 = pieceop.getIn(0);
            vn2 = pieceop.getIn(1);

            if (offset < vn2.getSize())
            {   // Sub draws from vn2
                if (offset + outsize > vn2.getSize()) return 0;    // Also from vn1
                vn = vn2;
            }
            else
            {           // Sub draws from vn1
                vn = vn1;
                offset -= vn2.getSize();   // offset relative to vn1
            }

            if (vn.isFree() && (!vn.isConstant())) return 0;
            if ((offset == 0) && (outsize == vn.getSize()))
            {
                // Eliminate SUB and CONCAT altogether
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, vn, 0); // Skip over CONCAT
            }
            else
            {
                // Eliminate CONCAT and adjust SUB
                data.opSetInput(op, vn, 0); // Skip over CONCAT
                data.opSetInput(op, data.newConstant(4, offset), 1);
            }
            return 1;
        }
    }
}
