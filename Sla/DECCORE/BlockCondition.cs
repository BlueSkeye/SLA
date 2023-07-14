using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.FlowBlock;

namespace ghidra
{
    /// \brief Two conditional blocks combined into one conditional using BOOL_AND or BOOL_OR
    /// This class is used to construct full conditional expressions.  An instance glues together
    /// two components, each with two outgoing edges. Of the four edges, 1 must go between the two
    /// components, and 2 must go to the same exit block, so there will be exactly 2 distinct exit
    /// blocks in total.  The new condition can be interpreted as either:
    ///   -  If condition one \b and condition two, goto exit 0, otherwise goto exit 1.
    ///   -  If condition one \b or condition two, goto exit 1, otherwise goto exit 0.
    /// depending on the boolean operation setting for the condition
    internal class BlockCondition : BlockGraph
    {
        /// Type of boolean operation
        private OpCode opc;

        /// Construct given the boolean operation
        public BlockCondition(OpCode c)
        {
            opc = c;
        }

        /// Get the boolean operation
        public OpCode getOpcode() => opc;

        public override block_type getType() => block_type.t_condition;

        public override void scopeBreak(int curexit, int curloopexit)
        {
            // No fixed exit
            getBlock(0).scopeBreak(-1, curloopexit);
            getBlock(1).scopeBreak(-1, curloopexit);
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Condition block(");
            s.Write((opc == CPUI_BOOL_AND) ? "&&" : "||");
            s.Write(") ");
            base.printHeader(s);
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockCondition(this);
        }

        public override bool negateCondition(bool toporbottom)
        {
            // Distribute the NOT
            bool res1 = getBlock(0).negateCondition(false);
            // to each side of condition
            bool res2 = getBlock(1).negateCondition(false);
            opc = (opc == CPUI_BOOL_AND) ? CPUI_BOOL_OR : CPUI_BOOL_AND;
            // Flip order of outofthis
            base.negateCondition(toporbottom);
            return (res1 || res2);
        }

        public override FlowBlock? getSplitPoint() => this;

        public override int flipInPlaceTest(List<PcodeOp> fliplist)
        {
            FlowBlock? split1 = getBlock(0).getSplitPoint();
            if (split1 == null) {
                return 2;
            }
            FlowBlock? split2 = getBlock(1).getSplitPoint();
            if (split2 == null) {
                return 2;
            }
            int subtest1 = split1.flipInPlaceTest(fliplist);
            if (subtest1 == 2) {
                return 2;
            }
            int subtest2 = split2.flipInPlaceTest(fliplist);
            if (subtest2 == 2) {
                return 2;
            }
            return subtest1;
        }

        public override void flipInPlaceExecute()
        {
            opc = (opc == CPUI_BOOL_AND) ? CPUI_BOOL_OR : CPUI_BOOL_AND;
            getBlock(0).getSplitPoint().flipInPlaceExecute();
            getBlock(1).getSplitPoint().flipInPlaceExecute();
        }

        public override PcodeOp lastOp()
        {
            // Is destination of condition reached
            // by an unstructured goto
            return getBlock(1).lastOp();
        }

        public override bool isComplex() => getBlock(0).isComplex();

        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            // Do not know where flow goes
            return null;
        }

        public override void encodeHeader(Encoder encoder)
        {
            base.encodeHeader(encoder);
            string nm = get_opname(opc);
            encoder.writeString(AttributeId.ATTRIB_OPCODE, nm);
        }
    }
}
