using Sla.CORE;
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
    /// \brief Simply a double precision operation, starting from a marked double precision input.
    ///
    /// This rule starts by trying to find a pair of Varnodes that are SUBPIECE from a whole,
    /// are marked as double precision, and that are then used in some double precision operation.
    /// The various operation \e forms are overlayed on the data-flow until a matching one is found.  The
    /// pieces of the double precision operation are then transformed into a single logical operation on the whole.
    internal class RuleDoubleIn : Rule
    {
        /// \brief Determine if the given Varnode from a SUBPIECE should be marked as a double precision piece
        ///
        /// If the given Varnode looks like the most significant piece, there is another SUBPIECE that looks
        /// like the least significant piece, and the whole is from an operation that produces a logical whole,
        /// then mark the Varnode (and its companion) as double precision pieces and return 1.
        /// \param data is the function owning the Varnode
        /// \param vn is the given Varnode
        /// \param subpieceOp is the SUBPIECE PcodeOp producing the Varnode
        private int attemptMarking(Funcdata data, Varnode vn, PcodeOp subpieceOp)
        {
            Varnode whole = subpieceOp.getIn(0);
            int offset = (int)subpieceOp.getIn(1).getOffset();
            if (offset != vn.getSize()) return 0;
            if (offset * 2 != whole.getSize()) return 0;       // Truncate exactly half
            if (whole.isInput())
            {
                if (!whole.isTypeLock()) return 0;
            }
            else if (!whole.isWritten())
            {
                return 0;
            }
            else
            {
                // Categorize opcodes as "producing a logical whole"
                switch (whole.getDef().code()) {
                    case OpCode.CPUI_INT_ADD:
                    // Its hard to tell if the bit operators are really being used to act on the "logical whole"
                    //      case OpCode.CPUI_INT_AND:
                    //      case OpCode.CPUI_INT_OR:
                    //      case OpCode.CPUI_INT_XOR:
                    //      case OpCode.CPUI_INT_NEGATE:
                    case OpCode.CPUI_INT_MULT:
                    case OpCode.CPUI_INT_DIV:
                    case OpCode.CPUI_INT_SDIV:
                    case OpCode.CPUI_INT_REM:
                    case OpCode.CPUI_INT_SREM:
                    case OpCode.CPUI_INT_2COMP:
                    case OpCode.CPUI_FLOAT_ADD:
                    case OpCode.CPUI_FLOAT_DIV:
                    case OpCode.CPUI_FLOAT_MULT:
                    case OpCode.CPUI_FLOAT_SUB:
                    case OpCode.CPUI_FLOAT_NEG:
                    case OpCode.CPUI_FLOAT_ABS:
                    case OpCode.CPUI_FLOAT_SQRT:
                    case OpCode.CPUI_FLOAT_INT2FLOAT:
                    case OpCode.CPUI_FLOAT_FLOAT2FLOAT:
                    case OpCode.CPUI_FLOAT_TRUNC:
                    case OpCode.CPUI_FLOAT_CEIL:
                    case OpCode.CPUI_FLOAT_FLOOR:
                    case OpCode.CPUI_FLOAT_ROUND:
                        break;
                    default:
                        return 0;
                }
            }
            Varnode? vnLo = (Varnode)null;
            IEnumerator<PcodeOp> iter = whole.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.code() != OpCode.CPUI_SUBPIECE) continue;
                if (op.getIn(1).getOffset() != 0) continue;
                if (op.getOut().getSize() == vn.getSize()) {
                    vnLo = op.getOut();
                    break;
                }
            }
            if (vnLo == (Varnode)null) return 0;
            vnLo.setPrecisLo();
            vn.setPrecisHi();
            return 1;
        }

        public RuleDoubleIn(string g)
            : base(g, 0, "doublein")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new RuleDoubleIn(getGroup());
        }

        public override void reset(Funcdata data)
        {
            data.setDoublePrecisRecovery(true); // Mark that we are doing double precision recovery
        }

        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            // Try to push double precision object "down" one level from input
            Varnode outvn = op.getOut();
            if (!outvn.isPrecisLo()) {
                if (outvn.isPrecisHi()) return 0;
                return attemptMarking(data, outvn, op);
            }
            if (data.hasUnreachableBlocks()) return 0;

            List<SplitVarnode> splitvec = new List<SplitVarnode>();
            SplitVarnode.wholeList(op.getIn(0), splitvec);
            if (splitvec.empty()) return 0;
            for (int i = 0; i < splitvec.size(); ++i) {
                SplitVarnode @in = splitvec[i];
                int res = SplitVarnode.applyRuleIn(@in, data);
                if (res != 0)
                    return res;
            }
            return 0;
        }
    }
}
