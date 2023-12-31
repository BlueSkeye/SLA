﻿using Sla.CORE;
using Sla.DECCORE;

namespace Sla.DECCORE
{
    /// \brief The java-language token emitter
    ///
    /// This builds heavily on the c-language PrintC emitter.  Most operator tokens, the format of
    /// function prototypes, and code structuring are shared.  Specifics of the java constant pool are handled
    /// through the overloaded opCpoolRefOp().
    ///
    /// Java data-types are mapped into the decompiler's data-type system in a specific way. The primitives
    /// \b int, \b long, \b short, \b byte, \b boolean, \b float, and \b double all map directly. The
    /// \b char primitive is treated as a 2 byte unsigned integer. A TypeStruct object holds the field
    /// layout for a java class, then java objects get mapped as follows:
    ///   - Class reference = pointer to type_metatype.TYPE_UINT
    ///   - Array of \b int, \b long, \b short, or \b byte = pointer to type_metatype.TYPE_INT
    ///   - Array of \b float or \b double = pointer to type_metatype.TYPE_FLOAT
    ///   - Array of \b boolean = pointer to type_metatype.TYPE_BOOL
    ///   - Array of class objects = pointer to type_metatype.TYPE_PTR
    ///
    /// There are some adjustments to the printing of data-types and LOAD/STORE expressions
    /// to account for this mapping.
    internal class PrintJava : PrintC
    {
        /// The \b instanceof keyword
        private static OpToken instanceof;

        ///< Does the given data-type reference a java array
        /// References to java array objects where the underlying element is a java primitive look like:
        ///   - Pointer to int
        ///   - Pointer to bool
        ///   - Pointer to float
        ///
        /// An array of java class objects is represented as a pointer to pointer data-type.
        /// \param ct is the given data-type
        /// \return \b true if the data-type references a java array object
        private static bool isArrayType(Datatype ct)
        {
            if (ct.getMetatype() != type_metatype.TYPE_PTR)  // Java arrays are always Ghidra pointer types
                return false;
            ct = ((TypePointer)ct).getPtrTo();
            switch (ct.getMetatype())
            {
                case type_metatype.TYPE_UINT:     // Pointer to unsigned is placeholder for class reference, not an array
                    if (ct.isCharPrint())
                        return true;
                    break;
                case type_metatype.TYPE_INT:
                case type_metatype.TYPE_BOOL:
                case type_metatype.TYPE_FLOAT:    // Pointer to primitive type is an array
                case type_metatype.TYPE_PTR:  // Pointer to class reference is an array
                    return true;
                default:
                    break;
            }
            return false;
        }

        ///< Do we need '[0]' syntax.
        /// Assuming the given Varnode is a dereferenced pointer, determine whether
        /// it needs to be represented using '[0]' syntax.
        /// \param vn is the given Varnode
        /// \return \b true if '[0]' syntax is required
        private static bool needZeroArray(Varnode vn)
        {
            if (!isArrayType(vn.getType()))
                return false;
            if (vn.isExplicit()) return true;
            if (!vn.isWritten()) return true;
            OpCode opc = vn.getDef().code();
            if ((opc == OpCode.CPUI_PTRADD) || (opc == OpCode.CPUI_PTRSUB) || (opc == OpCode.CPUI_CPOOLREF))
                return false;
            return true;
        }

        ///< Set options that are specific to Java
        private void resetDefaultsPrintJava()
        {
            option_NULL = true;         // Automatically use 'null' token
            option_convention = false;      // Automatically hide convention name
            mods |= modifiers.hide_thisparam;     // turn on hiding of 'this' parameter
        }

        protected override void printUnicode(TextWriter s, int onechar)
        {
            if (unicodeNeedsEscape(onechar))
            {
                switch (onechar)
                {       // Special escape characters
                    case 0:
                        s.Write("\\0");
                        return;
                    case 8:
                        s.Write("\\b");
                        return;
                    case 9:
                        s.Write("\\t");
                        return;
                    case 10:
                        s.Write("\\n");
                        return;
                    case 12:
                        s.Write("\\f");
                        return;
                    case 13:
                        s.Write("\\r");
                        return;
                    case 92:
                        s.Write("\\\\");
                        return;
                    case '"':
                        s.Write("\\\"");
                        return;
                    case '\'':
                        s.Write("\\\'");
                        return;
                }
                // Generic unicode escape
                if (onechar < 65536) {
                    s.Write("\\ux{onechar:X04}");
                }
                else
                    s.Write($"\\ux{onechar:X08}");
                return;
            }
            // Emit normally
            StringManager.writeUtf8(s, onechar);
        }

        public PrintJava(Architecture g, string nm="java-language")
            : base(g, nm)
        {
            resetDefaultsPrintJava();
            nullToken = "null";         // Java standard lower-case 'null'
            //if (castStrategy != (CastStrategy)null)
            //    delete castStrategy;
            castStrategy = new CastStrategyJava();
        }

        public override void resetDefaults()
        {
            base.resetDefaults();
            resetDefaultsPrintJava();
        }

        public override void docFunction(Funcdata fd)
        {
            bool singletonFunction = false;
            if (curscope == (Scope)null) {
                singletonFunction = true;
                // Always assume we are in the scope of the parent class
                pushScope(fd.getScopeLocal().getParent());
            }
            base.docFunction(fd);
            if (singletonFunction)
                popScope();
        }

        /// Print a data-type up to the identifier, store off array sizes
        /// for printing after the identifier. Find the root type (the one with an identifier)
        /// and the count number of wrapping arrays.
        /// \param ct is the given data-type
        /// \param noident is \b true if no identifier will be pushed with this declaration
        protected override void pushTypeStart(Datatype ct,bool noident)
        {
            int arrayCount = 0;
            while(true) {
                if (ct.getMetatype() == type_metatype.TYPE_PTR) {
                    if (isArrayType(ct))
                        arrayCount += 1;
                    ct = ((TypePointer)ct).getPtrTo();
                }
                else if (ct.getName().Length != 0)
                    break;
                else {
                    ct = glb.types.getTypeVoid();
                    break;
                }
            }
            OpToken tok = (noident) ? type_expr_nospace : type_expr_space;

            pushOp(tok, (PcodeOp)null);
            for (int i = 0; i < arrayCount; ++i) {
                pushOp(subscript, (PcodeOp)null);
            }
            if (ct.getName().Length == 0) {
                // Check for anonymous type
                // We could support a struct or enum declaration here
                string nm = genericTypeName(ct);
                pushAtom(new Atom(nm, tagtype.typetoken, EmitMarkup.syntax_highlight.type_color, ct));
            }
            else {
                pushAtom(new Atom(ct.getDisplayName(), tagtype.typetoken,
                    EmitMarkup.syntax_highlight.type_color, ct));
            }
            for (int i = 0; i < arrayCount; ++i) {
                // Fill in the blank array index
                pushAtom(new Atom(EMPTY_STRING, tagtype.blanktoken,
                    EmitMarkup.syntax_highlight.no_color));
            }
        }

        protected override void pushTypeEnd(Datatype ct)
        {
            // This routine doesn't have to do anything
        }

        protected override bool doEmitWideCharPrefix() => false;

        public override void adjustTypeOperators()
        {
            scope.print1 = ".";
            shift_right.print1 = ">>>";
            TypeOp.selectJavaOperators(glb.inst, true);
        }

        public override void opLoad(PcodeOp op)
        {
            modifiers m = mods | modifiers.print_load_value;
            bool printArrayRef = needZeroArray(op.getIn(1));
            if (printArrayRef)
                pushOp(subscript, op);
            pushVn(op.getIn(1), op, m);
            if (printArrayRef)
                push_integer(0, 4, false, (Varnode)null, op);
        }

        public override void opStore(PcodeOp op)
        {
            // Inform sub-tree that we are storing
            modifiers m = mods | modifiers.print_store_value;
            pushOp(assignment, op);    // This is an assignment
            if (needZeroArray(op.getIn(1))) {
                pushOp(subscript, op);
                pushVn(op.getIn(1), op, m);
                push_integer(0, 4, false, (Varnode)null, op);
                pushVn(op.getIn(2), op, mods);
            }
            else {
                // implied vn's pushed on in reverse order for efficiency
                // see PrintLanguage::pushVnImplied
                pushVn(op.getIn(2), op, mods);
                pushVn(op.getIn(1), op, m);
            }
        }

        public override void opCallind(PcodeOp op)
        {
            pushOp(function_call, op);
            Funcdata fd = op.getParent().getFuncdata();
            FuncCallSpecs fc = fd.getCallSpecs(op);
            if (fc == (FuncCallSpecs)null) {
                throw new LowlevelError("Missing indirect function callspec");
            }
            int skip = getHiddenThisSlot(op, fc);
            int count = op.numInput() - 1;
            count -= (skip < 0) ? 0 : 1;
            if (count > 1) {
                // Multiple parameters
                pushVn(op.getIn(0), op, mods);
                for (int i = 0; i < count - 1; ++i) {
                    pushOp(comma, op);
                }
                // implied vn's pushed on in reverse order for efficiency
                // see PrintLanguage::pushVnImplied
                for (int i = op.numInput() - 1; i >= 1; --i) {
                    if (i == skip) continue;
                    pushVn(op.getIn(i), op, mods);
                }
            }
            else if (count == 1) {
                // One parameter
                if (skip == 1)
                    pushVn(op.getIn(2), op, mods);
                else
                    pushVn(op.getIn(1), op, mods);
                pushVn(op.getIn(0), op, mods);
            }
            else {
                // A void function
                pushVn(op.getIn(0), op, mods);
                pushAtom(new Atom(EMPTY_STRING, tagtype.blanktoken, EmitMarkup.syntax_highlight.no_color));
            }
        }

        public override void opCpoolRefOp(PcodeOp op)
        {
            Varnode outvn = op.getOut();
            Varnode vn0 = op.getIn(0);
            List<ulong> refs = new List<ulong>();
            for (int i = 1; i < op.numInput(); ++i)
                refs.Add(op.getIn(i).getOffset());
            CPoolRecord rec = glb.cpool.getRecord(refs);
            if (rec == (CPoolRecord)null) {
                pushAtom(new Atom("UNKNOWNREF", tagtype.syntax, EmitMarkup.syntax_highlight.const_color, op, outvn));
            }
            else {
                switch (rec.getTag()) {
                    case CPoolRecord.ConstantPoolTagTypes.string_literal: {
                            TextWriter str = new StringWriter();
                            int len = rec.getByteDataLength();
                            if (len > 2048)
                                len = 2048;
                            str.Write("\"");
                            escapeCharacterData(str, rec.getByteData(), len, 1, false);
                            if (len == rec.getByteDataLength())
                                str.Write("\"");
                            else {
                                str.Write("...\"");
                            }
                            pushAtom(new Atom(str.ToString(), tagtype.vartoken,
                                EmitMarkup.syntax_highlight.const_color, op, outvn));
                            break;
                        }
                    case CPoolRecord.ConstantPoolTagTypes.class_reference:
                        pushAtom(new Atom(rec.getToken(), tagtype.vartoken,
                            EmitMarkup.syntax_highlight.type_color, op, outvn));
                        break;
                    case CPoolRecord.ConstantPoolTagTypes.instance_of: {
                            Datatype dt = rec.getType();
                            while (dt.getMetatype() == type_metatype.TYPE_PTR) {
                                dt = ((TypePointer)dt).getPtrTo();
                            }
                            pushOp(instanceof, op);
                            pushVn(vn0, op, mods);
                            pushAtom(new Atom(dt.getDisplayName(), tagtype.syntax,
                                EmitMarkup.syntax_highlight.type_color, op, outvn));
                            break;
                        }
                    case CPoolRecord.ConstantPoolTagTypes.primitive:        // Should be eliminated
                    case CPoolRecord.ConstantPoolTagTypes.pointer_method:
                    case CPoolRecord.ConstantPoolTagTypes.pointer_field:
                    case CPoolRecord.ConstantPoolTagTypes.array_length:
                    case CPoolRecord.ConstantPoolTagTypes.check_cast:
                    default: {
                            Datatype ct = rec.getType();
                            EmitMarkup.syntax_highlight color = EmitMarkup.syntax_highlight.var_color;
                            if (ct.getMetatype() == type_metatype.TYPE_PTR) {
                                ct = ((TypePointer)ct).getPtrTo();
                                if (ct.getMetatype() == type_metatype.TYPE_CODE)
                                    color = EmitMarkup.syntax_highlight.funcname_color;
                            }
                            if (vn0.isConstant()) {
                                // If this is NOT relative to an object reference
                                pushAtom(new Atom(rec.getToken(), tagtype.vartoken, color, op, outvn));
                            }
                            else {
                                pushOp(object_member, op);
                                pushVn(vn0, op, mods);
                                pushAtom(new Atom(rec.getToken(), tagtype.syntax, color, op, outvn));
                            }
                        }
                        break;
                }
            }
        }
    }
}
