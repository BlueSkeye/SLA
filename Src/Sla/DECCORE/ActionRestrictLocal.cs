using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
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

        public override int apply(Funcdata data)
        {
            for (int i = 0; i < data.numCalls(); ++i) {
                FuncCallSpecs fc = data.getCallSpecs(i);
                PcodeOp op = fc.getOp();

                if (!fc.isInputLocked()) continue;
                if (fc.getSpacebaseOffset() == FuncCallSpecs.offset_unknown) continue;
                int numparam = fc.numParams();
                for (int j = 0; j < numparam; ++j) {
                    ProtoParameter param = fc.getParam(j);
                    Address addr = param.getAddress();
                    if (addr.getSpace().getType() != spacetype.IPTR_SPACEBASE) continue;
                    ulong off = addr.getSpace().wrapOffset(fc.getSpacebaseOffset() + addr.getOffset());
                    data.getScopeLocal().markNotMapped(addr.getSpace(), off, param.getSize(), true);
                }
            }

            IEnumerator<EffectRecord> eiter = data.getFuncProto().effectBegin();
            while (eiter.MoveNext()) {
                // Iterate through saved registers
                if (eiter.Current.getType() == EffectRecord.EffectType.killedbycall)
                    // Not saved
                    continue;
                Varnode vn = data.findVarnodeInput(eiter.Current.getSize(), eiter.Current.getAddress());
                if ((vn != (Varnode)null) && (vn.isUnaffected())) {
                    // Mark storage locations for saved registers as not mapped
                    // This should pickup unaffected, reload, and return_address effecttypes
                    IEnumerator<PcodeOp> iter = vn.beginDescend();
                    while (iter.MoveNext()) {
                        PcodeOp op = iter.Current;
                        if (op.code() != OpCode.CPUI_COPY) continue;
                        Varnode outvn = op.getOut();
                        if (!data.getScopeLocal().isUnaffectedStorage(outvn))
                            // Is this where unaffected values get saved
                            continue;
                        data.getScopeLocal().markNotMapped(outvn.getSpace(), outvn.getOffset(),
                            outvn.getSize(), false);
                    }
                }
            }
            return 0;
        }
    }
}
