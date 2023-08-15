using Sla.CORE;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    // NOTES FROM 20121206 W/Decompiler-Man
    // direct reads is for all opcodes, with special for these:
    // BRANCH is direct read on input0.  No direct write.
    // CBRANCH is direct read on input0 and input1.  No direct write.
    // BRANCHIND is direct read on input0 (like call but no params).  No direct write.
    // CALL is direct read on input0 (putative/presumptive param flag on params--other inputs).  Special (non-direct) write of output.
    // CALLIND same as on CALL.  Special (non-direct) write of output.
    // CALLOTHER is direct read on ALL PARAMETERS (input0 and up)--is specified in sleigh.  Direct write if output exists.
    // INDIRECT is least powerful input and output of all.
    // MULTIEQUALS is flow through but must test for and not flow through loop paths (whether from param forward our return backward directions).
    //
    internal class ParamMeasure
    {
        private const int MAXDEPTH = 10;

        public enum ParamIDIO
        {
            INPUT = 0,
            OUTPUT = 1
        }
        
        public enum ParamRank
        {
            BESTRANK = 1,
            //Output
            DIRECTWRITEWITHOUTREAD = 1,
            //Input.  Must be same as DIRECTWRITEWITHREAD so that walkforward as part of walkbackward works
            //  for detecting(not that DIRECTREAD is lower rank that DIRECTWRITEWITHOUTREAD)
            DIRECTREAD = 2,
            //Output
            DIRECTWRITEWITHREAD = 2,
            //Output
            DIRECTWRITEUNKNOWNREAD = 3,
            //Input
            SUBFNPARAM = 4,
            //Output
            THISFNPARAM = 4,
            //Output
            SUBFNRETURN = 5,
            //Input
            THISFNRETURN = 5,
            //Input or Output
            INDIRECT = 6,
            WORSTRANK = 7
        }
        
        public struct WalkState
        {
            internal bool best;
            internal int depth;
            internal ParamRank terminalrank;
        }

        private VarnodeData vndata;
        private Datatype vntype;
        private ParamRank rank;
        private ParamIDIO io;
        private int numcalls;

        private void walkforward(WalkState state, PcodeOp ignoreop, Varnode vn)
        {
            state.depth += 1;
            if (state.depth >= MAXDEPTH)
            {
                state.depth -= 1;
                return;
            }
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (rank != state.terminalrank && iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op != ignoreop) {
                    OpCode oc = op.getOpcode().getOpcode();
                    switch (oc) {
                        case OpCode.CPUI_BRANCH:
                        case OpCode.CPUI_BRANCHIND:
                            if (op.getSlot(vn) == 0) updaterank(ParamRank.DIRECTREAD, state.best);
                            break;
                        case OpCode.CPUI_CBRANCH:
                            if (op.getSlot(vn) < 2) updaterank(ParamRank.DIRECTREAD, state.best);
                            break;
                        case OpCode.CPUI_CALL:
                        case OpCode.CPUI_CALLIND:
                            if (op.getSlot(vn) == 0) updaterank(ParamRank.DIRECTREAD, state.best);
                            else {
                                numcalls++;
                                updaterank(ParamRank.SUBFNPARAM, state.best);
                            }
                            break;
                        case OpCode.CPUI_CALLOTHER:
                            updaterank(ParamRank.DIRECTREAD, state.best);
                            break;
                        case OpCode.CPUI_RETURN:
                            updaterank(ParamRank.THISFNRETURN, state.best);
                            break;
                        case OpCode.CPUI_INDIRECT:
                            updaterank(ParamRank.INDIRECT, state.best);
                            break;
                        case OpCode.CPUI_MULTIEQUAL:
                            // The only op for which there can be a loop in the graph is with the MULTIEQUAL (not for CALL, etc.).
                            // Walk forward only if the path is not part of a loop.
                            if (!op.getParent().isLoopIn(op.getSlot(vn))) walkforward(state, (PcodeOp)null, op.getOut());
                            break;
                        default:
                            updaterank(ParamRank.DIRECTREAD, state.best);
                            break;
                    }
                }
            }
            state.depth -= 1;
        }

        private void walkbackward(WalkState state, PcodeOp ignoreop, Varnode vn)
        {
            if (vn.isInput()) {
                updaterank(ParamRank.THISFNPARAM, state.best);
                return;
            }
            else if (!vn.isWritten()) {
                updaterank(ParamRank.THISFNPARAM, state.best); //TODO: not sure about this.
                return;
            }

            PcodeOp op = vn.getDef() ?? throw new BugException();
            OpCode oc = op.getOpcode().getOpcode();
            switch (oc) {
                case OpCode.CPUI_BRANCH:
                case OpCode.CPUI_BRANCHIND:
                case OpCode.CPUI_CBRANCH:
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLIND:
                    break;
                case OpCode.CPUI_CALLOTHER:
                    if (op.getOut() != (Varnode)null) updaterank(ParamRank.DIRECTREAD, state.best);
                    break;
                case OpCode.CPUI_RETURN:
                    updaterank(ParamRank.SUBFNRETURN, state.best);
                    break;
                case OpCode.CPUI_INDIRECT:
                    updaterank(ParamRank.INDIRECT, state.best);
                    break;
                case OpCode.CPUI_MULTIEQUAL:
                    // The only op for which there can be a loop in the graph is with the MULTIEQUAL (not for CALL, etc.).
                    // Walk backward only if the path is not part of a loop.
                    for (int slot = 0; slot < op.numInput() && rank != state.terminalrank; slot++)
                        if (!op.getParent().isLoopIn(slot)) walkbackward(state, op, op.getIn(slot));
                    break;
                default:
                    //Might be DIRECTWRITEWITHOUTREAD, but we do not know yet.
                    //So now try to walk forward to see if there is at least one path
                    // forward (other than the path we took to get here walking backward)
                    // in which there is not a direct read of this write.
                    ParamMeasure pmfw = new ParamMeasure(vn.getAddr(), vn.getSize(), vn.getType(),
                        ParamIDIO.INPUT);
                    pmfw.calculateRank(false, vn, ignoreop);
                    if (pmfw.getMeasure() == ParamRank.DIRECTREAD)
                        updaterank(ParamRank.DIRECTWRITEWITHREAD, state.best);
                    else
                        updaterank(ParamRank.DIRECTWRITEWITHOUTREAD, state.best);
                    break;
            }
        }

        private void updaterank(ParamRank rank_in, bool best)
        {
            rank = (best == true)
                ? (ParamRank)Math.Min((int)rank, (int)rank_in)
                : (ParamRank)Math.Max((int)rank, (int)rank_in);
        }

        public ParamMeasure(Address addr, int sz, Datatype dt, ParamIDIO io_in)
        {
            vndata.space = addr.getSpace();
            vndata.offset = addr.getOffset();
            vndata.size = sz;
            vntype = dt;
            io = io_in;
            rank = ParamRank.WORSTRANK;
        }

        private void calculateRank(bool best, Varnode basevn, PcodeOp ignoreop)
        {
            WalkState state;
            state.best = best;
            state.depth = 0;
            if (best) {
                rank = ParamRank.WORSTRANK;
                state.terminalrank = (io == ParamIDIO.INPUT)
                    ? ParamRank.DIRECTREAD
                    : ParamRank.DIRECTWRITEWITHOUTREAD;
            }
            else {
                rank = ParamRank.BESTRANK;
                state.terminalrank = ParamRank.INDIRECT;
            }
            numcalls = 0;
            if (io == ParamIDIO.INPUT)
                walkforward(state, ignoreop, basevn);
            else
                walkbackward(state, ignoreop, basevn);
        }

        private void encode(Sla.CORE.Encoder encoder, ElementId tag, bool moredetail)
        {
            encoder.openElement(tag);
            encoder.openElement(ElementId.ELEM_ADDR);
            vndata.space.encodeAttributes(encoder, vndata.offset, (int)vndata.size);
            encoder.closeElement(ElementId.ELEM_ADDR);
            vntype.encode(encoder);
            if (moredetail)
            {
                encoder.openElement(ElementId.ELEM_RANK);
                encoder.writeSignedInteger(AttributeId.ATTRIB_VAL, (int)rank);
                encoder.closeElement(ElementId.ELEM_RANK);
            }
            encoder.closeElement(tag);
        }

        private void savePretty(TextWriter s, bool moredetail)
        {
            s.WriteLine($"  Space: {vndata.space.getName()}");
            s.WriteLine($"  Space: {vndata.offset}");
            s.WriteLine($"  Space: {vndata.size}");
            s.WriteLine($"  Space: {rank}");
        }

        private ParamRank getMeasure() => rank;
    }
}
