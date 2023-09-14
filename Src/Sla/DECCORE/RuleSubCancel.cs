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
    internal class RuleSubCancel : Rule
    {
        public RuleSubCancel(string g)
            : base(g, 0, "subcancel")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubCancel(getGroup());
        }

        /// \class RuleSubCancel
        /// \brief Simplify composition of SUBPIECE with INT_ZEXT or INT_SEXT
        ///
        /// The SUBPIECE may partially or wholly canceled out:
        ///  - `sub(zext(V),0)  =>  zext(V)`
        ///  - `sub(zext(V),0)  =>  V`
        ///  - `sub(zext(V),0)  =>  sub(V)`
        ///
        /// This also supports the corner case:
        ///  - `sub(zext(V),c)  =>  0  when c is big enough`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {               // A SUBPIECE of an extension may cancel
            Varnode @base;
            Varnode thruvn;
            int offset, outsize, insize, farinsize;
            PcodeOp extop;
            OpCode opc;

            @base = op.getIn(0);
            if (!@base.isWritten()) return 0;
            extop = @base.getDef();
            opc = extop.code();
            if ((opc != OpCode.CPUI_INT_ZEXT) && (opc != OpCode.CPUI_INT_SEXT))
                return 0;
            offset = (int)op.getIn(1).getOffset();
            outsize = op.getOut().getSize();
            insize = @base.getSize();
            farinsize = extop.getIn(0).getSize();

            if (offset == 0)
            {       // If SUBPIECE is of least sig part
                thruvn = extop.getIn(0);   // Something still comes through
                if (thruvn.isFree())
                {
                    if (thruvn.isConstant() && (insize > sizeof(ulong)) && (outsize == farinsize))
                    {
                        // If we have a constant that is too big to represent, and the elimination is total
                        opc = OpCode.CPUI_COPY;    // go ahead and do elimination
                        thruvn = data.newConstant(thruvn.getSize(), thruvn.getOffset()); // with new constant varnode
                    }
                    else
                        return 0; // If original is constant or undefined don't proceed
                }
                else if (outsize == farinsize)
                    opc = OpCode.CPUI_COPY;        // Total elimination of extension
                else if (outsize < farinsize)
                    opc = OpCode.CPUI_SUBPIECE;
            }
            else
            {
                if ((opc == OpCode.CPUI_INT_ZEXT) && (farinsize <= offset))
                { // output contains nothing of original input
                    opc = OpCode.CPUI_COPY;        // Nothing but zero coming through
                    thruvn = data.newConstant(outsize, 0);
                }
                else            // Missing one case here
                    return 0;
            }

            data.opSetOpcode(op, opc);  // SUBPIECE <- EXT replaced with one op
            data.opSetInput(op, thruvn, 0);

            if (opc == OpCode.CPUI_SUBPIECE)
                data.opSetInput(op, data.newConstant(op.getIn(1).getSize(), (ulong)offset), 1);
            else
                data.opRemoveInput(op, 1);  // ZEXT, SEXT, or COPY has only 1 input
            return 1;
        }
    }
}
