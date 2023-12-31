﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleDivOpt : Rule
    {
        /// Calculate the divisor
        /// Given the multiplicative encoding \b y and the \b n, the power of 2,
        /// Compute:
        /// \code
        ///       divisor = 2^n / (y-1)
        /// \endcode
        ///
        /// Do some additional checks on the parameters as an optimized encoding
        /// of a divisor.
        /// \param n is the power of 2
        /// \param y is the multiplicative coefficient
        /// \param xsize is the maximum power of 2
        /// \return the divisor or 0 if the checks fail
        private static ulong calcDivisor(ulong n, ulong y, int xsize)
        {
            if (n > 127) return 0;      // Not enough precision
            if (y <= 1) return 0;       // Boundary cases are wrong form

            ulong d, r;
            ulong power;
            if (n < 64) {
                power = 1UL << (int)n;
                d = power / (y - 1);
                r = power % (y - 1);
            }
            else {
                power = 0; // Prevent compilation error.
                if (0 != Globals.power2Divide((int)n, y - 1, out d, out r))
                    return 0;           // Result is bigger than 64-bits
            }
            if (d >= y) return 0;
            if (r >= d) return 0;
            // The optimization of division to multiplication
            // by the reciprocal holds true, if the maximum value
            // of x times the remainder is less than 2^n
            ulong maxx = 1;
            maxx <<= xsize;
            maxx -= 1;          // Maximum possible x value
            ulong tmp;
            if (n < 64)
                tmp = power / (d - r);  // r < d => divisor is non-zero
            else
            {
                ulong unused;
                if (0 != Globals.power2Divide((int)n, d - r, out tmp, out unused))
                    return (ulong)d;        // tmp is bigger than 2^64 > maxx
            }
            if (tmp <= maxx) return 0;
            return (ulong)d;
        }

        /// \brief Replace sign-bit extractions from the first given Varnode with the second Varnode
        /// Look for either:
        ///  - `V >> 0x1f`
        ///  - `V s>> 0x1f`
        ///
        /// Allow for the value to be COPYed around.
        /// \param firstVn is the first given Varnode
        /// \param replaceVn is the Varnode to replace it with in each extraction
        /// \param data is the function holding the Varnodes
        private static void moveSignBitExtraction(Varnode firstVn, Varnode replaceVn, Funcdata data)
        {
            List<Varnode> testList = new List<Varnode>();
            testList.Add(firstVn);
            if (firstVn.isWritten()) {
                PcodeOp op = firstVn.getDef() ?? throw new ApplicationException();
                if (op.code() == OpCode.CPUI_INT_SRIGHT) {
                    // Same sign bit could be extracted from previous shifted version
                    testList.Add(op.getIn(0));
                }
            }
            for (int i = 0; i < testList.size(); ++i) {
                Varnode vn = testList[i];
                IEnumerator<PcodeOp> iter = vn.beginDescend();
                while (iter.MoveNext()) {
                    PcodeOp op = iter.Current;
                    // ++iter;     // Increment before modifying the op
                    OpCode opc = op.code();
                    if (opc == OpCode.CPUI_INT_RIGHT || opc == OpCode.CPUI_INT_SRIGHT) {
                        Varnode constVn = op.getIn(1) ?? throw new ApplicationException();
                        if (constVn.isWritten()) {
                            PcodeOp constOp = constVn.getDef() ?? throw new ApplicationException();
                            if (constOp.code() == OpCode.CPUI_COPY)
                                constVn = constOp.getIn(0);
                            else if (constOp.code() == OpCode.CPUI_INT_AND) {
                                constVn = constOp.getIn(0);
                                Varnode otherVn = constOp.getIn(1);
                                if (!otherVn.isConstant()) continue;
                                if (constVn.getOffset() != (constVn.getOffset() & otherVn.getOffset())) continue;
                            }
                        }
                        if (constVn.isConstant()) {
                            int sa = firstVn.getSize() * 8 - 1;
                            if (sa == (int)constVn.getOffset()) {
                                data.opSetInput(op, replaceVn, 0);
                            }
                        }
                    }
                    else if (opc == OpCode.CPUI_COPY) {
                        testList.Add(op.getOut());
                    }
                }
            }
        }

        /// If form rooted at given PcodeOp is superseded by an overlapping form
        /// A form ending in a SUBPIECE, may be contained in a working form ending at
        /// the SUBPIECE followed by INT_SRIGHT.  The containing form would supersede.
        /// \param op is the root of the form to check
        /// \return \b true if it is (possibly) contained in a superseding form
        private static bool checkFormOverlap(PcodeOp op)
        {
            if (op.code() != OpCode.CPUI_SUBPIECE) return false;
            Varnode vn = op.getOut();
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp superOp = iter.Current;
                OpCode opc = superOp.code();
                if (opc != OpCode.CPUI_INT_RIGHT && opc != OpCode.CPUI_INT_SRIGHT) continue;
                Varnode cvn = superOp.getIn(1);
                if (!cvn.isConstant()) return true;    // Might be a form where constant has propagated yet
                int n, xsize;
                ulong y;
                OpCode extopc;
                Varnode? inVn = findForm(superOp, out n, out y, out xsize, out extopc);
                if (inVn != (Varnode)null) return true;
            }
            return false;
        }

        public RuleDivOpt(string g)
            : base(g, 0, "divopt")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null : new RuleDivOpt(getGroup());
        }

        /// \class RuleDivOpt
        /// \brief Convert INT_MULT and shift forms into INT_DIV or INT_SDIV
        ///
        /// The unsigned and signed variants are:
        ///   - `sub( (zext(V)*c)>>n, 0)   =>  V / (2^n/(c-1))`
        ///   - `sub( (sext(V)*c)s>>n, 0)  =>  V s/ (2^n/(c-1))`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
            oplist.Add(OpCode.CPUI_INT_RIGHT);
            oplist.Add(OpCode.CPUI_INT_SRIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int n, xsize;
            ulong y;
            OpCode extOpc;
            Varnode inVn = findForm(op, out n, out y, out xsize, out extOpc);
            if (inVn == (Varnode)null) return 0;
            if (checkFormOverlap(op)) return 0;
            if (extOpc == OpCode.CPUI_INT_SEXT)
                xsize -= 1;     // one less bit for signed, because of signbit
            ulong divisor = calcDivisor((ulong)n, y, xsize);
            if (divisor == 0) return 0;
            int outSize = op.getOut().getSize();

            if (inVn.getSize() < outSize) {
                // Do we need an extension to get to final size
                PcodeOp inExt = data.newOp(1, op.getAddr());
                data.opSetOpcode(inExt, extOpc);
                Varnode extOut = data.newUniqueOut(outSize, inExt);
                data.opSetInput(inExt, inVn, 0);
                inVn = extOut;
                data.opInsertBefore(inExt, op);
            }
            else if (inVn.getSize() > outSize) {
                // Do we need a truncation to get to final size
                // Create new op to hold the INT_DIV or INT_SDIV:INT_ADD
                PcodeOp newop = data.newOp(2, op.getAddr());
                // This gets changed immediately, but need it for opInsert
                data.opSetOpcode(newop, OpCode.CPUI_INT_ADD);
                Varnode resVn = data.newUniqueOut(inVn.getSize(), newop);
                data.opInsertBefore(newop, op);
                // Original op becomes a truncation
                data.opSetOpcode(op, OpCode.CPUI_SUBPIECE);
                data.opSetInput(op, resVn, 0);
                data.opSetInput(op, data.newConstant(4, 0), 1);
                // Main transform now changes newop
                op = newop;
                outSize = inVn.getSize();
            }
            if (extOpc == OpCode.CPUI_INT_ZEXT) {
                // Unsigned division
                data.opSetInput(op, inVn, 0);
                data.opSetInput(op, data.newConstant(outSize, divisor), 1);
                data.opSetOpcode(op, OpCode.CPUI_INT_DIV);
            }
            else {
                // Sign division
                moveSignBitExtraction(op.getOut(), inVn, data);
                PcodeOp divop = data.newOp(2, op.getAddr());
                data.opSetOpcode(divop, OpCode.CPUI_INT_SDIV);
                Varnode newout = data.newUniqueOut(outSize, divop);
                data.opSetInput(divop, inVn, 0);
                data.opSetInput(divop, data.newConstant(outSize, divisor), 1);
                data.opInsertBefore(divop, op);
                // Build the sign value correction
                PcodeOp sgnop = data.newOp(2, op.getAddr());
                data.opSetOpcode(sgnop, OpCode.CPUI_INT_SRIGHT);
                Varnode sgnvn = data.newUniqueOut(outSize, sgnop);
                data.opSetInput(sgnop, inVn, 0);
                data.opSetInput(sgnop, data.newConstant(outSize, (ulong)(outSize * 8 - 1)), 1);
                data.opInsertBefore(sgnop, op);
                // Add the correction into the division op
                data.opSetInput(op, newout, 0);
                data.opSetInput(op, sgnvn, 1);
                data.opSetOpcode(op, OpCode.CPUI_INT_ADD);
            }
            return 1;
        }

        /// \brief Check for INT_(S)RIGHT and/or SUBPIECE followed by INT_MULT
        ///
        /// Look for the forms:
        ///  - `sub(ext(X) * y,c)`       or
        ///  - `sub(ext(X) * y,c) >> n`  or
        ///  - `(ext(X) * y) >> n`
        ///
        /// Looks for truncation/multiplication consistent with an optimized division. The
        /// truncation can come as either a SUBPIECE operation and/or right shifts.
        /// The numerand and the amount it has been extended is discovered. The extension
        /// can be, but doesn't have to be, an explicit INT_ZEXT or INT_SEXT. If the form
        /// doesn't match NULL is returned. If the Varnode holding the extended numerand
        /// matches the final operand size, it is returned, otherwise the unextended numerand
        /// is returned. The total truncation, the multiplicative constant, the numerand
        /// size, and the extension type are all passed back.
        /// \param op is the root of the expression
        /// \param n is the reference that will hold the total number of bits of truncation
        /// \param y will hold the multiplicative constant
        /// \param xsize will hold the number of (non-zero) bits in the numerand
        /// \param extopc holds whether the extension is INT_ZEXT or INT_SEXT
        /// \return the extended numerand if possible, or the unextended numerand, or NULL
        public static Varnode? findForm(PcodeOp op, out int n, out ulong y, out int xsize,
            out OpCode extopc)
        {
            PcodeOp curOp = op;
            OpCode shiftopc = curOp.code();
            n = 0;
            y = 0;
            xsize = 0;
            extopc = 0;
            Varnode inVn;
            if (shiftopc == OpCode.CPUI_INT_RIGHT || shiftopc == OpCode.CPUI_INT_SRIGHT) {
                Varnode vn = curOp.getIn(0) ?? throw new ApplicationException();
                if (!vn.isWritten()) {
                    return (Varnode)null;
                }
                Varnode cvn = curOp.getIn(1) ?? throw new ApplicationException();
                if (!cvn.isConstant()) {
                    n = 0;
                    return (Varnode)null;
                }
                n = (int)cvn.getOffset();
                curOp = vn.getDef();
            }
            else {
                // No initial shift
                n = 0;
                if (shiftopc != OpCode.CPUI_SUBPIECE) {
                    // In this case SUBPIECE is not optional
                    n = 0;
                    return (Varnode)null;
                }
                shiftopc = OpCode.CPUI_MAX;
            }
            if (curOp.code() == OpCode.CPUI_SUBPIECE) {
                // Optional SUBPIECE
                int c = (int)curOp.getIn(1).getOffset();
                inVn = curOp.getIn(0) ?? throw new ApplicationException();
                if (!inVn.isWritten()) {
                    n = 0;
                    return (Varnode)null;
                }
                if (curOp.getOut().getSize() + c != inVn.getSize()) {
                    // Must keep high bits
                    n = 0;
                    return (Varnode)null;
                }
                n += 8 * c;
                curOp = inVn.getDef();
            }
            // There MUST be an INT_MULT
            if (curOp.code() != OpCode.CPUI_INT_MULT) {
                n = 0;
                return (Varnode)null;
            }
            inVn = curOp.getIn(0) ?? throw new ApplicationException();
            if (!inVn.isWritten())
                return (Varnode)null;
            if (inVn.isConstantExtended(out y) >= 0) {
                inVn = curOp.getIn(1) ?? throw new ApplicationException();
                if (!inVn.isWritten())
                    return (Varnode)null;
            }
            else if (curOp.getIn(1).isConstantExtended(out y) < 0)
                // There MUST be a constant
                return (Varnode)null;

            Varnode resVn;
            PcodeOp extOp = inVn.getDef();
            extopc = extOp.code();
            if (extopc != OpCode.CPUI_INT_SEXT) {
                ulong nzMask = inVn.getNZMask();
                xsize = 8 * sizeof(ulong) - Globals.count_leading_zeros(nzMask);
                if (xsize == 0) return (Varnode)null;
                if (xsize > 4 * inVn.getSize()) return (Varnode)null;
            }
            else
                xsize = extOp.getIn(0).getSize() * 8;

            if (extopc == OpCode.CPUI_INT_ZEXT || extopc == OpCode.CPUI_INT_SEXT) {
                Varnode extVn = extOp.getIn(0);
                if (extVn.isFree()) return (Varnode)null;
                if (inVn.getSize() == op.getOut().getSize())
                    resVn = inVn;
                else
                    resVn = extVn;
            }
            else {
                extopc = OpCode.CPUI_INT_ZEXT; // Treat as unsigned extension
                resVn = inVn;
            }
            // Check for signed mismatch
            if (((extopc == OpCode.CPUI_INT_ZEXT) && (shiftopc == OpCode.CPUI_INT_SRIGHT)) ||
                ((extopc == OpCode.CPUI_INT_SEXT) && (shiftopc == OpCode.CPUI_INT_RIGHT)))
            {
                if (8 * op.getOut().getSize() - n != xsize)
                    return (Varnode)null;
                // op's signedness does not matter because all the extension
                // bits are truncated
            }
            return resVn;
        }
    }
}
