using Sla.CORE;
using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.GrammarToken;
using static ghidra.XmlScan;

using ScopeMap = System.Collections.Generic.Dictionary<ulong, Sla.DECCORE.Scope>;

namespace Sla.DECCORE
{
    /// \brief The c-language token emitter
    ///
    /// The c-language specific rules for emitting:
    ///  - expressions
    ///  - statements
    ///  - function prototypes
    ///  - variable declarations
    ///  - if/else structures
    ///  - loop structures
    ///  - etc.
    internal class PrintC : PrintLanguage
    {
        /// Hidden functional (that may force parentheses)
        protected static readonly OpToken hidden = new OpToken ( "", "", 1, 70, false, OpToken.tokentype.hiddenfunction, 0, 0);
        /// The sub-scope/namespace operator
        protected static readonly OpToken scope = new OpToken ( "::", "", 2, 70, true, OpToken.tokentype.binary, 0, 0);
        /// The \e member operator
        protected static readonly OpToken object_member = new OpToken ( ".", "", 2, 66, true, OpToken.tokentype.binary, 0, 0);
        /// The \e points \e to \e member operator
        protected static readonly OpToken pointer_member = new OpToken ( ".", "", 2, 66, true, OpToken.tokentype.binary, 0, 0);
        /// The array subscript operator
        protected static readonly OpToken subscript = new OpToken ( "[", "]", 2, 66, false, OpToken.tokentype.postsurround, 0, 0);
        /// The \e function \e call operator
        protected static readonly OpToken function_call = new OpToken ( "(", ")", 2, 66, false, OpToken.tokentype.postsurround, 0, 10);
        /// The \e bitwise \e negate operator
        protected static readonly OpToken bitwise_not = new OpToken ( "~", "", 1, 62, false, OpToken.tokentype.unary_prefix, 0, 0);
        /// The \e boolean \e not operator
        protected static readonly OpToken boolean_not = new OpToken ( "!", "", 1, 62, false, OpToken.tokentype.unary_prefix, 0, 0);
        /// The \e unary \e minus operator
        protected static readonly OpToken unary_minus = new OpToken ( "-", "", 1, 62, false, OpToken.tokentype.unary_prefix, 0, 0);
        /// The \e unary \e plus operator
        protected static readonly OpToken unary_plus = new OpToken ( "+", "", 1, 62, false, OpToken.tokentype.unary_prefix, 0, 0);
        /// The \e address \e of operator
        protected static readonly OpToken addressof = new OpToken ( "&", "", 1, 62, false, OpToken.tokentype.unary_prefix, 0, 0);
        /// The \e pointer \e dereference operator
        protected static readonly OpToken dereference = new OpToken ( "*", "", 1, 62, false, OpToken.tokentype.unary_prefix, 0, 0);
        /// The \e type \e cast operator
        protected static readonly OpToken typecast = new OpToken ( "(", ")", 2, 62, false, OpToken.tokentype.presurround, 0, 0);
        /// The \e multiplication operator
        protected static readonly OpToken multiply = new OpToken ( "*", "", 2, 54, true, OpToken.tokentype.binary, 1, 0);
        /// The \e division operator
        protected static readonly OpToken divide = new OpToken ( "/", "", 2, 54, false, OpToken.tokentype.binary, 1, 0);
        /// The \e modulo operator
        protected static readonly OpToken modulo = new OpToken ( "%", "", 2, 54, false, OpToken.tokentype.binary, 1, 0);
        /// The \e binary \e addition operator
        protected static readonly OpToken binary_plus = new OpToken ( "+", "", 2, 50, true, OpToken.tokentype.binary, 1, 0);
        /// The \e binary \e subtraction operator
        protected static readonly OpToken binary_minus = new OpToken ( "-", "", 2, 50, false, OpToken.tokentype.binary, 1, 0);
        /// The \e left \e shift operator
        protected static readonly OpToken shift_left = new OpToken ( "<<", "", 2, 46, false, OpToken.tokentype.binary, 1, 0);
        /// The \e right \e shift operator
        protected static readonly OpToken shift_right = new OpToken ( ">>", "", 2, 46, false, OpToken.tokentype.binary, 1, 0);
        /// The signed \e right \e shift operator
        protected static readonly OpToken shift_sright = new OpToken ( ">>", "", 2, 46, false, OpToken.tokentype.binary, 1, 0);
        /// The \e less \e than operator
        protected static readonly OpToken less_than = new OpToken ( "<", "", 2, 42, false, OpToken.tokentype.binary, 1, 0);
        /// The \e less \e than \e or \e equal operator
        protected static readonly OpToken less_equal = new OpToken ( "<=", "", 2, 42, false, OpToken.tokentype.binary, 1, 0);
        /// The \e greater \e than operator
        protected static readonly OpToken greater_than = new OpToken ( ">", "", 2, 42, false, OpToken.tokentype.binary, 1, 0);
        /// The \e greater \e than \e or \e equal operator
        protected static readonly OpToken greater_equal = new OpToken ( ">=", "", 2, 42, false, OpToken.tokentype.binary, 1, 0);
        /// The \e equal operator
        protected static readonly OpToken equal = new OpToken ( "==", "", 2, 38, false, OpToken.tokentype.binary, 1, 0);
        /// The \e not \e equal operator
        protected static readonly OpToken not_equal = new OpToken ( "!=", "", 2, 38, false, OpToken.tokentype.binary, 1, 0);
        /// The \e logical \e and operator
        protected static readonly OpToken bitwise_and = new OpToken ( "&", "", 2, 34, true, OpToken.tokentype.binary, 1, 0);
        /// The \e logical \e xor operator
        protected static readonly OpToken bitwise_xor = new OpToken ( "^", "", 2, 30, true, OpToken.tokentype.binary, 1, 0);
        /// The \e logical \e or operator
        protected static readonly OpToken bitwise_or = new OpToken ( "|", "", 2, 26, true, OpToken.tokentype.binary, 1, 0);
        ///The \e boolean \e and operator
        protected static readonly OpToken boolean_and = new OpToken ( "&&", "", 2, 22, false, OpToken.tokentype.binary, 1, 0);
        /// The \e boolean \e xor operator
        protected static readonly OpToken boolean_xor = new OpToken ( "^^", "", 2, 20, false, OpToken.tokentype.binary, 1, 0);
        /// The \e boolean \e or operator
        protected static readonly OpToken boolean_or = new OpToken ( "||", "", 2, 18, false, OpToken.tokentype.binary, 1, 0);
        /// The \e assignment operator
        protected static readonly OpToken assignment = new OpToken ( "=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e comma operator (for parameter lists)
        protected static readonly OpToken comma = new OpToken ( ",", "", 2, 2, true, OpToken.tokentype.binary, 0, 0);
        /// The \e new operator
        protected static readonly OpToken new_op = new OpToken ( "", "", 2, 62, false, OpToken.tokentype.space, 1, 0);

        // Inplace assignment operators
        /// The \e in-place \e multiplication operator
        protected static readonly OpToken multequal = new OpToken ( "*=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e division operator
        protected static readonly OpToken divequal = new OpToken ( "/=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e modulo operator
        protected static readonly OpToken remequal = new OpToken ( "%=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e addition operator
        protected static readonly OpToken plusequal = new OpToken ( "+=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e subtraction operator
        protected static readonly OpToken minusequal = new OpToken ( "-=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e left \e shift operator
        protected static readonly OpToken leftequal = new OpToken ( "<<=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e right \e shift operator
        protected static readonly OpToken rightequal = new OpToken ( ">>=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e logical \e and operator
        protected static readonly OpToken andequal = new OpToken ( "&=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e logical \e or operator
        protected static readonly OpToken orequal = new OpToken ( "|=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);
        /// The \e in-place \e logical \e xor operator
        protected static readonly OpToken xorequal = new OpToken ( "^=", "", 2, 14, false, OpToken.tokentype.binary, 1, 5);

        // Operator tokens for type expressions
        /// Type declaration involving a space (identifier or adornment)
        protected static readonly OpToken type_expr_space = new OpToken ( "", "", 2, 10, false, OpToken.tokentype.space, 1, 0);
        /// Type declaration with no space
        protected static readonly OpToken type_expr_nospace = new OpToken ( "", "", 2, 10, false, OpToken.tokentype.space, 0, 0);
        /// Pointer adornment for a type declaration
        protected static readonly OpToken ptr_expr = new OpToken ( "*", "", 1, 62, false, OpToken.tokentype.unary_prefix, 0, 0);
        /// Array adornment for a type declaration
        protected static readonly OpToken array_expr = new OpToken ( "[", "]", 2, 66, false, OpToken.tokentype.postsurround, 1, 0);
        /// The \e concatenation operator for enumerated values
        protected static readonly OpToken enum_cat = new OpToken ( "|", "", 2, 26, true, OpToken.tokentype.binary, 0, 0);

        public const string EMPTY_STRING = ""; ///< An empty token
        public const string OPEN_CURLY = "{"; ///< "{" token
        public const string CLOSE_CURLY = "}";    ///< "}" token
        public const string SEMICOLON = ";";  ///< ";" token
        public const string COLON = ":";      ///< ":" token
        public const string EQUALSIGN = "=";  ///< "=" token
        public const string COMMA = ",";      ///< "," token
        public const string DOTDOTDOT = "...";  ///< "..." token
        public const string KEYWORD_VOID = "void";   ///< "void" keyword
        public const string KEYWORD_TRUE = "true";   ///< "true" keyword
        public const string KEYWORD_FALSE = "false";  ///< "false" keyword
        public const string KEYWORD_IF = "if"; ///< "if" keyword
        public const string KEYWORD_ELSE = "else";   ///< "else" keyword
        public const string KEYWORD_DO = "do"; ///< "do" keyword
        public const string KEYWORD_WHILE = "while";  ///< "while" keyword
        public const string KEYWORD_FOR = "for";    ///< "for" keyword
        public const string KEYWORD_GOTO = "goto";   ///< "goto" keyword
        public const string KEYWORD_BREAK = "break";  ///< "break" keyword
        public const string KEYWORD_CONTINUE = "continue";   ///< "continue" keyword
        public const string KEYWORD_CASE = "case";   ///< "case" keyword
        public const string KEYWORD_SWITCH = "switch"; ///< "switch" keyword
        public const string KEYWORD_DEFAULT = "default";    ///< "default" keyword
        public const string KEYWORD_RETURN = "return"; ///< "return" keyword
        public const string KEYWORD_NEW = "new";    ///< "new" keyword
        public const string typePointerRelToken = "ADJ";    ///< The token to print indicating PTRSUB relative to a TypePointerRel

        /// Set to \b true if we should emit NULL keyword
        protected bool option_NULL;
        /// Set to \b true if we should use '+=' '&=' etc.
        protected bool option_inplace_ops;
        /// Set to \b true if we should print calling convention
        protected bool option_convention;
        ///< Don't print a cast if \b true
        protected bool option_nocasts;
        /// Set to \b true if we should display unplaced comments
        protected bool option_unplaced;
        /// Set to \b true if we should hide implied extension operations
        protected bool option_hide_exts;
        /// Token to use for 'null'
        protected string nullToken;
        /// Characters to print to indicate a \e long integer token
        protected string sizeSuffix;
        /// Container/organizer for comments in the current function
        protected CommentSorter commsorter;

        // Routines that are specific to C/C++
        /// Prepare to push components of a data-type declaration
        /// Push nested components of a data-type declaration onto a stack, so we can access it bottom up
        /// \param ct is the data-type being emitted
        /// \param typestack will hold the sub-types involved in the displaying the declaration
        protected void buildTypeStack(Datatype ct, List<Datatype> typestack)
        {
            while(true) {
                typestack.Add(ct);
                if (ct.getName().Length != 0)  // This can be a base type
                    break;
                if (ct.getMetatype() == type_metatype.TYPE_PTR)
                    ct = ((TypePointer)ct).getPtrTo();
                else if (ct.getMetatype() == type_metatype.TYPE_ARRAY)
                    ct = ((TypeArray)ct).getBase();
                else if (ct.getMetatype() == type_metatype.TYPE_CODE) {
                    FuncProto proto = ((TypeCode)ct).getPrototype();
                    if (proto != (FuncProto)null)
                        ct = proto.getOutputType();
                    else
                        ct = glb.types.getTypeVoid();
                }
                else
                    break;          // Some other anonymous type
            }
        }

        /// Push input parameters
        /// Push the comma separated list of data-type declarations onto the RPN stack as
        /// part of emitting a given function prototype
        /// \param proto is the given function prototype
        protected void pushPrototypeInputs(FuncProto proto)
        {
            int sz = proto.numParams();

            if ((sz == 0) && (!proto.isDotdotdot()))
                pushAtom(new Atom(KEYWORD_VOID, syntax, EmitMarkup.syntax_highlight.keyword_color));
            else {
                for (int i = 0; i < sz - 1; ++i)
                    pushOp(&comma, (PcodeOp)null); // Print a comma for each parameter (above 1)
                if (proto.isDotdotdot() && (sz != 0)) // Print comma for dotdotdot (if it is not by itself)
                    pushOp(&comma, (PcodeOp)null);
                for (int i = 0; i < sz; ++i) {
                    ProtoParameter param = proto.getParam(i);
                    pushTypeStart(param.getType(), true);
                    pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
                    pushTypeEnd(param.getType());
                }
                if (proto.isDotdotdot()) {
                    if (sz != 0)
                        pushAtom(new Atom(DOTDOTDOT, syntax, EmitMarkup.syntax_highlight.no_color));
                    else {
                        // In ANSI C, a prototype with empty parens means the parameters are unspecified (not void)
                        // In C++, empty parens mean void, we use the ANSI C convention
                        pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color)); // An empty list of parameters
                    }
                }
            }
        }

        /// Push tokens resolving a symbol's scope
        /// Calculate what elements of a given symbol's namespace path are necessary to distinguish
        /// it within the current scope. Then print these elements.
        /// \param symbol is the given symbol
        protected void pushSymbolScope(Symbol symbol)
        {
            int scopedepth;
            if (namespc_strategy == namespace_strategy.MINIMAL_NAMESPACES)
                scopedepth = symbol.getResolutionDepth(curscope);
            else if (namespc_strategy == namespace_strategy.ALL_NAMESPACES) {
                if (symbol.getScope() == curscope)
                    scopedepth = 0;
                else
                    scopedepth = symbol.getResolutionDepth((Scope)null);
            }
            else
                scopedepth = 0;
            if (scopedepth != 0)
            {
                List<Scope> scopeList = new List<Scope>();
                Scope point = symbol.getScope();
                for (int i = 0; i < scopedepth; ++i) {
                    scopeList.Add(point);
                    point = point.getParent();
                    pushOp(&scope, (PcodeOp)null);
                }
                for (int i = scopedepth - 1; i >= 0; --i) {
                    pushAtom(new Atom(scopeList[i].getDisplayName(), syntax,
                        EmitMarkup.syntax_highlight.global_color, (PcodeOp)null, (Varnode)null));
                }
            }
        }

        /// Emit tokens resolving a symbol's scope
        /// Emit the elements of the given symbol's namespace path that distinguish it within
        /// the current scope.
        /// \param symbol is the given Symbol
        protected void emitSymbolScope(Symbol symbol)
        {
            int scopedepth;
            if (namespc_strategy == namespace_strategy.MINIMAL_NAMESPACES)
                scopedepth = symbol.getResolutionDepth(curscope);
            else if (namespc_strategy == namespace_strategy.ALL_NAMESPACES) {
                if (symbol.getScope() == curscope)
                    scopedepth = 0;
                else
                    scopedepth = symbol.getResolutionDepth((Scope)null);
            }
            else
                scopedepth = 0;
            if (scopedepth != 0) {
                List <Scope> scopeList = new List<Scope>();
                Scope point = symbol.getScope();
                for (int i = 0; i < scopedepth; ++i) {
                    scopeList.Add(point);
                    point = point.getParent();
                }
                for (int i = scopedepth - 1; i >= 0; --i) {
                    emit.print(scopeList[i].getDisplayName(), EmitMarkup.syntax_highlight.global_color);
                    emit.print(scope.print1, EmitMarkup.syntax_highlight.no_color);
                }
            }
        }

        /// Push part of a data-type declaration onto the RPN stack, up to the identifier
        /// Store off array sizes for printing after the identifier
        /// \param ct is the data-type to push
        /// \param noident is \b true if an identifier will not be pushed as part of the declaration
        protected virtual void pushTypeStart(Datatype ct, bool noident)
        {
            // Find the root type (the one with an identifier) and layout
            // the stack of types, so we can access in reverse order
            List<Datatype> typestack = new List<Datatype>();
            buildTypeStack(ct, typestack);

            ct = typestack.GetLastItem();  // The base type
            OpToken* tok;

            if (noident && (typestack.size() == 1))
                tok = &type_expr_nospace;
            else
                tok = &type_expr_space;

            if (ct.getName().size() == 0)
            {   // Check for anonymous type
                // We could support a struct or enum declaration here
                string nm = genericTypeName(ct);
                pushOp(tok, (PcodeOp)null);
                pushAtom(new Atom(nm, typetoken, EmitMarkup.syntax_highlight.type_color, ct));
            }
            else
            {
                pushOp(tok, (PcodeOp)null);
                pushAtom(new Atom(ct.getDisplayName(), typetoken, EmitMarkup.syntax_highlight.type_color, ct));
            }
            for (int i = typestack.size() - 2; i >= 0; --i)
            {
                ct = typestack[i];
                if (ct.getMetatype() == type_metatype.TYPE_PTR)
                    pushOp(&ptr_expr, (PcodeOp)null);
                else if (ct.getMetatype() == type_metatype.TYPE_ARRAY)
                    pushOp(&array_expr, (PcodeOp)null);
                else if (ct.getMetatype() == type_metatype.TYPE_CODE)
                    pushOp(&function_call, (PcodeOp)null);
                else
                {
                    clear();
                    throw new CORE.LowlevelError("Bad type expression");
                }
            }
        }

        /// Push the tail ends of a data-type declaration onto the RPN stack
        /// Because the front-ends were pushed on
        /// base-type . final-modifier, the tail-ends are pushed on
        /// final-modifier . base-type.
        /// The tail-ends amount to
        ///   - array subscripts      . [ # ] and
        ///   - function parameters   . ( paramlist )
        ///
        /// \param ct is the data-type being pushed
        protected virtual void pushTypeEnd(Datatype ct)
        {
            pushMod();
            setMod(force_dec);

            while(true) {
                if (ct.getName().Length != 0)  // This is the base type
                    break;
                if (ct.getMetatype() == type_metatype.TYPE_PTR)
                    ct = ((TypePointer)ct).getPtrTo();
                else if (ct.getMetatype() == type_metatype.TYPE_ARRAY) {
                    TypeArray ctarray = (TypeArray)ct;
                    ct = ctarray.getBase();
                    push_integer(ctarray.numElements(), 4, false, (Varnode)null, (PcodeOp)null);
                }
                else if (ct.getMetatype() == type_metatype.TYPE_CODE) {
                    TypeCode ctcode = (TypeCode)ct;
                    FuncProto proto = ctcode.getPrototype();
                    if (proto != (FuncProto)null) {
                        pushPrototypeInputs(proto);
                        ct = proto.getOutputType();
                    }
                    else
                        // An empty list of parameters
                        pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
                }
                else
                    break;          // Some other anonymous type
            }

            popMod();
        }

        /// \brief Push a \b true or \b false token to the RPN stack
        ///
        /// A single Atom representing the boolean value is emitted
        /// \param val is the boolean value (non-zero for \b true)
        /// \param ct is the data-type associated with the value
        /// \param vn is the Varnode holding the value
        /// \param op is the PcodeOp using the value
        protected void pushBoolConstant(ulong val, TypeBase ct, Varnode vn, PcodeOp op)
        {
            if (val != 0)
                pushAtom(new Atom(KEYWORD_TRUE, vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
            else
                pushAtom(new Atom(KEYWORD_FALSE, vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
        }

        /// \brief Push a single character constant to the RPN stack
        ///
        /// For C, a character constant is usually emitted as the character in single quotes.
        /// Handle unicode, wide characters, etc. Characters come in with the compiler's raw encoding.
        /// \param val is the constant value
        /// \param ct is data-type attached to the value
        /// \param vn is the Varnode holding the value
        /// \param op is the PcodeOp using the value
        protected void pushCharConstant(ulong val, Datatype ct, Varnode vn, PcodeOp op)
        {
            Symbol.DisplayFlags displayFormat = 0;
            bool isSigned = (ct.getMetatype() == type_metatype.TYPE_INT);
            if ((vn != (Varnode)null)&& (!vn.isAnnotation())) {
                HighVariable high = vn.getHigh();
                Symbol sym = high.getSymbol();
                if (sym != (Symbol)null) {
                    if (sym.isNameLocked() && (sym.getCategory() == Symbol.SymbolCategory.equate)) {
                        if (pushEquate(val, vn.getSize(), (EquateSymbol)sym, vn, op))
                            return;
                    }
                    displayFormat = sym.getDisplayFormat();
                }
                if (displayFormat == 0)
                    displayFormat = high.getType().getDisplayFormat();
            }
            if (displayFormat != 0 && displayFormat != Symbol.DisplayFlags.force_char) {
                if (!castStrategy.caresAboutCharRepresentation(vn, op)) {
                    push_integer(val, ct.getSize(), isSigned, vn, op);
                    return;
                }
            }
            if ((ct.getSize() == 1) && (val >= 0x80))
            {
                // For byte characters, the encoding is assumed to be ASCII, UTF-8, or some other
                // code-page that extends ASCII. At 0x80 and above, we cannot treat the value as a
                // unicode code-point. Its either part of a multi-byte UTF-8 encoding or an unknown
                // code-page value. In either case, we print as an integer or an escape sequence.
                if (displayFormat != Symbol.DisplayFlags.force_hex
                    && displayFormat != Symbol.DisplayFlags.force_char)
                {
                    push_integer(val, 1, isSigned, vn, op);
                    return;
                }
                displayFormat = Symbol.DisplayFlags.force_hex;  // Fallthru but force a hex representation
            }
            StringWriter t = new StringWriter();
            // From here we assume, the constant value is a direct unicode code-point.
            // The value could be an illegal code-point (surrogates or beyond the max code-point),
            // but this will just be emitted as an escape sequence.
            if (doEmitWideCharPrefix() && ct.getSize() > 1)
                t.Write('L');       // Print symbol indicating wide character
            t.Write("'");          // char is surrounded with single quotes
            if (displayFormat == Symbol.DisplayFlags.force_hex) {
                printCharHexEscape(t, (int)val);
            }
            else
                printUnicode(t, (int)val);
            t.Write("'");
            pushAtom(new Atom(t.str(), vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
        }

        protected override void pushConstant(ulong val, Datatype ct, Varnode vn, PcodeOp op)
        {
            Datatype subtype;
            switch (ct.getMetatype()) {
                case type_metatype.TYPE_UINT:
                    if (ct.isCharPrint())
                        pushCharConstant(val, (TypeChar)ct, vn, op);
                    else if (ct.isEnumType())
                        pushEnumConstant(val, (TypeEnum)ct, vn, op);
                    else
                        push_integer(val, ct.getSize(), false, vn, op);
                    return;
                case type_metatype.TYPE_INT:
                    if (ct.isCharPrint())
                        pushCharConstant(val, (TypeChar)ct, vn, op);
                    else if (ct.isEnumType())
                        pushEnumConstant(val, (TypeEnum)ct, vn, op);
                    else
                        push_integer(val, ct.getSize(), true, vn, op);
                    return;
                case type_metatype.TYPE_UNKNOWN:
                    push_integer(val, ct.getSize(), false, vn, op);
                    return;
                case type_metatype.TYPE_BOOL:
                    pushBoolConstant(val, (TypeBase)ct,vn,op);
                    return;
                case type_metatype.TYPE_VOID:
                    clear();
                    throw new CORE.LowlevelError("Cannot have a constant of type void");
                case type_metatype.TYPE_PTR:
                case type_metatype.TYPE_PTRREL:
                    if (option_NULL && (val == 0))
                    { // A null pointer
                        pushAtom(new Atom(nullToken, vartoken, EmitMarkup.syntax_highlight.var_color, op, vn));
                        return;
                    }
                    subtype = ((TypePointer)ct).getPtrTo();
                    if (subtype.isCharPrint())
                    {
                        if (pushPtrCharConstant(val, (TypePointer)ct,vn,op))
                            return;
                    }
                    else if (subtype.getMetatype() == type_metatype.TYPE_CODE)
                    {
                        if (pushPtrCodeConstant(val, (TypePointer)ct,vn,op))
                            return;
                    }
                    break;
                case type_metatype.TYPE_FLOAT:
                    push_float(val, ct.getSize(), vn, op);
                    return;
                case type_metatype.TYPE_SPACEBASE:
                case type_metatype.TYPE_CODE:
                case type_metatype.TYPE_ARRAY:
                case type_metatype.TYPE_STRUCT:
                case type_metatype.TYPE_UNION:
                case type_metatype.TYPE_PARTIALSTRUCT:
                case type_metatype.TYPE_PARTIALUNION:
                    break;
            }
            // Default printing
            if (!option_nocasts) {
                pushOp(&typecast, op);
                pushType(ct);
            }
            pushMod();
            if (!isSet(force_dec))
                setMod(force_hex);
            push_integer(val, ct.getSize(), false, vn, op);
            popMod();
        }

        /// \brief Push an enumerated value to the RPN stack
        ///
        /// Handle cases where the value is built out of multiple named elements of the
        /// enumeration or where the value cannot be expressed using named elements
        /// \param val is the enumerated value being pushed
        /// \param ct is the enumerated data-type attached to the value
        /// \param vn is the Varnode holding the value
        /// \param op is the PcodeOp using the value
        protected void pushEnumConstant(ulong val, TypeEnum ct, Varnode vn, PcodeOp op)
        {
            List<string> valnames = new List<string>();

            bool complement = ct.getMatches(val, valnames);
            if (valnames.Count > 0) {
                if (complement)
                    pushOp(&bitwise_not, op);
                for (int i = valnames.Count - 1; i > 0; --i)
                    pushOp(&enum_cat, op);
                for (int i = 0; i < valnames.Count; ++i)
                    pushAtom(new Atom(valnames[i], vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
            }
            else {
                push_integer(val, ct.getSize(), false, vn, op);
                //    ostringstream s;
                //    s << "BAD_ENUM(0x" << hex << val << ")";
                //    pushAtom(new Atom(s.str(),vartoken,EmitMarkup.syntax_highlight.const_color,op,vn));
            }
        }

        /// \brief Attempt to push a quoted string representing a given constant pointer onto the RPN stack
        ///
        /// Check if the constant pointer refers to character data that can be emitted as a quoted string.
        /// If so push the string, if not return \b false to indicate a token was not pushed
        /// \param val is the value of the given constant pointer
        /// \param ct is the pointer data-type attached to the value
        /// \param vn is the Varnode holding the value (may be null)
        /// \param op is the PcodeOp using the value (may be null)
        /// \return \b true if a quoted string was pushed to the RPN stack
        protected virtual bool pushPtrCharConstant(ulong val, TypePointer ct, Varnode vn, PcodeOp op)
        {
            if (val == 0) return false;
            AddrSpace spc = glb.getDefaultDataSpace();
            ulong fullEncoding;
            Address point;
            if (op != (PcodeOp)null)
                point = op.getAddr();
            Address stringaddr = glb.resolveConstant(spc, val, ct.getSize(), point, fullEncoding);
            if (stringaddr.isInvalid()) return false;
            if (!glb.symboltab.getGlobalScope().isReadOnly(stringaddr, 1, new Address()))
                return false;        // Check that string location is readonly

            StringWriter str = new StringWriter();
            Datatype subct = ct.getPtrTo();
            if (!printCharacterConstant(str, stringaddr, subct))
                return false;       // Can we get a nice ASCII string

            pushAtom(new Atom(str.ToString(), vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
            return true;
        }

        /// \brief Attempt to push a function name representing a constant pointer onto the RPN stack
        ///
        /// Given the pointer value, try to look up the function at that address and push
        /// the function's name as a single Atom.
        /// \param val is the given constant pointer value
        /// \param ct is the pointer data-type attached to the value
        /// \param vn is the Varnode holding the value
        /// \param op is the PcodeOp using the value
        /// \return \b true if a name was pushed to the RPN stack, return \b false otherwise
        protected bool pushPtrCodeConstant(ulong val, TypePointer ct, Varnode vn, PcodeOp op)
        {
            AddrSpace spc = glb.getDefaultCodeSpace();
            Funcdata fd = (Funcdata)null;
            val = AddrSpace.addressToByte(val, spc.getWordSize());
            fd = glb.symboltab.getGlobalScope().queryFunction(new Address(spc, val));
            if (fd != (Funcdata)null) {
                pushAtom(new Atom(fd.getDisplayName(), functoken, EmitMarkup.syntax_highlight.funcname_color, op, fd));
                return true;
            }
            return false;
        }

        /// \brief Return \b true if this language requires a prefix when expressing \e wide characters
        ///
        /// The c-language standard requires that strings (and character constants) made up of \e wide
        /// character elements have an 'L' prefix added before the quote characters.  Other related languages
        /// may not do this.  Having this as a virtual method lets derived languages to tailor their strings
        /// while still using the basic PrintC functionality
        /// \return \b true if a prefix should be printed
        protected virtual bool doEmitWideCharPrefix()
        {
            return true;
        }

        /// Determine whether a LOAD/STORE expression requires pointer '*' syntax
        /// An expression involving a LOAD or STORE can sometimes be emitted using
        /// \e array syntax (or \e field \e member syntax). This method determines
        /// if this kind of syntax is appropriate or if a '*' operator is required.
        /// \param vn is the root of the pointer expression (feeding into LOAD or STORE)
        /// \return \b false if '*' syntax is required, \b true if some other syntax is used
        protected bool checkArrayDeref(Varnode vn)
        {
            PcodeOp op;

            if (!vn.isImplied()) return false;
            if (!vn.isWritten()) return false;
            op = vn.getDef();
            if (op.code() == OpCode.CPUI_SEGMENTOP) {
                vn = op.getIn(2);
                if (!vn.isImplied()) return false;
                if (!vn.isWritten()) return false;
                op = vn.getDef();
            }
            if ((op.code() != OpCode.CPUI_PTRSUB) && (op.code() != OpCode.CPUI_PTRADD)) return false;
            return true;
        }

        /// Emit the definition of a \e structure data-type
        /// Print all the components making up the data-type, using the \b struct keyword
        /// \param ct is the structure data-type
        protected void emitStructDefinition(TypeStruct ct)
        {
            if (ct.getName().Length == 0) {
                clear();
                throw new CORE.LowlevelError("Trying to save unnamed structure");
            }

            emit.tagLine();
            emit.print("typedef struct", EmitMarkup.syntax_highlight.keyword_color);
            emit.spaces(1);
            int id = emit.startIndent();
            emit.print(OPEN_CURLY);
            emit.tagLine();
            IEnumerator<TypeField> iter = ct.beginField();
            if (iter.MoveNext()) {
                while (true) {
                    pushTypeStart(iter.Current.type, false);
                    pushAtom(new Atom(iter.Current.name, syntax, EmitMarkup.syntax_highlight.var_color));
                    pushTypeEnd(iter.Current.type);
                    if (!iter.MoveNext()) {
                        break;
                    }
                    emit.print(COMMA); // Print comma separator
                    emit.tagLine();
                }
            }
            emit.stopIndent(id);
            emit.tagLine();
            emit.print(CLOSE_CURLY);
            emit.spaces(1);
            emit.print(ct.getDisplayName());
            emit.print(SEMICOLON);
        }

        /// Emit the definition of an \e enumeration data-type
        /// Print all the named values making up the data-type, using the \b enum keyword
        /// \param ct is the enumerated data-type
        protected void emitEnumDefinition(TypeEnum ct)
        {
            if (ct.getName().Length == 0) {
                clear();
                throw new CORE.LowlevelError("Trying to save unnamed enumeration");
            }

            pushMod();
            bool sign = (ct.getMetatype() == type_metatype.TYPE_INT);
            emit.tagLine();
            emit.print("typedef enum", EmitMarkup.syntax_highlight.keyword_color);
            emit.spaces(1);
            int id = emit.startIndent();
            emit.print(OPEN_CURLY);
            emit.tagLine();
            IEnumerator<KeyValuePair<ulong, string>> iter = ct.beginEnum();
            bool firstLine = true;
            while (iter.MoveNext()) {
                if (firstLine)
                    firstLine = false;
                else
                    emit.tagLine();
                emit.print(iter.Current.Value, EmitMarkup.syntax_highlight.const_color);
                emit.spaces(1);
                emit.print(EQUALSIGN, EmitMarkup.syntax_highlight.no_color);
                emit.spaces(1);
                push_integer(iter.Current.Key, ct.getSize(), sign, (Varnode)null, (PcodeOp)null);
                recurse();
                emit.print(SEMICOLON);
            }
            popMod();
            emit.stopIndent(id);
            emit.tagLine();
            emit.print(CLOSE_CURLY);
            emit.spaces(1);
            emit.print(ct.getDisplayName());
            emit.print(SEMICOLON);
        }

        /// Emit the output data-type of a function prototype
        /// In C, when printing a function prototype, the function's output data-type is displayed first
        /// as a type declaration, where the function name acts as the declaration's identifier.
        /// This method emits the declaration in preparation for this.
        /// \param proto is the function prototype object
        /// \param fd is the (optional) Funcdata object providing additional meta-data about the function
        protected void emitPrototypeOutput(FuncProto proto, Funcdata fd)
        {
            PcodeOp op;
            Varnode vn;

            if (fd != (Funcdata)null) {
                op = fd.getFirstReturnOp();
                if (op != (PcodeOp)null && op.numInput() < 2)
                    op = (PcodeOp)null;
            }
            else
                op = (PcodeOp)null;

            Datatype outtype = proto.getOutputType();
            if ((outtype.getMetatype() != type_metatype.TYPE_VOID) && (op != (PcodeOp)null))
                vn = op.getIn(1);
            else
                vn = (Varnode)null;
            int id = emit.beginReturnType(vn);
            pushType(outtype);
            recurse();
            emit.endReturnType(id);
        }

        /// Emit the input data-types of a function prototype
        /// This emits the individual type declarations of the input parameters to the function as a
        /// comma separated list.
        /// \param proto is the given prototype of the function
        protected void emitPrototypeInputs(FuncProto proto)
        {
            int sz = proto.numParams();

            if (sz == 0)
                emit.print(KEYWORD_VOID, EmitMarkup.syntax_highlight.keyword_color);
            else {
                bool printComma = false;
                for (int i = 0; i < sz; ++i) {
                    if (printComma)
                        emit.print(COMMA);
                    ProtoParameter param = proto.getParam(i);
                    if (isSet(hide_thisparam) && param.isThisPointer())
                        continue;
                    Symbol sym = param.getSymbol();
                    printComma = true;
                    if (sym != (Symbol)null)
                        emitVarDecl(sym);
                    else {
                        // Emit type without name, if there is no backing symbol
                        pushTypeStart(param.getType(), true);
                        pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
                        pushTypeEnd(param.getType());
                        recurse();
                    }
                }
            }
            if (proto.isDotdotdot()) {
                if (sz != 0)
                    emit.print(COMMA);
                emit.print(DOTDOTDOT);
            }
        }

        /// Emit variable declarations for all global symbols under given scope
        /// For the given scope and all of its children that are not \e function scopes,
        /// emit a variable declaration for each symbol.
        /// \param symScope is the given scope
        protected void emitGlobalVarDeclsRecursive(Scope symScope)
        {
            if (!symScope.isGlobal()) return;
            emitScopeVarDecls(symScope, Symbol.SymbolCategory.no_category);
            ScopeMap.Enumerator iter = symScope.childrenBegin();
            while(iter.MoveNext()) {
                emitGlobalVarDeclsRecursive(iter.Current.Value);
            }
        }

        /// Emit variable declarations for a function
        /// A formal variable declaration is emitted for every symbol in the given
        /// function scope. I.e. all local variables are declared.
        /// \param fd is the function being emitted
        protected void emitLocalVarDecls(Funcdata fd)
        {
            bool notempty = false;

            if (emitScopeVarDecls(fd.getScopeLocal(), Symbol.SymbolCategory.no_category))
                notempty = true;
            ScopeMap.Enumerator iter = fd.getScopeLocal().childrenBegin();
            while (iter.MoveNext()) {
                Scope l1 = iter.Current.Value;
                if (emitScopeVarDecls(l1, Symbol.SymbolCategory.no_category))
                    notempty = true;
            }

            if (notempty)
                emit.tagLine();
        }

        /// Emit a statement in the body of a function
        /// This emits an entire statement rooted at a given operation. All associated expressions
        /// on the right-hand and left-hand sides are recursively emitted. Depending on the current
        /// printing properties, the statement is usually terminated with ';' character.
        /// \param inst is the given root PcodeOp of the statement
        protected void emitStatement(PcodeOp inst)
        {
            int id = emit.beginStatement(inst);
            emitExpression(inst);
            emit.endStatement(id);
            if (!isSet(comma_separate))
                emit.print(SEMICOLON);
        }

        /// Attempt to emit an expression rooted at an \e in-place operator
        /// Check that the given p-code op has an \e in-place token form and if the first input and the output
        /// are references to  the same variable. If so, emit the expression using the \e in-place token.
        /// \param op is the given PcodeOp
        /// \return \b true if the expression was emitted (as in-place), or \b false if not emitted at all
        protected bool emitInplaceOp(PcodeOp op)
        {
            OpToken tok;
            switch (op.code()) {
                case OpCode.CPUI_INT_MULT:
                    tok = &multequal;
                    break;
                case OpCode.CPUI_INT_DIV:
                case OpCode.CPUI_INT_SDIV:
                    tok = &divequal;
                    break;
                case OpCode.CPUI_INT_REM:
                case OpCode.CPUI_INT_SREM:
                    tok = &remequal;
                    break;
                case OpCode.CPUI_INT_ADD:
                    tok = &plusequal;
                    break;
                case OpCode.CPUI_INT_SUB:
                    tok = &minusequal;
                    break;
                case OpCode.CPUI_INT_LEFT:
                    tok = &leftequal;
                    break;
                case OpCode.CPUI_INT_RIGHT:
                case OpCode.CPUI_INT_SRIGHT:
                    tok = &rightequal;
                    break;
                case OpCode.CPUI_INT_AND:
                    tok = &andequal;
                    break;
                case OpCode.CPUI_INT_OR:
                    tok = &orequal;
                    break;
                case OpCode.CPUI_INT_XOR:
                    tok = &xorequal;
                    break;
                default:
                    return false;
            }
            Varnode vn = op.getIn(0);
            if (op.getOut().getHigh() != vn.getHigh()) return false;
            pushOp(tok, op);
            pushVnExplicit(vn, op);
            pushVn(op.getIn(1), op, mods);
            recurse();
            return true;
        }

        /// \brief Emit a statement representing an unstructured branch
        ///
        /// Given the type of unstructured branch, with source and destination blocks,
        /// construct a statement with the appropriate c-language keyword (\b goto, \b break, \b continue)
        /// representing a control-flow branch between the blocks.
        /// \param bl is the source block
        /// \param exp_bl is the destination block (which may provide a label)
        /// \param type is the given type of the branch
        protected void emitGotoStatement(FlowBlock bl, FlowBlock exp_bl, uint type)
        {
            int id = emit.beginStatement(bl.lastOp());
            switch (type) {
                case FlowBlock::f_break_goto:
                    emit.print(KEYWORD_BREAK, EmitMarkup.syntax_highlight.keyword_color);
                    break;
                case FlowBlock::f_continue_goto:
                    emit.print(KEYWORD_CONTINUE, EmitMarkup.syntax_highlight.keyword_color);
                    break;
                case FlowBlock::f_goto_goto:
                    emit.print(KEYWORD_GOTO, EmitMarkup.syntax_highlight.keyword_color);
                    emit.spaces(1);
                    emitLabel(exp_bl);
                    break;
            }
            emit.print(SEMICOLON);
            emit.endStatement(id);
        }

        /// Emit labels for a \e case block
        /// Given a \e switch block and an index indicating a particular \e case block,
        /// look up all the labels associated with that \e case and emit them
        /// using formal labels with the \b case keyword and a ':' terminator.
        /// \param casenum is the given index of the \e case block
        /// \param switchbl is the root block of the switch
        protected void emitSwitchCase(int casenum, BlockSwitch switchbl)
        {
            int i, num;
            ulong val;
            Datatype ct;

            ct = switchbl.getSwitchType();

            if (switchbl.isDefaultCase(casenum)) {
                emit.tagLine();
                emit.print(KEYWORD_DEFAULT, EmitMarkup.syntax_highlight.keyword_color);
                emit.print(COLON);
            }
            else {
                num = switchbl.getNumLabels(casenum);
                for (i = 0; i < num; ++i) {
                    val = switchbl.getLabel(casenum, i);
                    emit.tagLine();
                    emit.print(KEYWORD_CASE, EmitMarkup.syntax_highlight.keyword_color);
                    emit.spaces(1);
                    pushConstant(val, ct, (Varnode)null, (PcodeOp)null);
                    recurse();
                    emit.print(COLON);
                }
            }
        }

        /// Emit a formal label for a given control-flow block
        /// Check for an explicit label that has been registered with the basic block.
        /// Otherwise, construct a generic label based on the entry address
        /// of the block.  Emit the label as a single token.
        /// \param bl is the given block
        protected void emitLabel(FlowBlock bl)
        {
            bl = bl.getFrontLeaf();
            if (bl == (FlowBlock)null) return;
            BlockBasic bb = (BlockBasic)bl.subBlock(0);
            Address addr = bb.getEntryAddr();
            AddrSpace spc = addr.getSpace();
            ulong off = addr.getOffset();
            if (!bb.hasSpecialLabel()) {
                if (bb.getType() == FlowBlock::t_basic) {
                    Scope symScope = ((BlockBasic)bb).getFuncdata().getScopeLocal();
                    Symbol sym = symScope.queryCodeLabel(addr);
                    if (sym != (Symbol)null) {
                        emit.tagLabel(sym.getDisplayName(), EmitMarkup.syntax_highlight.no_color, spc, off);
                        return;
                    }
                }
            }
            StringWriter lb = new StringWriter();
            if (bb.isJoined())
                lb.Write("joined_");
            else if (bb.isDuplicated())
                lb.Write("dup_");
            else
                lb.Write("code_");
            lb.Write(addr.getShortcut());
            addr.printRaw(lb);
            emit.tagLabel(lb.str(), EmitMarkup.syntax_highlight.no_color, spc, off);
        }

        /// Emit any required label statement for a given basic block
        /// If the basic block is the destination of a \b goto statement, emit a
        /// label for the block followed by the ':' terminator.
        /// \param bl is the given control-flow block
        protected void emitLabelStatement(FlowBlock bl)
        {
            if (isSet(only_branch)) return;

            if (isSet(flat)) {
                // Printing flat version
                if (!bl.isJumpTarget()) return; // Print all jump targets
            }
            else {
                // Printing structured version
                if (!bl.isUnstructuredTarget()) return;
                if (bl.getType() != FlowBlock.block_type.t_copy) return;
                // Only print labels that have unstructured jump to them
            }
            emit.tagLine(0);
            emitLabel(bl);
            emit.print(COLON);
        }

        /// Emit any required label statement for a given control-flow block
        /// The block does not have to be a basic block.  This routine finds the entry basic
        /// block and prints any necessary labels for that.
        /// \param bl is the given control-flow block
        protected void emitAnyLabelStatement(FlowBlock bl)
        {
            if (bl.isLabelBumpUp()) return; // Label printed by someone else
            bl = bl.getFrontLeaf();
            if (bl == (FlowBlock)null) return;
            emitLabelStatement(bl);
        }

        /// Emit comments associated with a given statement
        /// Collect any comment lines the sorter has associated with a statement
        /// rooted at a given PcodeOp and emit them using appropriate delimiters
        /// \param inst is the given PcodeOp
        protected void emitCommentGroup(PcodeOp inst)
        {
            commsorter.setupOpList(inst);
            while (commsorter.hasNext()) {
                Comment comm = commsorter.getNext();
                if (comm.isEmitted()) continue;
                if ((instr_comment_type & comm.getType()) == 0) continue;
                emitLineComment(-1, comm);
            }
        }

        /// Emit any comments under the given control-flow subtree
        /// With the control-flow hierarchy, print any comments associated with basic blocks in
        /// the specified subtree.  Used where statements from multiple basic blocks are printed on
        /// one line and a normal comment would get printed in the middle of this line.
        /// \param bl is the root of the control-flow subtree
        protected void emitCommentBlockTree(FlowBlock bl)
        {
            if (bl == (FlowBlock)null) return;
            FlowBlock.block_type btype = bl.getType();
            if (btype == FlowBlock.block_type.t_copy) {
                bl = bl.subBlock(0);
                btype = bl.getType();
            }
            if (btype == FlowBlock.block_type.t_plain) return;
            if (bl.getType() != FlowBlock.block_flags.t_basic) {
                BlockGraph rootbl = (BlockGraph)bl;
                int size = rootbl.getSize();
                for (int i = 0; i < size; ++i) {
                    emitCommentBlockTree(rootbl.subBlock(i));
                }
                return;
            }
            commsorter.setupBlockList(bl);
            emitCommentGroup((PcodeOp)null);    // Emit any comments for the block
        }

        /// Emit comments in the given function's header
        /// Collect all comment lines marked as \e header for the function and
        /// emit them with the appropriate delimiters.
        /// \param fd is the given function
        protected void emitCommentFuncHeader(Funcdata fd)
        {
            bool extralinebreak = false;
            commsorter.setupHeader(CommentSorter.HeaderCommentFlag.header_basic);
            while (commsorter.hasNext()) {
                Comment comm = commsorter.getNext();
                if (comm.isEmitted()) continue;
                if ((head_comment_type & comm.getType()) == 0) continue;
                emitLineComment(0, comm);
                extralinebreak = true;
            }
            if (option_unplaced) {
                if (extralinebreak)
                    emit.tagLine();
                extralinebreak = false;
                commsorter.setupHeader(CommentSorter.HeaderCommentFlag.header_unplaced);
                while (commsorter.hasNext()) {
                    Comment comm = commsorter.getNext();
                    if (comm.isEmitted()) continue;
                    if (!extralinebreak) {
                        Comment label = new Comment(Comment.comment_type.warningheader, fd.getAddress(),
                            fd.getAddress(),0, "Comments that could not be placed in the function body:");
                        emitLineComment(0, &label);
                        extralinebreak = true;
                    }
                    emitLineComment(1, comm);
                }
            }
            if (option_nocasts) {
                if (extralinebreak)
                    emit.tagLine();
                Comment comm = new Comment(Comment.comment_type.warningheader, fd.getAddress(), fd.getAddress(),
                    0, "DISPLAY WARNING: Type casts are NOT being printed");
                emitLineComment(0, &comm);
                extralinebreak = true;
            }
            if (extralinebreak)
                emit.tagLine();        // Extra linebreak if comment exists
        }

        /// Emit block as a \e for loop
        /// Print the loop using the keyword \e for, followed by a semicolon separated
        ///   - Initializer statement
        ///   - Condition statment
        ///   - Iterate statement
        ///
        /// Then print the body of the loop
        protected void emitForLoop(BlockWhileDo bl)
        {
            PcodeOp op;
            int indent;

            pushMod();
            unsetMod(no_branch | only_branch);
            emitAnyLabelStatement(bl);
            FlowBlock condBlock = bl.getBlock(0);
            emitCommentBlockTree(condBlock);
            emit.tagLine();
            op = condBlock.lastOp();
            emit.tagOp(KEYWORD_FOR, EmitMarkup.syntax_highlight.keyword_color, op);
            emit.spaces(1);
            int id1 = emit.openParen(OPEN_PAREN);
            pushMod();
            setMod(comma_separate);
            op = bl.getInitializeOp();     // Emit the (optional) initializer statement
            if (op != (PcodeOp)null) {
                int id3 = emit.beginStatement(op);
                emitExpression(op);
                emit.endStatement(id3);
            }
            emit.print(SEMICOLON);
            emit.spaces(1);
            condBlock.emit(this);      // Emit the conditional statement
            emit.print(SEMICOLON);
            emit.spaces(1);
            op = bl.getIterateOp();        // Emit the iterator statement
            int id4 = emit.beginStatement(op);
            emitExpression(op);
            emit.endStatement(id4);
            popMod();
            emit.closeParen(CLOSE_PAREN, id1);
            emit.spaces(1);
            indent = emit.startIndent();
            emit.print(OPEN_CURLY);
            setMod(no_branch); // Dont print goto at bottom of clause
            int id2 = emit.beginBlock(bl.getBlock(1));
            bl.getBlock(1).emit(this);
            emit.endBlock(id2);
            emit.stopIndent(indent);
            emit.tagLine();
            emit.print(CLOSE_CURLY);
            popMod();
        }

        /// Push a \e functional expression based on the given p-code op to the RPN stack
        /// This is used for expression that require functional syntax, where the name of the
        /// function is the name of the operator. The inputs to the p-code op form the roots
        /// of the comma separated list of \e parameters within the syntax.
        /// \param op is the given PcodeOp
        protected void opFunc(PcodeOp op)
        {
            pushOp(&function_call, op);
            // Using function syntax but don't markup the name as
            // a normal function call
            string nm = op.getOpcode().getOperatorName(op);
            pushAtom(new Atom(nm, optoken, EmitMarkup.syntax_highlight.no_color, op));
            if (op.numInput() > 0) {
                for (int i = 0; i < op.numInput() - 1; ++i)
                    pushOp(&comma, op);
                // implied vn's pushed on in reverse order for efficiency
                // see PrintLanguage::pushVnImplied
                for (int i = op.numInput() - 1; i >= 0; --i)
                    pushVn(op.getIn(i), op, mods);
            }
            else                // Push empty token for void
                pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
        }

        /// Push the given p-code op using type-cast syntax to the RPN stack
        /// The syntax represents the given op using a standard c-language cast.  The data-type
        /// being cast to is obtained from the output variable of the op. The input expression is
        /// also recursively pushed.
        /// \param op is the given PcodeOp
        protected void opTypeCast(PcodeOp op)
        {
            if (!option_nocasts) {
                pushOp(&typecast, op);
                pushType(op.getOut().getHighTypeDefFacing());
            }
            pushVn(op.getIn(0), op, mods);
        }

        /// Push the given p-code op as a hidden token
        /// The syntax represents the given op using a function with one input,
        /// where the function name is not printed. The input expression is simply printed
        /// without adornment inside the larger expression, with one minor difference.
        /// The hidden operator protects against confusing evaluation order between
        /// the operators inside and outside the hidden function.  If both the inside
        /// and outside operators are the same associative token, the hidden token
        /// makes sure the inner expression is surrounded with parentheses.
        /// \param op is the given PcodeOp
        protected void opHiddenFunc(PcodeOp op)
        {
            pushOp(&hidden, op);
            pushVn(op.getIn(0), op, mods);
        }

        /// Print value as an escaped hex sequence
        /// Print the given value using the standard character hexadecimal escape sequence.
        /// \param s is the stream to write to
        /// \param val is the given value
        protected static void printCharHexEscape(TextWriter s, int val)
        {
            if (val < 256) {
                s << "\\x" << setfill('0') << setw(2) << hex << val;
            }
            else if (val < 65536) {
                s << "\\x" << setfill('0') << setw(4) << hex << val;
            }
            else
                s << "\\x" << setfill('0') << setw(8) << hex << val;
        }

        /// \brief Print a quoted (unicode) string at the given address.
        ///
        /// Data for the string is obtained directly from the LoadImage.  The bytes are checked
        /// for appropriate unicode encoding and the presence of a terminator. If all these checks
        /// pass, the string is emitted.
        /// \param s is the output stream to print to
        /// \param addr is the address of the string data within the LoadImage
        /// \param charType is the underlying character data-type
        /// \return \b true if a proper string was found and printed to the stream
        protected bool printCharacterConstant(TextWriter s, Address addr, Datatype charType)
        {
            StringManager manager = glb.stringManager;

            // Retrieve UTF8 version of string
            bool isTrunc = false;
            List<byte> buffer = manager.getStringData(addr, charType, isTrunc);
            if (buffer.empty())
                return false;
            if (doEmitWideCharPrefix() && charType.getSize() > 1 && !charType.isOpaqueString())
                s.Write('L');           // Print symbol indicating wide character
            s.Write('"');
            escapeCharacterData(s, buffer.data(), buffer.size(), 1, glb.translate.isBigEndian());
            if (isTrunc)
                s.Write("...\" /* TRUNCATED STRING LITERAL */");
            else
                s.Write('"');

            return true;
        }

        /// Get position of "this" pointer needing to be hidden
        /// For the given CALL op, if a "this" pointer exists and needs to be hidden because
        /// of the print configuration, return the Varnode slot corresponding to the "this".
        /// Otherwise return -1.
        /// \param op is the given CALL PcodeOp
        /// \param fc is the function prototype corresponding to the CALL
        /// \return the "this" Varnode slot or -1
        protected int getHiddenThisSlot(PcodeOp op, FuncProto fc)
        {
            int numInput = op.numInput();
            if (isSet(hide_thisparam) && fc.hasThisPointer()) {
                for (int i = 1; i < numInput - 1; ++i) {
                    ProtoParameter param = fc.getParam(i - 1);
                    if (param != (ProtoParameter)null && param.isThisPointer())
                        return i;
                }
                if (numInput >= 2) {
                    ProtoParameter param = fc.getParam(numInput - 2);
                    if (param != (ProtoParameter)null && param.isThisPointer())
                        return numInput - 1;
                }
            }
            return -1;
        }

        /// Set default values for options specific to PrintC
        protected void resetDefaultsPrintC()
        {
            option_convention = true;
            option_hide_exts = true;
            option_inplace_ops = false;
            option_nocasts = false;
            option_NULL = false;
            option_unplaced = false;
            setCStyleComments();
        }

        /// \brief Push a single character constant to the RPN stack
        ///
        /// For C, a character constant is usually emitted as the character in single quotes.
        /// Handle unicode, wide characters, etc. Characters come in with the compiler's raw encoding.
        /// \param val is the constant value
        /// \param ct is data-type attached to the value
        /// \param vn is the Varnode holding the value
        /// \param op is the PcodeOp using the value
        protected override void pushConstant(ulong val, Datatype ct, Varnode vn, PcodeOp op)
        {
            Symbol.DisplayFlags displayFormat = 0;
            bool isSigned = (ct.getMetatype() == type_metatype.TYPE_INT);
            if ((vn != (Varnode)null)&& (!vn.isAnnotation())) {
                HighVariable high = vn.getHigh();
                Symbol sym = high.getSymbol();
                if (sym != (Symbol)null) {
                    if (sym.isNameLocked() && (sym.getCategory() == Symbol.SymbolCategory.equate)) {
                        if (pushEquate(val, vn.getSize(), (EquateSymbol)sym, vn, op))
                            return;
                    }
                    displayFormat = sym.getDisplayFormat();
                }
                if (displayFormat == 0)
                    displayFormat = high.getType().getDisplayFormat();
            }
            if (displayFormat != 0 && displayFormat != Symbol.SymbolCategory.force_char) {
                if (!castStrategy.caresAboutCharRepresentation(vn, op)) {
                    push_integer(val, ct.getSize(), isSigned, vn, op);
                    return;
                }
            }
            if ((ct.getSize() == 1) && (val >= 0x80)) {
                // For byte characters, the encoding is assumed to be ASCII, UTF-8, or some other
                // code-page that extends ASCII. At 0x80 and above, we cannot treat the value as a
                // unicode code-point. Its either part of a multi-byte UTF-8 encoding or an unknown
                // code-page value. In either case, we print as an integer or an escape sequence.
                if (displayFormat != Symbol.DisplayFlags.force_hex
                    && displayFormat != Symbol.DisplayFlags.force_char)
                {
                    push_integer(val, 1, isSigned, vn, op);
                    return;
                }
                displayFormat = Symbol.DisplayFlags.force_hex;  // Fallthru but force a hex representation
            }
            StringWriter t = new StringWriter();
            // From here we assume, the constant value is a direct unicode code-point.
            // The value could be an illegal code-point (surrogates or beyond the max code-point),
            // but this will just be emitted as an escape sequence.
            if (doEmitWideCharPrefix() && ct.getSize() > 1)
                t.Write('L');       // Print symbol indicating wide character
            t.Write("'");          // char is surrounded with single quotes
            if (displayFormat == Symbol.DisplayFlags.force_hex) {
                printCharHexEscape(t, (int)val);
            }
            else
                printUnicode(t, (int)val);
            t.Write("'");
            pushAtom(new Atom(t.ToString(), vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
        }

        protected override bool pushEquate(ulong val, int sz, EquateSymbol sym, Varnode vn, PcodeOp op)
        {
            ulong mask = Globals.calc_mask((uint)sz);
            ulong baseval = sym.getValue();
            ulong modval = baseval & mask;
            if (modval != baseval) {
                // If 1-bits are getting masked
                if (Globals.sign_extend(modval, sz, sizeof(ulong)) != baseval)  // make sure we only mask sign extension bits
                    return false;
            }
            if (modval == val) {
                pushSymbol(sym, vn, op);
                return true;
            }
            modval = (~baseval) & mask;
            if (modval == val) {
                // Negation
                pushOp(bitwise_not, (PcodeOp)null);
                pushSymbol(sym, vn, op);
                return true;
            }
            modval = (-baseval) & mask;
            if (modval == val) {
                // twos complement
                pushOp(unary_minus, (PcodeOp)null);
                pushSymbol(sym, vn, op);
                return true;
            }
            modval = (baseval + 1) & mask;
            if (modval == val) {
                pushOp(binary_plus, (PcodeOp)null);
                pushSymbol(sym, vn, op);
                push_integer(1, sz, false, (Varnode)null, (PcodeOp)null);
                return true;
            }
            modval = (baseval - 1) & mask;
            if (modval == val) {
                pushOp(binary_minus, (PcodeOp)null);
                pushSymbol(sym, vn, op);
                push_integer(1, sz, false, (Varnode)null, (PcodeOp)null);
                return true;
            }
            return false;
        }

        protected override void pushAnnotation(Varnode vn, PcodeOp op)
        {
            Scope symScope = op.getParent().getFuncdata().getScopeLocal();
            int size = 0;
            if (op.code() == OpCode.CPUI_CALLOTHER) {
                int userind = (int)op.getIn(0).getOffset();
                size = glb.userops.getOp(userind).extractAnnotationSize(vn, op);
            }
            SymbolEntry entry;
            if (size != 0)
                entry = symScope.queryContainer(vn.getAddr(), size, op.getAddr());
            else {
                entry = symScope.queryContainer(vn.getAddr(), 1, op.getAddr());
                if (entry != (SymbolEntry)null)
                    size = entry.getSize();
                else
                    size = vn.getSize();
            }

            if (entry != (SymbolEntry)null) {
                if (entry.getSize() == size)
                    pushSymbol(entry.getSymbol(), vn, op);
                else {
                    int symboloff = (int)(vn.getOffset() - entry.getFirst());
                    pushPartialSymbol(entry.getSymbol(), symboloff, size, vn, op, -1);
                }
            }
            else {
                string regname = glb.translate.getRegisterName(vn.getSpace(), vn.getOffset(), size);
                if (regname.empty()) {
                    AddrSpace spc = vn.getSpace();
                    string spacename = spc.getName().Capitalize();
                    StringWriter s = new StringWriter();
                    s.Write(spacename);
                    string formatString = $"{{0:X{2 * spc.getAddrSize()}}}";
                    s.Write(formatString, AddrSpace.byteToAddress(vn.getOffset(), spc.getWordSize()));
                    regname = s.ToString();
                }
                pushAtom(new Atom(regname, vartoken, EmitMarkup.syntax_highlight.special_color, op, vn));
            }
        }

        protected override void pushSymbol(Symbol sym, Varnode vn, PcodeOp op)
        {
            EmitMarkup.syntax_highlight.syntax_highlight tokenColor;
            if (sym.isVolatile())
                tokenColor = EmitMarkup.syntax_highlight.special_color;
            else if (sym.getScope().isGlobal())
                tokenColor = EmitMarkup.syntax_highlight.global_color;
            else if (sym.getCategory() == Symbol.SymbolCategory.function_parameter)
                tokenColor = EmitMarkup.syntax_highlight.param_color;
            else
                tokenColor = EmitMarkup.syntax_highlight.var_color;
            pushSymbolScope(sym);
            if (sym.hasMergeProblems() && vn != (Varnode)null) {
                HighVariable high = vn.getHigh();
                if (high.isUnmerged()) {
                    StringWriter s = new StringWriter();
                    s.Write(sym.getDisplayName());
                    SymbolEntry entry = high.getSymbolEntry();
                    if (entry != (SymbolEntry)null) {
                        s << '$' << dec << entry.getSymbol().getMapEntryPosition(entry);
                    }
                    else
                        s.Write("$$");
                    pushAtom(new Atom(s.ToString(), vartoken, tokenColor, op, vn));
                    return;
                }
            }
            pushAtom(new Atom(sym.getDisplayName(), vartoken, tokenColor, op, vn));
        }

        protected override void pushUnnamedLocation(Address addr, Varnode vn, PcodeOp op)
        {
            StringWriter s = new StringWriter();
            s.Write(addr.getSpace().getName());
            addr.printRaw(s);
            pushAtom(new Atom(s.ToString(), vartoken, EmitMarkup.syntax_highlight.var_color, op, vn));
        }

        protected override void pushPartialSymbol(Symbol sym, int off, int sz, Varnode vn, PcodeOp op,
            int inslot)
        {
            // We need to print "bottom up" in order to get parentheses right
            // I.e. we want to print globalstruct.arrayfield[0], rather than
            //                       globalstruct.(arrayfield[0])
            List<PartialSymbolEntry> stack = new List<PartialSymbolEntry>();
            Datatype? finalcast = (Datatype)null;
            Datatype? ct = sym.getType();

            while (ct != (Datatype)null) {
                if (off == 0) {
                    if (sz == 0 || (sz == ct.getSize() && (!ct.needsResolution() || ct.getMetatype() == type_metatype.TYPE_PTR)))
                        break;
                }
                bool succeeded = false;
                if (ct.getMetatype() == type_metatype.TYPE_STRUCT) {
                    if (ct.needsResolution() && ct.getSize() == sz) {
                        Datatype outtype = ct.findResolve(op, inslot);
                        if (outtype == ct)
                            break;  // Turns out we don't resolve to the field
                    }
                    TypeField? field = ct.findTruncation(off, sz, op, inslot, off);
                    if (field != (TypeField)null) {
                        PartialSymbolEntry entry = new PartialSymbolEntry() {
                            token = object_member,
                            field = field,
                            parent = ct,
                            fieldname = field.name,
                            hilite = EmitMarkup.syntax_highlight.no_color
                        };
                        stack.Add(entry);
                        ct = field.type;
                        succeeded = true;
                    }
                }
                else if (ct.getMetatype() == type_metatype.TYPE_ARRAY) {
                    int el;
                    Datatype? arrayof = ((TypeArray)ct).getSubEntry(off, sz, off, el);
                    if (arrayof != (Datatype)null) {
                        StringWriter s = new StringWriter();
                        s.Write(el);
                        PartialSymbolEntry entry = new PartialSymbolEntry() {
                            token = subscript,
                            fieldname = s.ToString(),
                            field = (TypeField)null,
                            hilite = EmitMarkup.syntax_highlight.const_color
                        };
                        stack.Add(entry);
                        ct = arrayof;
                        succeeded = true;
                    }
                }
                else if (ct.getMetatype() == type_metatype.TYPE_UNION) {
                    TypeField? field = ct.findTruncation(off, sz, op, inslot, off);
                    if (field != (TypeField)null) {
                        PartialSymbolEntry entry = new PartialSymbolEntry() {
                            token = object_member,
                            field = field,
                            parent = ct,
                            fieldname = entry.field.name,
                            hilite = EmitMarkup.syntax_highlight.no_color
                        };
                        stack.Add(entry);
                        ct = field.type;
                        succeeded = true;
                    }
                    else if (ct.getSize() == sz)
                        break;      // Turns out we don't need to resolve the field
                }
                else if (inslot >= 0) {
                    Datatype outtype = vn.getHigh().getType();
                    if (castStrategy.isSubpieceCastEndian(outtype, ct, off,
                        sym.getFirstWholeMap().getAddr().getSpace().isBigEndian()))
                    {
                        // Treat truncation as SUBPIECE style cast
                        finalcast = outtype;
                        ct = (Datatype)null;
                        succeeded = true;
                    }
                }
                if (!succeeded) {
                    // Subtype was not good
                    if (sz == 0)
                        sz = ct.getSize() - off;
                    PartialSymbolEntry entry = new PartialSymbolEntry() {
                        token = object_member,
                        // If nothing else works, generate artificial field name
                        fieldname = unnamedField(off, sz),
                        field = (TypeField)null,
                        hilite = EmitMarkup.syntax_highlight.no_color
                    };
                    stack.Add(entry);
                    ct = (Datatype)null;
                }
            }

            if ((finalcast != (Datatype)null) && (!option_nocasts)) {
                pushOp(&typecast, op);
                pushType(finalcast);
            }
            // Push these on the RPN stack in reverse order
            for (int i = stack.Count - 1; i >= 0; --i)
                pushOp(stack[i].token, op);
            pushSymbol(sym, vn, op);    // Push base symbol name
            for (int i = 0; i < stack.Count; ++i) {
                TypeField field = stack[i].field;
                if (field == (TypeField)null)
                    pushAtom(new Atom(stack[i].fieldname, syntax, stack[i].hilite, op));
                else
                    pushAtom(new Atom(stack[i].fieldname, fieldtoken, stack[i].hilite, stack[i].parent, field.ident, op));
            }
        }

        protected override void pushMismatchSymbol(Symbol sym, int off, int sz, Varnode vn, PcodeOp op)
        {
            if (off == 0) {
                // The most common situation is when a user sees a reference
                // to a variable and forces a symbol to be there but guesses
                // the type (or size) incorrectly
                // The address of the symbol is correct, but the size is too small

                // We prepend an underscore to indicate a close
                // but not quite match
                string nm = '_' + sym.getDisplayName();
                pushAtom(new Atom(nm, vartoken, EmitMarkup.syntax_highlight.var_color, op, vn));
            }
            else
                pushUnnamedLocation(vn.getAddr(), vn, op);
        }

        protected override void pushImpliedField(Varnode vn, PcodeOp op)
        {
            bool proceed = false;
            Datatype parent = vn.getHigh().getType();
            TypeField field;
            if (parent.needsResolution() && parent.getMetatype() != type_metatype.TYPE_PTR) {
                Funcdata fd = op.getParent().getFuncdata();
                int slot = op.getSlot(vn);
                ResolvedUnion? res = fd.getUnionField(parent, op, slot);
                if (res != (ResolvedUnion)null && res.getFieldNum() >= 0) {
                    if (parent.getMetatype() == type_metatype.TYPE_STRUCT && res.getFieldNum() == 0) {
                        field = &(*((TypeStruct)parent).beginField());
                        proceed = true;
                    }
                    else if (parent.getMetatype() == type_metatype.TYPE_UNION) {
                        field = ((TypeUnion)parent).getField(res.getFieldNum());
                        proceed = true;
                    }
                }
            }

            PcodeOp defOp = vn.getDef();
            if (!proceed) {
                // Just push original op
                defOp.getOpcode().push(this, defOp, op);
                return;
            }
            pushOp(&object_member, op);
            defOp.getOpcode().push(this, defOp, op);
            pushAtom(new Atom(field.name, fieldtoken, EmitMarkup.syntax_highlight.no_color, parent,
                field.ident, op));
        }

        /// \brief Push a constant with an integer data-type to the RPN stack
        ///
        /// Various checks are made to see if the integer should be printed as an \e equate
        /// symbol or if there is other overriding information about what format it should be printed @in.
        /// In any case, a final determination of the format is made and the integer is pushed as
        /// a single token.
        /// \param val is the given integer value
        /// \param sz is the size (in bytes) to associate with the integer
        /// \param sign is set to \b true if the integer should be treated as a signed value
        /// \param vn is the Varnode holding the value
        /// \param op is the PcodeOp using the value
        protected virtual void push_integer(ulong val, int sz, bool sign, Varnode vn, PcodeOp op)
        {
            bool print_negsign;
            bool force_unsigned_token;
            bool force_sized_token;
            Symbol.DisplayFlags displayFormat = 0;

            force_unsigned_token = false;
            force_sized_token = false;
            if ((vn != (Varnode)null)&& (!vn.isAnnotation())) {
                HighVariable high = vn.getHigh();
                Symbol? sym = high.getSymbol();
                if (sym != (Symbol)null) {
                    if (sym.isNameLocked() && (sym.getCategory() == Symbol.SymbolCategory.equate)) {
                        if (pushEquate(val, sz, (EquateSymbol)sym, vn, op))
                            return;
                    }
                    displayFormat = sym.getDisplayFormat();
                }
                force_unsigned_token = vn.isUnsignedPrint();
                force_sized_token = vn.isLongPrint();
                if (displayFormat == 0) // The symbol's formatting overrides any formatting on the data-type
                    displayFormat = high.getType().getDisplayFormat();
            }
            if (sign && displayFormat != Symbol.DisplayFlags.force_char) {
                // Print the constant as signed
                ulong mask = Globals.calc_mask(sz);
                ulong flip = val ^ mask;
                print_negsign = (flip < val);
                if (print_negsign)
                    val = flip + 1;
                force_unsigned_token = false;
            }
            else {
                print_negsign = false;
            }

            // Figure whether to print as hex or decimal
            if (displayFormat != 0) {
                // Format is forced by the Symbol
            }
            else if ((mods & force_hex) != 0) {
                displayFormat = Symbol.DisplayFlags.force_hex;
            }
            else if ((val <= 10) || ((mods & force_dec))) {
                displayFormat = Symbol.DisplayFlags.force_dec;
            }
            else {
                // Otherwise decide if dec or hex is more natural
                displayFormat = (PrintLanguage.mostNaturalBase(val) == 16) 
                    ? Symbol.DisplayFlags.force_hex
                    : Symbol.DisplayFlags.force_dec;
            }

            StringWriter t = new StringWriter();
            if (print_negsign)
                t.Write('-');
            if (displayFormat == Symbol.DisplayFlags.force_hex)
                t.Write($"0x{val:X}");
            else if (displayFormat == Symbol.DisplayFlags.force_dec)
                t.Write(val);
            else if (displayFormat == Symbol.DisplayFlags.force_oct)
                t << oct << '0' << val;
            else if (displayFormat == Symbol.DisplayFlags.force_char) {
                if (doEmitWideCharPrefix() && sz > 1)
                    t.Write('L');           // Print symbol indicating wide character
                t.Write("'");          // char is surrounded with single quotes
                if (sz == 1 && val >= 0x80)
                    printCharHexEscape(t, (int)val);
                else
                    printUnicode(t, (int)val);
                t.Write("'");
            }
            else {
                // Must be Symbol::force_bin
                t.Write("0b");
                formatBinary(t, val);
            }
            if (force_unsigned_token)
                t.Write('U');           // Force unsignedness explicitly
            if (force_sized_token)
                t.Write(sizeSuffix);

            if (vn == (Varnode)null)
                pushAtom(new Atom(t.ToString(), syntax, EmitMarkup.syntax_highlight.const_color, op));
            else
                pushAtom(new Atom(t.ToString(), vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
        }

        /// \brief Push a constant with a floating-point data-type to the RPN stack
        ///
        /// The encoding is drawn from the underlying Translate object, and the print
        /// properties are checked for formatting overrides.  In any case, a format
        /// is decided upon, and the constant is pushed as a single token.
        /// \param val is the given encoded floating-point value
        /// \param sz is the size (in bytes) of the encoded value
        /// \param vn is the Varnode holding the value
        /// \param op is the PcodeOp using the value
        protected override void push_float(ulong val, int sz, Varnode vn, PcodeOp op)
        {
            string token;

            FloatFormat? format = glb.translate.getFloatFormat(sz);
            if (format == (FloatFormat)null) {
                token = "FLOAT_UNKNOWN";
            }
            else {
                FloatFormat.floatclass type;
                double floatval = format.getHostFloat(val, &type);
                if (type == FloatFormat.floatclass.infinity) {
                    if (format.extractSign(val))
                        token = "-INFINITY";
                    else
                        token = "INFINITY";
                }
                else if (type == FloatFormat.floatclass.nan) {
                    if (format.extractSign(val))
                        token = "-NAN";
                    else
                        token = "NAN";
                }
                else {
                    StringWriter t = new StringWriter();
                    if ((mods & force_scinote) != 0) {
                        t.setf(ios::scientific); // Set to scientific notation
                        t.precision(format.getDecimalPrecision() - 1);
                        t << floatval;
                        token = t.ToString();
                    }
                    else {
                        // Try to print "minimal" accurate representation of the float
                        t.unsetf(ios::floatfield);  // Use "default" notation
                        t.precision(format.getDecimalPrecision());
                        t << floatval;
                        token = t.ToString();
                        bool looksLikeFloat = false;
                        for (int i = 0; i < token.Length; ++i) {
                            char c = token[i];
                            if (c == '.' || c == 'e') {
                                looksLikeFloat = true;
                                break;
                            }
                        }
                        if (!looksLikeFloat) {
                            token += ".0";  // Force token to look like a floating-point value
                        }
                    }
                }
            }
            if (vn == (Varnode)null)
                pushAtom(new Atom(token, syntax, EmitMarkup.syntax_highlight.const_color, op));
            else
                pushAtom(new Atom(token, vartoken, EmitMarkup.syntax_highlight.const_color, op, vn));
        }

        protected override void printUnicode(TextWriter s, int onechar)
        {
            if (unicodeNeedsEscape(onechar)) {
                switch (onechar)
                {       // Special escape characters
                    case 0:
                        s.Write("\\0");
                        return;
                    case 7:
                        s.Write("\\a");
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
                    case 11:
                        s.Write("\\v");
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
                // Generic escape code
                printCharHexEscape(s, onechar);
                return;
            }
            StringManager.writeUtf8(s, onechar);       // emit normally
        }

        protected override void pushType(Datatype ct)
        {
            pushTypeStart(ct, true);                // Print type (as if for a cast)
            pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
            pushTypeEnd(ct);
        }

        /// \brief Create a generic function name base on the entry point address
        ///
        /// \param addr is the entry point address of the function
        /// \return the generated name
        protected virtual string genericFunctionName(Address addr)
        {
            StringWriter s = new StringWriter();

            s.Write("func_");
            addr.printRaw(s);
            return s.str();
        }

        /// \brief Generate a generic name for an unnamed data-type
        ///
        /// \param ct is the given data-type
        /// \return the generated name
        protected override string genericTypeName(Datatype ct)
        {
            StringWriter s = new StringWriter();
            switch (ct.getMetatype()) {
                case type_metatype.TYPE_INT:
                    s.Write("unkint");
                    break;
                case type_metatype.TYPE_UINT:
                    s.Write("unkuint");
                    break;
                case type_metatype.TYPE_UNKNOWN:
                    s.Write("unkbyte");
                    break;
                case type_metatype.TYPE_SPACEBASE:
                    s.Write("BADSPACEBASE");
                    return s.str();
                case type_metatype.TYPE_FLOAT:
                    s.Write("unkfloat");
                    break;
                default:
                    s.Write("BADTYPE");
                    return s.ToString();
            }
            s.Write(ct.getSize());
            return s.ToString();
        }

        protected override void emitExpression(PcodeOp op)
        {
            Varnode? outvn = op.getOut();
            if (outvn != (Varnode)null) {
                if (option_inplace_ops && emitInplaceOp(op)) return;
                pushOp(&assignment, op);
                pushSymbolDetail(outvn, op, false);
            }
            else if (op.doesSpecialPrinting()) {
                // Printing of constructor syntax
                PcodeOp newop = op.getIn(1).getDef();
                outvn = newop.getOut();
                pushOp(&assignment, newop);
                pushSymbolDetail(outvn, newop, false);
                opConstructor(op, true);
                recurse();
                return;
            }
            // If STORE, print  *( ) = ( )
            // If BRANCH, print nothing
            // If CBRANCH, print condition  ( )
            // If BRANCHIND, print switch( )
            // If CALL, CALLIND, CALLOTHER  print  call
            // If RETURN,   print return ( )
            op.getOpcode().push(this, op, (PcodeOp)null);
            recurse();
        }

        protected override void emitVarDecl(Symbol sym)
        {
            int id = emit.beginVarDecl(sym);

            pushTypeStart(sym.getType(), false);
            pushSymbol(sym, (Varnode)null, (PcodeOp)null);
            pushTypeEnd(sym.getType());
            recurse();

            emit.endVarDecl(id);
        }

        protected override void emitVarDeclStatement(Symbol sym)
        {
            emit.tagLine();
            emitVarDecl(sym);
            emit.print(SEMICOLON);
        }

        protected override bool emitScopeVarDecls(Scope symScope, int cat)
        {
            bool notempty = false;

            if (cat >= 0) {
                // If a category is specified
                int sz = symScope.getCategorySize(cat);
                for (int i = 0; i < sz; ++i) {
                    Symbol sym = symScope.getCategorySymbol(cat, i);
                    // Slightly different handling for categorized symbols (cat=1 is dynamic symbols)
                    if (sym.getName().Length == 0) continue;
                    if (sym.isNameUndefined()) continue;
                    notempty = true;
                    emitVarDeclStatement(sym);
                }
                return notempty;
            }
            IEnumerator<SymbolEntry> iter = symScope.begin();
            while (iter.MoveNext()) {
                SymbolEntry entry = iter.Current;
                if (entry.isPiece()) continue; // Don't do a partial entry
                Symbol sym = entry.getSymbol();
                if (sym.getCategory() != cat) continue;
                if (sym.getName().Length == 0) continue;
                if (sym is FunctionSymbol)
                    continue;
                if (sym is LabSymbol)
                    continue;
                if (sym.isMultiEntry()) {
                    if (sym.getFirstWholeMap() != entry)
                        continue;       // Only emit the first SymbolEntry for declaration of multi-entry Symbol
                }
                notempty = true;
                emitVarDeclStatement(sym);
            }
            IEnumerator<SymbolEntry> iter_d = symScope.beginDynamic();
            while (iter_d.MoveNext()) {
                SymbolEntry entry = iter_d.Current;
                if (entry.isPiece()) continue; // Don't do a partial entry
                Symbol sym = iter_d.Current.getSymbol();
                if (sym.getCategory() != cat) continue;
                if (sym.getName().Length == 0) continue;
                if ((FunctionSymbol)(sym) != (FunctionSymbol)null)
                    continue;
                if (null != (sym as LabSymbol))
                    continue;
                if (sym.isMultiEntry()) {
                    if (sym.getFirstWholeMap() != entry)
                        continue;
                }
                notempty = true;
                emitVarDeclStatement(sym);
            }
            return notempty;
        }

        protected override void emitFunctionDeclaration(Funcdata fd)
        {
            FuncProto proto = &fd.getFuncProto();
            int id = emit.beginFuncProto();
            emitPrototypeOutput(proto, fd);
            emit.spaces(1);
            if (option_convention) {
                if (fd.getFuncProto().printModelInDecl()) {
                    Emit.syntax_highlight highlight = fd.getFuncProto().isModelUnknown()
                        ? Emit.syntax_highlight.error_color
                        : Emit.syntax_highlight.keyword_color;
                    emit.print(fd.getFuncProto().getModelName(), highlight);
                    emit.spaces(1);
                }
            }
            int id1 = emit.openGroup();
            emitSymbolScope(fd.getSymbol());
            emit.tagFuncName(fd.getDisplayName(), EmitMarkup.syntax_highlight.funcname_color, fd,
                (PcodeOp)null);

            emit.spaces(function_call.spacing, function_call.bump);
            int id2 = emit.openParen(OPEN_PAREN);
            emit.spaces(0, function_call.bump);
            pushScope(fd.getScopeLocal());     // Enter the function's scope for parameters
            emitPrototypeInputs(proto);
            emit.closeParen(CLOSE_PAREN, id2);
            emit.closeGroup(id1);

            emit.endFuncProto(id);
        }

        /// \brief Emit the definition of the given data-type
        ///
        /// This is currently limited to a 'struct' or 'enum' definitions. The
        /// definition is emitted so that name associated with data-type object
        /// will be associated with the definition (in anything that parses it)
        /// \param ct is the given data-type
        protected override void emitTypeDefinition(Datatype ct)
        {
#if CPUI_DEBUG
            if (!isStackEmpty()) {
                clear();
                throw new LowlevelError("Expression stack not empty at beginning of emit");
            }
#endif
            if (ct.getMetatype() == type_metatype.TYPE_STRUCT)
                emitStructDefinition((TypeStruct)ct);
            else if (ct.isEnumType())
                emitEnumDefinition((TypeEnum)ct);
            else {
                clear();
                throw new CORE.LowlevelError("Unsupported typedef");
            }
        }

        protected override bool checkPrintNegation(Varnode vn)
        {
            if (!vn.isImplied()) return false;
            if (!vn.isWritten()) return false;
            PcodeOp op = vn.getDef();
            bool reorder = false;
            OpCode opc = Globals.get_booleanflip(op.code(), reorder); // This is the set of ops that can be negated as a token
            if (opc == OpCode.CPUI_MAX)
                return false;
            return true;
        }

        /// \brief Push a token indicating a PTRSUB (a . operator) is acting at an offset from the original pointer
        ///
        /// When a variable has TypePointerRel as its data-type, PTRSUB acts relative to the \e parent
        /// data-type.  We print a specific token to indicate this relative shift is happening.
        /// \param op is is the PTRSUB op
        protected void pushTypePointerRel(PcodeOp op)
        {
            pushOp(&function_call, op);
            pushAtom(new Atom(typePointerRelToken, optoken, EmitMarkup.syntax_highlight.funcname_color, op));
        }

        /// \param g is the Architecture owning this c-language emitter
        /// \param nm is the name assigned to this emitter
        public PrintC(Architecture g, string nm = "c-language")
        {
            nullToken = "NULL";

            // Set the flip tokens
            less_than.negate = &greater_equal;
            less_equal.negate = &greater_than;
            greater_than.negate = &less_equal;
            greater_equal.negate = &less_than;
            equal.negate = &not_equal;
            not_equal.negate = &equal;

            castStrategy = new CastStrategyC();
            resetDefaultsPrintC();
        }

        /// Toggle the printing of a 'NULL' token
        public void setNULLPrinting(bool val)
        {
            option_NULL = val;
        }

        /// Toggle the printing of \e in-place operators
        public void setInplaceOps(bool val)
        {
            option_inplace_ops = val;
        }

        /// Toggle whether calling conventions are printed
        public void setConvention(bool val)
        {
            option_convention = val;
        }

        /// Toggle whether casts should \b not be printed
        public void setNoCastPrinting(bool val)
        {
            option_nocasts = val;
        }

        /// Set c-style "/* */" comment delimiters
        public void setCStyleComments()
        {
            setCommentDelimeter("/* ", " */", false);
        }

        /// Set c++-style "//" comment delimiters
        public void setCPlusPlusStyleComments()
        {
            setCommentDelimeter("// ", "", true);
        }

        /// Toggle whether \e unplaced comments are displayed in the header
        public void setDisplayUnplaced(bool val)
        {
            option_unplaced = val;
        }

        /// Toggle whether implied extensions are hidden
        public void setHideImpliedExts(bool val)
        {
            option_hide_exts = val;
        }

        ~PrintC()
        {
        }

        public override void resetDefaults()
        {
            base.resetDefaults();
            resetDefaultsPrintC();
        }

        public override void initializeFromArchitecture()
        {
            castStrategy.setTypeFactory(glb.types);
            if (glb.types.getSizeOfLong() == glb.types.getSizeOfInt())  // If long and int sizes are the same
                sizeSuffix = "LL";      // Use "long long" suffix to indicate large integer
            else
                sizeSuffix = "L";       // Otherwise just use long suffix
        }

        public override void adjustTypeOperators()
        {
            scope.print1 = "::";
            shift_right.print1 = ">>";
            TypeOp.selectJavaOperators(glb.inst, false);
        }

        public override void setCommentStyle(string nm)
        {
            if ((nm == "c") ||
                ((nm.Length >= 2) && (nm[0] == '/') && (nm[1] == '*')))
                setCStyleComments();
            else if ((nm == "cplusplus") ||
                 ((nm.Length >= 2) && (nm[0] == '/') && (nm[1] == '/')))
                setCPlusPlusStyleComments();
            else
                throw new CORE.LowlevelError("Unknown comment style. Use \"c\" or \"cplusplus\"");
        }

        public override void docTypeDefinitions(TypeFactory typegrp)
        {
            List<Datatype> deporder = new List<Datatype>();

            typegrp.dependentOrder(deporder); // Put things in resolvable order
            foreach (Datatype type in deporder) {
                if (type.isCoreType()) continue;
                emitTypeDefinition(type);
            }
        }

        public override void docAllGlobals()
        {
            int id = emit.beginDocument();
            emitGlobalVarDeclsRecursive(glb.symboltab.getGlobalScope());
            emit.tagLine();
            emit.endDocument(id);
            emit.flush();
        }

        public override void docSingleGlobal(Symbol sym)
        {
            int id = emit.beginDocument();
            emitVarDeclStatement(sym);
            emit.tagLine();        // Extra line
            emit.endDocument(id);
            emit.flush();
        }

        public override void docFunction(Funcdata fd)
        {
            uint modsave = mods;
            if (!fd.isProcStarted())
                throw new RecovError("Function not decompiled");
            if ((!isSet(flat)) && (fd.hasNoStructBlocks()))
                throw new RecovError("Function not fully decompiled. No structure present.");
            try {
                commsorter.setupFunctionList(instr_comment_type | head_comment_type, fd, *fd.getArch().commentdb, option_unplaced);
                int id1 = emit.beginFunction(fd);
                emitCommentFuncHeader(fd);
                emit.tagLine();
                emitFunctionDeclaration(fd);    // Causes us to enter function's scope
                emit.tagLine();
                emit.tagLine();
                int id = emit.startIndent();
                emit.print(OPEN_CURLY);
                emitLocalVarDecls(fd);
                if (isSet(flat))
                    emitBlockGraph(&fd.getBasicBlocks());
                else
                    emitBlockGraph(&fd.getStructure());
                popScope();             // Exit function's scope
                emit.stopIndent(id);
                emit.tagLine();
                emit.print(CLOSE_CURLY);
                emit.tagLine();
                emit.endFunction(id1);
                emit.flush();
#if CPUI_DEBUG
                if ((mods != modsave) || (!isModStackEmpty()))
                    throw new RecovError("Printing modification stack has not been purged");
#endif
                mods = modsave;
            }
            catch (CORE.LowlevelError err) {
                base.clear();               // Don't leave printer in partial state
                throw err;
            }
        }

        public override void emitBlockBasic(BlockBasic bb)
        {
            PcodeOp inst;
            bool separator;

            commsorter.setupBlockList(bb);
            emitLabelStatement(bb); // Print label (for flat prints)
            if (isSet(only_branch)) {
                inst = bb.lastOp();
                if (inst.isBranch())
                    emitExpression(inst);   // Only print branch instruction
            }
            else {
                separator = false;
                IEnumerator<PcodeOp> iter = bb.beginOp();
                while (iter.MoveNext()) {
                    inst = iter.Current;
                    if (inst.notPrinted()) continue;
                    if (inst.isBranch()) {
                        if (isSet(no_branch)) continue;
                        // A straight branch is always printed by
                        // the block classes
                        if (inst.code() == OpCode.CPUI_BRANCH) continue;
                    }
                    Varnode vn = inst.getOut();
                    if ((vn != (Varnode)null) && (vn.isImplied()))
                        continue;
                    if (separator) {
                        if (isSet(comma_separate)) {
                            emit.print(COMMA);
                            emit.spaces(1);
                        }
                        else {
                            emitCommentGroup(inst);
                            emit.tagLine();
                        }
                    }
                    else if (!isSet(comma_separate)) {
                        emitCommentGroup(inst);
                        emit.tagLine();
                    }
                    emitStatement(inst);
                    separator = true;
                }
                // If we are printing flat structure and there
                // is no longer a normal fallthru, print a goto
                if (isSet(flat) && isSet(nofallthru)) {
                    inst = bb.lastOp();
                    emit.tagLine();
                    int id = emit.beginStatement(inst);
                    emit.print(KEYWORD_GOTO, EmitMarkup.syntax_highlight.keyword_color);
                    emit.spaces(1);
                    if (bb.sizeOut() == 2) {
                        if (inst.isFallthruTrue())
                            emitLabel(bb.getOut(1));
                        else
                            emitLabel(bb.getOut(0));
                    }
                    else
                        emitLabel(bb.getOut(0));
                    emit.print(SEMICOLON);
                    emit.endStatement(id);
                }
                emitCommentGroup((PcodeOp)null); // Any remaining comments
            }
        }

        public override void emitBlockGraph(BlockGraph bl)
        {
            List<FlowBlock> list = bl.getList();

            foreach (FlowBlock block in list) {
                int id = emit.beginBlock(block);
                block.emit(this);
                emit.endBlock(id);
            }
        }

        public override void emitBlockCopy(BlockCopy bl)
        {
            emitAnyLabelStatement(bl);
            bl.subBlock(0).emit(this);
        }

        public override void emitBlockGoto(BlockGoto bl)
        {
            pushMod();
            setMod(no_branch);
            bl.getBlock(0).emit(this);
            popMod();
            // Make sure we don't print goto, if it is the
            // next block to be printed
            if (bl.gotoPrints()) {
                emit.tagLine();
                emitGotoStatement(bl.getBlock(0), bl.getGotoTarget(), bl.getGotoType());
            }
        }

        public override void emitBlockLs(BlockList bl)
        {
            int i;
            FlowBlock subbl;

            if (isSet(only_branch)) {
                subbl = bl.getBlock(bl.getSize() - 1);
                subbl.emit(this);
                return;
            }

            if (bl.getSize() == 0) return;
            i = 0;
            subbl = bl.getBlock(i++);
            int id1 = emit.beginBlock(subbl);
            if (i == bl.getSize()) {
                subbl.emit(this);
                emit.endBlock(id1);
                return;
            }
            pushMod();
            if (!isSet(flat))
                setMod(no_branch);
            if (bl.getBlock(i) != subbl.nextInFlow()) {
                pushMod();
                setMod(nofallthru);
                subbl.emit(this);
                popMod();
            }
            else {
                subbl.emit(this);
            }
            emit.endBlock(id1);

            while (i < bl.getSize() - 1) {
                subbl = bl.getBlock(i++);
                int id2 = emit.beginBlock(subbl);
                if (bl.getBlock(i) != subbl.nextInFlow()) {
                    pushMod();
                    setMod(nofallthru);
                    subbl.emit(this);
                    popMod();
                }
                else
                    subbl.emit(this);
                emit.endBlock(id2);
            }
            popMod();
            subbl = bl.getBlock(i);        // The final block
            int id3 = emit.beginBlock(subbl);
            subbl.emit(this);      // Pass original no_branch state
            emit.endBlock(id3);
        }

        public override void emitBlockCondition(BlockCondition bl)
        {
            // FIXME: get rid of parens and properly emit && and ||
            if (isSet(no_branch)) {
                int id = emit.beginBlock(bl.getBlock(0));
                bl.getBlock(0).emit(this);
                emit.endBlock(id);
                return;
            }
            if (isSet(only_branch) || isSet(comma_separate)) {
                int id = emit.openParen(OPEN_PAREN);
                bl.getBlock(0).emit(this);
                pushMod();
                unsetMod(only_branch);
                // Notice comma_separate placed only on second block
                setMod(comma_separate);

                // Set up OpToken so it is emitted as if on the stack
                ReversePolish pol = new ReversePolish();
                pol.op = (PcodeOp)null;
                pol.visited = 1;
                if (bl.getOpcode() == OpCode.CPUI_BOOL_AND)
                    pol.tok = &boolean_and;
                else
                    pol.tok = &boolean_or;
                emitOp(pol);

                int id2 = emit.openParen(OPEN_PAREN);
                bl.getBlock(1).emit(this);
                emit.closeParen(CLOSE_PAREN, id2);
                popMod();
                emit.closeParen(CLOSE_PAREN, id);
            }
        }

        public override void emitBlockIf(BlockIf bl)
        {
            PcodeOp op;
            PendingBrace pendingBrace;

            if (isSet(pending_brace))
                emit.setPendingPrint(&pendingBrace);

            // if block never prints final branch
            // so no_branch and only_branch don't matter
            // and shouldn't be passed automatically to
            // the subblocks
            pushMod();
            unsetMod(no_branch | only_branch | pending_brace);

            pushMod();
            setMod(no_branch);
            FlowBlock condBlock = bl.getBlock(0);
            condBlock.emit(this);
            popMod();
            emitCommentBlockTree(condBlock);
            if (emit.hasPendingPrint(&pendingBrace))   // If we issued a brace but it did not emit
                emit.cancelPendingPrint();         // Cancel the brace in order to have "else if" syntax
            else
                emit.tagLine();                // Otherwise start the "if" on a new line

            op = condBlock.lastOp();
            emit.tagOp(KEYWORD_IF, EmitMarkup.syntax_highlight.keyword_color, op);
            emit.spaces(1);
            pushMod();
            setMod(only_branch);
            condBlock.emit(this);
            popMod();
            if (bl.getGotoTarget() != (FlowBlock)null) {
                emit.spaces(1);
                emitGotoStatement(condBlock, bl.getGotoTarget(), bl.getGotoType());
            }
            else {
                setMod(no_branch);
                emit.spaces(1);
                int id = emit.startIndent();
                emit.print(OPEN_CURLY);
                int id1 = emit.beginBlock(bl.getBlock(1));
                bl.getBlock(1).emit(this);
                emit.endBlock(id1);
                emit.stopIndent(id);
                emit.tagLine();
                emit.print(CLOSE_CURLY);
                if (bl.getSize() == 3) {
                    emit.tagLine();
                    emit.print(KEYWORD_ELSE, EmitMarkup.syntax_highlight.keyword_color);
                    emit.spaces(1);
                    FlowBlock elseBlock = bl.getBlock(2);
                    if (elseBlock.getType() == FlowBlock.block_type.t_if) {
                        // Attempt to merge the "else" and "if" syntax
                        setMod(pending_brace);
                        int id2 = emit.beginBlock(elseBlock);
                        elseBlock.emit(this);
                        emit.endBlock(id2);
                    }
                    else {
                        int id2 = emit.startIndent();
                        emit.print(OPEN_CURLY);
                        int id3 = emit.beginBlock(elseBlock);
                        elseBlock.emit(this);
                        emit.endBlock(id3);
                        emit.stopIndent(id2);
                        emit.tagLine();
                        emit.print(CLOSE_CURLY);
                    }
                }
            }
            popMod();
            if (pendingBrace.getIndentId() >= 0) {
                emit.stopIndent(pendingBrace.getIndentId());
                emit.tagLine();
                emit.print(CLOSE_CURLY);
            }
        }

        public override void emitBlockWhileDo(BlockWhileDo bl)
        {
            PcodeOp op;
            int indent;

            if (bl.getIterateOp() != (PcodeOp)null) {
                emitForLoop(bl);
                return;
            }
            // whiledo block NEVER prints final branch
            pushMod();
            unsetMod(no_branch | only_branch);
            emitAnyLabelStatement(bl);
            FlowBlock condBlock = bl.getBlock(0);
            op = condBlock.lastOp();
            if (bl.hasOverflowSyntax()) {
                // Print conditional block as
                //     while( true ) {
                //       conditionbody ...
                //       if (conditionalbranch) break;
                emit.tagLine();
                emit.tagOp(KEYWORD_WHILE, EmitMarkup.syntax_highlight.keyword_color, op);
                int id1 = emit.openParen(OPEN_PAREN);
                emit.spaces(1);
                emit.print(KEYWORD_TRUE, EmitMarkup.syntax_highlight.const_color);
                emit.spaces(1);
                emit.closeParen(CLOSE_PAREN, id1);
                emit.spaces(1);
                indent = emit.startIndent();
                emit.print(OPEN_CURLY);
                pushMod();
                setMod(no_branch);
                condBlock.emit(this);
                popMod();
                emitCommentBlockTree(condBlock);
                emit.tagLine();
                emit.tagOp(KEYWORD_IF, EmitMarkup.syntax_highlight.keyword_color, op);
                emit.spaces(1);
                pushMod();
                setMod(only_branch);
                condBlock.emit(this);
                popMod();
                emit.spaces(1);
                emitGotoStatement(condBlock, (FlowBlock)null,FlowBlock.block_flags.f_break_goto);
            }
            else {
                // Print conditional block "normally" as
                //     while(condition) {
                emitCommentBlockTree(condBlock);
                emit.tagLine();
                emit.tagOp(KEYWORD_WHILE, EmitMarkup.syntax_highlight.keyword_color, op);
                emit.spaces(1);
                int id1 = emit.openParen(OPEN_PAREN);
                pushMod();
                setMod(comma_separate);
                condBlock.emit(this);
                popMod();
                emit.closeParen(CLOSE_PAREN, id1);
                emit.spaces(1);
                indent = emit.startIndent();
                emit.print(OPEN_CURLY);
            }
            setMod(no_branch); // Dont print goto at bottom of clause
            int id2 = emit.beginBlock(bl.getBlock(1));
            bl.getBlock(1).emit(this);
            emit.endBlock(id2);
            emit.stopIndent(indent);
            emit.tagLine();
            emit.print(CLOSE_CURLY);
            popMod();
        }

        public override void emitBlockDoWhile(BlockDoWhile bl)
        {
            PcodeOp op;

            // dowhile block NEVER prints final branch
            pushMod();
            unsetMod(no_branch | only_branch);
            emitAnyLabelStatement(bl);
            emit.tagLine();
            emit.print(KEYWORD_DO, EmitMarkup.syntax_highlight.keyword_color);
            emit.spaces(1);
            int id = emit.startIndent();
            emit.print(OPEN_CURLY);
            pushMod();
            int id2 = emit.beginBlock(bl.getBlock(0));
            setMod(no_branch);
            bl.getBlock(0).emit(this);
            emit.endBlock(id2);
            popMod();
            emit.stopIndent(id);
            emit.tagLine();
            emit.print(CLOSE_CURLY);
            emit.spaces(1);
            op = bl.getBlock(0).lastOp();
            emit.tagOp(KEYWORD_WHILE, EmitMarkup.syntax_highlight.keyword_color, op);
            emit.spaces(1);
            setMod(only_branch);
            bl.getBlock(0).emit(this);
            emit.print(SEMICOLON);
            popMod();
        }

        public override void emitBlockInfLoop(BlockInfLoop bl)
        {
            PcodeOp op;

            pushMod();
            unsetMod(no_branch | only_branch);
            emitAnyLabelStatement(bl);
            emit.tagLine();
            emit.print(KEYWORD_DO, EmitMarkup.syntax_highlight.keyword_color);
            emit.spaces(1);
            int id = emit.startIndent();
            emit.print(OPEN_CURLY);
            int id1 = emit.beginBlock(bl.getBlock(0));
            bl.getBlock(0).emit(this);
            emit.endBlock(id1);
            emit.stopIndent(id);
            emit.tagLine();
            emit.print(CLOSE_CURLY);
            emit.spaces(1);
            op = bl.getBlock(0).lastOp();
            emit.tagOp(KEYWORD_WHILE, EmitMarkup.syntax_highlight.keyword_color, op);
            int id2 = emit.openParen(OPEN_PAREN);
            emit.spaces(1);
            emit.print(KEYWORD_TRUE, EmitMarkup.syntax_highlight.const_color);
            emit.spaces(1);
            emit.closeParen(CLOSE_PAREN, id2);
            emit.print(SEMICOLON);
            popMod();
        }

        public override void emitBlockSwitch(BlockSwitch bl)
        {
            FlowBlock bl2;

            pushMod();
            unsetMod(no_branch | only_branch);
            pushMod();
            setMod(no_branch);
            bl.getSwitchBlock().emit(this);
            popMod();
            emit.tagLine();
            pushMod();
            setMod(only_branch | comma_separate);
            bl.getSwitchBlock().emit(this);
            popMod();
            emit.spaces(1);
            emit.print(OPEN_CURLY);

            for (int i = 0; i < bl.getNumCaseBlocks(); ++i) {
                emitSwitchCase(i, bl);
                int id = emit.startIndent();
                if (bl.getGotoType(i) != 0) {
                    emit.tagLine();
                    emitGotoStatement(bl.getBlock(0), bl.getCaseBlock(i), bl.getGotoType(i));
                }
                else {
                    bl2 = bl.getCaseBlock(i);
                    int id2 = emit.beginBlock(bl2);
                    bl2.emit(this);
                    if (bl.isExit(i) && (i != bl.getNumCaseBlocks() - 1)) {
                        // Blocks that formally exit the switch
                        emit.tagLine();
                        emitGotoStatement(bl2, (FlowBlock)null, FlowBlock.block_flags.f_break_goto); // need an explicit break statement
                    }
                    emit.endBlock(id2);
                }
                emit.stopIndent(id);
            }
            emit.tagLine();
            emit.print(CLOSE_CURLY);
            popMod();
        }

        public override void opCopy(PcodeOp op)
        {
            pushVn(op.getIn(0), op, mods);
        }

        public override void opLoad(PcodeOp op)
        {
            bool usearray = checkArrayDeref(op.getIn(1));
            uint m = mods;
            if (usearray && (!isSet(force_pointer)))
                m |= print_load_value;
            else {
                pushOp(&dereference, op);
            }
            pushVn(op.getIn(1), op, m);
        }

        public override void opStore(PcodeOp op)
        {
            bool usearray;

            // We assume the STORE is a statement
            uint m = mods;
            pushOp(&assignment, op);    // This is an assignment
            usearray = checkArrayDeref(op.getIn(1));
            if (usearray && (!isSet(force_pointer)))
                m |= print_store_value;
            else
            {
                pushOp(&dereference, op);
            }
            // implied vn's pushed on in reverse order for efficiency
            // see PrintLanguage::pushVnImplied
            pushVn(op.getIn(2), op, mods);
            pushVn(op.getIn(1), op, m);
        }

        public override void opBranch(PcodeOp op)
        {
            if (isSet(flat)) {
                // Assume the BRANCH is a statement
                emit.tagOp(KEYWORD_GOTO, EmitMarkup.syntax_highlight.keyword_color, op);
                emit.spaces(1);
                pushVn(op.getIn(0), op, mods);
            }
        }

        /// Print the branching condition:
        ///   - If it is the first condition, print \b if
        ///   - If there is no block structure, print \b goto
        ///
        /// \param op is the CBRANCH PcodeOp
        public override void opCbranch(PcodeOp op)
        {
            // FIXME:  This routine shouldn't emit directly
            bool yesif = isSet(flat);
            bool yesparen = !isSet(comma_separate);
            bool booleanflip = op.isBooleanFlip();
            uint m = mods;

            if (yesif) {
                // If not printing block structure
                emit.tagOp(KEYWORD_IF, EmitMarkup.syntax_highlight.keyword_color, op);
                emit.spaces(1);
                if (op.isFallthruTrue()) {
                    // and the fallthru is the true branch
                    booleanflip = !booleanflip; // print negation of condition
                    m |= falsebranch;     // and print the false (non-fallthru) branch
                }
            }
            int id;
            if (yesparen)
                id = emit.openParen(OPEN_PAREN);
            else
                id = emit.openGroup();
            if (booleanflip) {
                if (checkPrintNegation(op.getIn(1))) {
                    m |= PrintLanguage.modifiers.negatetoken;
                    booleanflip = false;
                }
            }
            if (booleanflip)
                pushOp(&boolean_not, op);
            pushVn(op.getIn(1), op, m);
            // Make sure stack is clear before emitting more
            recurse();
            if (yesparen)
                emit.closeParen(CLOSE_PAREN, id);
            else
                emit.closeGroup(id);

            if (yesif) {
                emit.spaces(1);
                emit.print(KEYWORD_GOTO, EmitMarkup.syntax_highlight.keyword_color);
                emit.spaces(1);
                pushVn(op.getIn(0), op, mods);
            }
        }

        public override void opBranchind(PcodeOp op)
        {
            // FIXME:  This routine shouldn't emit directly
            emit.tagOp(KEYWORD_SWITCH, EmitMarkup.syntax_highlight.keyword_color, op); // Print header for switch
            int id = emit.openParen(OPEN_PAREN);
            pushVn(op.getIn(0), op, mods);
            recurse();
            emit.closeParen(CLOSE_PAREN, id);
        }

        public override void opCall(PcodeOp op)
        {
            pushOp(&function_call, op);
            Varnode callpoint = op.getIn(0);
            FuncCallSpecs fc;
            if (callpoint.getSpace().getType() == spacetype.IPTR_FSPEC) {
                fc = FuncCallSpecs::getFspecFromConst(callpoint.getAddr());
                if (fc.getName().Length == 0) {
                    string nm = genericFunctionName(fc.getEntryAddress());
                    pushAtom(new Atom(nm, functoken, EmitMarkup.syntax_highlight.funcname_color, op, (Funcdata)null));
                }
                else {
                    Funcdata? fd = fc.getFuncdata();
                    if (fd != (Funcdata)null)
                        pushSymbolScope(fd.getSymbol());
                    pushAtom(new Atom(fc.getName(), functoken, EmitMarkup.syntax_highlight.funcname_color, op, (Funcdata)null));
                }
            }
            else {
                clear();
                throw new CORE.LowlevelError("Missing function callspec");
            }
            // TODO: Cannot hide "this" on a direct call until we print the whole
            // thing with the proper C++ method invocation format. Otherwise the output
            // gives no indication of what object has a method being called.
            // int skip = getHiddenThisSlot(op, fc);
            int skip = -1;
            int count = op.numInput() - 1;    // Number of parameter expressions printed
            count -= (skip < 0) ? 0 : 1;        // Subtract one if "this" is hidden
            if (count > 0) {
                for (int i = 0; i < count - 1; ++i)
                    pushOp(&comma, op);
                // implied vn's pushed on in reverse order for efficiency
                // see PrintLanguage::pushVnImplied
                for (int i = op.numInput() - 1; i >= 1; --i) {
                    if (i == skip) continue;
                    pushVn(op.getIn(i), op, mods);
                }
            }
            else                // Push empty token for void
                pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
        }

        public override void opCallind(PcodeOp op)
        {
            pushOp(&function_call, op);
            pushOp(&dereference, op);
            Funcdata fd = op.getParent().getFuncdata();
            FuncCallSpecs? fc = fd.getCallSpecs(op);
            if (fc == (FuncCallSpecs)null)
                throw new CORE.LowlevelError("Missing indirect function callspec");
            int skip = getHiddenThisSlot(op, fc);
            int count = op.numInput() - 1;
            count -= (skip < 0) ? 0 : 1;
            if (count > 1) {
                // Multiple parameters
                pushVn(op.getIn(0), op, mods);
                for (int i = 0; i < count - 1; ++i)
                    pushOp(&comma, op);
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
                pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
            }
        }

        public override void opCallother(PcodeOp op)
        {
            UserPcodeOp userop = glb.userops.getOp(op.getIn(0).getOffset());
            uint display = userop.getDisplay();
            if (display == UserPcodeOp.userop_flags.annotation_assignment) {
                pushOp(&assignment, op);
                pushVn(op.getIn(2), op, mods);
                pushVn(op.getIn(1), op, mods);
            }
            else if (display == UserPcodeOp.userop_flags.no_operator) {
                pushVn(op.getIn(1), op, mods);
            }
            else {
                // Emit using functional syntax
                string nm = op.getOpcode().getOperatorName(op);
                pushOp(&function_call, op);
                pushAtom(new Atom(nm, optoken, EmitMarkup.syntax_highlight.funcname_color, op));
                if (op.numInput() > 1) {
                    for (int i = 1; i < op.numInput() - 1; ++i)
                        pushOp(&comma, op);
                    // implied vn's pushed on in reverse order for efficiency
                    // see PrintLanguage::pushVnImplied
                    for (int i = op.numInput() - 1; i >= 1; --i)
                        pushVn(op.getIn(i), op, mods);
                }
                else
                    pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color)); // Push empty token for void
            }
        }

        public override void opConstructor(PcodeOp op,bool withNew)
        {
            Datatype dt;
            if (withNew) {
                PcodeOp newop = op.getIn(1).getDef();
                Varnode outvn = newop.getOut();
                pushOp(&new_op, newop);
                pushAtom(new Atom(KEYWORD_NEW, optoken, EmitMarkup.syntax_highlight.keyword_color, newop, outvn));
                dt = outvn.getTypeDefFacing();
            }
            else {
                Varnode thisvn = op.getIn(1);
                dt = thisvn.getType();
            }
            if (dt.getMetatype() == type_metatype.TYPE_PTR) {
                dt = ((TypePointer)dt).getPtrTo();
            }
            string nm = dt.getDisplayName();
            pushOp(&function_call, op);
            pushAtom(new Atom(nm, optoken, EmitMarkup.syntax_highlight.funcname_color, op));
            // implied vn's pushed on in reverse order for efficiency
            // see PrintLanguage::pushVnImplied
            if (op.numInput() > 3) {
                // Multiple (non-this) parameters
                for (int i = 2; i < op.numInput() - 1; ++i)
                    pushOp(&comma, op);
                for (int i = op.numInput() - 1; i >= 2; --i)
                    pushVn(op.getIn(i), op, mods);
            }
            else if (op.numInput() == 3) {
                // One parameter
                pushVn(op.getIn(2), op, mods);
            }
            else {
                // A void function
                pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
            }
        }

        public override void opReturn(PcodeOp op)
        {
            string nm;
            switch (op.getHaltType()) {
                default:            // The most common case, plain return
                                    // FIXME:  This routine shouldn't emit directly
                    emit.tagOp(KEYWORD_RETURN, EmitMarkup.syntax_highlight.keyword_color, op);
                    if (op.numInput() > 1) {
                        emit.spaces(1);
                        pushVn(op.getIn(1), op, mods);
                    }
                    return;
                case PcodeOp.Flags.noreturn: // Previous instruction does not exit
                case PcodeOp.Flags.halt:     // Process halts
                    nm = "halt";
                    break;
                case PcodeOp.Flags.badinstruction:
                    nm = "halt_baddata";    // CPU executes bad instruction
                    break;
                case PcodeOp.Flags.unimplemented:    // instruction is unimplemented
                    nm = "halt_unimplemented";
                    break;
                case PcodeOp.Flags.missing:  // Did not analyze this instruction
                    nm = "halt_missing";
                    break;
            }
            pushOp(&function_call, op);
            pushAtom(new Atom(nm, optoken, EmitMarkup.syntax_highlight.funcname_color, op));
            pushAtom(new Atom(EMPTY_STRING, blanktoken, EmitMarkup.syntax_highlight.no_color));
        }

        public override void opIntEqual(PcodeOp op)
        {
            opBinary(&equal, op);
        }

        public override void opIntNotEqual(PcodeOp op)
        {
            opBinary(&not_equal, op);
        }

        public override void opIntSless(PcodeOp op)
        {
            opBinary(&less_than, op);
        }

        public override void opIntSlessEqual(PcodeOp op)
        {
            opBinary(&less_equal, op);
        }

        public override void opIntLess(PcodeOp op)
        {
            opBinary(&less_than, op);
        }

        public override void opIntLessEqual(PcodeOp op)
        {
            opBinary(&less_equal, op);
        }

        public override void opIntZext(PcodeOp op, PcodeOp readOp)
        {
            if (castStrategy.isZextCast(op.getOut().getHighTypeDefFacing(), op.getIn(0).getHighTypeReadFacing(op))) {
                if (option_hide_exts && castStrategy.isExtensionCastImplied(op, readOp))
                    opHiddenFunc(op);
                else
                    opTypeCast(op);
            }
            else
                opFunc(op);
        }

        public override void opIntSext(PcodeOp op, PcodeOp readOp)
        {
            if (castStrategy.isSextCast(op.getOut().getHighTypeDefFacing(), op.getIn(0).getHighTypeReadFacing(op))) {
                if (option_hide_exts && castStrategy.isExtensionCastImplied(op, readOp))
                    opHiddenFunc(op);
                else
                    opTypeCast(op);
            }
            else
                opFunc(op);
        }

        public override void opIntAdd(PcodeOp op)
        {
            opBinary(&binary_plus, op);
        }

        public override void opIntSub(PcodeOp op)
        {
            opBinary(&binary_minus, op);
        }

        public override void opIntCarry(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opIntScarry(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opIntSborrow(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opInt2Comp(PcodeOp op)
        {
            opUnary(&unary_minus, op);
        }

        public override void opIntNegate(PcodeOp op)
        {
            opUnary(&bitwise_not, op);
        }

        public override void opIntXor(PcodeOp op)
        {
            opBinary(&bitwise_xor, op);
        }

        public override void opIntAnd(PcodeOp op)
        {
            opBinary(&bitwise_and, op);
        }

        public override void opIntOr(PcodeOp op)
        {
            opBinary(&bitwise_or, op);
        }

        public override void opIntLeft(PcodeOp op)
        {
            opBinary(&shift_left, op);
        }

        public override void opIntRight(PcodeOp op)
        {
            opBinary(&shift_right, op);
        }

        public override void opIntSright(PcodeOp op)
        {
            opBinary(&shift_sright, op);
        }

        public override void opIntMult(PcodeOp op)
        {
            opBinary(&multiply, op);
        }

        public override void opIntDiv(PcodeOp op)
        {
            opBinary(&divide, op);
        }

        public override void opIntSdiv(PcodeOp op)
        {
            opBinary(&divide, op);
        }

        public override void opIntRem(PcodeOp op)
        {
            opBinary(&modulo, op);
        }

        public override void opIntSrem(PcodeOp op)
        {
            opBinary(&modulo, op);
        }

        /// Print the BOOL_NEGATE but check for opportunities to flip the next operator instead
        /// \param op is the BOOL_NEGATE PcodeOp
        public override void opBoolNegate(PcodeOp op)
        {
            if (isSet(negatetoken))
            {   // Check if we are negated by a previous BOOL_NEGATE
                unsetMod(negatetoken);  // If so, mark that negatetoken is consumed
                pushVn(op.getIn(0), op, mods); // Don't print ourselves, but print our input unmodified
            }
            else if (checkPrintNegation(op.getIn(0)))
            { // If the next operator can be flipped
                pushVn(op.getIn(0), op, mods | negatetoken); // Don't print ourselves, but print a modified input
            }
            else
            {
                pushOp(&boolean_not, op);   // Otherwise print ourselves
                pushVn(op.getIn(0), op, mods); // And print our input
            }
        }

        public override void opBoolXor(PcodeOp op)
        {
            opBinary(&boolean_xor, op);
        }

        public override void opBoolAnd(PcodeOp op)
        {
            opBinary(&boolean_and, op);
        }

        public override void opBoolOr(PcodeOp op)
        {
            opBinary(&boolean_or, op);
        }

        public override void opFloatEqual(PcodeOp op)
        {
            opBinary(&equal, op);
        }

        public override void opFloatNotEqual(PcodeOp op)
        {
            opBinary(&not_equal, op);
        }

        public override void opFloatLess(PcodeOp op)
        {
            opBinary(&less_than, op);
        }

        public override void opFloatLessEqual(PcodeOp op)
        {
            opBinary(&less_equal, op);
        }

        public override void opFloatNan(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opFloatAdd(PcodeOp op)
        {
            opBinary(&binary_plus, op);
        }

        public override void opFloatDiv(PcodeOp op)
        {
            opBinary(&divide, op);
        }

        public override void opFloatMult(PcodeOp op)
        {
            opBinary(&multiply, op);
        }

        public override void opFloatSub(PcodeOp op)
        {
            opBinary(&binary_minus, op);
        }

        public override void opFloatNeg(PcodeOp op)
        {
            opUnary(&unary_minus, op);
        }

        public override void opFloatAbs(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opFloatSqrt(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opFloatInt2Float(PcodeOp op)
        {
            opTypeCast(op);
        }

        public override void opFloatFloat2Float(PcodeOp op)
        {
            opTypeCast(op);
        }

        public override void opFloatTrunc(PcodeOp op)
        {
            opTypeCast(op);
        }

        public override void opFloatCeil(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opFloatFloor(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opFloatRound(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opMultiequal(PcodeOp op)
        {
        }

        public override void opIndirect(PcodeOp op)
        {
        }

        public override void opPiece(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opSubpiece(PcodeOp op)
        {
            if (op.doesSpecialPrinting()) {
                // Special printing means it is a field extraction
                Varnode vn = op.getIn(0);
                Datatype ct = vn.getHighTypeReadFacing(op);
                if (ct.isPieceStructured()) {
                    int offset;
                    int byteOff = TypeOpSubpiece.computeByteOffsetForComposite(op);
                    TypeField? field = ct.findTruncation(byteOff, op.getOut().getSize(), op, 1, offset);   // Use artificial slot
                    if (field != (TypeField)null && offset == 0) {
                        // A formal structure field
                        pushOp(&object_member, op);
                        pushVn(vn, op, mods);
                        pushAtom(new Atom(field.name, fieldtoken, EmitMarkup.syntax_highlight.no_color, ct, field.ident, op));
                        return;
                    }
                    else if (vn.isExplicit() && vn.getHigh().getSymbolOffset() == -1)
                    {   // An explicit, entire, structured object
                        Symbol? sym = vn.getHigh().getSymbol();
                        if (sym != (Symbol)null)
                        {
                            int sz = op.getOut().getSize();
                            int off = (int)op.getIn(1).getOffset();
                            off = vn.getSpace().isBigEndian() ? vn.getSize() - (sz + off) : off;
                            pushPartialSymbol(sym, off, sz, vn, op, -1);
                            return;
                        }
                    }
                    // Fall thru to functional printing
                }
            }
            if (castStrategy.isSubpieceCast(op.getOut().getHighTypeDefFacing(),
                             op.getIn(0).getHighTypeReadFacing(op),
                             (uint)op.getIn(1).getOffset()))
                opTypeCast(op);
            else
                opFunc(op);
        }

        public override void opCast(PcodeOp op)
        {
            opTypeCast(op);
        }

        public override void opPtradd(PcodeOp op)
        {
            bool printval = isSet(print_load_value | print_store_value);
            uint m = mods & ~(print_load_value | print_store_value);
            if (!printval) {
                TypePointer tp = (TypePointer)op.getIn(0).getHighTypeReadFacing(op);
                if (tp.getMetatype() == type_metatype.TYPE_PTR) {
                    if (tp.getPtrTo().getMetatype() == type_metatype.TYPE_ARRAY)
                        printval = true;
                }
            }
            if (printval)           // Use array notation if we need value
                pushOp(&subscript, op);
            else                // just a '+'
                pushOp(&binary_plus, op);
            // implied vn's pushed on in reverse order for efficiency
            // see PrintLanguage::pushVnImplied
            pushVn(op.getIn(1), op, m);
            pushVn(op.getIn(0), op, m);
        }

        /// We need to distinguish between the following cases:
        ///  - ptr.        struct spacebase or array
        ///  - valueoption  on/off   (from below)
        ///  - valueflex    yes/no   (can we turn valueoption above?)
        ///
        /// Then the printing breaks up into the following table:
        /// \code
        ///         val flex   |   val flex   |   val flex   |   val flex
        ///         off  yes       off   no        on  yes        on   no
        ///
        /// struct  &( ).name      &( ).name     ( ).name       ( ).name
        /// spcbase n/a            &name          n/a            name
        /// array   ( )            *( )           ( )[0]         *( )[0]
        /// \endcode
        /// The '&' is dropped if the output type is an array
        /// \param op is the PTRSUB PcodeOp
        public override void opPtrsub(PcodeOp op)
        {
            TypePointer ptype;
            TypePointerRel ptrel;
            Datatype ct;
            Varnode in0;
            ulong in1const;
            bool valueon, flex, arrayvalue;
            uint m;

            in0 = op.getIn(0);
            in1const = op.getIn(1).getOffset();
            ptype = (TypePointer)in0.getHighTypeReadFacing(op);
            if (ptype.getMetatype() != type_metatype.TYPE_PTR) {
                clear();
                throw new CORE.LowlevelError("PTRSUB off of non-pointer type");
            }
            if (ptype.isFormalPointerRel() && ((TypePointerRel)ptype).evaluateThruParent(in1const)) {
                ptrel = (TypePointerRel)ptype;
                ct = ptrel.getParent();
            }
            else {
                ptrel = (TypePointerRel)null;
                ct = ptype.getPtrTo();
            }
            m = mods & ~(print_load_value | print_store_value); // Current state of mods
            valueon = (mods & (print_load_value | print_store_value)) != 0;
            flex = isValueFlexible(in0);

            if (ct.getMetatype() == type_metatype.TYPE_STRUCT || ct.getMetatype() == type_metatype.TYPE_UNION) {
                ulong suboff = in1const;    // How far into container
                if (ptrel != (TypePointerRel)null) {
                    suboff += ptrel.getPointerOffset();
                    suboff &= Globals.calc_mask(ptype.getSize());
                    if (suboff == 0) {
                        // Special case where we do not print a field
                        pushTypePointerRel(op);
                        if (flex)
                            pushVn(in0, op, m | print_load_value);
                        else
                            pushVn(in0, op, m);
                        return;
                    }
                }
                suboff = AddrSpace.addressToByte(suboff, ptype.getWordSize());
                string fieldname;
                Datatype fieldtype;
                int fieldid;
                int newoff;
                if (ct.getMetatype() == type_metatype.TYPE_UNION) {
                    if (suboff != 0)
                        throw new CORE.LowlevelError("PTRSUB accesses union with non-zero offset");
                    Funcdata fd = op.getParent().getFuncdata();
                    ResolvedUnion? resUnion = fd.getUnionField(ptype, op, -1);
                    if (resUnion == (ResolvedUnion)null || resUnion.getFieldNum() < 0)
                        throw new CORE.LowlevelError("PTRSUB for union that does not resolve to a field");
                    TypeField fld = ((TypeUnion)ct).getField(resUnion.getFieldNum());
                    fieldid = fld.ident;
                    fieldname = fld.name;
                    fieldtype = fld.type;
                }
                else {
                    // type_metatype.TYPE_STRUCT
                    TypeField? fld = ct.findTruncation((int)suboff, 0, op, 0, newoff);
                    if (fld == (TypeField)null) {
                        if (ct.getSize() <= suboff) {
                            clear();
                            throw new CORE.LowlevelError("PTRSUB out of bounds into struct");
                        }
                        // Try to match the Ghidra's default field name from DataTypeComponent.getDefaultFieldName
                        StringWriter s = new StringWriter();
                        s.Write($"field_0x{suboff:X}");
                        fieldname = s.ToString();
                        fieldtype = (Datatype)null;
                        fieldid = (int)suboff;
                    }
                    else {
                        fieldname = fld.name;
                        fieldtype = fld.type;
                        fieldid = fld.ident;
                    }
                }
                arrayvalue = false;
                // The '&' is dropped if the output type is an array
                if ((fieldtype != (Datatype)null) && (fieldtype.getMetatype() == type_metatype.TYPE_ARRAY)) {
                    arrayvalue = valueon;   // If printing value, use [0]
                    valueon = true;     // Don't print &
                }

                if (!valueon) {
                    // Printing an ampersand
                    if (flex) {
                        // EMIT  &( ).name
                        pushOp(&addressof, op);
                        pushOp(&object_member, op);
                        if (ptrel != (TypePointerRel*)0)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m | print_load_value);
                        pushAtom(new Atom(fieldname, fieldtoken, EmitMarkup.syntax_highlight.no_color, ct, fieldid, op));
                    }
                    else {           // EMIT  &( ).name
                        pushOp(&addressof, op);
                        pushOp(&pointer_member, op);
                        if (ptrel != (TypePointerRel)null)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m);
                        pushAtom(new Atom(fieldname, fieldtoken, EmitMarkup.syntax_highlight.no_color, ct, fieldid, op));
                    }
                }
                else {
                    // Not printing an ampersand
                    if (arrayvalue)
                        pushOp(&subscript, op);
                    if (flex) {
                        // EMIT  ( ).name
                        pushOp(&object_member, op);
                        if (ptrel != (TypePointerRel)null)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m | print_load_value);
                        pushAtom(new Atom(fieldname, fieldtoken, EmitMarkup.syntax_highlight.no_color, ct, fieldid, op));
                    }
                    else {
                        // EMIT  ( ).name
                        pushOp(&pointer_member, op);
                        if (ptrel != (TypePointerRel)null)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m);
                        pushAtom(new Atom(fieldname, fieldtoken, EmitMarkup.syntax_highlight.no_color, ct, fieldid, op));
                    }
                    if (arrayvalue)
                        push_integer(0, 4, false, (Varnode)null, op);
                }
            }
            else if (ct.getMetatype() == type_metatype.TYPE_SPACEBASE) {
                HighVariable high = op.getIn(1).getHigh();
                Symbol symbol = high.getSymbol();
                arrayvalue = false;
                if (symbol != (Symbol)null) {
                    ct = symbol.getType();
                    // The '&' is dropped if the output type is an array
                    if (ct.getMetatype() == type_metatype.TYPE_ARRAY) {
                        arrayvalue = valueon;   // If printing value, use [0]
                        valueon = true;     // If printing ptr, don't use &
                    }
                    else if (ct.getMetatype() == type_metatype.TYPE_CODE)
                        valueon = true;     // If printing ptr, don't use &
                }
                if (!valueon) {
                    // EMIT  &name
                    pushOp(&addressof, op);
                }
                else {
                    // EMIT  name
                    if (arrayvalue)
                        pushOp(&subscript, op);
                }
                if (symbol == (Symbol)null) {
                    TypeSpacebase sb = (TypeSpacebase)ct;
                    Address addr = sb.getAddress(in1const, in0.getSize(), op.getAddr());
                    pushUnnamedLocation(addr, (Varnode)null, op);
                }
                else {
                    int off = high.getSymbolOffset();
                    if (off == 0)
                        pushSymbol(symbol, (Varnode)null, op);
                    else {
                        // If this "value" is getting used as a storage location
                        // we can't use a cast in its description, so turn off
                        // casting when printing the partial symbol
                        //	Datatype *exttype = ((mods & print_store_value)!=0) ? (Datatype)null : ct;
                        pushPartialSymbol(symbol, off, 0, (Varnode)null, op, -1);
                    }
                }
                if (arrayvalue)
                    push_integer(0, 4, false, (Varnode)null, op);
            }
            else if (ct.getMetatype() == type_metatype.TYPE_ARRAY) {
                if (in1const != 0) {
                    clear();
                    throw new CORE.LowlevelError("PTRSUB with non-zero offset into array type");
                }
                // We are treating array as a structure
                // and this PTRSUB(*,0) represents changing
                // to treating it as a pointer to its element type
                if (!valueon) {
                    if (flex) {       // EMIT  ( )
                            // (*&struct.arrayfield)[i]
                            // becomes struct.arrayfield[i]
                        if (ptrel != (TypePointerRel)null)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m);
                    }
                    else {
                        // EMIT  *( )
                        pushOp(&dereference, op);
                        if (ptrel != (TypePointerRel)null)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m);
                    }
                }
                else {
                    if (flex) {
                        // EMIT  ( )[0]
                        pushOp(&subscript, op);
                        if (ptrel != (TypePointerRel)null)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m);
                        push_integer(0, 4, false, (Varnode)null, op);
                    }
                    else {
                        // EMIT  (* )[0]
                        pushOp(&subscript, op);
                        pushOp(&dereference, op);
                        if (ptrel != (TypePointerRel)null)
                            pushTypePointerRel(op);
                        pushVn(in0, op, m);
                        push_integer(0, 4, false, (Varnode)null, op);
                    }
                }
            }
            else {
                clear();
                throw new CORE.LowlevelError("PTRSUB off of non structured pointer type");
            }
        }

        /// - slot 0 is the spaceid constant
        /// - slot 1 is the segment, we could conceivably try to annotate the segment here
        /// - slot 2 is the pointer we are really interested in printing
        ///
        /// \param op is the SEGMENTOP PcodeOp
        public override void opSegmentOp(PcodeOp op)
        {
            pushVn(op.getIn(2), op, mods);
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
                pushAtom(new Atom("UNKNOWNREF", syntax, EmitMarkup.syntax_highlight.const_color, op, outvn));
            }
            else {
                switch (rec.getTag()) {
                    case CPoolRecord.ConstantPoolTagTypes.string_literal:
                        {
                            StringWriter str = new StringWriter();
                            int len = rec.getByteDataLength();
                            if (len > 2048)
                                len = 2048;
                            str.Write('"');
                            escapeCharacterData(str, rec.getByteData(), len, 1, false);
                            if (len == rec.getByteDataLength())
                                str.Write('"');
                            else {
                                str.Write("...\"");
                            }
                            pushAtom(new Atom(str.str(), vartoken, EmitMarkup.syntax_highlight.const_color, op, outvn));
                            break;
                        }
                    case CPoolRecord.ConstantPoolTagTypes.class_reference:
                        pushAtom(new Atom(rec.getToken(), vartoken, EmitMarkup.syntax_highlight.type_color, op, outvn));
                        break;
                    case CPoolRecord.ConstantPoolTagTypes.instance_of:
                        {
                            Datatype dt = rec.getType();
                            while (dt.getMetatype() == type_metatype.TYPE_PTR) {
                                dt = ((TypePointer)dt).getPtrTo();
                            }
                            pushOp(&function_call, op);
                            pushAtom(new Atom(rec.getToken(), functoken, EmitMarkup.syntax_highlight.funcname_color, op, outvn));
                            pushOp(&comma, (PcodeOp)null);
                            pushVn(vn0, op, mods);
                            pushAtom(new Atom(dt.getDisplayName(), syntax, EmitMarkup.syntax_highlight.type_color, op, outvn));
                            break;
                        }
                    case CPoolRecord.ConstantPoolTagTypes.primitive:        // Should be eliminated
                    case CPoolRecord.ConstantPoolTagTypes.pointer_method:
                    case CPoolRecord.ConstantPoolTagTypes.pointer_field:
                    case CPoolRecord.ConstantPoolTagTypes.array_length:
                    case CPoolRecord.ConstantPoolTagTypes.check_cast:
                    default:
                        {
                            Datatype ct = rec.getType();
                            EmitMarkup.syntax_highlight color = EmitMarkup.syntax_highlight.var_color;
                            if (ct.getMetatype() == type_metatype.TYPE_PTR) {
                                ct = ((TypePointer)ct).getPtrTo();
                                if (ct.getMetatype() == type_metatype.TYPE_CODE)
                                    color = EmitMarkup.syntax_highlight.funcname_color;
                            }
                            if (vn0.isConstant()) {
                                // If this is NOT relative to an object reference
                                pushAtom(new Atom(rec.getToken(), vartoken, color, op, outvn));
                            }
                            else {
                                pushOp(&pointer_member, op);
                                pushVn(vn0, op, mods);
                                pushAtom(new Atom(rec.getToken(), syntax, color, op, outvn));
                            }
                            break;
                        }
                }
            }
        }

        public override void opNewOp(PcodeOp op)
        {
            Varnode? outvn = op.getOut();
            Varnode vn0 = op.getIn(0);
            if (op.numInput() == 2) {
                Varnode vn1 = op.getIn(1);
                if (!vn0.isConstant()) {
                    // Array allocation form
                    pushOp(&new_op, op);
                    pushAtom(new Atom(KEYWORD_NEW, optoken, EmitMarkup.syntax_highlight.keyword_color, op, outvn));
                    string nm;
                    if (outvn == (Varnode)null) {   // Its technically possible, for new result to be unused
                        nm = "<unused>";
                    }
                    else {
                        Datatype dt = outvn.getTypeDefFacing();
                        while (dt.getMetatype() == type_metatype.TYPE_PTR) {
                            dt = ((TypePointer)dt).getPtrTo();
                        }
                        nm = dt.getDisplayName();
                    }
                    pushOp(&subscript, op);
                    pushAtom(new Atom(nm, optoken, EmitMarkup.syntax_highlight.type_color, op));
                    pushVn(vn1, op, mods);
                    return;
                }
            }
            // This printing is used only if the 'new' operator doesn't feed directly into a constructor
            pushOp(&function_call, op);
            pushAtom(new Atom(KEYWORD_NEW, optoken, EmitMarkup.syntax_highlight.keyword_color, op, outvn));
            pushVn(vn0, op, mods);
        }

        public override void opInsertOp(PcodeOp op)
        {
            opFunc(op); // If no other way to print it, print as functional operator
        }

        public override void opExtractOp(PcodeOp op)
        {
            opFunc(op); // If no other way to print it, print as functional operator
        }

        public override void opPopcountOp(PcodeOp op)
        {
            opFunc(op);
        }

        public override void opLzcountOp(PcodeOp op)
        {
            opFunc(op);
        }

        private static bool isValueFlexible(Varnode vn)
        {
            if ((vn.isImplied()) && (vn.isWritten())) {
                PcodeOp def = vn.getDef();
                if (def.code() == OpCode.CPUI_PTRSUB) return true;
                if (def.code() == OpCode.CPUI_PTRADD) return true;
            }
            return false;
        }
    }
}
