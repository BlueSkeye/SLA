using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief Class encapsulating the action/behavior of specific pcode opcodes
    ///
    /// At the lowest level, a pcode op is one of a small set of opcodes that
    /// operate on varnodes (address space, offset, size). Classes derived from
    /// this base class encapsulate this basic behavior for each possible opcode.
    /// These classes describe the most basic behaviors and include:
    ///    * uintb evaluateBinary(int4 sizeout,int4 sizein,uintb in1,uintb int2)
    ///    * uintb evaluateUnary(int4 sizeout,int4 sizein,uintb in1)
    ///    * uintb recoverInputBinary(int4 slo  t,int4 sizeout,uintb out,int4 sizein,uintb in)
    ///    * uintb recoverInputUnary(int4 sizeout,uintb out,int4 sizein)
    public class OpBehavior
    {
        /// the internal enumeration for pcode types
        private OpCode opcode;
        /// true= use unary interfaces,  false = use binary
        private bool isunary;
        /// Is op not a normal unary or binary op
        private bool isspecial;

        /// A behavior constructor
        /// This kind of OpBehavior is associated with a particular opcode and is either unary or binary
        /// \param opc is the opcode of the behavior
        /// \param isun is \b true if the behavior is unary, \b false if binary
        public OpBehavior(OpCode opc, bool isun)
        {
            opcode = opc;
            isunary = isun;
            isspecial = false;
        }

        /// A special behavior constructor
        /// This kind of OpBehavior can be set to \b special, if it neither unary or binary.
        /// \param opc is the opcode of the behavior
        /// \param isun is \b true if the behavior is unary
        /// \param isspec is \b true if the behavior is neither unary or binary
        public OpBehavior(OpCode opc, bool isun, bool isspec)
        {
            opcode = opc;
            isunary = isun;
            isspecial = isspec;
        }

        ~OpBehavior()
        {
        }

        /// \brief Get the opcode for this pcode operation
        /// There is an internal enumeration value for each type of pcode operation.
        /// This routine returns that value.
        /// \return the opcode value
        public OpCode getOpcode()
        {
            return opcode;
        }

        /// \brief Check if this is a special operator
        /// If this function returns false, the operation is a normal unary or binary operation
        /// which can be evaluated calling evaluateBinary() or evaluateUnary().
        /// Otherwise, the operation requires special handling to emulate properly
        public bool isSpecial()
        {
            return isspecial;
        }

        /// \brief Check if operator is unary
        /// The operated can either be evaluated as unary or binary
        /// \return \b true if the operator is unary
        public bool isUnary()
        {
            return isunary;
        }

        /// \brief Emulate the unary op-code on an input value
        /// \param sizeout is the size of the output in bytes
        /// \param sizein is the size of the input in bytes
        /// \param in1 is the input value
        /// \return the output value
        public virtual ulong evaluateUnary(int sizeout, int sizein, ulong in1)
        {
            throw new LowlevelError(
                $"Unary emulation unimplemented for {Globals.get_opname(opcode)}");
        }

        /// \brief Emulate the binary op-code on input values
        /// \param sizeout is the size of the output in bytes
        /// \param sizein is the size of the inputs in bytes
        /// \param in1 is the first input value
        /// \param in2 is the second input value
        /// \return the output value
        public virtual ulong evaluateBinary(int sizeout, int sizein, ulong in1,
            ulong in2)
        {
            throw new LowlevelError(
                $"Binary emulation unimplemented for {Globals.get_opname(opcode)}");
        }

        /// \brief Reverse the binary op-code operation, recovering an input value
        /// If the output value is known, recover the input value.
        /// \param sizeout is the size of the output in bytes
        /// \param out is the output value
        /// \param sizein is the size of the input in bytes
        /// \return the input value
        public virtual ulong recoverInputBinary(int slot, int sizeout, ulong @out,
            int sizein, ulong @in)
        {
            throw new LowlevelError(
                "Cannot recover input parameter without loss of information");
        }

        /// \brief Reverse the unary op-code operation, recovering the input value
        /// If the output value and one of the input values is known, recover the value
        /// of the other input.
        /// \param slot is the input slot to recover
        /// \param sizeout is the size of the output in bytes
        /// \param out is the output value
        /// \param sizein is the size of the inputs in bytes
        /// \param in is the known input value
        /// \return the input value corresponding to the \b slot
        public virtual ulong recoverInputUnary(int sizeout, ulong @out, int sizein)
        {
            throw new LowlevelError(
                "Cannot recover input parameter without loss of information");
        }

        /// Build all pcode behaviors
        /// This routine generates a List of OpBehavior objects indexed by opcode
        /// \param inst is the List of behaviors to be filled
        /// \param trans is the translator object needed by the floating point behaviors
        public static void registerInstructions(ref List<OpBehavior> inst,
            Translate trans)
        {
            throw new NotImplementedException();
            //inst.insert(inst.end(),CPUI_MAX,(OpBehavior*)0);

            //inst[CPUI_COPY] = new OpBehaviorCopy();
            //inst[CPUI_LOAD] = new OpBehavior(CPUI_LOAD,false,true);
            //inst[CPUI_STORE] = new OpBehavior(CPUI_STORE,false,true);
            //inst[CPUI_BRANCH] = new OpBehavior(CPUI_BRANCH,false,true);
            //inst[CPUI_CBRANCH] = new OpBehavior(CPUI_CBRANCH,false,true);
            //inst[CPUI_BRANCHIND] = new OpBehavior(CPUI_BRANCHIND,false,true);
            //inst[CPUI_CALL] = new OpBehavior(CPUI_CALL,false,true);
            //inst[CPUI_CALLIND] = new OpBehavior(CPUI_CALLIND,false,true);
            //inst[CPUI_CALLOTHER] = new OpBehavior(CPUI_CALLOTHER,false,true);
            //inst[CPUI_RETURN] = new OpBehavior(CPUI_RETURN,false,true);

            //inst[CPUI_MULTIEQUAL] = new OpBehavior(CPUI_MULTIEQUAL,false,true);
            //inst[CPUI_INDIRECT] = new OpBehavior(CPUI_INDIRECT,false,true);

            //inst[CPUI_PIECE] = new OpBehaviorPiece();
            //inst[CPUI_SUBPIECE] = new OpBehaviorSubpiece();
            //inst[CPUI_INT_EQUAL] = new OpBehaviorEqual();
            //inst[CPUI_INT_NOTEQUAL] = new OpBehaviorNotEqual();
            //inst[CPUI_INT_SLESS] = new OpBehaviorIntSless();
            //inst[CPUI_INT_SLESSEQUAL] = new OpBehaviorIntSlessEqual();
            //inst[CPUI_INT_LESS] = new OpBehaviorIntLess();
            //inst[CPUI_INT_LESSEQUAL] = new OpBehaviorIntLessEqual();
            //inst[CPUI_INT_ZEXT] = new OpBehaviorIntZext();
            //inst[CPUI_INT_SEXT] = new OpBehaviorIntSext();
            //inst[CPUI_INT_ADD] = new OpBehaviorIntAdd();
            //inst[CPUI_INT_SUB] = new OpBehaviorIntSub();
            //inst[CPUI_INT_CARRY] = new OpBehaviorIntCarry();
            //inst[CPUI_INT_SCARRY] = new OpBehaviorIntScarry();
            //inst[CPUI_INT_SBORROW] = new OpBehaviorIntSborrow();
            //inst[CPUI_INT_2COMP] = new OpBehaviorInt2Comp();
            //inst[CPUI_INT_NEGATE] = new OpBehaviorIntNegate();
            //inst[CPUI_INT_XOR] = new OpBehaviorIntXor();
            //inst[CPUI_INT_AND] = new OpBehaviorIntAnd();
            //inst[CPUI_INT_OR] = new OpBehaviorIntOr();
            //inst[CPUI_INT_LEFT] = new OpBehaviorIntLeft();
            //inst[CPUI_INT_RIGHT] = new OpBehaviorIntRight();
            //inst[CPUI_INT_SRIGHT] = new OpBehaviorIntSright();
            //inst[CPUI_INT_MULT] = new OpBehaviorIntMult();
            //inst[CPUI_INT_DIV] = new OpBehaviorIntDiv();
            //inst[CPUI_INT_SDIV] = new OpBehaviorIntSdiv();
            //inst[CPUI_INT_REM] = new OpBehaviorIntRem();
            //inst[CPUI_INT_SREM] = new OpBehaviorIntSrem();

            //inst[CPUI_BOOL_NEGATE] = new OpBehaviorBoolNegate();
            //inst[CPUI_BOOL_XOR] = new OpBehaviorBoolXor();
            //inst[CPUI_BOOL_AND] = new OpBehaviorBoolAnd();
            //inst[CPUI_BOOL_OR] = new OpBehaviorBoolOr();

            //inst[CPUI_CAST] = new OpBehavior(CPUI_CAST,false,true);
            //inst[CPUI_PTRADD] = new OpBehavior(CPUI_PTRADD,false);
            //inst[CPUI_PTRSUB] = new OpBehavior(CPUI_PTRSUB,false);

            //inst[CPUI_FLOAT_EQUAL] = new OpBehaviorFloatEqual(trans);
            //inst[CPUI_FLOAT_NOTEQUAL] = new OpBehaviorFloatNotEqual(trans);
            //inst[CPUI_FLOAT_LESS] = new OpBehaviorFloatLess(trans);
            //inst[CPUI_FLOAT_LESSEQUAL] = new OpBehaviorFloatLessEqual(trans);
            //inst[CPUI_FLOAT_NAN] = new OpBehaviorFloatNan(trans);

            //inst[CPUI_FLOAT_ADD] = new OpBehaviorFloatAdd(trans);
            //inst[CPUI_FLOAT_DIV] = new OpBehaviorFloatDiv(trans);
            //inst[CPUI_FLOAT_MULT] = new OpBehaviorFloatMult(trans);
            //inst[CPUI_FLOAT_SUB] = new OpBehaviorFloatSub(trans);
            //inst[CPUI_FLOAT_NEG] = new OpBehaviorFloatNeg(trans);
            //inst[CPUI_FLOAT_ABS] = new OpBehaviorFloatAbs(trans);
            //inst[CPUI_FLOAT_SQRT] = new OpBehaviorFloatSqrt(trans);

            //inst[CPUI_FLOAT_INT2FLOAT] = new OpBehaviorFloatInt2Float(trans);
            //inst[CPUI_FLOAT_FLOAT2FLOAT] = new OpBehaviorFloatFloat2Float(trans);
            //inst[CPUI_FLOAT_TRUNC] = new OpBehaviorFloatTrunc(trans);
            //inst[CPUI_FLOAT_CEIL] = new OpBehaviorFloatCeil(trans);
            //inst[CPUI_FLOAT_FLOOR] = new OpBehaviorFloatFloor(trans);
            //inst[CPUI_FLOAT_ROUND] = new OpBehaviorFloatRound(trans);
            //inst[CPUI_SEGMENTOP] = new OpBehavior(CPUI_SEGMENTOP,false,true);
            //inst[CPUI_CPOOLREF] = new OpBehavior(CPUI_CPOOLREF,false,true);
            //inst[CPUI_NEW] = new OpBehavior(CPUI_NEW,false,true);
            //inst[CPUI_INSERT] = new OpBehavior(CPUI_INSERT,false);
            //inst[CPUI_EXTRACT] = new OpBehavior(CPUI_EXTRACT,false);
            //inst[CPUI_POPCOUNT] = new OpBehaviorPopcount();
            //inst[CPUI_LZCOUNT] = new OpBehaviorLzcount();
        }
    }
}
