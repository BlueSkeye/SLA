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
    internal class RuleSLess2Zero : Rule
    {
        /// \brief Get the piece containing the sign-bit
        ///
        /// If the given PcodeOp pieces together 2 Varnodes only one of which is
        /// determining the high bit, return that Varnode.
        /// \param op is the given PcodeOp
        /// \return the Varnode holding the high bit
        private static Varnode getHiBit(PcodeOp op)
        {
            OpCode opc = op.code();
            if ((opc != OpCode.CPUI_INT_ADD) && (opc != OpCode.CPUI_INT_OR) && (opc != OpCode.CPUI_INT_XOR))
                return (Varnode)null;

            Varnode vn1 = op.getIn(0);
            Varnode vn2 = op.getIn(1);
            ulong mask = Globals.calc_mask(vn1.getSize());
            mask = (mask ^ (mask >> 1));    // Only high-bit is set
            ulong nzmask1 = vn1.getNZMask();
            if ((nzmask1 != mask) && ((nzmask1 & mask) != 0)) // If high-bit is set AND some other bit
                return (Varnode)null;
            ulong nzmask2 = vn2.getNZMask();
            if ((nzmask2 != mask) && ((nzmask2 & mask) != 0))
                return (Varnode)null;

            if (nzmask1 == mask)
                return vn1;
            if (nzmask2 == mask)
                return vn2;
            return (Varnode)null;
        }

        public RuleSLess2Zero(string g)
            : base(g, 0, "sless2zero")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSLess2Zero(getGroup());
        }

        /// \class RuleSLess2Zero
        /// \brief Simplify INT_SLESS applied to 0 or -1
        ///
        /// Forms include:
        ///  - `0 s< V * -1  =>  V s< 0`
        ///  - `V * -1 s< 0  =>  0 s< V`
        ///  - `-1 s< SUB(V,hi) => -1 s< V`
        ///  - `SUB(V,hi) s< 0  => V s< 0`
        ///  - `-1 s< ~V     => V s< 0`
        ///  - `~V s< 0      => -1 s< V`
        ///  - `(V & 0xf000) s< 0   =>  V s< 0`
        ///  - `-1 s< (V & 0xf000)  =>  -1 s< V
        ///  - `CONCAT(V,W) s< 0    =>  V s< 0`
        ///  - `-1 s< CONCAT(V,W)   =>  -1 s> V`
        ///
        /// There is a second set of forms where one side of the comparison is
        /// built out of a high and low piece, where the high piece determines the
        /// sign bit:
        ///  - `-1 s< (hi + lo)  =>  -1 s< hi`
        ///  - `(hi + lo) s< 0   =>  hi s< 0`
        ///
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_SLESS);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode lvn;
            Varnode rvn;
            Varnode coeff;
            Varnode avn;
            PcodeOp feedOp;
            OpCode feedOpCode;
            lvn = op.getIn(0);
            rvn = op.getIn(1);

            if (lvn.isConstant())
            {
                if (!rvn.isWritten()) return 0;
                if (lvn.getOffset() == 0)
                {
                    feedOp = rvn.getDef();
                    feedOpCode = feedOp.code();
                    if (feedOpCode == OpCode.CPUI_INT_MULT)
                    {
                        coeff = feedOp.getIn(1);
                        if (!coeff.isConstant()) return 0;
                        if (coeff.getOffset() != Globals.calc_mask(coeff.getSize())) return 0;
                        avn = feedOp.getIn(0);
                        if (avn.isFree()) return 0;
                        data.opSetInput(op, avn, 0);
                        data.opSetInput(op, lvn, 1);
                        return 1;
                    }
                }
                else if (lvn.getOffset() == Globals.calc_mask(lvn.getSize()))
                {
                    feedOp = rvn.getDef();
                    feedOpCode = feedOp.code();
                    Varnode hibit = getHiBit(feedOp);
                    if (hibit != (Varnode)null)
                    { // Test for -1 s<  (hi ^ lo)
                        if (hibit.isConstant())
                            data.opSetInput(op, data.newConstant(hibit.getSize(), hibit.getOffset()), 1);
                        else
                            data.opSetInput(op, hibit, 1);
                        data.opSetOpcode(op, OpCode.CPUI_INT_EQUAL);
                        data.opSetInput(op, data.newConstant(hibit.getSize(), 0), 0);
                        return 1;
                    }
                    else if (feedOpCode == OpCode.CPUI_SUBPIECE)
                    {
                        avn = feedOp.getIn(0);
                        if (avn.isFree() || avn.getSize() > 8)    // Don't create comparison bigger than 8 bytes
                            return 0;
                        if (rvn.getSize() + (int)feedOp.getIn(1).getOffset() == avn.getSize())
                        {
                            // We have -1 s< SUB( avn, #hi )
                            data.opSetInput(op, avn, 1);
                            data.opSetInput(op, data.newConstant(avn.getSize(), Globals.calc_mask(avn.getSize())), 0);
                            return 1;
                        }
                    }
                    else if (feedOpCode == OpCode.CPUI_INT_NEGATE)
                    {
                        // We have -1 s< ~avn
                        avn = feedOp.getIn(0);
                        if (avn.isFree())
                            return 0;
                        data.opSetInput(op, avn, 0);
                        data.opSetInput(op, data.newConstant(avn.getSize(), 0), 1);
                        return 1;
                    }
                    else if (feedOpCode == OpCode.CPUI_INT_AND)
                    {
                        avn = feedOp.getIn(0);
                        if (avn.isFree() || rvn.loneDescend() == (PcodeOp)null)
                            return 0;

                        Varnode maskVn = feedOp.getIn(1);
                        if (maskVn.isConstant())
                        {
                            ulong mask = maskVn.getOffset();
                            mask >>= (8 * avn.getSize() - 1);  // Fetch sign-bit
                            if ((mask & 1) != 0)
                            {
                                // We have -1 s< avn & 0x8...
                                data.opSetInput(op, avn, 1);
                                return 1;
                            }
                        }
                    }
                    else if (feedOpCode == OpCode.CPUI_PIECE)
                    {
                        // We have -1 s< CONCAT(V,W)
                        avn = feedOp.getIn(0);     // Most significant piece
                        if (avn.isFree())
                            return 0;
                        data.opSetInput(op, avn, 1);
                        data.opSetInput(op, data.newConstant(avn.getSize(), Globals.calc_mask(avn.getSize())), 0);
                        return 1;
                    }
                }
            }
            else if (rvn.isConstant())
            {
                if (!lvn.isWritten()) return 0;
                if (rvn.getOffset() == 0)
                {
                    feedOp = lvn.getDef();
                    feedOpCode = feedOp.code();
                    if (feedOpCode == OpCode.CPUI_INT_MULT)
                    {
                        coeff = feedOp.getIn(1);
                        if (!coeff.isConstant()) return 0;
                        if (coeff.getOffset() != Globals.calc_mask(coeff.getSize())) return 0;
                        avn = feedOp.getIn(0);
                        if (avn.isFree()) return 0;
                        data.opSetInput(op, avn, 1);
                        data.opSetInput(op, rvn, 0);
                        return 1;
                    }
                    else
                    {
                        Varnode hibit = getHiBit(feedOp);
                        if (hibit != (Varnode)null)
                        { // Test for (hi ^ lo) s< 0
                            if (hibit.isConstant())
                                data.opSetInput(op, data.newConstant(hibit.getSize(), hibit.getOffset()), 0);
                            else
                                data.opSetInput(op, hibit, 0);
                            data.opSetOpcode(op, OpCode.CPUI_INT_NOTEQUAL);
                            return 1;
                        }
                        else if (feedOpCode == OpCode.CPUI_SUBPIECE)
                        {
                            avn = feedOp.getIn(0);
                            if (avn.isFree() || avn.getSize() > 8)    // Don't create comparison greater than 8 bytes
                                return 0;
                            if (lvn.getSize() + (int)feedOp.getIn(1).getOffset() == avn.getSize())
                            {
                                // We have SUB( avn, #hi ) s< 0
                                data.opSetInput(op, avn, 0);
                                data.opSetInput(op, data.newConstant(avn.getSize(), 0), 1);
                                return 1;
                            }
                        }
                        else if (feedOpCode == OpCode.CPUI_INT_NEGATE)
                        {
                            // We have ~avn s< 0
                            avn = feedOp.getIn(0);
                            if (avn.isFree()) return 0;
                            data.opSetInput(op, avn, 1);
                            data.opSetInput(op, data.newConstant(avn.getSize(), Globals.calc_mask(avn.getSize())), 0);
                            return 1;
                        }
                        else if (feedOpCode == OpCode.CPUI_INT_AND)
                        {
                            avn = feedOp.getIn(0);
                            if (avn.isFree() || lvn.loneDescend() == (PcodeOp)null)
                                return 0;
                            Varnode maskVn = feedOp.getIn(1);
                            if (maskVn.isConstant())
                            {
                                ulong mask = maskVn.getOffset();
                                mask >>= (8 * avn.getSize() - 1);  // Fetch sign-bit
                                if ((mask & 1) != 0)
                                {
                                    // We have avn & 0x8... s< 0
                                    data.opSetInput(op, avn, 0);
                                    return 1;
                                }
                            }
                        }
                        else if (feedOpCode == OpCode.CPUI_PIECE)
                        {
                            // We have CONCAT(V,W) s< 0
                            avn = feedOp.getIn(0);     // Most significant piece
                            if (avn.isFree())
                                return 0;
                            data.opSetInput(op, avn, 0);
                            data.opSetInput(op, data.newConstant(avn.getSize(), 0), 1);
                            return 1;
                        }
                    }
                }
            }
            return 0;
        }
    }
}
