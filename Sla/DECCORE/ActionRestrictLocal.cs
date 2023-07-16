using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Restrict possible range of local variables
    ///
    /// Mark what we know of parameters and unaffected stores
    /// so that they cannot be treated as local variables.
    internal class ActionRestrictLocal : Action
    {
        public ActionRestrictLocal(string g)
            : base(0,"restrictlocal", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionRestrictLocal(getGroup());
        }

        public override int4 apply(Funcdata data)
        {
            FuncCallSpecs* fc;
            list<PcodeOp*>::const_iterator iter;
            PcodeOp* op;
            Varnode* vn;
            int4 i;
            vector<EffectRecord>::const_iterator eiter, endeiter;

            for (i = 0; i < data.numCalls(); ++i)
            {
                fc = data.getCallSpecs(i);
                op = fc->getOp();

                if (!fc->isInputLocked()) continue;
                if (fc->getSpacebaseOffset() == FuncCallSpecs::offset_unknown) continue;
                int4 numparam = fc->numParams();
                for (int4 j = 0; j < numparam; ++j)
                {
                    ProtoParameter* param = fc->getParam(j);
                    Address addr = param->getAddress();
                    if (addr.getSpace()->getType() != IPTR_SPACEBASE) continue;
                    uintb off = addr.getSpace()->wrapOffset(fc->getSpacebaseOffset() + addr.getOffset());
                    data.getScopeLocal()->markNotMapped(addr.getSpace(), off, param->getSize(), true);
                }
            }

            eiter = data.getFuncProto().effectBegin();
            endeiter = data.getFuncProto().effectEnd();
            for (; eiter != endeiter; ++eiter)
            { // Iterate through saved registers
                if ((*eiter).getType() == EffectRecord::killedbycall) continue;  // Not saved
                vn = data.findVarnodeInput((*eiter).getSize(), (*eiter).getAddress());
                if ((vn != (Varnode*)0) && (vn->isUnaffected()))
                {
                    // Mark storage locations for saved registers as not mapped
                    // This should pickup unaffected, reload, and return_address effecttypes
                    for (iter = vn->beginDescend(); iter != vn->endDescend(); ++iter)
                    {
                        op = *iter;
                        if (op->code() != CPUI_COPY) continue;
                        Varnode* outvn = op->getOut();
                        if (!data.getScopeLocal()->isUnaffectedStorage(outvn))  // Is this where unaffected values get saved
                            continue;
                        data.getScopeLocal()->markNotMapped(outvn->getSpace(), outvn->getOffset(), outvn->getSize(), false);
                    }
                }
            }
            return 0;
        }
    }
}
