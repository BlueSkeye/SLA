﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Create symbols that map out the local stack-frame for the function.
    ///
    /// This produces on intermediate view of symbols on the stack.
    internal class ActionRestructureVarnode : Action
    {
        /// Number of passes performed for this function
        private int numpass;

        /// Protect path to the given switch from INDIRECT collapse
        /// Test if the path to the given BRANCHIND originates from a constant but passes through INDIRECT operations.
        /// This indicates that the switch value is produced indirectly, so we mark these INDIRECT
        /// operations as \e not \e collapsible, to guarantee that the indirect value is not lost during analysis.
        /// \param op is the given BRANCHIND op
        private static void protectSwitchPathIndirects(PcodeOp op)
        {
            vector<PcodeOp*> indirects;
            Varnode* curVn = op->getIn(0);
            while (curVn->isWritten())
            {
                PcodeOp* curOp = curVn->getDef();
                uint4 evalType = curOp->getEvalType();
                if ((evalType & (PcodeOp::binary | PcodeOp::ternary)) != 0)
                {
                    if (curOp->numInput() > 1)
                    {
                        if (!curOp->getIn(1)->isConstant()) return; // Multiple paths
                    }
                    curVn = curOp->getIn(0);
                }
                else if ((evalType & PcodeOp::unary) != 0)
                    curVn = curOp->getIn(0);
                else if (curOp->code() == CPUI_INDIRECT)
                {
                    indirects.push_back(curOp);
                    curVn = curOp->getIn(0);
                }
                else if (curOp->code() == CPUI_LOAD)
                {
                    curVn = curOp->getIn(1);
                }
                else
                    return;
            }
            if (!curVn->isConstant()) return;
            // If we reach here, there is exactly one path, from a constant to a switch
            for (int4 i = 0; i < indirects.size(); ++i)
            {
                indirects[i]->setNoIndirectCollapse();
            }
        }

        /// Look for switches and protect path of switch variable
        /// Run through BRANCHIND ops, treat them as switches and protect the data-flow path to the destination variable
        /// \param data is the function to examine
        private static void protectSwitchPaths(Funcdata data)
        {
            const BlockGraph &bblocks(data.getBasicBlocks());
            for (int4 i = 0; i < bblocks.getSize(); ++i)
            {
                PcodeOp* op = bblocks.getBlock(i)->lastOp();
                if (op == (PcodeOp*)0) continue;
                if (op->code() != CPUI_BRANCHIND) continue;
                protectSwitchPathIndirects(op);
            }
        }

        public ActionRestructureVarnode(string g)
            : base(0,"restructure_varnode", g)
        {
        }
        
        public override void reset(Funcdata data)
        {
            numpass = 0;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionRestructureVarnode(getGroup());
        }

        public override int apply(Funcdata data)
        {
            ScopeLocal* l1 = data.getScopeLocal();

            bool aliasyes = (numpass != 0); // Alias calculations are not reliable on the first pass
            l1->restructureVarnode(aliasyes);
            if (data.syncVarnodesWithSymbols(l1, false, aliasyes))
                count += 1;

            if (data.isJumptableRecoveryOn())
                protectSwitchPaths(data);

            numpass += 1;
#if OPACTION_DEBUG
            if ((flags & rule_debug) == 0) return 0;
            ostringstream s;
            data.getScopeLocal()->printEntries(s);
            data.getArch()->printDebug(s.str());
#endif
            return 0;
        }
    }
}