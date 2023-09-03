using Sla.CORE;

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
            if (op.getIn(0).getTypeReadFacing(op).isPieceStructured()) {
                // Print this as a field extraction
                data.opMarkSpecialPrint(op);
                return 0;
            }

            int c = (int)op.getIn(1).getOffset();
            if (c == 0)
                // SUBPIECE is not least sig
                return 0;
            Varnode a = op.getIn(0) ?? throw new ApplicationException();
            Varnode outvn = op.getOut() ?? throw new ApplicationException();
            if (outvn.isAddrTied() && a.isAddrTied()) {
                if (outvn.overlap(a) == c)
                    // This SUBPIECE should get converted to a marker by ActionCopyMarker
                    // So don't convert it
                    return 0;
            }
            // Default shift type
            OpCode opc = OpCode.CPUI_INT_RIGHT;
            // Convert to bit shift
            int d = c * 8;
            // Search for lone right shift descendant
            PcodeOp? lone = outvn.loneDescend();
            if (lone != (PcodeOp)null) {
                OpCode opc2 = lone.code();
                if ((opc2 == OpCode.CPUI_INT_RIGHT) || (opc2 == OpCode.CPUI_INT_SRIGHT)) {
                    if (lone.getIn(1).isConstant()) {
                        // Shift by constant
                        if (outvn.getSize() + c == a.getSize()) {
                            // If SUB is "hi" lump the SUB and shift together
                            d += (int)lone.getIn(1).getOffset();
                            if (d >= a.getSize() * 8) {
                                if (opc2 == OpCode.CPUI_INT_RIGHT)
                                    // Result should have been 0
                                    return 0;
                                // sign extraction
                                d = a.getSize() * 8 - 1;
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
            Datatype ct = data.getArch().types.getBase(a.getSize(),
                (opc == OpCode.CPUI_INT_RIGHT) ? type_metatype.TYPE_UINT : type_metatype.TYPE_INT);
            PcodeOp shiftop = data.newOp(2, op.getAddr());
            data.opSetOpcode(shiftop, opc);
            Varnode newout = data.newUnique(a.getSize(), ct);
            data.opSetOutput(shiftop, newout);
            data.opSetInput(shiftop, a, 0);
            data.opSetInput(shiftop, data.newConstant(4, (ulong)d), 1);
            data.opInsertBefore(shiftop, op);

            // Change SUBPIECE into a least sig SUBPIECE
            data.opSetInput(op, newout, 0);
            data.opSetInput(op, data.newConstant(4, 0), 1);
            return 1;
        }
    }
}
