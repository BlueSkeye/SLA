using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Check for one CPUI_MULTIEQUAL input set defining more than one Varnode
    internal class ActionShadowVar
    {
        /// Constructor
        public ActionShadowVar(string g)
            : base(0,"shadowvar", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionShadowVar(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            BlockGraph bblocks = data.getBasicBlocks();
            BlockBasic* bl;
            PcodeOp* op;
            Varnode* vn;
            vector<Varnode*> vnlist;
            list<PcodeOp*> oplist;
            uintb startoffset;
            for (int4 i = 0; i < bblocks.getSize(); ++i)
            {
                vnlist.clear();
                bl = (BlockBasic*)bblocks.getBlock(i);
                // Iterator over all MULTIEQUALs in the block
                // We have to check all ops in the first address
                // We cannot stop at first non-MULTIEQUAL because
                // other ops creep in because of multi_collapse
                startoffset = bl->getStart().getOffset();
                list<PcodeOp*>::iterator iter = bl->beginOp();
                while (iter != bl->endOp())
                {
                    op = *iter++;
                    if (op->getAddr().getOffset() != startoffset) break;
                    if (op->code() != CPUI_MULTIEQUAL) continue;
                    vn = op->getIn(0);
                    if (vn->isMark())
                        oplist.push_back(op);
                    else
                    {
                        vn->setMark();
                        vnlist.push_back(vn);
                    }
                }
                for (int4 j = 0; j < vnlist.size(); ++j)
                    vnlist[j]->clearMark();
            }
            list<PcodeOp*>::iterator oiter;
            for (oiter = oplist.begin(); oiter != oplist.end(); ++oiter)
            {
                op = *oiter;
                PcodeOp* op2;
                for (op2 = op->previousOp(); op2 != (PcodeOp*)0; op2 = op2->previousOp())
                {
                    if (op2->code() != CPUI_MULTIEQUAL) continue;
                    int4 i;
                    for (i = 0; i < op->numInput(); ++i) // Check for match in each branch
                        if (op->getIn(i) != op2->getIn(i)) break;
                    if (i != op->numInput()) continue; // All branches did not match

                    vector<Varnode*> plist;
                    plist.push_back(op2->getOut());
                    data.opSetOpcode(op, CPUI_COPY);
                    data.opSetAllInput(op, plist);
                    count += 1;
                }
            }

            return 0;
        }
    }
}
