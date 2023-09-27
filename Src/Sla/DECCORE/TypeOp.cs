using Sla.CORE;
using Sla.DECCORE;

namespace Sla.DECCORE
{
    /// \brief Associate data-type and behavior information with a specific p-code op-code.
    ///
    /// This holds all information about a p-code op-code. The main PcodeOp object holds this
    /// as a representative of the op-code.  The evaluate* methods can be used to let the op-code
    /// act on constant input values. The getOutput* and getInput* methods are used to obtain
    /// data-type information that is specific to the op-code. This also holds other PcodeOp
    /// boolean properties that are set in common for the op-code.
    internal abstract class TypeOp
    {
        [Flags()]
        public enum OperationType
        {
            // Operator token inherits signedness from its inputs
            inherits_sign = 1,
            // Only inherits sign from first operand, not the second
            inherits_sign_zero = 2,
            // Shift operation
            shift_op = 4,
            // Operation involving addition, multiplication, or division
            arithmetic_op = 8,
            // Logical operation
            logical_op = 0x10,
            // Floating-point operation
            floatingpoint_op = 0x20
        }

        /// Pointer to data-type factory
        protected TypeFactory tlst;
        /// The op-code value
        protected OpCode opcode;
        /// Cached pcode-op properties for this op-code
        protected PcodeOp.Flags opflags;
        /// Additional properties
        protected OperationType addlflags;
        /// Symbol denoting this operation
        protected string name;
        /// Object for emulating the behavior of the op-code
        protected OpBehavior? behave;

        /// Set the data-type associated with inputs to this opcode
        protected virtual void setMetatypeIn(type_metatype val)
        {
        }

        /// Set the data-type associated with outputs of this opcode
        protected virtual void setMetatypeOut(type_metatype val)
        {
        }

        /// Set the display symbol associated with the op-code
        protected virtual void setSymbol(string nm)
        {
            name = nm;
        }

        // \param t is the TypeFactory used to construct data-types
        // \param opc is the op-code value the new object will represent
        // \param n is the display name that will represent the op-code
        public TypeOp(TypeFactory t, OpCode opc, string n)
        {
            tlst = t;
            opcode = opc;
            name = n;
            opflags = 0;
            addlflags = 0;
            behave = (OpBehavior)null;
        }

        ~TypeOp()
        {
            //if (behave != (OpBehavior)null)
            //    delete behave;
        }

        /// Get the display name of the op-code
        public string getName() => name;

        /// Get the op-code value
        public OpCode getOpcode() => opcode;

        /// Get the properties associated with the op-code
        public PcodeOp.Flags getFlags() => opflags;

        /// Get the behavior associated with the op-code
        public OpBehavior? getBehavior() => behave;

        /// \brief Emulate the unary op-code on an input value
        ///
        /// \param sizeout is the size of the output in bytes
        /// \param sizein is the size of the input in bytes
        /// \param in1 is the input value
        /// \return the output value
        public ulong evaluateUnary(int sizeout, int sizein, ulong in1) 
            => behave.evaluateUnary(sizeout, sizein, in1);

        /// \brief Emulate the binary op-code on an input value
        ///
        /// \param sizeout is the size of the output in bytes
        /// \param sizein is the size of the inputs in bytes
        /// \param in1 is the first input value
        /// \param in2 is the second input value
        /// \return the output value
        public ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
            => behave.evaluateBinary(sizeout, sizein, in1, in2);

        /// \brief Reverse the binary op-code operation, recovering a constant input value
        /// If the output value and one of the input values is known, recover the value
        /// of the other input.
        /// \param slot is the input slot to recover
        /// \param sizeout is the size of the output in bytes
        /// \param out is the output value
        /// \param sizein is the size of the inputs in bytes
        /// \param in is the known input value
        /// \return the input value corresponding to the \b slot
        public ulong recoverInputBinary(int slot, int sizeout, ulong @out, int sizein, ulong @in)
            => behave.recoverInputBinary(slot, sizeout, @out, sizein, @in);

        /// \brief Reverse the unary op-code operation, recovering a constant input value
        ///
        /// If the output value is known, recover the input value.
        /// \param sizeout is the size of the output in bytes
        /// \param out is the output value
        /// \param sizein is the size of the input in bytes
        /// \return the input value
        public ulong recoverInputUnary(int sizeout, ulong @out, int sizein) 
            => behave.recoverInputUnary(sizeout, @out, sizein);

        /// Return \b true if this op-code is commutative
        /// \return \b true if the ordering of the inputs does not affect the output
        public bool isCommutative()
        {
            return ((opflags & PcodeOp.Flags.commutative) != 0);
        }

        /// \brief Return \b true if the op-code inherits its signedness from its inputs
        public bool inheritsSign() => ((addlflags & OperationType.inherits_sign)!= 0);

        /// \brief Return \b true if the op-code inherits its signedness from only its first input
        public bool inheritsSignFirstParamOnly() => ((addlflags & OperationType.inherits_sign_zero)!= 0);

        /// \brief Return \b true if the op-code is a shift (INT_LEFT, INT_RIGHT, or INT_SRIGHT)
        public bool isShiftOp() => ((addlflags & OperationType.shift_op)!= 0);

        /// \brief Return \b true if the opcode is INT_ADD, INT_MULT, INT_DIV, INT_REM, or other arithmetic op
        public bool isArithmeticOp() => ((addlflags & OperationType.arithmetic_op)!= 0);

        /// \brief Return \b true if the opcode is INT_AND, INT_OR, INT_XOR, or other logical op
        public bool isLogicalOp() => ((addlflags & OperationType.logical_op)!= 0);

        /// \brief Return \b true if the opcode is FLOAT_ADD, FLOAT_MULT, or other floating-point operation
        public bool isFloatingPointOp() => ((addlflags & OperationType.floatingpoint_op)!= 0);

        /// \brief Find the minimal (or suggested) data-type of an output to \b this op-code
        /// The result should depend only on the op-code itself (and the size of the output)
        /// \param op is the PcodeOp being considered
        /// \return the data-type
        public virtual Datatype getOutputLocal(PcodeOp op)
        {
            // Default type lookup
            return tlst.getBase(op.getOut().getSize(), type_metatype.TYPE_UNKNOWN);
        }

        /// \brief Find the minimal (or suggested) data-type of an input to \b this op-code
        /// The result should depend only on the op-code itself (and the size of the input)
        /// \param op is the PcodeOp being considered
        /// \param slot is the input being considered
        /// \return the data-type
        public virtual Datatype getInputLocal(PcodeOp op, int slot)
        {
            // Default type lookup
            return tlst.getBase(op.getIn(slot).getSize(), type_metatype.TYPE_UNKNOWN);
        }

        /// \brief Find the data-type of the output that would be assigned by a compiler
        /// Calculate the actual data-type of the output for a specific PcodeOp
        /// as would be assigned by a C compiler parsing a grammar containing this op.
        /// \param op is the specific PcodeOp
        /// \param castStrategy is the current casting strategy
        /// \return the data-type
        public virtual Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return op.outputTypeLocal();
        }

        /// \brief Find the data-type of the input to a specific PcodeOp
        /// Calculate the actual data-type of the input to the specific PcodeOp.
        /// A \b null result indicates the input data-type is the same as
        /// or otherwise doesn't need a cast from the data-type of the actual input Varnode
        /// \param op is the specific PcodeOp
        /// \param slot is the input to consider
        /// \param castStrategy is the current casting strategy
        /// \return the data-type
        public virtual Datatype? getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Varnode vn = op.getIn(slot);
            if (vn.isAnnotation()) return (Datatype)null;
            Datatype reqtype = op.inputTypeLocal(slot);
            Datatype curtype = vn.getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, curtype, false, true);
        }

        /// \brief Propagate an incoming data-type across a specific PcodeOp
        /// The data-type can propagate between any two Varnodes attached to the PcodeOp, either in or out.
        /// The pair \b invn and \b inslot indicate the Varnode holding the \e incoming data-type.
        /// The pair \b outvn and \b outslot indicate the Varnode that will hold the \e outgoing data-type.
        /// The data-type for the outgoing Varnode is returned, which may be different then the incoming data-type
        /// as the PcodeOp can transform the data-type as it propagates.
        /// \param alttype is the incoming data-type
        /// \param op is the PcodeOp to propagate across
        /// \param invn is the Varnode holding the incoming data-type
        /// \param outvn is the Varnode that will hold the outgoing data-type
        /// \param inslot indicates how the incoming Varnode is attached to the PcodeOp (-1 indicates output >= indicates input)
        /// \param outslot indicates how the outgoing Varnode is attached to the PcodeOp
        /// \return the outgoing data-type or null (to indicate no propagation)
        public virtual Datatype? propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
                int inslot, int outslot)
        {
            return (Datatype)null;        // Don't propagate by default
        }

        /// \brief Push the specific PcodeOp to the emitter's RPN stack
        ///
        /// Given a specific language and PcodeOp, emit the expression rooted at the operation.
        /// \param lng is the PrintLanguage to emit
        /// \param op is the specific PcodeOp
        /// \param readOp is the PcodeOp consuming the output (or null)
        public abstract void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp);

        /// \brief Print (for debugging purposes) \b this specific PcodeOp to the stream
        ///
        /// \param s is the output stream
        /// \param op is the specific PcodeOp to print
        public abstract void printRaw(TextWriter s, PcodeOp op);

        /// \brief Get the name of the op-code as it should be displayed in context.
        ///
        /// Depending on the context, the same op-code may get displayed in different ways.
        /// \param op is the PcodeOp context
        /// \return the display token
        public virtual string getOperatorName(PcodeOp op) => name;

        /// \brief Build a map from op-code value to the TypeOp information objects
        /// \param inst will hold the array of TypeOp objects, indexed on op-code
        /// \param tlst is the corresponding TypeFactory for the Architecture
        /// \param trans is the Translate object for floating-point formats
        public static void registerInstructions(List<TypeOp> inst, TypeFactory tlst, Translate trans)
        {
            for(int index = 0; index < (int)OpCode.CPUI_MAX; index++) {
                inst.Add(null);
            }

            inst[(int)OpCode.CPUI_COPY] = new TypeOpCopy(tlst);
            inst[(int)OpCode.CPUI_LOAD] = new TypeOpLoad(tlst);
            inst[(int)OpCode.CPUI_STORE] = new TypeOpStore(tlst);
            inst[(int)OpCode.CPUI_BRANCH] = new TypeOpBranch(tlst);
            inst[(int)OpCode.CPUI_CBRANCH] = new TypeOpCbranch(tlst);
            inst[(int)OpCode.CPUI_BRANCHIND] = new TypeOpBranchind(tlst);
            inst[(int)OpCode.CPUI_CALL] = new TypeOpCall(tlst);
            inst[(int)OpCode.CPUI_CALLIND] = new TypeOpCallind(tlst);
            inst[(int)OpCode.CPUI_CALLOTHER] = new TypeOpCallother(tlst);
            inst[(int)OpCode.CPUI_RETURN] = new TypeOpReturn(tlst);

            inst[(int)OpCode.CPUI_MULTIEQUAL] = new TypeOpMulti(tlst);
            inst[(int)OpCode.CPUI_INDIRECT] = new TypeOpIndirect(tlst);

            inst[(int)OpCode.CPUI_PIECE] = new TypeOpPiece(tlst);
            inst[(int)OpCode.CPUI_SUBPIECE] = new TypeOpSubpiece(tlst);
            inst[(int)OpCode.CPUI_INT_EQUAL] = new TypeOpEqual(tlst);
            inst[(int)OpCode.CPUI_INT_NOTEQUAL] = new TypeOpNotEqual(tlst);
            inst[(int)OpCode.CPUI_INT_SLESS] = new TypeOpIntSless(tlst);
            inst[(int)OpCode.CPUI_INT_SLESSEQUAL] = new TypeOpIntSlessEqual(tlst);
            inst[(int)OpCode.CPUI_INT_LESS] = new TypeOpIntLess(tlst);
            inst[(int)OpCode.CPUI_INT_LESSEQUAL] = new TypeOpIntLessEqual(tlst);
            inst[(int)OpCode.CPUI_INT_ZEXT] = new TypeOpIntZext(tlst);
            inst[(int)OpCode.CPUI_INT_SEXT] = new TypeOpIntSext(tlst);
            inst[(int)OpCode.CPUI_INT_ADD] = new TypeOpIntAdd(tlst);
            inst[(int)OpCode.CPUI_INT_SUB] = new TypeOpIntSub(tlst);
            inst[(int)OpCode.CPUI_INT_CARRY] = new TypeOpIntCarry(tlst);
            inst[(int)OpCode.CPUI_INT_SCARRY] = new TypeOpIntScarry(tlst);
            inst[(int)OpCode.CPUI_INT_SBORROW] = new TypeOpIntSborrow(tlst);
            inst[(int)OpCode.CPUI_INT_2COMP] = new TypeOpInt2Comp(tlst);
            inst[(int)OpCode.CPUI_INT_NEGATE] = new TypeOpIntNegate(tlst);
            inst[(int)OpCode.CPUI_INT_XOR] = new TypeOpIntXor(tlst);
            inst[(int)OpCode.CPUI_INT_AND] = new TypeOpIntAnd(tlst);
            inst[(int)OpCode.CPUI_INT_OR] = new TypeOpIntOr(tlst);
            inst[(int)OpCode.CPUI_INT_LEFT] = new TypeOpIntLeft(tlst);
            inst[(int)OpCode.CPUI_INT_RIGHT] = new TypeOpIntRight(tlst);
            inst[(int)OpCode.CPUI_INT_SRIGHT] = new TypeOpIntSright(tlst);
            inst[(int)OpCode.CPUI_INT_MULT] = new TypeOpIntMult(tlst);
            inst[(int)OpCode.CPUI_INT_DIV] = new TypeOpIntDiv(tlst);
            inst[(int)OpCode.CPUI_INT_SDIV] = new TypeOpIntSdiv(tlst);
            inst[(int)OpCode.CPUI_INT_REM] = new TypeOpIntRem(tlst);
            inst[(int)OpCode.CPUI_INT_SREM] = new TypeOpIntSrem(tlst);

            inst[(int)OpCode.CPUI_BOOL_NEGATE] = new TypeOpBoolNegate(tlst);
            inst[(int)OpCode.CPUI_BOOL_XOR] = new TypeOpBoolXor(tlst);
            inst[(int)OpCode.CPUI_BOOL_AND] = new TypeOpBoolAnd(tlst);
            inst[(int)OpCode.CPUI_BOOL_OR] = new TypeOpBoolOr(tlst);

            inst[(int)OpCode.CPUI_CAST] = new TypeOpCast(tlst);
            inst[(int)OpCode.CPUI_PTRADD] = new TypeOpPtradd(tlst);
            inst[(int)OpCode.CPUI_PTRSUB] = new TypeOpPtrsub(tlst);

            inst[(int)OpCode.CPUI_FLOAT_EQUAL] = new TypeOpFloatEqual(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_NOTEQUAL] = new TypeOpFloatNotEqual(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_LESS] = new TypeOpFloatLess(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_LESSEQUAL] = new TypeOpFloatLessEqual(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_NAN] = new TypeOpFloatNan(tlst, trans);

            inst[(int)OpCode.CPUI_FLOAT_ADD] = new TypeOpFloatAdd(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_DIV] = new TypeOpFloatDiv(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_MULT] = new TypeOpFloatMult(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_SUB] = new TypeOpFloatSub(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_NEG] = new TypeOpFloatNeg(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_ABS] = new TypeOpFloatAbs(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_SQRT] = new TypeOpFloatSqrt(tlst, trans);

            inst[(int)OpCode.CPUI_FLOAT_INT2FLOAT] = new TypeOpFloatInt2Float(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_FLOAT2FLOAT] = new TypeOpFloatFloat2Float(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_TRUNC] = new TypeOpFloatTrunc(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_CEIL] = new TypeOpFloatCeil(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_FLOOR] = new TypeOpFloatFloor(tlst, trans);
            inst[(int)OpCode.CPUI_FLOAT_ROUND] = new TypeOpFloatRound(tlst, trans);
            inst[(int)OpCode.CPUI_SEGMENTOP] = new TypeOpSegment(tlst);
            inst[(int)OpCode.CPUI_CPOOLREF] = new TypeOpCpoolref(tlst);
            inst[(int)OpCode.CPUI_NEW] = new TypeOpNew(tlst);
            inst[(int)OpCode.CPUI_INSERT] = new TypeOpInsert(tlst);
            inst[(int)OpCode.CPUI_EXTRACT] = new TypeOpExtract(tlst);
            inst[(int)OpCode.CPUI_POPCOUNT] = new TypeOpPopcount(tlst);
            inst[(int)OpCode.CPUI_LZCOUNT] = new TypeOpLzcount(tlst);
        }

        /// \brief Toggle Java specific aspects of the op-code information
        /// Change basic data-type info (signed vs unsigned) and operator names ( '>>' vs '>>>' )
        /// depending on the specific language.
        /// \param inst is the array of TypeOp information objects
        /// \param val is set to \b true for Java operators, \b false for C operators
        public static void selectJavaOperators(List<TypeOp> inst, bool val)
        {
            if (val)
            {
                inst[(int)OpCode.CPUI_INT_ZEXT].setMetatypeIn(type_metatype.TYPE_UNKNOWN);
                inst[(int)OpCode.CPUI_INT_ZEXT].setMetatypeOut(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_NEGATE].setMetatypeIn(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_NEGATE].setMetatypeOut(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_XOR].setMetatypeIn(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_XOR].setMetatypeOut(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_OR].setMetatypeIn(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_OR].setMetatypeOut(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_AND].setMetatypeIn(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_AND].setMetatypeOut(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_RIGHT].setMetatypeIn(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_RIGHT].setMetatypeOut(type_metatype.TYPE_INT);
                inst[(int)OpCode.CPUI_INT_RIGHT].setSymbol(">>>");
            }
            else {
                inst[(int)OpCode.CPUI_INT_ZEXT].setMetatypeIn(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_ZEXT].setMetatypeOut(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_NEGATE].setMetatypeIn(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_NEGATE].setMetatypeOut(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_XOR].setMetatypeIn(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_XOR].setMetatypeOut(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_OR].setMetatypeIn(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_OR].setMetatypeOut(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_AND].setMetatypeIn(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_AND].setMetatypeOut(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_RIGHT].setMetatypeIn(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_RIGHT].setMetatypeOut(type_metatype.TYPE_UINT);
                inst[(int)OpCode.CPUI_INT_RIGHT].setSymbol(">>");
            }
        }
    }
}
