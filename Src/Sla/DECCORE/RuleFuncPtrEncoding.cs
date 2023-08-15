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
    internal class RuleFuncPtrEncoding : Rule
    {
        public RuleFuncPtrEncoding(string g)
            : base(g, 0, "funcptrencoding")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleFuncPtrEncoding(getGroup());
        }

        /// \class RuleFuncPtrEncoding
        /// \brief Eliminate ARM/THUMB style masking of the low order bits on function pointers
        ///
        /// NOTE: The emulation philosophy is that it really isn't eliminated but,
        /// the CALLIND operator is now dealing with it.  Hence actions like ActionDeindirect
        /// that are modeling a CALLIND's behavior need to take this into account.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_CALLIND);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            int align = data.getArch().funcptr_align;
            if (align == 0) return 0;
            Varnode* vn = op.getIn(0);
            if (!vn.isWritten()) return 0;
            PcodeOp* andop = vn.getDef();
            if (andop.code() != OpCode.CPUI_INT_AND) return 0;
            Varnode* maskvn = andop.getIn(1);
            if (!maskvn.isConstant()) return 0;
            ulong val = maskvn.getOffset();
            ulong testmask = Globals.calc_mask(maskvn.getSize());
            ulong slide = ~((ulong)0);
            slide <<= align;
            if ((testmask & slide) == val)
            { // 1-bit encoding
                data.opRemoveInput(andop, 1);   // Eliminate the mask
                data.opSetOpcode(andop, OpCode.CPUI_COPY);
                return 1;
            }
            return 0;
        }
    }
}
