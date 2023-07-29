﻿using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief Derive Varnode sizes and optimize p-code in SLEIGH Constructors
    ///
    /// This class examines p-code parsed from a SLEIGH file and performs three main tasks:
    ///   - Enforcing size rules in Constructor p-code,
    ///   - Optimizing p-code within a Constructor, and
    ///   - Searching for other p-code validity violations
    ///
    /// Many p-code operators require that their input and/or output operands are all the same size
    /// or have other specific size restrictions on their operands.  This class enforces those requirements.
    ///
    /// This class performs limited optimization of p-code within a Constructor by performing COPY
    /// propagation through \e temporary registers.
    ///
    /// This class searches for unnecessary truncations and extensions, temporary varnodes that are either dead,
    /// read before written, or that exceed the standard allocation size.
    internal class ConsistencyChecker
    {
        /// \brief Description of how a temporary register is being used within a Constructor
        ///
        /// This counts reads and writes of the register.  If the register is read only once, the
        /// particular p-code op and input slot reading it is recorded.  If the register is written
        /// only once, the particular p-code op writing it is recorded.
        private struct OptimizeRecord
        {
            internal int4 writeop;       ///< Index of the (last) p-code op writing to register (or -1)
            internal int4 readop;        ///< Index of the (last) p-code op reading the register (or -1)
            internal int4 inslot;        ///< Input slot of p-code op reading the register (or -1)
            internal int4 writecount;        ///< Number of times the register is written
            internal int4 readcount;     ///< Number of times the register is read
            internal int4 writesection;      ///< Section containing (last) p-code op writing to the register (or -2)
            internal int4 readsection;       ///< Section containing (last) p-code op reading the register (or -2)
            internal /*mutable*/ int4 opttype;  ///< 0 = register read by a COPY, 1 = register written by a COPY (-1 otherwise)

            /// \brief Construct a record, initializing counts
            internal OptimizeRecord()
            {
                writeop = -1; readop = -1; inslot = -1; writecount = 0; readcount = 0; writesection = -2; readsection = -2; opttype = -1;
            }
        }
        
        private SleighCompile compiler;    ///< Parsed form of the SLEIGH file being examined
        private int4 unnecessarypcode;  ///< Count of unnecessary extension/truncation operations
        private int4 readnowrite;       ///< Count of temporary registers that are read but not written
        private int4 writenoread;       ///< Count of temporary registers that are written but not read
        private int4 largetemp;     ///< Count of temporary registers that are too large
        private bool printextwarning;       ///< Set to \b true if warning emitted for each unnecessary truncation/extension
        private bool printdeadwarning;  ///< Set to \b true if warning emitted for each written but not read temporary
        private bool printlargetempwarning; ///< Set to \b true if warning emitted for each too large temporary
        private SubtableSymbol root_symbol;    ///< The root symbol table for the parsed SLEIGH file
        private List<SubtableSymbol> postorder;  ///< Subtables sorted into \e post order (dependent tables listed earlier)
        private Dictionary<SubtableSymbol, int4> sizemap; ///< Sizes associated with table \e exports

        /// \brief Get the OperandSymbol associated with an input/output Varnode of the given p-code operator
        ///
        /// Find the Constructor operand associated with a specified Varnode, if it exists.
        /// The Varnode is specified by the p-code operator using it and the input \e slot index, with -1
        /// indicating the operator's output Varnode.  Not all Varnode's are associated with a
        /// Constructor operand, in which case \e null is returned.
        /// \param slot is the input \e slot index, or -1 for an output Varnode
        /// \param op is the p-code operator using the Varnode
        /// \param ct is the Constructor containing the p-code and operands
        /// \return the associated operand or null
        private OperandSymbol getOperandSymbol(int4 slot, OpTpl op, Constructor ct)
        {
            VarnodeTpl* vn;
            OperandSymbol* opsym;
            int4 handindex;

            if (slot == -1)
                vn = op.getOut();
            else
                vn = op.getIn(slot);

            switch (vn.getSize().getType())
            {
                case ConstTpl::handle:
                    handindex = vn.getSize().getHandleIndex();
                    opsym = ct.getOperand(handindex);
                    break;
                default:
                    opsym = (OperandSymbol*)0;
                    break;
            }
            return opsym;
        }

        /// \brief Print the name of a p-code operator (for warning and error messages)
        ///
        /// Print the full name of the operator with its syntax token in parentheses.
        /// \param s is the output stream to write to
        /// \param op is the operator to print
        private void printOpName(TextWriter s, OpTpl op)
        {
            switch (op.getOpcode())
            {
                case CPUI_COPY:
                    s << "Copy(=)";
                    break;
                case CPUI_LOAD:
                    s << "Load(*)";
                    break;
                case CPUI_STORE:
                    s << "Store(*)";
                    break;
                case CPUI_BRANCH:
                    s << "Branch(goto)";
                    break;
                case CPUI_CBRANCH:
                    s << "Conditional branch(if)";
                    break;
                case CPUI_BRANCHIND:
                    s << "Indirect branch(goto[])";
                    break;
                case CPUI_CALL:
                    s << "Call";
                    break;
                case CPUI_CALLIND:
                    s << "Indirect Call";
                    break;
                case CPUI_CALLOTHER:
                    s << "User defined";
                    break;
                case CPUI_RETURN:
                    s << "Return";
                    break;
                case CPUI_INT_EQUAL:
                    s << "Equality(==)";
                    break;
                case CPUI_INT_NOTEQUAL:
                    s << "Notequal(!=)";
                    break;
                case CPUI_INT_SLESS:
                    s << "Signed less than(s<)";
                    break;
                case CPUI_INT_SLESSEQUAL:
                    s << "Signed less than or equal(s<=)";
                    break;
                case CPUI_INT_LESS:
                    s << "Less than(<)";
                    break;
                case CPUI_INT_LESSEQUAL:
                    s << "Less than or equal(<=)";
                    break;
                case CPUI_INT_ZEXT:
                    s << "Zero extension(zext)";
                    break;
                case CPUI_INT_SEXT:
                    s << "Signed extension(sext)";
                    break;
                case CPUI_INT_ADD:
                    s << "Addition(+)";
                    break;
                case CPUI_INT_SUB:
                    s << "Subtraction(-)";
                    break;
                case CPUI_INT_CARRY:
                    s << "Carry";
                    break;
                case CPUI_INT_SCARRY:
                    s << "Signed carry";
                    break;
                case CPUI_INT_SBORROW:
                    s << "Signed borrow";
                    break;
                case CPUI_INT_2COMP:
                    s << "Twos complement(-)";
                    break;
                case CPUI_INT_NEGATE:
                    s << "Negate(~)";
                    break;
                case CPUI_INT_XOR:
                    s << "Exclusive or(^)";
                    break;
                case CPUI_INT_AND:
                    s << "And(&)";
                    break;
                case CPUI_INT_OR:
                    s << "Or(|)";
                    break;
                case CPUI_INT_LEFT:
                    s << "Left shift(<<)";
                    break;
                case CPUI_INT_RIGHT:
                    s << "Right shift(>>)";
                    break;
                case CPUI_INT_SRIGHT:
                    s << "Signed right shift(s>>)";
                    break;
                case CPUI_INT_MULT:
                    s << "Multiplication(*)";
                    break;
                case CPUI_INT_DIV:
                    s << "Division(/)";
                    break;
                case CPUI_INT_SDIV:
                    s << "Signed division(s/)";
                    break;
                case CPUI_INT_REM:
                    s << "Remainder(%)";
                    break;
                case CPUI_INT_SREM:
                    s << "Signed remainder(s%)";
                    break;
                case CPUI_BOOL_NEGATE:
                    s << "Boolean negate(!)";
                    break;
                case CPUI_BOOL_XOR:
                    s << "Boolean xor(^^)";
                    break;
                case CPUI_BOOL_AND:
                    s << "Boolean and(&&)";
                    break;
                case CPUI_BOOL_OR:
                    s << "Boolean or(||)";
                    break;
                case CPUI_FLOAT_EQUAL:
                    s << "Float equal(f==)";
                    break;
                case CPUI_FLOAT_NOTEQUAL:
                    s << "Float notequal(f!=)";
                    break;
                case CPUI_FLOAT_LESS:
                    s << "Float less than(f<)";
                    break;
                case CPUI_FLOAT_LESSEQUAL:
                    s << "Float less than or equal(f<=)";
                    break;
                case CPUI_FLOAT_NAN:
                    s << "Not a number(nan)";
                    break;
                case CPUI_FLOAT_ADD:
                    s << "Float addition(f+)";
                    break;
                case CPUI_FLOAT_DIV:
                    s << "Float division(f/)";
                    break;
                case CPUI_FLOAT_MULT:
                    s << "Float multiplication(f*)";
                    break;
                case CPUI_FLOAT_SUB:
                    s << "Float subtractions(f-)";
                    break;
                case CPUI_FLOAT_NEG:
                    s << "Float minus(f-)";
                    break;
                case CPUI_FLOAT_ABS:
                    s << "Absolute value(abs)";
                    break;
                case CPUI_FLOAT_SQRT:
                    s << "Square root";
                    break;
                case CPUI_FLOAT_INT2FLOAT:
                    s << "Integer to float conversion(int2float)";
                    break;
                case CPUI_FLOAT_FLOAT2FLOAT:
                    s << "Float to float conversion(float2float)";
                    break;
                case CPUI_FLOAT_TRUNC:
                    s << "Float truncation(trunc)";
                    break;
                case CPUI_FLOAT_CEIL:
                    s << "Ceiling(ceil)";
                    break;
                case CPUI_FLOAT_FLOOR:
                    s << "Floor";
                    break;
                case CPUI_FLOAT_ROUND:
                    s << "Round";
                    break;
                case CPUI_MULTIEQUAL:
                    s << "Build";
                    break;
                case CPUI_INDIRECT:
                    s << "Delay";
                    break;
                case CPUI_SUBPIECE:
                    s << "Truncation(:)";
                    break;
                case CPUI_SEGMENTOP:
                    s << "Segment table(segment)";
                    break;
                case CPUI_CPOOLREF:
                    s << "Constant Pool(cpool)";
                    break;
                case CPUI_NEW:
                    s << "New object(newobject)";
                    break;
                case CPUI_POPCOUNT:
                    s << "Count bits(popcount)";
                    break;
                case CPUI_LZCOUNT:
                    s << "Count leading zero bits(lzcount)";
                    break;
                default:
                    break;
            }
        }

        /// \brief Print an error message describing a size restriction violation
        ///
        /// The given p-code operator is assumed to violate the Varnode size rules for its opcode.
        /// If the violation is for two Varnodes that should be the same size, each Varnode is indicated
        /// as an input \e slot index, where -1 indicates the operator's output Varnode.
        /// If the violation is for a single Varnode, its \e slot index is passed in twice.
        /// \param op is the given p-code operator
        /// \param ct is the containing Constructor
        /// \param err1 is the slot of the first violating Varnode
        /// \param err2 is the slot of the second violating Varnode (or equal to \b err1)
        /// \param msg is additional description that is appended to the error message
        private void printOpError(OpTpl op, Constructor ct, int4 err1, int4 err2, string message)
        {
            SubtableSymbol* sym = ct.getParent();
            OperandSymbol* op1,*op2;

            op1 = getOperandSymbol(err1, op, ct);
            if (err2 != err1)
                op2 = getOperandSymbol(err2, op, ct);
            else
                op2 = (OperandSymbol*)0;

            ostringstream msgBuilder;

            msgBuilder << "Size restriction error in table '" << sym.getName() << "'" << endl;
            if ((op1 != (OperandSymbol*)0) && (op2 != (OperandSymbol*)0))
                msgBuilder << "  Problem with operands '" << op1.getName() << "' and '" << op2.getName() << "'";
            else if (op1 != (OperandSymbol*)0)
                msgBuilder << "  Problem with operand 1 '" << op1.getName() << "'";
            else if (op2 != (OperandSymbol*)0)
                msgBuilder << "  Problem with operand 2 '" << op2.getName() << "'";
            else
                msgBuilder << "  Problem";
            msgBuilder << " in ";
            printOpName(msgBuilder, op);
            msgBuilder << " operator" << endl << "  " << msg;

            compiler.reportError(compiler.getLocation(ct), msgBuilder.str());
        }

        /// \brief Recover a specific value for the size associated with a Varnode template
        ///
        /// This method is passed a ConstTpl that is assumed to be the \e size attribute of
        /// a VarnodeTpl (as returned by getSize()).  This method recovers the specific
        /// integer value for this constant template or throws an exception.
        /// The integer value can either be immediately available from parsing, derived
        /// from a Constructor operand symbol whose size is known, or taken from
        /// the calculated export size of a subtable symbol.
        /// \param sizeconst is the Varnode size template
        /// \param ct is the Constructor containing the Varnode
        /// \return the integer value
        private int4 recoverSize(ConstTpl sizeconst,Constructor ct)
        {
            int4 size, handindex;
            OperandSymbol* opsym;
            SubtableSymbol* tabsym;
            map<SubtableSymbol*, int4>::const_iterator iter;

            switch (sizeconst.getType())
            {
                case ConstTpl::real:
                    size = (int4)sizeconst.getReal();
                    break;
                case ConstTpl::handle:
                    handindex = sizeconst.getHandleIndex();
                    opsym = ct.getOperand(handindex);
                    size = opsym.getSize();
                    if (size == -1)
                    {
                        tabsym = dynamic_cast<SubtableSymbol*>(opsym.getDefiningSymbol());
                        if (tabsym == (SubtableSymbol*)0)
                            throw SleighError("Could not recover varnode template size");
                        iter = sizemap.find(tabsym);
                        if (iter == sizemap.end())
                            throw SleighError("Subtable out of order");
                        size = (*iter).second;
                    }
                    break;
                default:
                    throw SleighError("Bad constant type as varnode template size");
            }
            return size;
        }

        /// \brief Check for misuse of the given operator and print a warning
        ///
        /// This method currently checks for:
        ///   - Unsigned less-than comparison with zero
        ///
        /// \param op is the given operator
        /// \param ct is the Constructor owning the operator
        /// \return \b false if the operator is fatally misused
        private bool checkOpMisuse(OpTplop, Constructor ct)
        {
            switch (op.getOpcode())
            {
                case CPUI_INT_LESS:
                    {
                        VarnodeTpl* vn = op.getIn(1);
                        if (vn.getSpace().isConstSpace() && vn.getOffset().isZero())
                        {
                            compiler.reportWarning(compiler.getLocation(ct), "Unsigned comparison with zero is always false");
                        }
                    }
                    break;
                default:
                    break;
            }
            return true;
        }

        /// \brief Make sure the given operator meets size restrictions
        ///
        /// Many SLEIGH operators require that inputs and/or outputs are the
        /// same size, or they have other specific size requirement.
        /// Print an error and return \b false for any violations.
        /// \param op is the given p-code operator
        /// \param ct is the Constructor owning the operator
        /// \return \b true if there are no size restriction violations
        private bool sizeRestriction(OpTpl op, Constructor ct)
        { // Make sure op template meets size restrictions
          // Return false and any info about mismatched sizes
            int4 vnout, vn0, vn1;
            AddrSpace* spc;

            switch (op.getOpcode())
            {
                case CPUI_COPY:         // Instructions where all inputs and output are same size
                case CPUI_INT_2COMP:
                case CPUI_INT_NEGATE:
                case CPUI_FLOAT_NEG:
                case CPUI_FLOAT_ABS:
                case CPUI_FLOAT_SQRT:
                case CPUI_FLOAT_CEIL:
                case CPUI_FLOAT_FLOOR:
                case CPUI_FLOAT_ROUND:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vnout == vn0) return true;
                    if ((vnout == 0) || (vn0 == 0)) return true;
                    printOpError(op, ct, -1, 0, "Input and output sizes must match");
                    return false;
                case CPUI_INT_ADD:
                case CPUI_INT_SUB:
                case CPUI_INT_XOR:
                case CPUI_INT_AND:
                case CPUI_INT_OR:
                case CPUI_INT_MULT:
                case CPUI_INT_DIV:
                case CPUI_INT_SDIV:
                case CPUI_INT_REM:
                case CPUI_INT_SREM:
                case CPUI_FLOAT_ADD:
                case CPUI_FLOAT_DIV:
                case CPUI_FLOAT_MULT:
                case CPUI_FLOAT_SUB:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    vn1 = recoverSize(op.getIn(1).getSize(), ct);
                    if (vn1 == -1)
                    {
                        printOpError(op, ct, 1, 1, "Using subtable with exports in expression");
                        return false;
                    }
                    if ((vnout != 0) && (vn0 != 0) && (vnout != vn0))
                    {
                        printOpError(op, ct, -1, 0, "The output and all input sizes must match");
                        return false;
                    }
                    if ((vnout != 0) && (vn1 != 0) && (vnout != vn1))
                    {
                        printOpError(op, ct, -1, 1, "The output and all input sizes must match");
                        return false;
                    }
                    if ((vn0 != 0) && (vn1 != 0) && (vn0 != vn1))
                    {
                        printOpError(op, ct, 0, 1, "The output and all input sizes must match");
                        return false;
                    }
                    return true;
                case CPUI_FLOAT_NAN:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vnout != 1)
                    {
                        printOpError(op, ct, -1, -1, "Output must be a boolean (size 1)");
                        return false;
                    }
                    break;
                case CPUI_INT_EQUAL:        // Instructions with bool output, all inputs equal size
                case CPUI_INT_NOTEQUAL:
                case CPUI_INT_SLESS:
                case CPUI_INT_SLESSEQUAL:
                case CPUI_INT_LESS:
                case CPUI_INT_LESSEQUAL:
                case CPUI_INT_CARRY:
                case CPUI_INT_SCARRY:
                case CPUI_INT_SBORROW:
                case CPUI_FLOAT_EQUAL:
                case CPUI_FLOAT_NOTEQUAL:
                case CPUI_FLOAT_LESS:
                case CPUI_FLOAT_LESSEQUAL:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vnout != 1)
                    {
                        printOpError(op, ct, -1, -1, "Output must be a boolean (size 1)");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    vn1 = recoverSize(op.getIn(1).getSize(), ct);
                    if (vn1 == -1)
                    {
                        printOpError(op, ct, 1, 1, "Using subtable with exports in expression");
                        return false;
                    }
                    if ((vn0 == 0) || (vn1 == 0)) return true;
                    if (vn0 != vn1)
                    {
                        printOpError(op, ct, 0, 1, "Inputs must be the same size");
                        return false;
                    }
                    return true;
                case CPUI_BOOL_XOR:
                case CPUI_BOOL_AND:
                case CPUI_BOOL_OR:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vnout != 1)
                    {
                        printOpError(op, ct, -1, -1, "Output must be a boolean (size 1)");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vn0 != 1)
                    {
                        printOpError(op, ct, 0, 0, "Input must be a boolean (size 1)");
                        return false;
                    }
                    return true;
                case CPUI_BOOL_NEGATE:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vnout != 1)
                    {
                        printOpError(op, ct, -1, -1, "Output must be a boolean (size 1)");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vn0 != 1)
                    {
                        printOpError(op, ct, 0, 0, "Input must be a boolean (size 1)");
                        return false;
                    }
                    return true;
                // The shift amount does not necessarily have to be the same size
                // But the output and first parameter must be same size
                case CPUI_INT_LEFT:
                case CPUI_INT_RIGHT:
                case CPUI_INT_SRIGHT:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    if ((vnout == 0) || (vn0 == 0)) return true;
                    if (vnout != vn0)
                    {
                        printOpError(op, ct, -1, 0, "Output and first input must be the same size");
                        return false;
                    }
                    return true;
                case CPUI_INT_ZEXT:
                case CPUI_INT_SEXT:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    if ((vnout == 0) || (vn0 == 0)) return true;
                    if (vnout == vn0)
                    {
                        dealWithUnnecessaryExt(op, ct);
                        return true;
                    }
                    else if (vnout < vn0)
                    {
                        printOpError(op, ct, -1, 0, "Output size must be strictly bigger than input size");
                        return false;
                    }
                    return true;
                case CPUI_CBRANCH:
                    vn1 = recoverSize(op.getIn(1).getSize(), ct);
                    if (vn1 == -1)
                    {
                        printOpError(op, ct, 1, 1, "Using subtable with exports in expression");
                        return false;
                    }
                    if (vn1 != 1)
                    {
                        printOpError(op, ct, 1, 1, "Input must be a boolean (size 1)");
                        return false;
                    }
                    return true;
                case CPUI_LOAD:
                case CPUI_STORE:
                    if (op.getIn(0).getOffset().getType() != ConstTpl::spaceid)
                        return true;
                    spc = op.getIn(0).getOffset().getSpace();
                    vn1 = recoverSize(op.getIn(1).getSize(), ct);
                    if (vn1 == -1)
                    {
                        printOpError(op, ct, 1, 1, "Using subtable with exports in expression");
                        return false;
                    }
                    if ((vn1 != 0) && (vn1 != spc.getAddrSize()))
                    {
                        printOpError(op, ct, 1, 1, "Pointer size must match size of space");
                        return false;
                    }
                    return true;
                case CPUI_SUBPIECE:
                    vnout = recoverSize(op.getOut().getSize(), ct);
                    if (vnout == -1)
                    {
                        printOpError(op, ct, -1, -1, "Using subtable with exports in expression");
                        return false;
                    }
                    vn0 = recoverSize(op.getIn(0).getSize(), ct);
                    if (vn0 == -1)
                    {
                        printOpError(op, ct, 0, 0, "Using subtable with exports in expression");
                        return false;
                    }
                    vn1 = op.getIn(1).getOffset().getReal();
                    if ((vnout == 0) || (vn0 == 0)) return true;
                    if ((vnout == vn0) && (vn1 == 0))
                    { // No actual truncation is occuring
                        dealWithUnnecessaryTrunc(op, ct);
                        return true;
                    }
                    else if (vnout >= vn0)
                    {
                        printOpError(op, ct, -1, 0, "Output must be strictly smaller than input");
                        return false;
                    }
                    if (vnout > vn0 - vn1)
                    {
                        printOpError(op, ct, -1, 0, "Too much truncation");
                        return false;
                    }
                    return true;
                default:
                    break;
            }
            return true;
        }

        /// \brief Check all p-code operators within a given Constructor section for misuse and size consistency
        ///
        /// Each operator within the section is checked in turn, and warning and error messages are emitted
        /// if necessary. The method returns \b false if there is a fatal error associated with any
        /// operator.
        /// \param ct is the Constructor to check
        /// \param cttpl is the specific p-code section to check
        /// \return \b true if there are no fatal errors in the section
        private bool checkConstructorSection(Constructor ct, ConstructTpl cttpl)
        {
            if (cttpl == (ConstructTpl*)0)
                return true;        // Nothing to check
            vector<OpTpl*>::const_iterator iter;
            List<OpTpl> ops = cttpl.getOpvec();
            bool testresult = true;

            for (iter = ops.begin(); iter != ops.end(); ++iter)
            {
                if (!sizeRestriction(*iter, ct))
                    testresult = false;
                if (!checkOpMisuse(*iter, ct))
                    testresult = false;
            }
            return testresult;
        }

        /// \brief Check the given p-code operator for too large temporary registers
        ///
        /// Return \b true if the output or one of the inputs to the operator
        /// is in the \e unique space and larger than SleighBase::MAX_UNIQUE_SIZE
        /// \param op is the given operator
        /// \return \b true if the operator has a too large temporary parameter
        private bool hasLargeTemporary(OpTpl op)
        {
            VarnodeTpl *out = op.getOut();
            if ((out != (VarnodeTpl*)0x0) && isTemporaryAndTooBig(out)) {
                return true;
            }
            for (int4 i = 0; i < op.numInput(); ++i)
            {
                VarnodeTpl *in = op.getIn(i);
                if (isTemporaryAndTooBig(in))
                {
                    return true;
                }
            }
            return false;
        }

        /// \brief Check if the given Varnode is a too large temporary register
        ///
        /// Return \b true precisely when the Varnode is in the \e unique space and
        /// has size larger than SleighBase::MAX_UNIQUE_SIZE
        /// \param vn is the given Varnode
        /// \return \b true if the Varnode is a too large temporary register
        private bool isTemporaryAndTooBig(VarnodeTpl vn)
        {
            return vn.getSpace().isUniqueSpace() && (vn.getSize().getReal() > SleighBase::MAX_UNIQUE_SIZE);
        }

        /// \brief Resolve the offset of the given \b truncated Varnode
        ///
        /// SLEIGH allows a Varnode to be derived from another larger Varnode using
        /// truncation or bit range notation.  The final offset of the truncated Varnode may not
        /// be calculable immediately during parsing, especially if the address space is big endian
        /// and the size of the containing Varnode is not immediately known.
        /// This method recovers the final offset of the truncated Varnode now that all sizes are
        /// known and otherwise checks that the truncation expression is valid.
        /// \param ct is the Constructor containing the Varnode
        /// \param slot is the \e slot index of the truncated Varnode (for error messages)
        /// \param op is the operator using the truncated Varnode (for error messages)
        /// \param vn is the given truncated Varnode
        /// \param isbigendian is \b true if the Varnode is in a big endian address space
        /// \return \b true if the truncation expression was valid
        private bool checkVarnodeTruncation(Constructor ct, int4 slot, OpTpl op, VarnodeTpl vn, bool isbigendian)
        {
            ConstTpl off = vn.getOffset();
            if (off.getType() != ConstTpl::handle) return true;
            if (off.getSelect() != ConstTpl::v_offset_plus) return true;
            ConstTpl::const_type sztype = vn.getSize().getType();
            if ((sztype != ConstTpl::real) && (sztype != ConstTpl::handle))
            {
                printOpError(op, ct, slot, slot, "Bad truncation expression");
                return false;
            }
            int4 sz = recoverSize(off, ct); // Recover the size of the original operand
            if (sz <= 0)
            {
                printOpError(op, ct, slot, slot, "Could not recover size");
                return false;
            }
            bool res = vn.adjustTruncation(sz, isbigendian);
            if (!res)
            {
                printOpError(op, ct, slot, slot, "Truncation operator out of bounds");
                return false;
            }
            return true;
        }

        /// \brief Check and adjust truncated Varnodes in the given Constructor p-code section
        ///
        /// Run through all Varnodes looking for offset templates marked as ConstTpl::v_offset_plus,
        /// which indicates they were constructed using truncation notation. These truncation expressions
        /// are checked for validity and adjusted depending on the endianess of the address space.
        /// \param ct is the Constructor
        /// \param cttpl is the given p-code section
        /// \param isbigendian is set to \b true if the SLEIGH specification is big endian
        /// \return \b true if all truncation expressions were valid
        private bool checkSectionTruncations(Constructor ct, ConstructTpl cttpl, bool isbigendian)
        {
            vector<OpTpl*>::const_iterator iter;
            List<OpTpl> ops = cttpl.getOpvec();
            bool testresult = true;

            for (iter = ops.begin(); iter != ops.end(); ++iter)
            {
                OpTpl* op = *iter;
                VarnodeTpl* outvn = op.getOut();
                if (outvn != (VarnodeTpl*)0)
                {
                    if (!checkVarnodeTruncation(ct, -1, op, outvn, isbigendian))
                        testresult = false;
                }
                for (int4 i = 0; i < op.numInput(); ++i)
                {
                    if (!checkVarnodeTruncation(ct, i, op, op.getIn(i), isbigendian))
                        testresult = false;
                }
            }
            return testresult;
        }

        /// \brief Check all Constructors within the given subtable for operator misuse and size consistency
        ///
        /// Each Constructor and section is checked in turn.  Additionally, the size of Constructor
        /// exports is checked for consistency across the subtable.  Constructors within one subtable must
        /// all export the same size Varnode if the export at all.
        /// \param sym is the given subtable to check
        /// \return \b true if there are no fatal misuse or consistency violations
        private bool checkSubtable(SubtableSymbol sym)
        {
            int4 tablesize = -1;
            int4 numconstruct = sym.getNumConstructors();
            Constructor* ct;
            bool testresult = true;
            bool seenemptyexport = false;
            bool seennonemptyexport = false;

            for (int4 i = 0; i < numconstruct; ++i)
            {
                ct = sym.getConstructor(i);
                if (!checkConstructorSection(ct, ct.getTempl()))
                    testresult = false;
                int4 numsection = ct.getNumSections();
                for (int4 j = 0; j < numsection; ++j)
                {
                    if (!checkConstructorSection(ct, ct.getNamedTempl(j)))
                        testresult = false;
                }

                if (ct.getTempl() == (ConstructTpl*)0) continue;   // Unimplemented
                HandleTpl* exportres = ct.getTempl().getResult();
                if (exportres != (HandleTpl*)0)
                {
                    if (seenemptyexport && (!seennonemptyexport))
                    {
                        ostringstream msg;
                        msg << "Table '" << sym.getName() << "' exports inconsistently; ";
                        msg << "Constructor starting at line " << dec << ct.getLineno() << " is first inconsistency";
                        compiler.reportError(compiler.getLocation(ct), msg.str());
                        testresult = false;
                    }
                    seennonemptyexport = true;
                    int4 exsize = recoverSize(exportres.getSize(), ct);
                    if (tablesize == -1)
                        tablesize = exsize;
                    if (exsize != tablesize)
                    {
                        ostringstream msg;
                        msg << "Table '" << sym.getName() << "' has inconsistent export size; ";
                        msg << "Constructor starting at line " << dec << ct.getLineno() << " is first conflict";
                        compiler.reportError(compiler.getLocation(ct), msg.str());
                        testresult = false;
                    }
                }
                else
                {
                    if (seennonemptyexport && (!seenemptyexport))
                    {
                        ostringstream msg;
                        msg << "Table '" << sym.getName() << "' exports inconsistently; ";
                        msg << "Constructor starting at line " << dec << ct.getLineno() << " is first inconsistency";
                        compiler.reportError(compiler.getLocation(ct), msg.str());
                        testresult = false;
                    }
                    seenemptyexport = true;
                }
            }
            if (seennonemptyexport)
            {
                if (tablesize == 0)
                {
                    compiler.reportWarning(compiler.getLocation(sym), "Table '" + sym.getName() + "' exports size 0");
                }
                sizemap[sym] = tablesize;   // Remember recovered size
            }
            else
                sizemap[sym] = -1;

            return testresult;
        }

        /// \brief Convert an unnecessary CPUI_INT_ZEXT and CPUI_INT_SEXT into a COPY
        ///
        /// SLEIGH allows \b zext and \b sext notation even if the input and output
        /// Varnodes are ultimately the same size.  In this case, a warning may be
        /// issued and the operator is converted to a CPUI_COPY.
        /// \param op is the given CPUI_INT_ZEXT or CPUI_INT_SEXT operator to check
        /// \param ct is the Constructor containing the operator
        private void dealWithUnnecessaryExt(OpTpl op, Constructor ct)
        {
            if (printextwarning)
            {
                ostringstream msg;
                msg << "Unnecessary ";
                printOpName(msg, op);
                compiler.reportWarning(compiler.getLocation(ct), msg.str());
            }
            op.setOpcode(CPUI_COPY);   // Equivalent to copy
            unnecessarypcode += 1;
        }

        /// \brief Convert an unnecessary CPUI_SUBPIECE into a COPY
        ///
        /// SLEIGH allows truncation notation even if the input and output Varnodes are
        /// ultimately the same size.  In this case, a warning may be issued and the operator
        /// is converted to a CPUI_COPY.
        /// \param op is the given CPUI_SUBPIECE operator
        /// \param ct is the containing Constructor
        private void dealWithUnnecessaryTrunc(OpTpl op, Constructor ct)
        {
            if (printextwarning)
            {
                ostringstream msg;
                msg << "Unnecessary ";
                printOpName(msg, op);
                compiler.reportWarning(compiler.getLocation(ct), msg.str());
            }
            op.setOpcode(CPUI_COPY);   // Equivalent to copy
            op.removeInput(1);
            unnecessarypcode += 1;
        }

        /// \brief Establish ordering on subtables so that more dependent tables come first
        ///
        /// Do a depth first traversal of SubtableSymbols starting at the root table going
        /// through Constructors and then through their operands, establishing a post-order on the
        /// subtables. This allows the size restriction checks to recursively calculate sizes of dependent
        /// subtables first and propagate their values into more global Varnodes (as Constructor operands)
        /// \param root is the root subtable
        private void setPostOrder(SubtableSymbol root)
        {
            postorder.clear();
            sizemap.clear();

            vector<SubtableSymbol*> path;
            vector<int4> state;
            vector<int4> ctstate;

            sizemap[root] = -1;     // Mark root as traversed
            path.push_back(root);
            state.push_back(0);
            ctstate.push_back(0);

            while (!path.empty())
            {
                SubtableSymbol* cur = path.back();
                int4 ctind = state.back();
                if (ctind >= cur.getNumConstructors())
                {
                    path.pop_back();        // Table is fully traversed
                    state.pop_back();
                    ctstate.pop_back();
                    postorder.push_back(cur);   // Post the traversed table
                }
                else
                {
                    Constructor* ct = cur.getConstructor(ctind);
                    int4 oper = ctstate.back();
                    if (oper >= ct.getNumOperands())
                    {
                        state.back() = ctind + 1; // Constructor fully traversed
                        ctstate.back() = 0;
                    }
                    else
                    {
                        ctstate.back() = oper + 1;
                        OperandSymbol* opsym = ct.getOperand(oper);
                        SubtableSymbol* subsym = dynamic_cast<SubtableSymbol*>(opsym.getDefiningSymbol());
                        if (subsym != (SubtableSymbol*)0)
                        {
                            map<SubtableSymbol*, int4>::const_iterator iter;
                            iter = sizemap.find(subsym);
                            if (iter == sizemap.end())
                            { // Not traversed yet
                                sizemap[subsym] = -1; // Mark table as traversed
                                path.push_back(subsym); // Recurse
                                state.push_back(0);
                                ctstate.push_back(0);
                            }
                        }
                    }
                }
            }
        }

        // Optimization routines
        /// \brief Accumulate read/write info if the given Varnode is temporary
        ///
        /// If the Varnode is in the \e unique space, an OptimizationRecord for it is looked
        /// up based on its offset.  Information about how a p-code operator uses the Varnode
        /// is accumulated in the record.
        /// \param recs is collection of OptimizationRecords associated with temporary Varnodes
        /// \param vn is the given Varnode to check (which may or may not be temporary)
        /// \param i is the index of the operator using the Varnode (within its p-code section)
        /// \param inslot is the \e slot index of the Varnode within its operator
        /// \param secnum is the section number containing the operator
        private static void examineVn(Dictionary<uintb, OptimizeRecord> recs, VarnodeTpl vn, uint4 i,int4 inslot, int4 secnum)
        {
            if (vn == (VarnodeTpl*)0) return;
            if (!vn.getSpace().isUniqueSpace()) return;
            if (vn.getOffset().getType() != ConstTpl::real) return;

            map<uintb, OptimizeRecord>::iterator iter;
            iter = recs.insert(pair<uint4, OptimizeRecord>(vn.getOffset().getReal(), OptimizeRecord())).first;
            if (inslot >= 0)
            {
                (*iter).second.readop = i;
                (*iter).second.readcount += 1;
                (*iter).second.inslot = inslot;
                (*iter).second.readsection = secnum;
            }
            else
            {
                (*iter).second.writeop = i;
                (*iter).second.writecount += 1;
                (*iter).second.writesection = secnum;
            }
        }

        /// \brief Test whether two given Varnodes intersect
        ///
        /// This test must be conservative.  If it can't explicitly prove that the
        /// Varnodes don't intersect, it returns \b true (a possible intersection).
        /// \param vn1 is the first Varnode to check
        /// \param vn2 is the second Varnode to check
        /// \return \b true if there is a possible intersection of the Varnodes' storage
        private static bool possibleIntersection(VarnodeTpl vn1, VarnodeTpl vn2)
        { // Conservatively test whether vn1 and vn2 can intersect
            if (vn1.getSpace().isConstSpace()) return false;
            if (vn2.getSpace().isConstSpace()) return false;

            bool u1 = vn1.getSpace().isUniqueSpace();
            bool u2 = vn2.getSpace().isUniqueSpace();

            if (u1 != u2) return false;

            if (vn1.getSpace().getType() != ConstTpl::spaceid) return true;
            if (vn2.getSpace().getType() != ConstTpl::spaceid) return true;
            AddrSpace* spc = vn1.getSpace().getSpace();
            if (spc != vn2.getSpace().getSpace()) return false;


            if (vn2.getOffset().getType() != ConstTpl::real) return true;
            if (vn2.getSize().getType() != ConstTpl::real) return true;

            if (vn1.getOffset().getType() != ConstTpl::real) return true;
            if (vn1.getSize().getType() != ConstTpl::real) return true;

            uintb offset = vn1.getOffset().getReal();
            uintb size = vn1.getSize().getReal();

            uintb off = vn2.getOffset().getReal();
            if (off + vn2.getSize().getReal() - 1 < offset) return false;
            if (off > (offset + size - 1)) return false;
            return true;
        }

        /// \brief Check if a p-code operator reads from or writes to a given Varnode
        ///
        /// A write check is always performed. A read check is performed only if requested.
        /// Return \b true if there is a possible write (or read) of the Varnode.
        /// The checks need to be extremely conservative.  If it can't be determined what
        /// exactly is being read or written, \b true (possible interference) is returned.
        /// \param vn is the given Varnode
        /// \param op is p-code operator to test for interference
        /// \param checkread is \b true if read interference should be checked
        /// \return \b true if there is write (or read) interference
        private bool readWriteInterference(VarnodeTpl vn, OpTpl op,bool checkread)
        {
            switch (op.getOpcode())
            {
                case BUILD:
                case CROSSBUILD:
                case DELAY_SLOT:
                case MACROBUILD:
                case CPUI_LOAD:
                case CPUI_STORE:
                case CPUI_BRANCH:
                case CPUI_CBRANCH:
                case CPUI_BRANCHIND:
                case CPUI_CALL:
                case CPUI_CALLIND:
                case CPUI_CALLOTHER:
                case CPUI_RETURN:
                case LABELBUILD:        // Another value might jump in here
                    return true;
                default:
                    break;
            }

            if (checkread)
            {
                int4 numinputs = op.numInput();
                for (int4 i = 0; i < numinputs; ++i)
                    if (possibleIntersection(vn, op.getIn(i)))
                        return true;
            }

            // We always check for writes to -vn-
            VarnodeTpl vn2 = op.getOut();
            if (vn2 != (VarnodeTpl*)0) {
                if (possibleIntersection(vn, vn2))
                    return true;
            }
            return false;
        }

        /// \brief Gather statistics about read and writes to temporary Varnodes within a given p-code section
        ///
        /// For each temporary Varnode, count how many times it is read from or written to
        /// in the given section of p-code operators.
        /// \param ct is the given Constructor
        /// \param recs is the (initially empty) collection of count records
        /// \param secnum is the given p-code section number
        private void optimizeGather1(Constructor ct, Dictionary<uintb, OptimizeRecord> recs, int4 secnum)
        {
            ConstructTpl* tpl;
            if (secnum < 0)
                tpl = ct.getTempl();
            else
                tpl = ct.getNamedTempl(secnum);
            if (tpl == (ConstructTpl*)0)
                return;
            List<OpTpl> ops = tpl.getOpvec();
            for (uint4 i = 0; i < ops.size(); ++i)
            {
                OpTpl op = ops[i];
                for (uint4 j = 0; j < op.numInput(); ++j)
                {
                    VarnodeTpl vnin = op.getIn(j);
                    examineVn(recs, vnin, i, j, secnum);
                }
                VarnodeTpl vn = op.getOut();
                examineVn(recs, vn, i, -1, secnum);
            }
        }

        /// \brief Mark Varnodes in the export of the given p-code section as read and written
        ///
        /// As part of accumulating read/write info for temporary Varnodes, examine the export Varnode
        /// for the section, and if it involves a temporary, mark it as both read and written, guaranteeing
        /// that the Varnode is not optimized away.
        /// \param ct is the given Constructor
        /// \param recs is the collection of count records
        /// \param secnum is the given p-code section number
        private void optimizeGather2(Constructor ct, Dictionary<uintb, OptimizeRecord> recs, int4 secnum)
        {
            ConstructTpl* tpl;
            if (secnum < 0)
                tpl = ct.getTempl();
            else
                tpl = ct.getNamedTempl(secnum);
            if (tpl == (ConstructTpl*)0)
                return;
            HandleTpl* hand = tpl.getResult();
            if (hand == (HandleTpl*)0) return;
            if (hand.getPtrSpace().isUniqueSpace())
            {
                if (hand.getPtrOffset().getType() == ConstTpl::real)
                {
                    pair<map<uintb, OptimizeRecord>::iterator, bool> res;
                    uintb offset = hand.getPtrOffset().getReal();
                    res = recs.insert(pair<uintb, OptimizeRecord>(offset, OptimizeRecord()));
                    (*res.first).second.writeop = 0;
                    (*res.first).second.readop = 0;
                    (*res.first).second.writecount = 2;
                    (*res.first).second.readcount = 2;
                    (*res.first).second.readsection = -2;
                    (*res.first).second.writesection = -2;
                }
            }
            if (hand.getSpace().isUniqueSpace())
            {
                if ((hand.getPtrSpace().getType() == ConstTpl::real) &&
                (hand.getPtrOffset().getType() == ConstTpl::real))
                {
                    pair<map<uintb, OptimizeRecord>::iterator, bool> res;
                    uintb offset = hand.getPtrOffset().getReal();
                    res = recs.insert(pair<uintb, OptimizeRecord>(offset, OptimizeRecord()));
                    (*res.first).second.writeop = 0;
                    (*res.first).second.readop = 0;
                    (*res.first).second.writecount = 2;
                    (*res.first).second.readcount = 2;
                    (*res.first).second.readsection = -2;
                    (*res.first).second.writesection = -2;
                }
            }
        }

        /// \brief Search for an OptimizeRecord indicating a temporary Varnode that can be optimized away
        ///
        /// OptimizeRecords for all temporary Varnodes must already be calculated.
        /// Find a record indicating a temporary Varnode that is written once and read once through a COPY.
        /// Test propagation of the other Varnode associated with the COPY, making sure:
        /// if propagation is backward, the Varnode must not cross another read or write, and
        /// if propagation is forward, the Varnode must not cross another write.
        /// If all the requirements pass, return the record indicating that the COPY can be removed.
        /// \param ct is the Constructor owning the p-code
        /// \param recs is the collection of OptimizeRecords to search
        /// \return a passing OptimizeRecord or null
        private OptimizeRecord findValidRule(Constructor ct, Dictionary<uintb, OptimizeRecord> recs)
        {
            map<uintb, OptimizeRecord>::const_iterator iter;
            iter = recs.begin();
            while (iter != recs.end())
            {
                OptimizeRecord currec = (*iter).second;
                ++iter;
                if ((currec.writecount == 1) && (currec.readcount == 1) && (currec.readsection == currec.writesection))
                {
                    // Temporary must be read and written exactly once
                    ConstructTpl* tpl;
                    if (currec.readsection < 0)
                        tpl = ct.getTempl();
                    else
                        tpl = ct.getNamedTempl(currec.readsection);
                    List<OpTpl> ops = tpl.getOpvec();
                    OpTpl op = ops[currec.readop];
                    if (currec.writeop >= currec.readop) // Read must come after write
                        throw SleighError("Read of temporary before write");
                    if (op.getOpcode() == CPUI_COPY)
                    {
                        bool saverecord = true;
                        currec.opttype = 0; // Read op is a COPY
                        VarnodeTpl vn = op.getOut();
                        for (int4 i = currec.writeop + 1; i < currec.readop; ++i)
                        { // Check for interference between write and read
                            if (readWriteInterference(vn, ops[i], true))
                            {
                                saverecord = false;
                                break;
                            }
                        }
                        if (saverecord)
                            return &currec;
                    }
                    op = ops[currec.writeop];
                    if (op.getOpcode() == CPUI_COPY)
                    {
                        bool saverecord = true;
                        currec.opttype = 1; // Write op is a COPY
                        VarnodeTpl vn = op.getIn(0);
                        for (int4 i = currec.writeop + 1; i < currec.readop; ++i)
                        { // Check for interference between write and read
                            if (readWriteInterference(vn, ops[i], false))
                            {
                                saverecord = false;
                                break;
                            }
                        }
                        if (saverecord)
                            return &currec;
                    }
                }
            }
            return (OptimizeRecord*)0;
        }

        /// \brief Remove an extraneous COPY going through a temporary Varnode
        ///
        /// If an OptimizeRecord has determined that a temporary Varnode is read once, written once,
        /// and goes through a COPY operator, remove the COPY operator.
        /// If the Varnode is an input to the COPY, the operator writing the Varnode is changed to
        /// write to the output of the COPY instead.  If the Varnode is an output of the COPY, the
        /// operator reading the Varnode is changed to read the input of the COPY instead.
        /// In either case, the COPY operator is removed.
        /// \param ct is the Constructor
        /// \param rec is record describing the temporary and its read/write operators
        private void applyOptimization(Constructor ct, OptimizeRecord rec)
        {
            vector<int4> deleteops;
            ConstructTpl* ctempl;
            if (rec.readsection < 0)
                ctempl = ct.getTempl();
            else
                ctempl = ct.getNamedTempl(rec.readsection);

            if (rec.opttype == 0)
            { // If read op is COPY
                int4 readop = rec.readop;
                OpTpl* op = ctempl.getOpvec()[readop];
                VarnodeTpl* vnout = new VarnodeTpl(*op.getOut()); // Make COPY output
                ctempl.setOutput(vnout, rec.writeop); // become write output
                deleteops.push_back(readop); // and then delete the read (COPY)
            }
            else if (rec.opttype == 1)
            { // If write op is COPY
                int4 writeop = rec.writeop;
                OpTpl* op = ctempl.getOpvec()[writeop];
                VarnodeTpl* vnin = new VarnodeTpl(*op.getIn(0));   // Make COPY input
                ctempl.setInput(vnin, rec.readop, rec.inslot); // become read input
                deleteops.push_back(writeop); // and then delete the write (COPY)
            }
            ctempl.deleteOps(deleteops);
        }

        /// \brief Issue error/warning messages for unused temporary Varnodes
        ///
        /// An error message is issued if a temporary is read but not written.
        /// A warning may be issued if a temporary is written but not read.
        /// \param ct is the Constructor
        /// \param recs is the collection of records associated with each temporary Varnode
        private void checkUnusedTemps(Constructor ct, Dictionary<uintb, OptimizeRecord> recs)
        {
            map<uintb, OptimizeRecord>::const_iterator iter;
            iter = recs.begin();
            while (iter != recs.end())
            {
                OptimizeRecord currec = (*iter).second;
                if (currec.readcount == 0)
                {
                    if (printdeadwarning)
                        compiler.reportWarning(compiler.getLocation(ct), "Temporary is written but not read");
                    writenoread += 1;
                }
                else if (currec.writecount == 0)
                {
                    compiler.reportError(compiler.getLocation(ct), "Temporary is read but not written");
                    readnowrite += 1;
                }
                ++iter;
            }
        }

        /// \brief In the given Constructor p-code section, check for temporary Varnodes that are too large
        ///
        /// Run through all Varnodes in the constructor, if a Varnode is in the \e unique
        /// space and its size exceeds the threshold SleighBase::MAX_UNIQUE_SIZE, issue
        /// a warning. Note that this method returns after the first large Varnode is found.
        /// \param ct is the given Constructor
        /// \param ctpl is the specific p-code section
        private void checkLargeTemporaries(Constructor ct, ConstructTpl ctpl)
        {
            vector<OpTpl*> ops = ctpl.getOpvec();
            for (vector<OpTpl*>::iterator iter = ops.begin(); iter != ops.end(); ++iter)
            {
                if (hasLargeTemporary(*iter))
                {
                    if (printlargetempwarning)
                    {
                        compiler.reportWarning(
                            compiler.getLocation(ct),
                            "Constructor uses temporary varnode larger than " + to_string(SleighBase::MAX_UNIQUE_SIZE) + " bytes.");
                    }
                    largetemp++;
                    return;
                }
            }
        }

        /// \brief Do p-code optimization on each section of the given Constructor
        ///
        /// For p-code section, statistics on temporary Varnode usage is collected,
        /// and unnecessary COPY operators are removed.
        /// \param ct is the given Constructor
        private void optimize(Constructor ct)
        {
            OptimizeRecord currec;
            map<uintb, OptimizeRecord> recs;
            int4 numsections = ct.getNumSections();
            do
            {
                recs.clear();
                for (int4 i = -1; i < numsections; ++i)
                {
                    optimizeGather1(ct, recs, i);
                    optimizeGather2(ct, recs, i);
                }
                currec = findValidRule(ct, recs);
                if (currec != (OptimizeRecord*)0)
                    applyOptimization(ct, *currec);
            } while (currec != (OptimizeRecord*)0);
            checkUnusedTemps(ct, recs);
        }

        /// \brief Construct the consistency checker and optimizer
        ///
        /// \param sleigh is the parsed SLEIGH spec
        /// \param rt is the root subtable of the SLEIGH spec
        /// \param un is \b true to request "Unnecessary extension" warnings
        /// \param warndead is \b true to request warnings for written but not read temporaries
        /// \param warnlargetemp is \b true to request warnings for temporaries that are too large
        public ConsistencyChecker(SleighCompile sleigh, SubtableSymbol rt, bool unnecessary, bool warndead, bool warnlargetemp)
        {
            compiler = sleigh;
            root_symbol = rt;
            unnecessarypcode = 0;
            readnowrite = 0;
            writenoread = 0;
            largetemp = 0;        ///<Number of constructors using at least one temporary varnode larger than SleighBase::MAX_UNIQUE_SIZE
            printextwarning = un;
            printdeadwarning = warndead;
            printlargetempwarning = warnlargetemp; ///< If true, prints a warning about each constructor using a temporary varnode larger than SleighBase::MAX_UNIQUE_SIZE
        }

        /// Test size consistency of all p-code
        /// Warnings or errors for individual violations may be printed, depending on settings.
        /// \return \b true if all size consistency checks pass
        public bool testSizeRestrictions()
        {
            setPostOrder(root_symbol);
            bool testresult = true;

            for (int4 i = 0; i < postorder.size(); ++i)
            {
                SubtableSymbol* sym = postorder[i];
                if (!checkSubtable(sym))
                    testresult = false;
            }
            return testresult;
        }

        /// Test truncation validity of all p-code
        /// Update truncated Varnodes given complete size information. Print errors
        /// for any invalid truncation constructions.
        /// \return \b true if there are no invalid truncations
        public bool testTruncations()
        {
            bool testresult = true;
            bool isbigendian = slgh.isBigEndian();
            for (int4 i = 0; i < postorder.size(); ++i)
            {
                SubtableSymbol* sym = postorder[i];
                int4 numconstruct = sym.getNumConstructors();
                Constructor* ct;
                for (int4 j = 0; j < numconstruct; ++j)
                {
                    ct = sym.getConstructor(j);

                    int4 numsections = ct.getNumSections();
                    for (int4 k = -1; k < numsections; ++k)
                    {
                        ConstructTpl* tpl;
                        if (k < 0)
                            tpl = ct.getTempl();
                        else
                            tpl = ct.getNamedTempl(k);
                        if (tpl == (ConstructTpl*)0)
                            continue;
                        if (!checkSectionTruncations(ct, tpl, isbigendian))
                            testresult = false;
                    }
                }
            }
            return testresult;
        }

        /// Test for temporary Varnodes that are too large
        /// This counts Constructors that contain temporary Varnodes that are too large.
        /// If requested, an individual warning is printed for each Constructor.
        public void testLargeTemporary()
        {
            for (int4 i = 0; i < postorder.size(); ++i)
            {
                SubtableSymbol* sym = postorder[i];
                int4 numconstruct = sym.getNumConstructors();
                Constructor* ct;
                for (int4 j = 0; j < numconstruct; ++j)
                {
                    ct = sym.getConstructor(j);

                    int4 numsections = ct.getNumSections();
                    for (int4 k = -1; k < numsections; ++k)
                    {
                        ConstructTpl* tpl;
                        if (k < 0)
                            tpl = ct.getTempl();
                        else
                            tpl = ct.getNamedTempl(k);
                        if (tpl == (ConstructTpl*)0)
                            continue;
                        checkLargeTemporaries(ct, tpl);
                    }
                }
            }
        }

        /// Do COPY propagation optimization on all p-code
        public void optimizeAll()
        {
            for (int4 i = 0; i < postorder.size(); ++i)
            {
                SubtableSymbol* sym = postorder[i];
                int4 numconstruct = sym.getNumConstructors();
                Constructor* ct;
                for (int4 i = 0; i < numconstruct; ++i)
                {
                    ct = sym.getConstructor(i);
                    optimize(ct);
                }
            }
        }

        public int4 getNumUnnecessaryPcode() => unnecessarypcode; ///< Return the number of unnecessary extensions and truncations

        public int4 getNumReadNoWrite() => readnowrite; ///< Return the number of temporaries read but not written

        public int4 getNumWriteNoRead() => writenoread; ///< Return the number of temporaries written but not read

        public int4 getNumLargeTemporaries() => largetemp; ///< Return the number of \e too large temporaries
    }
}
