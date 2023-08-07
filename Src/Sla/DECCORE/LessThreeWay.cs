using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class LessThreeWay
    {
        private SplitVarnode @in;
        private SplitVarnode in2;
        private BlockBasic hilessbl;
        private BlockBasic lolessbl;
        private BlockBasic hieqbl;
        private BlockBasic hilesstrue;
        private BlockBasic hilessfalse;
        private BlockBasic hieqtrue;
        private BlockBasic hieqfalse;
        private BlockBasic lolesstrue;
        private BlockBasic lolessfalse;
        private PcodeOp hilessbool;
        private PcodeOp lolessbool;
        private PcodeOp hieqbool;
        private PcodeOp hiless;
        private PcodeOp hiequal;
        private PcodeOp loless;
        private Varnode vnhil1;
        private Varnode vnhil2;
        private Varnode vnhie1;
        private Varnode vnhie2;
        private Varnode vnlo1;
        private Varnode vnlo2;
        private Varnode hi;
        private Varnode lo;
        private Varnode hi2;
        private Varnode lo2;
        private int hislot;
        private bool hiflip;
        private bool equalflip;
        private bool loflip;
        private bool lolessiszerocomp;
        private bool lolessequalform;
        private bool hilessequalform;
        private bool signcompare;
        private bool midlessform;
        private bool midlessequal;
        private bool midsigncompare;
        private bool hiconstform;
        private bool midconstform;
        private bool loconstform;
        private ulong hival;
        private ulong midval;
        private ulong loval;
        private OpCode finalopc;

        private bool mapBlocksFromLow(BlockBasic lobl)
        { // Assuming -lobl- is the block containing the low precision test of a double precision lessthan
          // Map out all the blocks if possible, otherwise return false
            lolessbl = lobl;
            if (lolessbl.sizeIn() != 1) return false;
            if (lolessbl.sizeOut() != 2) return false;
            hieqbl = (BlockBasic*)lolessbl.getIn(0);
            if (hieqbl.sizeIn() != 1) return false;
            if (hieqbl.sizeOut() != 2) return false;
            hilessbl = (BlockBasic*)hieqbl.getIn(0);
            if (hilessbl.sizeOut() != 2) return false;
            return true;
        }

        private bool mapOpsFromBlocks()
        {
            lolessbool = lolessbl.lastOp();
            if (lolessbool == (PcodeOp)null) return false;
            if (lolessbool.code() != OpCode.CPUI_CBRANCH) return false;
            hieqbool = hieqbl.lastOp();
            if (hieqbool == (PcodeOp)null) return false;
            if (hieqbool.code() != OpCode.CPUI_CBRANCH) return false;
            hilessbool = hilessbl.lastOp();
            if (hilessbool == (PcodeOp)null) return false;
            if (hilessbool.code() != OpCode.CPUI_CBRANCH) return false;

            Varnode vn;

            hiflip = false;
            equalflip = false;
            loflip = false;
            midlessform = false;
            lolessiszerocomp = false;

            vn = hieqbool.getIn(1);
            if (!vn.isWritten()) return false;
            hiequal = vn.getDef();
            switch (hiequal.code()) {
                case OpCode.CPUI_INT_EQUAL:
                    midlessform = false;
                    break;
                case OpCode.CPUI_INT_NOTEQUAL:
                    midlessform = false;
                    break;
                case OpCode.CPUI_INT_LESS:
                    midlessequal = false;
                    midsigncompare = false;
                    midlessform = true;
                    break;
                case OpCode.CPUI_INT_LESSEQUAL:
                    midlessequal = true;
                    midsigncompare = false;
                    midlessform = true;
                    break;
                case OpCode.CPUI_INT_SLESS:
                    midlessequal = false;
                    midsigncompare = true;
                    midlessform = true;
                    break;
                case OpCode.CPUI_INT_SLESSEQUAL:
                    midlessequal = true;
                    midsigncompare = true;
                    midlessform = true;
                    break;
                default:
                    return false;
            }

            vn = lolessbool.getIn(1);
            if (!vn.isWritten()) return false;
            loless = vn.getDef();
            switch (loless.code()) {
                // Only unsigned forms
                case OpCode.CPUI_INT_LESS:
                    lolessequalform = false;
                    break;
                case OpCode.CPUI_INT_LESSEQUAL:
                    lolessequalform = true;
                    break;
                case OpCode.CPUI_INT_EQUAL:
                    if (!loless.getIn(1).isConstant()) return false;
                    if (loless.getIn(1).getOffset() != 0) return false;
                    lolessiszerocomp = true;
                    lolessequalform = true;
                    break;
                case OpCode.CPUI_INT_NOTEQUAL:
                    if (!loless.getIn(1).isConstant()) return false;
                    if (loless.getIn(1).getOffset() != 0) return false;
                    lolessiszerocomp = true;
                    lolessequalform = false;
                    break;
                default:
                    return false;
            }

            vn = hilessbool.getIn(1);
            if (!vn.isWritten()) return false;
            hiless = vn.getDef();
            switch (hiless.code()) {
                case OpCode.CPUI_INT_LESS:
                    hilessequalform = false;
                    signcompare = false;
                    break;
                case OpCode.CPUI_INT_LESSEQUAL:
                    hilessequalform = true;
                    signcompare = false;
                    break;
                case OpCode.CPUI_INT_SLESS:
                    hilessequalform = false;
                    signcompare = true;
                    break;
                case OpCode.CPUI_INT_SLESSEQUAL:
                    hilessequalform = true;
                    signcompare = true;
                    break;
                default:
                    return false;
            }
            return true;
        }

        private bool checkSignedness()
        {
            return !midlessform || (midsigncompare == signcompare);
        }

        private bool normalizeHi()
        {
            Varnode tmpvn;
            vnhil1 = hiless.getIn(0);
            vnhil2 = hiless.getIn(1);
            if (vnhil1.isConstant())
            {   // Start with constant on the right
                hiflip = !hiflip;
                hilessequalform = !hilessequalform;
                tmpvn = vnhil1;
                vnhil1 = vnhil2;
                vnhil2 = tmpvn;
            }
            hiconstform = false;
            if (vnhil2.isConstant())
            {
                hiconstform = true;
                hival = vnhil2.getOffset();
                SplitVarnode.getTrueFalse(hilessbool, hiflip, out hilesstrue, out hilessfalse);
                int inc = 1;
                if (hilessfalse != hieqbl)
                {   // Make sure the hiless false branch goes to the hieq block
                    hiflip = !hiflip;
                    hilessequalform = !hilessequalform;
                    tmpvn = vnhil1;
                    vnhil1 = vnhil2;
                    vnhil2 = tmpvn;
                    inc = -1;
                }
                if (hilessequalform)
                {   // Make sure to normalize lessequal to less
                    hival += inc;
                    hival &= Globals.calc_mask(@in.getSize());
                    hilessequalform = false;
                }
                hival >>= @in.getLo().getSize() * 8;
            }
            else
            {
                if (hilessequalform)
                {   // Make sure the false branch contains the equal case
                    hilessequalform = false;
                    hiflip = !hiflip;
                    tmpvn = vnhil1;
                    vnhil1 = vnhil2;
                    vnhil2 = tmpvn;
                }
            }
            return true;
        }

        private bool normalizeMid()
        {
            Varnode* tmpvn;
            vnhie1 = hiequal.getIn(0);
            vnhie2 = hiequal.getIn(1);
            if (vnhie1.isConstant())
            {   // Make sure constant is on the right
                tmpvn = vnhie1;
                vnhie1 = vnhie2;
                vnhie2 = tmpvn;
                if (midlessform)
                {
                    equalflip = !equalflip;
                    midlessequal = !midlessequal;
                }
            }
            midconstform = false;
            if (vnhie2.isConstant())
            {
                if (!hiconstform) return false; // If mid is constant, both mid and hi must be constant
                midconstform = true;
                midval = vnhie2.getOffset();
                if (vnhie2.getSize() == @in.getSize()) {
                    // Convert to comparison on high part
                    ulong lopart = midval & Globals.calc_mask(@in.getLo().getSize());
                    midval >>= @in.getLo().getSize() * 8;
                    if (midlessform)
                    {
                        if (midlessequal)
                        {
                            if (lopart != Globals.calc_mask(@in.getLo().getSize())) return false;
                        }
                        else
                        {
                            if (lopart != 0) return false;
                        }
                    }
                    else
                        return false;       // Compare is forcing restriction on lo part
                }
                if (midval != hival)
                {   // If the mid and hi don't match
                    if (!midlessform) return false;
                    midval += (midlessequal) ? 1 : -1; // We may just be one off
                    midval &= Globals.calc_mask(@in.getLo().getSize());
                    midlessequal = !midlessequal;
                    if (midval != hival) return false; // Last chance
                }
            }
            if (midlessform)
            {       // Normalize to EQUAL

                if (!midlessequal)
                {
                    equalflip = !equalflip;
                }
            }
            else
            {
                if (hiequal.code() == OpCode.CPUI_INT_NOTEQUAL)
                {
                    equalflip = !equalflip;
                }
            }
            return true;
        }

        private bool normalizeLo()
        { // This is basically identical to normalizeHi
            Varnode* tmpvn;
            vnlo1 = loless.getIn(0);
            vnlo2 = loless.getIn(1);
            if (lolessiszerocomp)
            {
                loconstform = true;
                if (lolessequalform)
                {   // Treat as if we see vnlo1 <= 0
                    loval = 1;
                    lolessequalform = false;
                }
                else
                {           // Treat as if we see 0 < vnlo1
                    loflip = !loflip;
                    loval = 1;
                }
                return true;
            }
            if (vnlo1.isConstant())
            {   // Make sure constant is on the right
                loflip = !loflip;
                lolessequalform = !lolessequalform;
                tmpvn = vnlo1;
                vnlo1 = vnlo2;
                vnlo2 = tmpvn;
            }
            loconstform = false;
            if (vnlo2.isConstant())
            {   // Make sure normalize lessequal to less
                loconstform = true;
                loval = vnlo2.getOffset();
                if (lolessequalform)
                {
                    loval += 1;
                    loval &= Globals.calc_mask(vnlo2.getSize());
                    lolessequalform = false;
                }
            }
            else
            {
                if (lolessequalform)
                {
                    lolessequalform = false;
                    loflip = !loflip;
                    tmpvn = vnlo1;
                    vnlo1 = vnlo2;
                    vnlo2 = tmpvn;
                }
            }
            return true;
        }

        private bool checkBlockForm()
        {
            SplitVarnode.getTrueFalse(hilessbool, hiflip, out hilesstrue, out hilessfalse);
            SplitVarnode.getTrueFalse(lolessbool, loflip, out lolesstrue, out lolessfalse);
            SplitVarnode.getTrueFalse(hieqbool, equalflip, out hieqtrue, out hieqfalse);
            if ((hilesstrue == lolesstrue) &&
                (hieqfalse == lolessfalse) &&
                (hilessfalse == hieqbl) &&
                (hieqtrue == lolessbl))
            {
                if (SplitVarnode.otherwiseEmpty(hieqbool) && SplitVarnode.otherwiseEmpty(lolessbool))
                    return true;
            }
            //  else if ((hilessfalse == lolessfalse)&&
            //	   (hieqfalse == lolesstrue)&&
            //	   (hilesstrue == hieqbl)&&
            //	   (hieqtrue == lolessbl)) {
            //    if (SplitVarnode.otherwiseEmpty(hieqbool)&&SplitVarnode.otherwiseEmpty(lolessbool))
            //      return true;
            //  }
            return false;
        }

        private bool checkOpForm()
        {
            lo = @in.getLo();
            hi = @in.getHi();

            if (midconstform)
            {
                if (!hiconstform) return false;
                if (vnhie2.getSize() == @in.getSize()) {
                    if ((vnhie1 != vnhil1) && (vnhie1 != vnhil2)) return false;
                }
                else
                {
                    if (vnhie1 !=@in.getHi()) return false;
                }
                // normalizeMid checks that midval == hival
            }
            else
            {
                // hi and hi2 must appear as inputs in both -hiless- and -hiequal-
                if ((vnhil1 != vnhie1) && (vnhil1 != vnhie2)) return false;
                if ((vnhil2 != vnhie1) && (vnhil2 != vnhie2)) return false;
            }
            if ((hi != (Varnode)null) && (hi == vnhil1))
            {
                if (hiconstform) return false;
                hislot = 0;
                hi2 = vnhil2;
                if (vnlo1 != lo)
                { // Pieces must be on the same side
                    Varnode tmpvn = vnlo1;
                    vnlo1 = vnlo2;
                    vnlo2 = tmpvn;
                    if (vnlo1 != lo) return false;
                    loflip = !loflip;
                    lolessequalform = !lolessequalform;
                }
                lo2 = vnlo2;
            }
            else if ((hi != (Varnode)null) && (hi == vnhil2))
            {
                if (hiconstform) return false;
                hislot = 1;
                hi2 = vnhil1;
                if (vnlo2 != lo)
                {
                    Varnode tmpvn = vnlo1;
                    vnlo1 = vnlo2;
                    vnlo2 = tmpvn;
                    if (vnlo2 != lo) return false;
                    loflip = !loflip;
                    lolessequalform = !lolessequalform;
                }
                lo2 = vnlo1;
            }
            else if (@in.getWhole() == vnhil1) {
                if (!hiconstform) return false;
                if (!loconstform) return false;
                if (vnlo1 != lo) return false;
                hislot = 0;
            }
            else if (@in.getWhole() == vnhil2) { // Whole constant appears on the left
                if (!hiconstform) return false;
                if (!loconstform) return false;
                if (vnlo2 != lo)
                {
                    loflip = !loflip;
                    loval -= 1;
                    loval &= Globals.calc_mask(lo.getSize());
                    if (vnlo1 != lo) return false;
                }
                hislot = 1;
            }
            else
                return false;

            return true;
        }

        private void setOpCode()
        { // Decide on the opcode of the final double precision compare
            if (lolessequalform != hiflip)
                finalopc = signcompare ? OpCode.CPUI_INT_SLESSEQUAL : OpCode.CPUI_INT_LESSEQUAL;
            else
                finalopc = signcompare ? OpCode.CPUI_INT_SLESS : OpCode.CPUI_INT_LESS;
            if (hiflip)
            {
                hislot = 1 - hislot;
                hiflip = false;
            }
        }

        private bool setBoolOp()
        { // Make changes to the threeway branch so that it becomes a single double precision branch
            if (hislot == 0)
            {
                if (SplitVarnode.prepareBoolOp(@in, in2, hilessbool))
                    return true;
            }
            else
            {
                if (SplitVarnode.prepareBoolOp(in2, @in, hilessbool))
                    return true;
            }
            return false;
        }

        private bool mapFromLow(PcodeOp op)
        { // Given the less than comparison for the lo piece and an input varnode explicitly marked as isPrecisLo
          // try to map out the threeway lessthan form
            PcodeOp loop = op.getOut().loneDescend();
            if (loop == (PcodeOp)null) return false;
            if (!mapBlocksFromLow(loop.getParent())) return false;
            if (!mapOpsFromBlocks()) return false;
            if (!checkSignedness()) return false;
            if (!normalizeHi()) return false;
            if (!normalizeMid()) return false;
            if (!normalizeLo()) return false;
            if (!checkOpForm()) return false;
            if (!checkBlockForm()) return false;
            return true;
        }

        private bool testReplace()
        {
            setOpCode();
            if (hiconstform)
            {
                in2.initPartial(@in.getSize(), (hival << (8 *@in.getLo().getSize()))| loval);
                if (!setBoolOp()) return false;
            }
            else
            {
                in2.initPartial(@in.getSize(), lo2, hi2);
                if (!setBoolOp()) return false;
            }
            return true;
        }

        // Given a known double precis input, look for double precision less than forms, i.e.
        //    a < b,   a s< b,  a <= b,   a s<= b
        //
        // In this form we look for three separate comparison ops
        //     hiless  = hi1 LESS hi2                   where LESS is in { <, s<, <=, s<= }
        //     hiequal = hi1 == hi2
        //     loless  = lo1 < lo2  OR  lo1 <= lo2      where the comparison is unsigned
        //
        // This boolean values are either combined in the following formula:
        //     resbool = hiless || (hiequal && loless)
        // OR each of the three initial comparison induces a CBRANCH
        //                  if (hiless)  blocktrue  else  blocksecond
        //     blocksecond: if (hiequal) blockthird else  blockfalse
        //     blockthird:  if (loless) blocktrue else blockfalse
        public bool applyRule(SplitVarnode i, PcodeOp loop, bool workishi, Funcdata data)
        {
            if (workishi) return false;
            if (i.getLo() == (Varnode)null) return false; // Doesn't necessarily need the hi
            @in = i;
            if (!mapFromLow(loop)) return false;
            bool res = testReplace();
            if (res)
            {
                if (hislot == 0)
                    SplitVarnode.createBoolOp(data, hilessbool, @in, in2, finalopc);
                else
                    SplitVarnode.createBoolOp(data, hilessbool, in2, @in, finalopc);
                // We change hieqbool so that it always goes to the original FALSE block
                data.opSetInput(hieqbool, data.newConstant(1, equalflip ? 1 : 0), 1);
                // The lolessbool block now becomes unreachable and is eventually removed
            }
            return res;
        }
    }
}
