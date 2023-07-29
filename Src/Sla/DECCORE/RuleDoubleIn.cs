using System;
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
            Varnode* whole = subpieceOp.getIn(0);
            int4 offset = (int4)subpieceOp.getIn(1).getOffset();
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
                switch (whole.getDef().code())
                {
                    case CPUI_INT_ADD:
                    // Its hard to tell if the bit operators are really being used to act on the "logical whole"
                    //      case CPUI_INT_AND:
                    //      case CPUI_INT_OR:
                    //      case CPUI_INT_XOR:
                    //      case CPUI_INT_NEGATE:
                    case CPUI_INT_MULT:
                    case CPUI_INT_DIV:
                    case CPUI_INT_SDIV:
                    case CPUI_INT_REM:
                    case CPUI_INT_SREM:
                    case CPUI_INT_2COMP:
                    case CPUI_FLOAT_ADD:
                    case CPUI_FLOAT_DIV:
                    case CPUI_FLOAT_MULT:
                    case CPUI_FLOAT_SUB:
                    case CPUI_FLOAT_NEG:
                    case CPUI_FLOAT_ABS:
                    case CPUI_FLOAT_SQRT:
                    case CPUI_FLOAT_INT2FLOAT:
                    case CPUI_FLOAT_FLOAT2FLOAT:
                    case CPUI_FLOAT_TRUNC:
                    case CPUI_FLOAT_CEIL:
                    case CPUI_FLOAT_FLOOR:
                    case CPUI_FLOAT_ROUND:
                        break;
                    default:
                        return 0;
                }
            }
            Varnode* vnLo = (Varnode*)0;
            list<PcodeOp*>::const_iterator iter;
            for (iter = whole.beginDescend(); iter != whole.endDescend(); ++iter)
            {
                PcodeOp* op = *iter;
                if (op.code() != CPUI_SUBPIECE) continue;
                if (op.getIn(1).getOffset() != 0) continue;
                if (op.getOut().getSize() == vn.getSize())
                {
                    vnLo = op.getOut();
                    break;
                }
            }
            if (vnLo == (Varnode*)0) return 0;
            vnLo.setPrecisLo();
            vn.setPrecisHi();
            return 1;
        }

        public RuleDoubleIn(string g)
            : base(g, 0, "doublein")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new RuleDoubleIn(getGroup());
        }

        public override void reset(Funcdata data)
        {
            data.setDoublePrecisRecovery(true); // Mark that we are doing double precision recovery
        }

        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        { // Try to push double precision object "down" one level from input
            Varnode* outvn = op.getOut();
            if (!outvn.isPrecisLo())
            {
                if (outvn.isPrecisHi()) return 0;
                return attemptMarking(data, outvn, op);
            }
            if (data.hasUnreachableBlocks()) return 0;

            vector<SplitVarnode> splitvec;
            SplitVarnode::wholeList(op.getIn(0), splitvec);
            if (splitvec.empty()) return 0;
            for (int4 i = 0; i < splitvec.size(); ++i)
            {
                SplitVarnode @in(splitvec[i]);
                int4 res = SplitVarnode::applyRuleIn(@in, data);
                if (res != 0)
                    return res;
            }
            return 0;
        }
    }
}
