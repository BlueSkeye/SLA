using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class Equal1Form
    {
        private SplitVarnode in1;
        private SplitVarnode in2;
        private PcodeOp loop;
        private PcodeOp hiop;
        private PcodeOp hibool;
        private PcodeOp lobool;
        private Varnode hi1;
        private Varnode lo1;
        private Varnode hi2;
        private Varnode lo2;
        private int hi1slot;
        private int lo1slot;
        private bool notequalformhi;
        private bool notequalformlo;
        private bool setonlow;

        // Given a known double precis input, look for double precision compares of the form
        //   a == b,  a != b
        //
        // We look for
        //     hibool = hi1 == hi2
        //     lobool = lo1 == lo2
        // each of the bools induces a CBRANCH
        //               if (hibool) blocksecond else blockfalse
        // blocksecond:  if (lobool) blocktrue else blockfalse
        public bool applyRule(SplitVarnode i, PcodeOp hop, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            in1 = i;

            hiop = hop;
            hi1 = in1.getHi();
            lo1 = in1.getLo();
            hi1slot = hiop.getSlot(hi1);
            hi2 = hiop.getIn(1 - hi1slot);
            notequalformhi = (hiop.code() == OpCode.CPUI_INT_NOTEQUAL);

            list<PcodeOp*>::const_iterator iter, enditer;
            list<PcodeOp*>::const_iterator iter2, enditer2;
            list<PcodeOp*>::const_iterator iter3, enditer3;
            iter = lo1.beginDescend();
            enditer = lo1.endDescend();
            while (iter != enditer)
            {
                loop = *iter;
                ++iter;
                if (loop.code() == OpCode.CPUI_INT_EQUAL)
                    notequalformlo = false;
                else if (loop.code() == OpCode.CPUI_INT_NOTEQUAL)
                    notequalformlo = true;
                else
                    continue;
                lo1slot = loop.getSlot(lo1);
                lo2 = loop.getIn(1 - lo1slot);

                iter2 = hiop.getOut().beginDescend();
                enditer2 = hiop.getOut().endDescend();
                while (iter2 != enditer2)
                {
                    hibool = *iter2;
                    ++iter2;
                    iter3 = loop.getOut().beginDescend();
                    enditer3 = loop.getOut().endDescend();
                    while (iter3 != enditer3)
                    {
                        lobool = *iter3;
                        ++iter3;

                        in2.initPartial(in1.getSize(), lo2, hi2);

                        if ((hibool.code() == OpCode.CPUI_CBRANCH) && (lobool.code() == OpCode.CPUI_CBRANCH))
                        {
                            // Branching form of the equal operation
                            BlockBasic* hibooltrue,*hiboolfalse;
                            BlockBasic* lobooltrue,*loboolfalse;
                            SplitVarnode::getTrueFalse(hibool, notequalformhi, hibooltrue, hiboolfalse);
                            SplitVarnode::getTrueFalse(lobool, notequalformlo, lobooltrue, loboolfalse);

                            if ((hibooltrue == lobool.getParent()) &&  // hi is checked first then lo
                                (hiboolfalse == loboolfalse) &&
                                SplitVarnode::otherwiseEmpty(lobool))
                            {
                                if (SplitVarnode::prepareBoolOp(in1, in2, hibool))
                                {
                                    setonlow = true;
                                    SplitVarnode::createBoolOp(data, hibool, in1, in2, notequalformhi ? OpCode.CPUI_INT_NOTEQUAL : OpCode.CPUI_INT_EQUAL);
                                    // We change lobool so that it always goes to the original TRUE block
                                    data.opSetInput(lobool, data.newConstant(1, notequalformlo ? 0 : 1), 1);
                                    return true;
                                }
                            }
                            else if ((lobooltrue == hibool.getParent()) && // lo is checked first then hi
                                 (hiboolfalse == loboolfalse) &&
                                 SplitVarnode::otherwiseEmpty(hibool))
                            {
                                if (SplitVarnode::prepareBoolOp(in1, in2, lobool))
                                {
                                    setonlow = false;
                                    SplitVarnode::createBoolOp(data, lobool, in1, in2, notequalformlo ? OpCode.CPUI_INT_NOTEQUAL : OpCode.CPUI_INT_EQUAL);
                                    // We change hibool so that it always goes to the original TRUE block
                                    data.opSetInput(hibool, data.newConstant(1, notequalformhi ? 0 : 1), 1);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
