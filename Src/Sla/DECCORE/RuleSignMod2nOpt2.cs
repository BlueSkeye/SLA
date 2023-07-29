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
    internal class RuleSignMod2nOpt2 : Rule
    {
        /// \brief Verify an \e if block like `V = (V s< 0) ? V + 2^n-1 : V`
        ///
        /// \param op is the MULTIEQUAL
        /// \param npow is the constant 2^n
        /// \return the Varnode V in the form, or null if the form doesn't match
        private static Varnode checkMultiequalForm(PcodeOp op, ulong npow)
        {
            if (op.numInput() != 2) return (Varnode)null;
            npow -= 1;      // 2^n - 1
            int slot;
            Varnode * base;
            for (slot = 0; slot < op.numInput(); ++slot)
            {
                Varnode* addOut = op.getIn(slot);
                if (!addOut.isWritten()) continue;
                PcodeOp* addOp = addOut.getDef();
                if (addOp.code() != CPUI_INT_ADD) continue;
                Varnode* constVn = addOp.getIn(1);
                if (!constVn.isConstant()) continue;
                if (constVn.getOffset() != npow) continue;
                base = addOp.getIn(0);
                Varnode* otherBase = op.getIn(1 - slot);
                if (otherBase == base)
                    break;
            }
            if (slot > 1) return (Varnode)null;
            BlockBasic* bl = op.getParent();
            int innerSlot = 0;
            BlockBasic* inner = (BlockBasic*)bl.getIn(innerSlot);
            if (inner.sizeOut() != 1 || inner.sizeIn() != 1)
            {
                innerSlot = 1;
                inner = (BlockBasic*)bl.getIn(innerSlot);
                if (inner.sizeOut() != 1 || inner.sizeIn() != 1)
                    return (Varnode)null;
            }
            BlockBasic* decision = (BlockBasic*)inner.getIn(0);
            if (bl.getIn(1 - innerSlot) != decision) return (Varnode)null;
            PcodeOp* cbranch = decision.lastOp();
            if (cbranch == (PcodeOp)null || cbranch.code() != CPUI_CBRANCH) return (Varnode)null;
            Varnode* boolVn = cbranch.getIn(1);
            if (!boolVn.isWritten()) return (Varnode)null;
            PcodeOp* lessOp = boolVn.getDef();
            if (lessOp.code() != CPUI_INT_SLESS) return (Varnode)null;
            if (!lessOp.getIn(1).isConstant()) return (Varnode)null;
            if (lessOp.getIn(1).getOffset() != 0) return (Varnode)null;
            FlowBlock* negBlock = cbranch.isBooleanFlip() ? decision.getFalseOut() : decision.getTrueOut();
            int negSlot = (negBlock == inner) ? innerSlot : (1 - innerSlot);
            if (negSlot != slot) return (Varnode)null;
            return base;
        }

        /// \brief Verify a form of `V - (V s>> 0x3f)`
        ///
        /// \param op is the possible root INT_ADD of the form
        /// \return the Varnode V in the form, or null if the form doesn't match
        private static Varnode checkSignExtForm(PcodeOp op)
        {
            int slot;
            for (slot = 0; slot < 2; ++slot)
            {
                Varnode* minusVn = op.getIn(slot);
                if (!minusVn.isWritten()) continue;
                PcodeOp* multOp = minusVn.getDef();
                if (multOp.code() != CPUI_INT_MULT) continue;
                Varnode* constVn = multOp.getIn(1);
                if (!constVn.isConstant()) continue;
                if (constVn.getOffset() != Globals.calc_mask(constVn.getSize())) continue;
                Varnode * base = op.getIn(1 - slot);
                Varnode* signExt = multOp.getIn(0);
                if (!signExt.isWritten()) continue;
                PcodeOp* shiftOp = signExt.getDef();
                if (shiftOp.code() != CPUI_INT_SRIGHT) continue;
                if (shiftOp.getIn(0) != base) continue;
                constVn = shiftOp.getIn(1);
                if (!constVn.isConstant()) continue;
                if ((int)constVn.getOffset() != 8 * @base.getSize() - 1) continue;
                return base;
            }
            return (Varnode)null;
        }

        public RuleSignMod2nOpt2(string g)
            : base(g, 0, "signmod2nopt2")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSignMod2nOpt2(getGroup());
        }

        /// \class RuleSignMod2nOpt2
        /// \brief Convert INT_SREM form:  `V - (Vadj & ~(2^n-1)) =>  V s% 2^n`
        ///
        /// Note: `Vadj = (V<0) ? V + 2^n-1 : V`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_MULT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* constVn = op.getIn(1);
            if (!constVn.isConstant()) return 0;
            ulong mask = Globals.calc_mask(constVn.getSize());
            if (constVn.getOffset() != mask) return 0; // Must be INT_MULT by -1
            Varnode* andOut = op.getIn(0);
            if (!andOut.isWritten()) return 0;
            PcodeOp* andOp = andOut.getDef();
            if (andOp.code() != CPUI_INT_AND) return 0;
            constVn = andOp.getIn(1);
            if (!constVn.isConstant()) return 0;
            ulong npow = (~constVn.getOffset() + 1) & mask;
            if (popcount(npow) != 1) return 0;      // constVn must be of form 11111..000..
            if (npow == 1) return 0;
            Varnode* adjVn = andOp.getIn(0);
            if (!adjVn.isWritten()) return 0;
            PcodeOp* adjOp = adjVn.getDef();
            Varnode * base;
            if (adjOp.code() == CPUI_INT_ADD)
            {
                if (npow != 2) return 0;        // Special mod 2 form
                base = checkSignExtForm(adjOp);
            }
            else if (adjOp.code() == CPUI_MULTIEQUAL)
            {
                base = checkMultiequalForm(adjOp, npow);
            }
            else
                return 0;
            if (base == (Varnode)null) return 0;
            if (@base.isFree()) return 0;
            Varnode* multOut = op.getOut();
            list<PcodeOp*>::const_iterator iter;
            for (iter = multOut.beginDescend(); iter != multOut.endDescend(); ++iter)
            {
                PcodeOp* rootOp = *iter;
                if (rootOp.code() != CPUI_INT_ADD) continue;
                int slot = rootOp.getSlot(multOut);
                if (rootOp.getIn(1 - slot) != base) continue;
                if (slot == 0)
                    data.opSetInput(rootOp, base, 0);
                data.opSetInput(rootOp, data.newConstant(@base.getSize(), npow), 1);
                data.opSetOpcode(rootOp, CPUI_INT_SREM);
                return 1;
            }
            return 0;
        }
    }
}
