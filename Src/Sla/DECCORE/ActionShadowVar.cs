using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Check for one OpCode.CPUI_MULTIEQUAL input set defining more than one Varnode
    internal class ActionShadowVar : Action
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
            BlockBasic bl;
            PcodeOp op;
            Varnode vn;
            List<Varnode> vnlist = new List<Varnode>();
            List<PcodeOp> oplist = new List<PcodeOp>();
            ulong startoffset;
            for (int i = 0; i < bblocks.getSize(); ++i) {
                vnlist.Clear();
                bl = (BlockBasic)bblocks.getBlock(i);
                // Iterator over all MULTIEQUALs in the block
                // We have to check all ops in the first address
                // We cannot stop at first non-MULTIEQUAL because
                // other ops creep in because of multi_collapse
                startoffset = bl.getStart().getOffset();
                LinkedListNode<PcodeOp>? iter = bl.beginOp();
                while (iter != null) {
                    op = iter.Value;
                    iter = iter.Next;
                    if (op.getAddr().getOffset() != startoffset) break;
                    if (op.code() != OpCode.CPUI_MULTIEQUAL) continue;
                    vn = op.getIn(0);
                    if (vn.isMark()) {
                        oplist.Add(op);
                    }
                    else {
                        vn.setMark();
                        vnlist.Add(vn);
                    }
                }
                for (int j = 0; j < vnlist.size(); ++j)
                    vnlist[j].clearMark();
            }
            IEnumerator<PcodeOp> oiter = oplist.GetEnumerator();
            while (oiter.MoveNext()) {
                op = oiter.Current;
                PcodeOp op2;
                for (op2 = op.previousOp(); op2 != (PcodeOp)null; op2 = op2.previousOp()) {
                    if (op2.code() != OpCode.CPUI_MULTIEQUAL)
                        continue;
                    int i;
                    for (i = 0; i < op.numInput(); ++i)
                        // Check for match in each branch
                        if (op.getIn(i) != op2.getIn(i))
                            break;
                    if (i != op.numInput())
                        // All branches did not match
                        continue;

                    List<Varnode> plist = new List<Varnode>();
                    plist.Add(op2.getOut());
                    data.opSetOpcode(op, OpCode.CPUI_COPY);
                    data.opSetAllInput(op, plist);
                    count += 1;
                }
            }
            return 0;
        }
    }
}
