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
    internal class RuleSignMod2nOpt : Rule
    {
        public RuleSignMod2nOpt(string g)
            : base(g, 0, "signmod2nopt")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSignMod2nOpt(getGroup());
        }

        /// \class RuleSignMod2nOpt
        /// \brief Convert INT_SREM forms:  `(V + (sign >> (64-n)) & (2^n-1)) - (sign >> (64-n)  =>  V s% 2^n`
        ///
        /// Note: `sign = V s>> 63`  The INT_AND may be performed on a truncated result and then reextended.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_RIGHT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant()) return 0;
            int4 shiftAmt = op.getIn(1).getOffset();
            Varnode* a = checkSignExtraction(op.getIn(0));
            if (a == (Varnode*)0 || a.isFree()) return 0;
            Varnode* correctVn = op.getOut();
            int4 n = a.getSize() * 8 - shiftAmt;
            uintb mask = 1;
            mask = (mask << n) - 1;
            list<PcodeOp*>::const_iterator iter;
            for (iter = correctVn.beginDescend(); iter != correctVn.endDescend(); ++iter)
            {
                PcodeOp* multop = *iter;
                if (multop.code() != CPUI_INT_MULT) continue;
                Varnode* negone = multop.getIn(1);
                if (!negone.isConstant()) continue;
                if (negone.getOffset() != calc_mask(correctVn.getSize())) continue;
                PcodeOp* baseOp = multop.getOut().loneDescend();
                if (baseOp == (PcodeOp*)0) continue;
                if (baseOp.code() != CPUI_INT_ADD) continue;
                int4 slot = 1 - baseOp.getSlot(multop.getOut());
                Varnode* andOut = baseOp.getIn(slot);
                if (!andOut.isWritten()) continue;
                PcodeOp* andOp = andOut.getDef();
                int4 truncSize = -1;
                if (andOp.code() == CPUI_INT_ZEXT)
                {   // Look for intervening extension after INT_AND
                    andOut = andOp.getIn(0);
                    if (!andOut.isWritten()) continue;
                    andOp = andOut.getDef();
                    if (andOp.code() != CPUI_INT_AND) continue;
                    truncSize = andOut.getSize();      // If so we have a truncated form
                }
                else if (andOp.code() != CPUI_INT_AND)
                    continue;

                Varnode* constVn = andOp.getIn(1);
                if (!constVn.isConstant()) continue;
                if (constVn.getOffset() != mask) continue;
                Varnode* addOut = andOp.getIn(0);
                if (!addOut.isWritten()) continue;
                PcodeOp* addOp = addOut.getDef();
                if (addOp.code() != CPUI_INT_ADD) continue;
                // Search for "a" as one of the inputs to addOp
                int4 aSlot;
                for (aSlot = 0; aSlot < 2; ++aSlot)
                {
                    Varnode* vn = addOp.getIn(aSlot);
                    if (truncSize >= 0)
                    {
                        if (!vn.isWritten()) continue;
                        PcodeOp* subOp = vn.getDef();
                        if (subOp.code() != CPUI_SUBPIECE) continue;
                        if (subOp.getIn(1).getOffset() != 0) continue;
                        vn = subOp.getIn(0);
                    }
                    if (a == vn) break;
                }
                if (aSlot > 1) continue;
                // Verify that the other input to addOp is an INT_RIGHT by shiftAmt
                Varnode* extVn = addOp.getIn(1 - aSlot);
                if (!extVn.isWritten()) continue;
                PcodeOp* shiftOp = extVn.getDef();
                if (shiftOp.code() != CPUI_INT_RIGHT) continue;
                constVn = shiftOp.getIn(1);
                if (!constVn.isConstant()) continue;
                int4 shiftval = constVn.getOffset();
                if (truncSize >= 0)
                    shiftval += (a.getSize() - truncSize) * 8;
                if (shiftval != shiftAmt) continue;
                // Verify that the input to INT_RIGHT is a sign extraction of "a"
                extVn = checkSignExtraction(shiftOp.getIn(0));
                if (extVn == (Varnode*)0) continue;
                if (truncSize >= 0)
                {
                    if (!extVn.isWritten()) continue;
                    PcodeOp* subOp = extVn.getDef();
                    if (subOp.code() != CPUI_SUBPIECE) continue;
                    if ((int4)subOp.getIn(1).getOffset() != truncSize) continue;
                    extVn = subOp.getIn(0);
                }
                if (a != extVn) continue;

                data.opSetOpcode(baseOp, CPUI_INT_SREM);
                data.opSetInput(baseOp, a, 0);
                data.opSetInput(baseOp, data.newConstant(a.getSize(), mask + 1), 1);
                return 1;
            }
            return 0;
        }

        /// \brief Verify that the given Varnode is a sign extraction of the form `V s>> 63`
        ///
        /// If not, null is returned.  Otherwise the Varnode whose sign is extracted is returned.
        /// \param outVn is the given Varnode
        /// \return the Varnode being extracted or null
        public static Varnode checkSignExtraction(Varnode outVn)
        {
            if (!outVn.isWritten()) return 0;
            PcodeOp* signOp = outVn.getDef();
            if (signOp.code() != CPUI_INT_SRIGHT)
                return (Varnode*)0;
            Varnode* constVn = signOp.getIn(1);
            if (!constVn.isConstant())
                return (Varnode*)0;
            int4 val = constVn.getOffset();
            Varnode* resVn = signOp.getIn(0);
            int4 insize = resVn.getSize();
            if (val != insize * 8 - 1)
                return (Varnode*)0;
            return resVn;
        }
    }
}
