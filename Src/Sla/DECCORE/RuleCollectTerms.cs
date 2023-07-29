﻿using System;
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
    internal class RuleCollectTerms: Rule
    {
        /// Get the multiplicative coefficient
        /// Given a Varnode term in the expression, check if the last operation producing it
        /// is to multiply by a constant.  If so pass back the constant coefficient and
        /// return the underlying Varnode. Otherwise pass back the constant 1, and return
        /// the original Varnode
        /// \param vn is the given Varnode
        /// \param coef is the reference for passing back the coefficient
        /// \return the underlying Varnode of the term
        private static Varnode getMultCoeff(Varnode vn, uintb coef)
        {
            PcodeOp* testop;
            if (!vn->isWritten())
            {
                coef = 1;
                return vn;
            }
            testop = vn->getDef();
            if ((testop->code() != CPUI_INT_MULT) || (!testop->getIn(1)->isConstant()))
            {
                coef = 1;
                return vn;
            }
            coef = testop->getIn(1)->getOffset();
            return testop->getIn(0);
        }

        public RuleCollectTerms(string g)
            : base(g, 0, "collect_terms")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleCollectTerms(getGroup());
        }

        /// \class RuleCollectTerms
        /// \brief Collect terms in a sum: `V * c + V * d   =>  V * (c + d)`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_ADD);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* nextop = op->getOut()->loneDescend();
            // Do we have the root of an ADD tree
            if ((nextop != (PcodeOp*)0) && (nextop->code() == CPUI_INT_ADD)) return 0;

            TermOrder termorder(op);
            termorder.collect();        // Collect additive terms in the expression
            termorder.sortTerms();  // Sort them based on termorder
            Varnode* vn1,*vn2;
            uintb coef1, coef2;
            const vector<AdditiveEdge*> &order(termorder.getSort());
            int4 i = 0;

            if (!order[0]->getVarnode()->isConstant())
            {
                for (i = 1; i < order.size(); ++i)
                {
                    vn1 = order[i - 1]->getVarnode();
                    vn2 = order[i]->getVarnode();
                    if (vn2->isConstant()) break;
                    vn1 = getMultCoeff(vn1, coef1);
                    vn2 = getMultCoeff(vn2, coef2);
                    if (vn1 == vn2)
                    {       // Terms that can be combined
                        if (order[i - 1]->getMultiplier() != (PcodeOp*)0)
                            return data.distributeIntMultAdd(order[i - 1]->getMultiplier()) ? 1 : 0;
                        if (order[i]->getMultiplier() != (PcodeOp*)0)
                            return data.distributeIntMultAdd(order[i]->getMultiplier()) ? 1 : 0;
                        coef1 = (coef1 + coef2) & calc_mask(vn1->getSize()); // The new coefficient
                        Varnode* newcoeff = data.newConstant(vn1->getSize(), coef1);
                        Varnode* zerocoeff = data.newConstant(vn1->getSize(), 0);
                        data.opSetInput(order[i - 1]->getOp(), zerocoeff, order[i - 1]->getSlot());
                        if (coef1 == 0)
                            data.opSetInput(order[i]->getOp(), newcoeff, order[i]->getSlot());
                        else
                        {
                            nextop = data.newOp(2, order[i]->getOp()->getAddr());
                            vn2 = data.newUniqueOut(vn1->getSize(), nextop);
                            data.opSetOpcode(nextop, CPUI_INT_MULT);
                            data.opSetInput(nextop, vn1, 0);
                            data.opSetInput(nextop, newcoeff, 1);
                            data.opInsertBefore(nextop, order[i]->getOp());
                            data.opSetInput(order[i]->getOp(), vn2, order[i]->getSlot());
                        }
                        return 1;
                    }
                }
            }
            coef1 = 0;
            int4 nonzerocount = 0;      // Count non-zero constants
            int4 lastconst = 0;
            for (int4 j = order.size() - 1; j >= i; --j)
            {
                if (order[j]->getMultiplier() != (PcodeOp*)0) continue;
                vn1 = order[j]->getVarnode();
                uintb val = vn1->getOffset();
                if (val != 0)
                {
                    nonzerocount += 1;
                    coef1 += val; // Sum up all the constants
                    lastconst = j;
                }
            }
            if (nonzerocount <= 1) return 0; // Must sum at least two things
            vn1 = order[lastconst]->getVarnode();
            coef1 &= calc_mask(vn1->getSize());
            // Lump all the non-zero constants into one varnode
            for (int4 j = lastconst + 1; j < order.size(); ++j)
                if (order[j]->getMultiplier() == (PcodeOp*)0)
                    data.opSetInput(order[j]->getOp(), data.newConstant(vn1->getSize(), 0), order[j]->getSlot());
            data.opSetInput(order[lastconst]->getOp(), data.newConstant(vn1->getSize(), coef1), order[lastconst]->getSlot());

            return 1;
        }
    }
}