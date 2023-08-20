using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RulePushMulti : Rule
    {
        /// \brief Find a previously existing MULTIEQUAL taking given inputs
        ///
        /// The MULTIEQUAL must be in the given block \b bb.
        /// If the MULTIEQUAL does not exist, check if the inputs have
        /// level 1 functional equality and if a common sub-expression is present in the block
        /// \param in1 is the first input
        /// \param in2 is the second input
        /// \param bb is the given block to search in
        /// \param earliest is the earliest of the inputs
        /// \return the discovered MULTIEQUAL or the equivalent sub-expression
        private static PcodeOp? findSubstitute(Varnode in1, Varnode in2, BlockBasic bb, PcodeOp earliest)
        {
            IEnumerator<PcodeOp> iter = in1.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.getParent() != bb) continue;
                if (op.code() != OpCode.CPUI_MULTIEQUAL) continue;
                if (op.getIn(0) != in1) continue;
                if (op.getIn(1) != in2) continue;
                return op;
            }
            if (in1 == in2) return (PcodeOp)null;
            Varnode[] buf1 = new Varnode[2];
            Varnode[] buf2 = new Varnode[2];
            if (0 != functionalEqualityLevel(in1, in2, buf1, buf2)) return (PcodeOp)null;
            PcodeOp op1 = in1.getDef();   // in1 and in2 must be written to not be equal and pass functional equality test
            PcodeOp op2 = in2.getDef();
            for (int i = 0; i < op1.numInput(); ++i) {
                Varnode vn = op1.getIn(i);
                if (vn.isConstant()) continue;
                if (vn == op2.getIn(i))    // Find matching inputs to op1 and op2,
                    return cseFindInBlock(op1, vn, bb, earliest); // search for cse of op1 in bb
            }

            return (PcodeOp)null;
        }

        public RulePushMulti(string g)
            : base(g, 0, "push_multi")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RulePushMulti(getGroup());
        }

        /// \class RulePushMulti
        /// \brief Simplify MULTIEQUAL operations where the branches hold the same value
        ///
        /// Look for a two-branch MULTIEQUAL where both inputs are constructed in
        /// functionally equivalent ways.  Remove (the reference to) one construction
        /// and move the other into the merge block.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_MULTIEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (op.numInput() != 2) return 0;

            Varnode in1 = op.getIn(0);
            Varnode in2 = op.getIn(1);

            if (!in1.isWritten()) return 0;
            if (!in2.isWritten()) return 0;
            if (in1.isSpacebase()) return 0;
            if (in2.isSpacebase()) return 0;
            Varnode[] buf1 = new Varnode[2];
            Varnode[] buf2 = new Varnode[2];
            int res = functionalEqualityLevel(in1, in2, buf1, buf2);
            if (res < 0) return 0;
            if (res > 1) return 0;
            PcodeOp op1 = in1.getDef() ?? throw new BugException();
            if (op1.code() == OpCode.CPUI_SUBPIECE) return 0; // SUBPIECE is pulled not pushed

            BlockBasic bl = op.getParent();
            PcodeOp earliest = earliestUseInBlock(op.getOut(), bl);
            if (op1.code() == OpCode.CPUI_COPY) {
                // Special case of MERGE of 2 shadowing varnodes
                if (res == 0) return 0;
                PcodeOp substitute = findSubstitute(buf1[0], buf2[0], bl, earliest);
                if (substitute == (PcodeOp)null) return 0;
                // Eliminate this op in favor of the shadowed merge
                data.totalReplace(op.getOut(), substitute.getOut());
                data.opDestroy(op);
                return 1;
            }
            PcodeOp op2 = in2.getDef() ?? throw new BugException();
            if (in1.loneDescend() != op) return 0;
            if (in2.loneDescend() != op) return 0;

            Varnode outvn = op.getOut();

            data.opSetOutput(op1, outvn);   // Move MULTIEQUAL output to op1, which will be new unified op
            data.opUninsert(op1);       // Move the unified op
            if (res == 1) {
                int slot1 = op1.getSlot(buf1[0]);
                PcodeOp? substitute = findSubstitute(buf1[0], buf2[0], bl, earliest);
                if (substitute == (PcodeOp)null) {
                    substitute = data.newOp(2, op.getAddr());
                    data.opSetOpcode(substitute, OpCode.CPUI_MULTIEQUAL);
                    // Try to preserve the storage location if the input varnodes share it
                    // But don't propagate addrtied varnode (thru MULTIEQUAL)
                    if ((buf1[0].getAddr() == buf2[0].getAddr()) && (!buf1[0].isAddrTied()))
                        data.newVarnodeOut(buf1[0].getSize(), buf1[0].getAddr(), substitute);
                    else
                        data.newUniqueOut(buf1[0].getSize(), substitute);
                    data.opSetInput(substitute, buf1[0], 0);
                    data.opSetInput(substitute, buf2[0], 1);
                    data.opInsertBegin(substitute, bl);
                }
                data.opSetInput(op1, substitute.getOut(), slot1); // Replace input to the unified op with the unified varnode
                data.opInsertAfter(op1, substitute);    // Complete move of unified op into merge block
            }
            else
                data.opInsertBegin(op1, bl);    // Complete move of unified op into merge block
            data.opDestroy(op);     // Destroy the MULTIEQUAL
            data.opDestroy(op2);        // Remove the duplicate (in favor of the unified)
            return 1;
        }
    }
}
