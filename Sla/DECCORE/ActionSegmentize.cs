using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Make sure pointers into segmented spaces have the correct form.
    /// Convert user-defined ops defined as segment p-code ops by a cspec tag into the internal CPUI_SEGMENTOP
    internal class ActionSegmentize : Action
    {
        /// Number of times this Action has been performed on the function
        private int localcount;

        /// Constructor
        public ActionSegmentize(string g)
            : base(0,"segmentize", g)
        {
        }
        
        public override void reset(Funcdata data)
        {
            localcount = 0;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionSegmentize(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            int numops = data.getArch().userops.numSegmentOps();
            if (numops == 0) {
                return 0;
            }
            // Only perform once
            if (localcount > 0) {
                return 0;
            }
            // Mark as having performed once
            localcount = 1;

            List<Varnode> bindlist = new List<Varnode>();
            bindlist.Add(null);
            bindlist.Add(null);

            for (int i = 0; i < numops; ++i) {
                SegmentOp segdef = data.getArch().userops.getSegmentOp(i);
                if (segdef == null) {
                    continue;
                }
                AddrSpace spc = segdef.getSpace();

                IEnumerator<PcodeOp> iter = data.beginOp(CPUI_CALLOTHER);
                IEnumerator<PcodeOp> enditer = data.endOp(CPUI_CALLOTHER);
                int uindex = segdef.getIndex();
                while (iter != enditer) {
                    PcodeOp segroot = *iter++;
                    if (segroot.isDead()) {
                        continue;
                    }
                    if (segroot.getIn(0).getOffset() != uindex) {
                        continue;
                    }
                    if (!segdef.unify(data, segroot, bindlist)) {
                        TextWriter err = new StringWriter();
                        err.Write("Segment op in wrong form at ");
                        segroot.getAddr().printRaw(err);
                        throw new LowlevelError(err.ToString());
                    }

                    if (segdef.getNumVariableTerms() == 1) {
                        bindlist[0] = data.newConstant(4, 0);
                    }
                    // Redefine the op as a segmentop
                    data.opSetOpcode(segroot, CPUI_SEGMENTOP);
                    data.opSetInput(segroot, data.newVarnodeSpace(spc), 0);
                    data.opSetInput(segroot, bindlist[0], 1);
                    data.opSetInput(segroot, bindlist[1], 2);
                    for (int j = segroot.numInput() - 1; j > 2; --j) {
                        // Remove anything else
                        data.opRemoveInput(segroot, j);
                    }
                    count += 1;
                }
            }
            return 0;
        }
    }
}
