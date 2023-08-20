using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSubRight : Rule
    {
        public RuleSubRight(string g)
            : base(g, 0, "subright")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubRight(getGroup());
        }

        /// \class RuleSubRight
        /// \brief Cleanup: Convert truncation to cast: `sub(V,c)  =>  sub(V>>c*8,0)`
        ///
        /// Before attempting the transform, check if the SUBPIECE is really extracting a field
        /// from a structure. If so, mark the op as requiring special printing and return.
        /// If the lone descendant of the SUBPIECE is a INT_RIGHT or INT_SRIGHT,
        /// we lump that into the shift as well.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (op.doesSpecialPrinting())
                return 0;
            if (op.getIn(0).getTypeReadFacing(op).isPieceStructured())
            {
                data.opMarkSpecialPrint(op);    // Print this as a field extraction
                return 0;
            }

            int c = op.getIn(1).getOffset();
            if (c == 0) return 0;       // SUBPIECE is not least sig
            Varnode a = op.getIn(0);
            Varnode outvn = op.getOut();
            if (outvn.isAddrTied() && a.isAddrTied())
            {
                if (outvn.overlap(*a) == c) // This SUBPIECE should get converted to a marker by ActionCopyMarker
                    return 0;           // So don't convert it
            }
            OpCode opc = OpCode.CPUI_INT_RIGHT; // Default shift type
            int d = c * 8;         // Convert to bit shift
                                    // Search for lone right shift descendant
            PcodeOp lone = outvn.loneDescend();
            if (lone != (PcodeOp)null)
            {
                OpCode opc2 = lone.code();
                if ((opc2 == OpCode.CPUI_INT_RIGHT) || (opc2 == OpCode.CPUI_INT_SRIGHT))
                {
                    if (lone.getIn(1).isConstant())
                    { // Shift by constant
                        if (outvn.getSize() + c == a.getSize())
                        {
                            // If SUB is "hi" lump the SUB and shift together
                            d += lone.getIn(1).getOffset();
                            if (d >= a.getSize() * 8)
                            {
                                if (opc2 == OpCode.CPUI_INT_RIGHT)
                                    return 0;       // Result should have been 0
                                d = a.getSize() * 8 - 1;   // sign extraction
                            }
                            data.opUnlink(op);
                            op = lone;
                            data.opSetOpcode(op, OpCode.CPUI_SUBPIECE);
                            opc = opc2;
                        }
                    }
                }
            }
            // Create shift BEFORE the SUBPIECE happens
            Datatype* ct;
            if (opc == OpCode.CPUI_INT_RIGHT)
                ct = data.getArch().types.getBase(a.getSize(), type_metatype.TYPE_UINT);
            else
                ct = data.getArch().types.getBase(a.getSize(), type_metatype.TYPE_INT);
            PcodeOp shiftop = data.newOp(2, op.getAddr());
            data.opSetOpcode(shiftop, opc);
            Varnode newout = data.newUnique(a.getSize(), ct);
            data.opSetOutput(shiftop, newout);
            data.opSetInput(shiftop, a, 0);
            data.opSetInput(shiftop, data.newConstant(4, d), 1);
            data.opInsertBefore(shiftop, op);

            // Change SUBPIECE into a least sig SUBPIECE
            data.opSetInput(op, newout, 0);
            data.opSetInput(op, data.newConstant(4, 0), 1);
            return 1;
        }
    }
}
