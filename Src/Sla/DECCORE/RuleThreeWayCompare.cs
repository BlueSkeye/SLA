using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleThreeWayCompare : Rule
    {
        public RuleThreeWayCompare(string g)
            : base(g, 0, "threewaycomp")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleThreeWayCompare(getGroup());
        }

        /// \class RuleThreeWayCompare
        /// \brief Simplify expressions involving \e three-way comparisons
        ///
        /// A \b three-way comparison is the expression
        ///  - `X = zext( V < W ) + ZEXT( V <= W ) - 1` in some permutation
        ///
        /// This gives the result (-1, 0, or 1) depending on whether V is
        /// less-than, equal, or greater-than W.  This Rule looks for secondary
        /// comparisons of the three-way, such as
        ///  - `X < 1`  which simplifies to
        ///  - `V <= W`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_SLESS);
            oplist.push_back(CPUI_INT_SLESSEQUAL);
            oplist.push_back(CPUI_INT_EQUAL);
            oplist.push_back(CPUI_INT_NOTEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int constSlot = 0;
            int form;
            Varnode* tmpvn = op.getIn(constSlot);
            if (!tmpvn.isConstant())
            {       // One of the two inputs must be a constant
                constSlot = 1;
                tmpvn = op.getIn(constSlot);
                if (!tmpvn.isConstant()) return 0;
            }
            ulong val = tmpvn.getOffset(); // Encode const value (-1, 0, 1, 2) as highest 3 bits of form (000, 001, 010, 011)
            if (val <= 2)
                form = (int)val + 1;
            else if (val == calc_mask(tmpvn.getSize()))
                form = 0;
            else
                return 0;

            tmpvn = op.getIn(1 - constSlot);
            if (!tmpvn.isWritten()) return 0;
            if (tmpvn.getDef().code() != CPUI_INT_ADD) return 0;
            bool isPartial = false;
            PcodeOp* lessop = detectThreeWay(tmpvn.getDef(), isPartial);
            if (lessop == (PcodeOp*)0)
                return 0;
            if (isPartial)
            {   // Only found a partial three-way
                if (form == 0)
                    return 0;       // -1 const value is now out of range
                form -= 1;      // Subtract 1 (from both sides of equation) to complete the three-way form
            }
            form <<= 1;
            if (constSlot == 1)         // Encode const position (0 or 1) as next bit
                form += 1;
            OpCode lessform = lessop.code();   // Either INT_LESS, INT_SLESS, or FLOAT_LESS
            form <<= 2;
            if (op.code() == CPUI_INT_SLESSEQUAL)
                form += 1;
            else if (op.code() == CPUI_INT_EQUAL)
                form += 2;
            else if (op.code() == CPUI_INT_NOTEQUAL)
                form += 3;
            // Encode base op (SLESS, SLESSEQUAL, EQUAL, NOTEQUAL) as final 2 bits

            Varnode* bvn = lessop.getIn(0);    // First parameter to LESSTHAN is second parameter to cmp3way function
            Varnode* avn = lessop.getIn(1);    // Second parameter to LESSTHAN is first parameter to cmp3way function
            if ((!avn.isConstant()) && (avn.isFree())) return 0;
            if ((!bvn.isConstant()) && (bvn.isFree())) return 0;
            switch (form)
            {
                case 1: // -1  s<= threeway   =>   always true
                case 21:    // threeway  s<=  1   =>   always true
                    data.opSetOpcode(op, CPUI_INT_EQUAL);
                    data.opSetInput(op, data.newConstant(1, 0), 0);
                    data.opSetInput(op, data.newConstant(1, 0), 1);
                    break;
                case 4: // threeway  s<  -1   =>   always false
                case 16:    //  1  s<  threeway   =>   always false
                    data.opSetOpcode(op, CPUI_INT_NOTEQUAL);
                    data.opSetInput(op, data.newConstant(1, 0), 0);
                    data.opSetInput(op, data.newConstant(1, 0), 1);
                    break;
                case 2: // -1  ==  threeway   =>   a < b
                case 5: // threeway  s<= -1   =>   a < b
                case 6: // threeway  ==  -1   =>   a < b
                case 12:    // threeway  s<   0   =>   a < b
                    data.opSetOpcode(op, lessform);
                    data.opSetInput(op, avn, 0);
                    data.opSetInput(op, bvn, 1);
                    break;
                case 13:    // threeway  s<=  0   =>   a <= b
                case 19:    //  1  !=  threeway   =>   a <= b
                case 20:    // threeway  s<   1   =>   a <= b
                case 23:    // threeway  !=   1   =>   a <= b
                    data.opSetOpcode(op, (OpCode)(lessform + 1));       // LESSEQUAL form
                    data.opSetInput(op, avn, 0);
                    data.opSetInput(op, bvn, 1);
                    break;
                case 8: //  0  s<  threeway   =>   a > b
                case 17:    //  1  s<= threeway   =>   a > b
                case 18:    //  1  ==  threeway   =>   a > b
                case 22:    // threeway  ==   1   =>   a > b
                    data.opSetOpcode(op, lessform);
                    data.opSetInput(op, bvn, 0);
                    data.opSetInput(op, avn, 1);
                    break;
                case 0: // -1  s<  threeway   =>   a >= b
                case 3: // -1  !=  threeway   =>   a >= b
                case 7: // threeway  !=  -1   =>   a >= b
                case 9: //  0  s<= threeway   =>   a >= b
                    data.opSetOpcode(op, (OpCode)(lessform + 1));       // LESSEQUAL form
                    data.opSetInput(op, bvn, 0);
                    data.opSetInput(op, avn, 1);
                    break;
                case 10:    //  0  ==  threeway   =>   a == b
                case 14:    // threeway  ==   0   =>   a == b
                    if (lessform == CPUI_FLOAT_LESS)            // Choose the right equal form
                        lessform = CPUI_FLOAT_EQUAL;            // float form
                    else
                        lessform = CPUI_INT_EQUAL;          // or integer form
                    data.opSetOpcode(op, lessform);
                    data.opSetInput(op, avn, 0);
                    data.opSetInput(op, bvn, 1);
                    break;
                case 11:    //  0  !=  threeway   =>   a != b
                case 15:    // threeway  !=   0   =>   a != b
                    if (lessform == CPUI_FLOAT_LESS)            // Choose the right notequal form
                        lessform = CPUI_FLOAT_NOTEQUAL;         // float form
                    else
                        lessform = CPUI_INT_NOTEQUAL;           // or integer form
                    data.opSetOpcode(op, lessform);
                    data.opSetInput(op, avn, 0);
                    data.opSetInput(op, bvn, 1);
                    break;
                default:
                    return 0;
            }
            return 1;
        }

        /// \brief Detect a three-way calculation
        ///
        /// A \b three-way expression looks like:
        ///  - `zext( V < W ) + zext( V <= W ) - 1`  in some permutation
        ///
        /// The comparisons can signed, unsigned, or floating-point.
        /// \param op is the putative root INT_ADD of the calculation
        /// \param isPartial is set to \b true if a partial form is detected
        /// \return the less-than op or NULL if no three-way was detected
        public static PcodeOp detectThreeWay(PcodeOp op, bool isPartial)
        {
            Varnode* vn1, *vn2, *tmpvn;
            PcodeOp* zext1, *zext2;
            PcodeOp* addop, *lessop, *lessequalop;
            ulong mask;
            vn2 = op.getIn(1);
            if (vn2.isConstant())
            {       // Form 1 :  (z + z) - 1
                mask = calc_mask(vn2.getSize());
                if (mask != vn2.getOffset()) return (PcodeOp*)0;       // Match the -1
                vn1 = op.getIn(0);
                if (!vn1.isWritten()) return (PcodeOp*)0;
                addop = vn1.getDef();
                if (addop.code() != CPUI_INT_ADD) return (PcodeOp*)0;  // Match the add
                tmpvn = addop.getIn(0);
                if (!tmpvn.isWritten()) return (PcodeOp*)0;
                zext1 = tmpvn.getDef();
                if (zext1.code() != CPUI_INT_ZEXT) return (PcodeOp*)0; // Match the first zext
                tmpvn = addop.getIn(1);
                if (!tmpvn.isWritten()) return (PcodeOp*)0;
                zext2 = tmpvn.getDef();
                if (zext2.code() != CPUI_INT_ZEXT) return (PcodeOp*)0; // Match the second zext
            }
            else if (vn2.isWritten())
            {
                PcodeOp* tmpop = vn2.getDef();
                if (tmpop.code() == CPUI_INT_ZEXT)
                {   // Form 2 : (z - 1) + z
                    zext2 = tmpop;                  // Second zext is already matched
                    vn1 = op.getIn(0);
                    if (!vn1.isWritten()) return (PcodeOp*)0;
                    addop = vn1.getDef();
                    if (addop.code() != CPUI_INT_ADD)
                    {   // Partial form:  (z + z)
                        zext1 = addop;
                        if (zext1.code() != CPUI_INT_ZEXT)
                            return (PcodeOp*)0;         // Match the first zext
                        isPartial = true;
                    }
                    else
                    {
                        tmpvn = addop.getIn(1);
                        if (!tmpvn.isConstant()) return (PcodeOp*)0;
                        mask = calc_mask(tmpvn.getSize());
                        if (mask != tmpvn.getOffset()) return (PcodeOp*)0; // Match the -1
                        tmpvn = addop.getIn(0);
                        if (!tmpvn.isWritten()) return (PcodeOp*)0;
                        zext1 = tmpvn.getDef();
                        if (zext1.code() != CPUI_INT_ZEXT) return (PcodeOp*)0; // Match the first zext
                    }
                }
                else if (tmpop.code() == CPUI_INT_ADD)
                {   // Form 3 : z + (z - 1)
                    addop = tmpop;              // Matched the add
                    vn1 = op.getIn(0);
                    if (!vn1.isWritten()) return (PcodeOp*)0;
                    zext1 = vn1.getDef();
                    if (zext1.code() != CPUI_INT_ZEXT) return (PcodeOp*)0; // Match the first zext
                    tmpvn = addop.getIn(1);
                    if (!tmpvn.isConstant()) return (PcodeOp*)0;
                    mask = calc_mask(tmpvn.getSize());
                    if (mask != tmpvn.getOffset()) return (PcodeOp*)0; // Match the -1
                    tmpvn = addop.getIn(0);
                    if (!tmpvn.isWritten()) return (PcodeOp*)0;
                    zext2 = tmpvn.getDef();
                    if (zext2.code() != CPUI_INT_ZEXT) return (PcodeOp*)0; // Match the second zext
                }
                else
                    return (PcodeOp*)0;
            }
            else
                return (PcodeOp*)0;
            vn1 = zext1.getIn(0);
            if (!vn1.isWritten()) return (PcodeOp*)0;
            vn2 = zext2.getIn(0);
            if (!vn2.isWritten()) return (PcodeOp*)0;
            lessop = vn1.getDef();
            lessequalop = vn2.getDef();
            OpCode opc = lessop.code();
            if ((opc != CPUI_INT_LESS) && (opc != CPUI_INT_SLESS) && (opc != CPUI_FLOAT_LESS))
            {   // Make sure first zext is less
                PcodeOp* tmpop = lessop;
                lessop = lessequalop;
                lessequalop = tmpop;
            }
            int form = testCompareEquivalence(lessop, lessequalop);
            if (form < 0)
                return (PcodeOp*)0;
            if (form == 1)
            {
                PcodeOp* tmpop = lessop;
                lessop = lessequalop;
                lessequalop = tmpop;
            }
            return lessop;
        }

        /// \brief Make sure comparisons match properly for a three-way
        ///
        /// Given `zext(V < W) + zext(X <= Y)`, make sure comparisons match, i.e  V matches X and W matches Y.
        /// Take into account that the LESSEQUAL may have been converted to a LESS.
        /// Return:
        ///    - 0 if configuration is correct
        ///    - 1 if correct but roles of \b lessop and \b lessequalop must be swapped
        ///    - -1 if not the correct configuration
        /// \param lessop is the putative LESS PcodeOp
        /// \param lessequalop is the putative LESSEQUAL PcodeOp
        /// \return 0, 1, or -1
        public static int testCompareEquivalence(PcodeOp lessop, PcodeOp lessequalop)
        {
            bool twoLessThan;
            if (lessop.code() == CPUI_INT_LESS)
            {   // Make sure second zext is matching lessequal
                if (lessequalop.code() == CPUI_INT_LESSEQUAL)
                    twoLessThan = false;
                else if (lessequalop.code() == CPUI_INT_LESS)
                    twoLessThan = true;
                else
                    return -1;
            }
            else if (lessop.code() == CPUI_INT_SLESS)
            {
                if (lessequalop.code() == CPUI_INT_SLESSEQUAL)
                    twoLessThan = false;
                else if (lessequalop.code() == CPUI_INT_SLESS)
                    twoLessThan = true;
                else
                    return -1;
            }
            else if (lessop.code() == CPUI_FLOAT_LESS)
            {
                if (lessequalop.code() == CPUI_FLOAT_LESSEQUAL)
                    twoLessThan = false;
                else
                    return -1;              // No partial form for floating-point comparison
            }
            else
                return -1;
            Varnode* a1 = lessop.getIn(0);
            Varnode* a2 = lessequalop.getIn(0);
            Varnode* b1 = lessop.getIn(1);
            Varnode* b2 = lessequalop.getIn(1);
            int res = 0;
            if (a1 != a2)
            {   // Make sure a1 and a2 are equivalent
                if ((!a1.isConstant()) || (!a2.isConstant())) return -1;
                if ((a1.getOffset() != a2.getOffset()) && twoLessThan)
                {
                    if (a2.getOffset() + 1 == a1.getOffset())
                    {
                        twoLessThan = false;        // -lessequalop- is LESSTHAN, equivalent to LESSEQUAL
                    }
                    else if (a1.getOffset() + 1 == a2.getOffset())
                    {
                        twoLessThan = false;        // -lessop- is LESSTHAN, equivalent to LESSEQUAL
                        res = 1;            // we need to swap
                    }
                    else
                        return -1;
                }
            }
            if (b1 != b2)
            {   // Make sure b1 and b2 are equivalent
                if ((!b1.isConstant()) || (!b2.isConstant())) return -1;
                if ((b1.getOffset() != b2.getOffset()) && twoLessThan)
                {
                    if (b1.getOffset() + 1 == b2.getOffset())
                    {
                        twoLessThan = false;
                    }
                    else if (b2.getOffset() + 1 == b1.getOffset())
                    {
                        twoLessThan = false;
                        res = 1;            // we need to swap
                    }
                }
                else
                    return -1;
            }
            if (twoLessThan)
                return -1;              // Did not compensate for two LESSTHANs with differing constants
            return res;
        }
    }
}
