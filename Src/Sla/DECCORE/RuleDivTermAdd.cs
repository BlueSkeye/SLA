using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleDivTermAdd : Rule
    {
        public RuleDivTermAdd(string g)
            : base(g, 0, "divtermadd")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleDivTermAdd(getGroup());
        }

        /// \class RuleDivTermAdd
        /// \brief Simplify expressions associated with optimized division expressions
        ///
        /// The form looks like:
        ///   - `sub(ext(V)*c,b)>>d + V  .  sub( (ext(V)*(c+2^n))>>n,0)`
        ///
        /// where n = d + b*8, and the left-shift signedness (if it exists)
        /// matches the extension signedness.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_SUBPIECE);
            oplist.Add(CPUI_INT_RIGHT); // added
            oplist.Add(CPUI_INT_SRIGHT); // added
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int n;
            OpCode shiftopc;
            PcodeOp* subop = findSubshift(op, n, shiftopc);
            if (subop == (PcodeOp)null) return 0;
            // TODO: Cannot currently support 128-bit arithmetic, except in special case of 2^64
            if (n > 64) return 0;

            Varnode* multvn = subop.getIn(0);
            if (!multvn.isWritten()) return 0;
            PcodeOp* multop = multvn.getDef();
            if (multop.code() != OpCode.CPUI_INT_MULT) return 0;
            ulong multConst;
            int constExtType = multop.getIn(1).isConstantExtended(multConst);
            if (constExtType < 0) return 0;

            Varnode* extvn = multop.getIn(0);
            if (!extvn.isWritten()) return 0;
            PcodeOp* extop = extvn.getDef();
            OpCode opc = extop.code();
            if (opc == OpCode.CPUI_INT_ZEXT)
            {
                if (op.code() == OpCode.CPUI_INT_SRIGHT) return 0;
            }
            else if (opc == OpCode.CPUI_INT_SEXT)
            {
                if (op.code() == OpCode.CPUI_INT_RIGHT) return 0;
            }

            ulong newc;
            if (n < 64 || (extvn.getSize() <= 8))
            {
                ulong pow = 1;
                pow <<= n;          // Calculate 2^n
                newc = multConst + pow;
            }
            else
            {
                if (constExtType != 2) return 0; // TODO: Can't currently represent
                if (!signbit_negative(multConst, 8)) return 0;
                // Adding 2^64 to a sign-extended 64-bit value with its sign set, causes all the
                // set extension bits to be cancelled out, converting it into a
                // zero-extended 64-bit value.
                constExtType = 1;       // Set extension of constant to INT_ZEXT
            }
            Varnode* x = extop.getIn(0);

            list<PcodeOp*>::const_iterator iter;
            for (iter = op.getOut().beginDescend(); iter != op.getOut().endDescend(); ++iter)
            {
                PcodeOp* addop = *iter;
                if (addop.code() != OpCode.CPUI_INT_ADD) continue;
                if ((addop.getIn(0) != x) && (addop.getIn(1) != x))
                    continue;

                // Construct the new constant
                Varnode* newConstVn;
                if (constExtType == 0)
                    newConstVn = data.newConstant(extvn.getSize(), newc);
                else
                {
                    // Create new extension of the constant
                    PcodeOp* newExtOp = data.newOp(1, op.getAddr());
                    data.opSetOpcode(newExtOp, (constExtType == 1) ? OpCode.CPUI_INT_ZEXT : OpCode.CPUI_INT_SEXT);
                    newConstVn = data.newUniqueOut(extvn.getSize(), newExtOp);
                    data.opSetInput(newExtOp, data.newConstant(8, multConst), 0);
                    data.opInsertBefore(newExtOp, op);
                }

                // Construct the new multiply
                PcodeOp* newmultop = data.newOp(2, op.getAddr());
                data.opSetOpcode(newmultop, OpCode.CPUI_INT_MULT);
                Varnode* newmultvn = data.newUniqueOut(extvn.getSize(), newmultop);
                data.opSetInput(newmultop, extvn, 0);
                data.opSetInput(newmultop, newConstVn, 1);
                data.opInsertBefore(newmultop, op);

                PcodeOp* newshiftop = data.newOp(2, op.getAddr());
                if (shiftopc == OpCode.CPUI_MAX)
                    shiftopc = OpCode.CPUI_INT_RIGHT;
                data.opSetOpcode(newshiftop, shiftopc);
                Varnode* newshiftvn = data.newUniqueOut(extvn.getSize(), newshiftop);
                data.opSetInput(newshiftop, newmultvn, 0);
                data.opSetInput(newshiftop, data.newConstant(4, n), 1);
                data.opInsertBefore(newshiftop, op);

                data.opSetOpcode(addop, OpCode.CPUI_SUBPIECE);
                data.opSetInput(addop, newshiftvn, 0);
                data.opSetInput(addop, data.newConstant(4, 0), 1);
                return 1;
            }
            return 0;
        }

        /// \brief Check for shift form of expression
        ///
        /// Look for the two forms:
        ///  - `sub(V,c)`   or
        ///  - `sub(V,c) >> n`
        ///
        /// Pass back whether a shift was involved and the total truncation in bits:  `n+c*8`
        /// \param op is the root of the expression
        /// \param n is the reference that will hold the total truncation
        /// \param shiftopc will hold the shift OpCode if used, OpCode.CPUI_MAX otherwise
        /// \return the SUBPIECE op if present or NULL otherwise
        public static PcodeOp findSubshift(PcodeOp op, int n, OpCode shiftopc)
        { // SUB( .,#c) or SUB(.,#c)>>n  return baseop and n+c*8
          // make SUB is high
            PcodeOp* subop;
            shiftopc = op.code();
            if (shiftopc != OpCode.CPUI_SUBPIECE)
            { // Must be right shift
                Varnode* vn = op.getIn(0);
                if (!vn.isWritten()) return (PcodeOp)null;
                subop = vn.getDef();
                if (subop.code() != OpCode.CPUI_SUBPIECE) return (PcodeOp)null;
                if (!op.getIn(1).isConstant()) return (PcodeOp)null;
                n = op.getIn(1).getOffset();
            }
            else
            {
                shiftopc = OpCode.CPUI_MAX;    // Indicate there was no shift
                subop = op;
                n = 0;
            }
            int c = subop.getIn(1).getOffset();
            if (subop.getOut().getSize() + c != subop.getIn(0).getSize())
                return (PcodeOp)null; // SUB is not high
            n += 8 * c;

            return subop;
        }
    }
}
