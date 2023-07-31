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
    ///    * ulong evaluateBinary(int sizeout,int sizein,ulong in1,ulong short)
    ///    * ulong evaluateUnary(int sizeout,int sizein,ulong in1)
    ///    * ulong recoverInputBinary(int slo  t,int sizeout,ulong out,int sizein,ulong in)
    ///    * ulong recoverInputUnary(int sizeout,ulong out,int sizein)
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
        public bool isSpecial() => isspecial;

        /// \brief Check if operator is unary
        /// The operated can either be evaluated as unary or binary
        /// \return \b true if the operator is unary
        public bool isUnary() => isunary;

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
        public static void registerInstructions(List<OpBehavior> inst, Translate trans)
        {
            while(inst.Count < (int)OpCode.CPUI_MAX) {
                inst.Add(null);
            }

            inst[(int)OpCode.CPUI_COPY] = new OpBehaviorCopy();
            inst[(int)OpCode.CPUI_LOAD] = new OpBehavior(OpCode.CPUI_LOAD, false, true);
            inst[(int)OpCode.CPUI_STORE] = new OpBehavior(OpCode.CPUI_STORE, false, true);
            inst[(int)OpCode.CPUI_BRANCH] = new OpBehavior(OpCode.CPUI_BRANCH, false, true);
            inst[(int)OpCode.CPUI_CBRANCH] = new OpBehavior(OpCode.CPUI_CBRANCH, false, true);
            inst[(int)OpCode.CPUI_BRANCHIND] = new OpBehavior(OpCode.CPUI_BRANCHIND, false, true);
            inst[(int)OpCode.CPUI_CALL] = new OpBehavior(OpCode.CPUI_CALL, false, true);
            inst[(int)OpCode.CPUI_CALLIND] = new OpBehavior(OpCode.CPUI_CALLIND, false, true);
            inst[(int)OpCode.CPUI_CALLOTHER] = new OpBehavior(OpCode.CPUI_CALLOTHER, false, true);
            inst[(int)OpCode.CPUI_RETURN] = new OpBehavior(OpCode.CPUI_RETURN, false, true);

            inst[(int)OpCode.CPUI_MULTIEQUAL] = new OpBehavior(OpCode.CPUI_MULTIEQUAL, false, true);
            inst[(int)OpCode.CPUI_INDIRECT] = new OpBehavior(OpCode.CPUI_INDIRECT, false, true);

            inst[(int)OpCode.CPUI_PIECE] = new OpBehaviorPiece();
            inst[(int)OpCode.CPUI_SUBPIECE] = new OpBehaviorSubpiece();
            inst[(int)OpCode.CPUI_INT_EQUAL] = new OpBehaviorEqual();
            inst[(int)OpCode.CPUI_INT_NOTEQUAL] = new OpBehaviorNotEqual();
            inst[(int)OpCode.CPUI_INT_SLESS] = new OpBehaviorIntSless();
            inst[(int)OpCode.CPUI_INT_SLESSEQUAL] = new OpBehaviorIntSlessEqual();
            inst[(int)OpCode.CPUI_INT_LESS] = new OpBehaviorIntLess();
            inst[(int)OpCode.CPUI_INT_LESSEQUAL] = new OpBehaviorIntLessEqual();
            inst[(int)OpCode.CPUI_INT_ZEXT] = new OpBehaviorIntZext();
            inst[(int)OpCode.CPUI_INT_SEXT] = new OpBehaviorIntSext();
            inst[(int)OpCode.CPUI_INT_ADD] = new OpBehaviorIntAdd();
            inst[(int)OpCode.CPUI_INT_SUB] = new OpBehaviorIntSub();
            inst[(int)OpCode.CPUI_INT_CARRY] = new OpBehaviorIntCarry();
            inst[(int)OpCode.CPUI_INT_SCARRY] = new OpBehaviorIntScarry();
            inst[(int)OpCode.CPUI_INT_SBORROW] = new OpBehaviorIntSborrow();
            inst[(int)OpCode.CPUI_INT_2COMP] = new OpBehaviorInt2Comp();
            inst[(int)OpCode.CPUI_INT_NEGATE] = new OpBehaviorIntNegate();
            inst[(int)OpCode.CPUI_INT_XOR] = new OpBehaviorIntXor();
            inst[(int)OpCode.CPUI_INT_AND] = new OpBehaviorIntAnd();
            inst[(int)OpCode.CPUI_INT_OR] = new OpBehaviorIntOr();
            inst[(int)OpCode.CPUI_INT_LEFT] = new OpBehaviorIntLeft();
            inst[(int)OpCode.CPUI_INT_RIGHT] = new OpBehaviorIntRight();
            inst[(int)OpCode.CPUI_INT_SRIGHT] = new OpBehaviorIntSright();
            inst[(int)OpCode.CPUI_INT_MULT] = new OpBehaviorIntMult();
            inst[(int)OpCode.CPUI_INT_DIV] = new OpBehaviorIntDiv();
            inst[(int)OpCode.CPUI_INT_SDIV] = new OpBehaviorIntSdiv();
            inst[(int)OpCode.CPUI_INT_REM] = new OpBehaviorIntRem();
            inst[(int)OpCode.CPUI_INT_SREM] = new OpBehaviorIntSrem();

            inst[(int)OpCode.CPUI_BOOL_NEGATE] = new OpBehaviorBoolNegate();
            inst[(int)OpCode.CPUI_BOOL_XOR] = new OpBehaviorBoolXor();
            inst[(int)OpCode.CPUI_BOOL_AND] = new OpBehaviorBoolAnd();
            inst[(int)OpCode.CPUI_BOOL_OR] = new OpBehaviorBoolOr();

            inst[(int)OpCode.CPUI_CAST] = new OpBehavior(OpCode.CPUI_CAST, false, true);
            inst[(int)OpCode.CPUI_PTRADD] = new OpBehavior(OpCode.CPUI_PTRADD, false);
            inst[(int)OpCode.CPUI_PTRSUB] = new OpBehavior(OpCode.CPUI_PTRSUB, false);

            inst[(int)OpCode.CPUI_FLOAT_EQUAL] = new OpBehaviorFloatEqual(trans);
            inst[(int)OpCode.CPUI_FLOAT_NOTEQUAL] = new OpBehaviorFloatNotEqual(trans);
            inst[(int)OpCode.CPUI_FLOAT_LESS] = new OpBehaviorFloatLess(trans);
            inst[(int)OpCode.CPUI_FLOAT_LESSEQUAL] = new OpBehaviorFloatLessEqual(trans);
            inst[(int)OpCode.CPUI_FLOAT_NAN] = new OpBehaviorFloatNan(trans);

            inst[(int)OpCode.CPUI_FLOAT_ADD] = new OpBehaviorFloatAdd(trans);
            inst[(int)OpCode.CPUI_FLOAT_DIV] = new OpBehaviorFloatDiv(trans);
            inst[(int)OpCode.CPUI_FLOAT_MULT] = new OpBehaviorFloatMult(trans);
            inst[(int)OpCode.CPUI_FLOAT_SUB] = new OpBehaviorFloatSub(trans);
            inst[(int)OpCode.CPUI_FLOAT_NEG] = new OpBehaviorFloatNeg(trans);
            inst[(int)OpCode.CPUI_FLOAT_ABS] = new OpBehaviorFloatAbs(trans);
            inst[(int)OpCode.CPUI_FLOAT_SQRT] = new OpBehaviorFloatSqrt(trans);

            inst[(int)OpCode.CPUI_FLOAT_INT2FLOAT] = new OpBehaviorFloatInt2Float(trans);
            inst[(int)OpCode.CPUI_FLOAT_FLOAT2FLOAT] = new OpBehaviorFloatFloat2Float(trans);
            inst[(int)OpCode.CPUI_FLOAT_TRUNC] = new OpBehaviorFloatTrunc(trans);
            inst[(int)OpCode.CPUI_FLOAT_CEIL] = new OpBehaviorFloatCeil(trans);
            inst[(int)OpCode.CPUI_FLOAT_FLOOR] = new OpBehaviorFloatFloor(trans);
            inst[(int)OpCode.CPUI_FLOAT_ROUND] = new OpBehaviorFloatRound(trans);
            inst[(int)OpCode.CPUI_SEGMENTOP] = new OpBehavior(OpCode.CPUI_SEGMENTOP, false, true);
            inst[(int)OpCode.CPUI_CPOOLREF] = new OpBehavior(OpCode.CPUI_CPOOLREF, false, true);
            inst[(int)OpCode.CPUI_NEW] = new OpBehavior(OpCode.CPUI_NEW, false, true);
            inst[(int)OpCode.CPUI_INSERT] = new OpBehavior(OpCode.CPUI_INSERT, false);
            inst[(int)OpCode.CPUI_EXTRACT] = new OpBehavior(OpCode.CPUI_EXTRACT, false);
            inst[(int)OpCode.CPUI_POPCOUNT] = new OpBehaviorPopcount();
            inst[(int)OpCode.CPUI_LZCOUNT] = new OpBehaviorLzcount();
        }
    }
}
