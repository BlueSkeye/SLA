using Sla.CORE;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief The base class API for emitting a high-level language
    ///
    /// Instances of this object are responsible for converting a function's
    /// (transformed) data-flow graph into the final stream of tokens of a high-level
    /// source code language.  There a few main entry points including:
    ///   - docFunction()
    ///   - docAllGlobals()
    ///   - docTypeDefinitions()
    ///
    /// The system is responsible for printing:
    ///   - Control-flow structures
    ///   - Expressions
    ///   - Type declarations
    ///   - Function prototypes
    ///   - Comments
    ///
    /// As part of all this printing, the system is also responsible for
    ///   - Emitting integers, floats, and character constants
    ///   - Placing parentheses within expressions to properly represent data-flow
    ///   - Deciding whether \e cast operations need an explicit cast token
    ///   - Indenting and line wrapping
    ///
    /// To accomplish this, the API is broken up into three sections. The first section
    /// are the main entry point 'doc' methods. The second section are 'emit' methods, which
    /// are responsible for printing a representation of a particular high-level code construct.
    /// The third section are 'push' and 'op' methods, which are responsible for walking expression trees.
    /// The order in which tokens are emitted for an expression is determined by a
    /// Reverse Polish Notation (RPN) stack, that the 'push' methods manipulate. Operators and variables
    /// are \e pushed onto this stack and are ultimately \e emitted in the correct order.
    ///
    /// The base class provides a generic \e printing \e modifications stack and a \e symbol \e scope
    /// stack to provide a printing context mechanism for derived classes.
    internal abstract class PrintLanguage
    {
        public const string OPEN_PAREN = "(";   ///< "(" token
        public const string CLOSE_PAREN = ")";    ///< ")" token

        /// \brief Possible context sensitive modifiers to how tokens get emitted
        [Flags()]
        public enum modifiers
        {
            force_hex = 1,      ///< Force printing of hex
            force_dec = 2,          ///< Force printing of dec
            bestfit = 4,            ///< Decide on most aesthetic form
            force_scinote = 8,      ///< Force scientific notation for floats
            force_pointer = 0x10,   ///< Force '*' notation for pointers
            print_load_value = 0x20,    ///< Hide pointer deref for load with other ops
            print_store_value = 0x40,   ///< Hide pointer deref for store with other ops
            no_branch = 0x80,       ///< Do not print branch instruction
            only_branch = 0x100,    ///< Print only the branch instruction
            comma_separate = 0x200, ///< Statements within condition
            flat = 0x400,       ///< Do not print block structure
            falsebranch = 0x800,    ///< Print the false branch (for flat)
            nofallthru = 0x1000,        ///< Fall-thru no longer exists
            negatetoken = 0x2000,   ///< Print the token representing the negation of current token
            hide_thisparam = 0x4000,    ///< Do not print the 'this' parameter in argument lists
            pending_brace = 0x8000  ///< The current block may need to surround itself with additional braces
        }
        
        /// \brief Possible types of Atom
        public enum tagtype
        {
            syntax,         ///< Emit atom as syntax
            vartoken,           ///< Emit atom as variable
            functoken,          ///< Emit atom as function name
            optoken,            ///< Emit atom as operator
            typetoken,          ///< Emit atom as operator
            fieldtoken,         ///< Emit atom as structure field
            blanktoken          ///< For anonymous types
        }

        /// \brief Strategies for displaying namespace tokens
        public enum namespace_strategy
        {
            MINIMAL_NAMESPACES = 0, ///< (default) Print just enough namespace info to fully resolve symbol
            NO_NAMESPACES = 1,      ///< Never print namespace information
            ALL_NAMESPACES = 2      ///< Always print all namespace information
        };

        /// \brief An entry on the reverse polish notation (RPN) stack
        public struct ReversePolish
        {
            internal readonly OpToken tok;     ///< The operator token
            internal int visited;       ///< The current stage of printing for the operator
            internal bool paren;         ///< True if parentheses are required
            internal readonly PcodeOp op;      ///< The PcodeOp associated with the operator token
            internal int id;            ///< The id of the token group which \b this belongs to
            internal /*mutable*/ int id2;		///< The id of the token group \b this surrounds (for surround operator tokens)
        }

        /// \brief A pending data-flow node; waiting to be placed on the reverse polish notation stack
        ///
        /// This holds an \e implied Varnode in the data-flow graph, which prints as the expression producing
        /// the value in the Varnode.
        public struct NodePending
        {
            internal readonly Varnode vn;      ///< The implied Varnode
            internal readonly PcodeOp op;      ///< The single operator consuming value from the implied Varnode
            internal uint vnmod;        ///< Printing modifications to enforce on the expression

            /// \brief Construct a pending data-flow node
            internal NodePending(Varnode v, PcodeOp o, uint m)
            {
                vn = v;
                op = o;
                vnmod = m;
            }
        }

        /// \brief A single non-operator token emitted by the decompiler
        /// These play the role of variable tokens on the RPN stack with the operator tokens.
        /// The term \e variable has a broader meaning than just a Varnode. An Atom can also be a data-type
        /// name, a function name, or a structure field etc.
        public struct Atom
        {
            internal readonly string name;		///< The actual printed characters of the token
            internal tagtype type;       ///< The type of Atom
            internal EmitMarkup.syntax_highlight highlight; ///< The type of highlighting to use when emitting the token
            internal readonly PcodeOp op;      ///< A p-code operation associated with the token
            ///< Other meta-data associated with the token
            internal struct /*union*/ Strptr_second
            {
                internal Varnode vn;    ///< A Varnode associated with the token
                internal Funcdata fd; ///< A function associated with the token
                internal Datatype ct; ///< A type associated with the token
            }
            internal Strptr_second ptr_second = new Strptr_second();
            internal int offset;        	///< The offset (within the parent structure) for a \e field token

            /// \brief Construct a token with no associated data-flow annotations
            internal Atom(string nm, tagtype t, EmitMarkup.syntax_highlight hl)
            {
                name = nm;
                type = t;
                highlight = hl;
            }

            /// \brief Construct a token for a data-type name
            internal Atom(string nm, tagtype t, EmitMarkup.syntax_highlight hl, Datatype c)
            {
                name = nm;
                type = t;
                highlight = hl;
                ptr_second.ct = c;
            }

            /// \brief Construct a token for a field name
            internal Atom(string nm, tagtype t, EmitMarkup.syntax_highlight hl, Datatype c, int off,
                PcodeOp o)
            {
                name = nm;
                type = t;
                highlight = hl;
                ptr_second.ct = c;
                offset = off;
                op = o;
            }

            /// \brief Construct a token with an associated PcodeOp
            internal Atom(string nm, tagtype t, EmitMarkup.syntax_highlight hl, PcodeOp o)
            {
                name = nm;
                type = t;
                highlight = hl;
                op = o;
            }

            /// \brief Construct a token with an associated PcodeOp and Varnode
            internal Atom(string nm, tagtype t, EmitMarkup.syntax_highlight hl, PcodeOp o, Varnode v)
            {
                name = nm;
                type = t;
                highlight = hl;
                ptr_second.vn = v;
                op = o;
            }

            /// \brief Construct a token for a function name
            internal Atom(string nm, tagtype t, EmitMarkup.syntax_highlight hl, PcodeOp o, Funcdata f)
            {
                name = nm;
                type = t;
                highlight = hl;
                op = o;
                ptr_second.fd = f;
            }
        }

        private string name;              ///< The name of the high-level language
        private List<uint> modstack;     ///< Printing modification stack
        private List<Scope> scopestack;   ///< The symbol scope stack
        private List<ReversePolish> revpol;       ///< The Reverse Polish Notation (RPN) token stack
        private List<NodePending> nodepend;       ///< Data-flow nodes waiting to be pushed onto the RPN stack
        private int pending;               ///< Number of data-flow nodes waiting to be pushed
        private int line_commentindent;        ///< Number of characters a comment line should be indented
        private string commentstart;            ///< Delimiter characters for the start of a comment
        private string commentend;			///< Delimiter characters (if any) for the end of a comment

        protected Architecture glb;            ///< The Architecture owning the language emitter
        protected Scope curscope;      ///< The current symbol scope
        protected CastStrategy castStrategy;     ///< The strategy for emitting explicit \e case operations
        protected Emit emit;             ///< The low-level token emitter
        protected uint mods;             ///< Currently active printing modifications
        protected uint instr_comment_type;       ///< Type of instruction comments to display
        protected uint head_comment_type;        ///< Type of header comments to display
        protected namespace_strategy namespc_strategy;	///< How should namespace tokens be displayed
#if CPUI_DEBUG
        protected bool isStackEmpty() => (nodepend.empty()&& revpol.empty());	///< Return \b true if the RPN stack is empty

        protected bool isModStackEmpty() => modstack.empty(); ///< Return \b true if the printing modification stack is empty
#endif
        
        // Routines that are probably consistent across languages
        protected bool isSet(uint m) => ((mods & m)!= 0); ///< Is the given printing modification active

        ///< Push a new symbol scope
        protected void pushScope(Scope sc)
        {
            scopestack.Add(sc);
            curscope = sc;
        }

        ///< Pop to the previous symbol scope
        protected void popScope()
        {
            scopestack.pop_back();
            if (scopestack.empty())
                curscope = (Scope*)0;
            else
                curscope = scopestack.back();
        }

        ///< Push current printing modifications to the stack
        protected void pushMod()
        {
            modstack.Add(mods);
        }

        ///< Pop to the previous printing modifications
        protected void popMod()
        {
            mods = modstack.back();
            modstack.pop_back();
        }

        ///< Activate the given printing modification
        protected void setMod(uint m)
        {
            mods |= m;
        }

        ///< Deactivate the given printing modification
        protected void unsetMod(uint m)
        {
            mods &= ~m;
        }

        ///< Push an operator token onto the RPN stack
        /// This generally will recursively push an entire expression onto the RPN stack,
        /// up to Varnode objects marked as \e explicit, and will decide token order
        /// and parenthesis placement. As the ordering gets resolved,
        /// some amount of the expression may get emitted.
        /// \param tok is the operator token to push
        /// \param op is the PcodeOp associated with the token
        protected void pushOp(OpToken tok, PcodeOp op)
        {
            if (pending < nodepend.size()) // Pending varnode pushes before op
                recurse();          // So we must recurse

            bool paren;
            int id;

            if (revpol.empty()) {
                paren = false;
                id = emit.openGroup();
            }
            else {
                emitOp(revpol.GetLastItem());
                paren = parentheses(tok);
                id = (paren)
                    ? emit.openParen(OPEN_PAREN)
                    : emit.openGroup();
            }
            revpol.Add(new ReversePolish() {
                tok = tok,
                visited = 0,
                paren = paren,
                op = op,
                id = id
            });
        }

        ///< Push a variable token onto the RPN stack
        /// Push a single token (an Atom) onto the RPN stack. This may trigger some amount
        /// of the RPN stack to get emitted, depending on what was pushed previously.
        /// The 'emit' routines are called, popping off as much as possible.
        /// \param atom is the token to be pushed
        protected void pushAtom(Atom atom)
        {
            if (pending < nodepend.size()) // pending varnodes before atom
                recurse();          // So we must recurse

            if (revpol.empty())
                emitAtom(atom);
            else
            {
                emitOp(revpol.back());
                emitAtom(atom);
                do
                {
                    revpol.back().visited += 1;
                    if (revpol.back().visited == revpol.back().tok.stage)
                    {
                        emitOp(revpol.back());
                        if (revpol.back().paren)
                            emit.closeParen(CLOSE_PAREN, revpol.back().id);
                        else
                            emit.closeGroup(revpol.back().id);
                        revpol.pop_back();
                    }
                    else
                        break;
                } while (!revpol.empty());
            }
        }

        ///< Push an expression rooted at a Varnode onto the RPN stack
        /// For a given implied Varnode, the entire expression producing it is
        /// recursively pushed onto the RPN stack.
        ///
        /// When calling this method multiple times to push Varnode inputs for a
        /// single p-code op, the inputs must be pushed in reverse order.
        /// \param vn is the given implied Varnode
        /// \param op is PcodeOp taking the Varnode as input
        /// \param m is the set of printing modifications to apply for this sub-expression
        protected void pushVn(Varnode vn, PcodeOp op, uint m)
        {
            //   if (pending == nodepend.size())
            //     nodepend.push_back(NodePending(vn,op,m));
            //   else {
            //     nodepend.push_back(NodePending());
            //     for(i=vnvec.size()-1;i>pending;--i)
            //       nodepend[i] = nodepend[i-1];
            //     nodepend[pending] = NodePending(vn,op,m);
            //   }

            // But it is more efficient to just call them in reverse order
            nodepend.Add(new NodePending(vn, op, m));
        }

        ///< Push an explicit variable onto the RPN stack
        /// This method pushes a given Varnode as a \b leaf of the current expression.
        /// It decides how the Varnode should get emitted, as a symbol, constant, etc.,
        /// and then pushes the resulting leaf Atom onto the stack.
        /// \param vn is the given explicit Varnode
        /// \param op is the PcodeOp incorporating the Varnode into the current expression
        protected void pushVnExplicit(Varnode vn, PcodeOp op)
        {
            if (vn.isAnnotation()) {
                pushAnnotation(vn, op);
                return;
            }
            if (vn.isConstant()) {
                pushConstant(vn.getOffset(), vn.getHighTypeReadFacing(op), vn, op);
                return;
            }
            pushSymbolDetail(vn, op, true);
        }

        ///< Push symbol name with adornments matching given Varnode
        /// We know that the given Varnode matches part of a single Symbol.
        /// Push a set of tokens that represents the Varnode, which may require
        /// extracting subfields or casting to get the correct value.
        /// \param vn is the given Varnode
        /// \param op is the PcodeOp involved in the expression with the Varnode
        /// \param isRead is \b true if the PcodeOp reads the Varnode
        protected void pushSymbolDetail(Varnode vn, PcodeOp op, bool isRead)
        {
            HighVariable high = vn.getHigh();
            Symbol sym = high.getSymbol();
            if (sym == (Symbol)null) {
                pushUnnamedLocation(high.getNameRepresentative().getAddr(), vn, op);
            }
            else {
                int symboloff = high.getSymbolOffset();
                if (symboloff == -1) {
                    if (!sym.getType().needsResolution()) {
                        pushSymbol(sym, vn, op);
                        return;
                    }
                    symboloff = 0;
                }
                if (symboloff + vn.getSize() <= sym.getType().getSize()) {
                    int inslot = isRead ? op.getSlot(vn) : -1;
                    pushPartialSymbol(sym, symboloff, vn.getSize(), vn, op, inslot);
                }
                else
                    pushMismatchSymbol(sym, symboloff, vn.getSize(), vn, op);
            }
        }

        ///< Determine if the given token should be emitted in its own parenthetic expression
        /// The token at the top of the stack is being emitted. Check if its input expression,
        /// ending with the given operator token, needs to be surrounded by parentheses to convey
        /// the proper meaning.
        /// \param op2 is the input token to \b this operator
        /// \return \b true if \b op2 (as input to \b this) should be parenthesized
        protected bool parentheses(OpToken op2)
        {
            ReversePolish top = revpol.back();
            OpToken topToken = top.tok;
            int stage = top.visited;
            switch (topToken.type) {
                case OpToken::space:
                case OpToken::binary:
                    if (topToken.precedence > op2.precedence) return true;
                    if (topToken.precedence < op2.precedence) return false;
                    if (topToken.associative && (topToken == op2)) return false;
                    // If operators are adjacent to each other, the
                    // operator printed first must be evaluated first
                    // In this case op2 must be evaluated first, so we
                    // check if it is printed first (in first stage of binary)
                    if ((op2.type == OpToken::postsurround) && (stage == 0)) return false;
                    return true;
                case OpToken::unary_prefix:
                    if (topToken.precedence > op2.precedence) return true;
                    if (topToken.precedence < op2.precedence) return false;
                    //    if (associative && (this == &op2)) return false;
                    if ((op2.type == OpToken::unary_prefix) || (op2.type == OpToken::presurround)) return false;
                    return true;
                case OpToken::postsurround:
                    if (stage == 1) return false;   // Inside the surround
                    if (topToken.precedence > op2.precedence) return true;
                    if (topToken.precedence < op2.precedence) return false;
                    // If the precedences are equal, we know this postsurround
                    // comes after, so op2 being first doesn't need parens
                    if ((op2.type == OpToken::postsurround) || (op2.type == OpToken::binary)) return false;
                    //    if (associative && (this == &op2)) return false;
                    return true;
                case OpToken::presurround:
                    if (stage == 0) return false;   // Inside the surround
                    if (topToken.precedence > op2.precedence) return true;
                    if (topToken.precedence < op2.precedence) return false;
                    //    if (associative && (this == &op2)) return false;
                    if ((op2.type == OpToken::unary_prefix) || (op2.type == OpToken::presurround)) return false;
                    return true;
                case OpToken::hiddenfunction:
                    if ((stage == 0) && (revpol.size() > 1)) {
                        // If there is an unresolved previous token
                        // New token is printed next to the previous token.
                        OpToken prevToken = revpol[revpol.size() - 2].tok;
                        if (prevToken.type != OpToken::binary && prevToken.type != OpToken::unary_prefix)
                            return false;
                        if (prevToken.precedence < op2.precedence) return false;
                        // If precedence is equal, make sure we don't treat two tokens as associative,
                        // i.e. we should have parentheses
                    }
                    return true;
            }

            return true;
        }

        ///< Send an operator token from the RPN to the emitter
        /// An OpToken directly from the RPN is sent to the low-level emitter,
        /// resolving any final spacing or parentheses.
        /// \param entry is the RPN entry to be emitted
        protected void emitOp(ReversePolish entry)
        {
            switch (entry.tok.type) {
                case OpToken::binary:
                    if (entry.visited != 1) return;
                    emit.spaces(entry.tok.spacing, entry.tok.bump); // Spacing around operator
                    emit.tagOp(entry.tok.print1, EmitMarkup::no_color, entry.op);
                    emit.spaces(entry.tok.spacing, entry.tok.bump);
                    break;
                case OpToken::unary_prefix:
                    if (entry.visited != 0) return;
                    emit.tagOp(entry.tok.print1, EmitMarkup::no_color, entry.op);
                    emit.spaces(entry.tok.spacing, entry.tok.bump);
                    break;
                case OpToken::postsurround:
                    if (entry.visited == 0) return;
                    if (entry.visited == 1) {
                        // Front surround token 
                        emit.spaces(entry.tok.spacing, entry.tok.bump);
                        entry.id2 = emit.openParen(entry.tok.print1);
                        emit.spaces(0, entry.tok.bump);
                    }
                    else {
                        // Back surround token
                        emit.closeParen(entry.tok.print2, entry.id2);
                    }
                    break;
                case OpToken::presurround:
                    if (entry.visited == 2) return;
                    if (entry.visited == 0) {
                        // Front surround token 
                        entry.id2 = emit.openParen(entry.tok.print1);
                    }
                    else {
                        // Back surround token
                        emit.closeParen(entry.tok.print2, entry.id2);
                        emit.spaces(entry.tok.spacing, entry.tok.bump);
                    }
                    break;
                case OpToken::space:           // Like binary but just a space between
                    if (entry.visited != 1) return;
                    emit.spaces(entry.tok.spacing, entry.tok.bump);
                    break;
                case OpToken::hiddenfunction:
                    return;         // Never directly prints anything
            }
        }

        ///< Send an variable token from the RPN to the emitter
        /// Send the given Atom to the low-level emitter, marking it up according to its type
        /// \param atom is the given Atom to emit
        protected void emitAtom(Atom atom)
        {
            switch (atom.type) {
                case syntax:
                    emit.print(atom.name, atom.highlight);
                    break;
                case vartoken:
                    emit.tagVariable(atom.name, atom.highlight, atom.ptr_second.vn, atom.op);
                    break;
                case functoken:
                    emit.tagFuncName(atom.name, atom.highlight, atom.ptr_second.fd, atom.op);
                    break;
                case optoken:
                    emit.tagOp(atom.name, atom.highlight, atom.op);
                    break;
                case typetoken:
                    emit.tagType(atom.name, atom.highlight, atom.ptr_second.ct);
                    break;
                case fieldtoken:
                    emit.tagField(atom.name, atom.highlight, atom.ptr_second.ct, atom.offset, atom.op);
                    break;
                case blanktoken:
                    break;          // Print nothing
            }
        }

        ///< Determine if the given codepoint needs to be escaped
        /// Separate unicode characters that can be clearly emitted in a source code string
        /// (letters, numbers, punctuation, symbols) from characters that are better represented
        /// in source code with an escape sequence (control characters, unusual spaces, separators,
        /// private use characters etc.
        /// \param codepoint is the given unicode codepoint to categorize.
        /// \return \b true if the codepoint needs to be escaped
        protected static bool unicodeNeedsEscape(int codepoint)
        {
            if (codepoint < 0x20) {
                // C0 Control characters
                return true;
            }
            if (codepoint < 0x7F) {
                // Printable ASCII
                switch (codepoint) {
                    case 92:            // back-slash
                    case '"':
                    case '\'':
                        return true;
                }
                return false;
            }
            if (codepoint < 0x100) {
                if (codepoint > 0xa0) {
                    // Printable codepoints  A1-FF
                    return false;
                }
                return true;        // Delete + C1 Control characters
            }
            if (codepoint >= 0x2fa20) {
                // Up to last currently defined language
                return true;
            }
            if (codepoint < 0x2000) {
                if (codepoint >= 0x180b && codepoint <= 0x180e) {
                    return true;            // Mongolian separators
                }
                if (codepoint == 0x61c) {
                    return true;            // arabic letter mark
                }
                if (codepoint == 0x1680) {
                    return true;            // ogham space mark
                }
                return false;
            }
            if (codepoint < 0x3000) {
                if (codepoint < 0x2010) {
                    return true;            // white space and separators
                }
                if (codepoint >= 0x2028 && codepoint <= 0x202f) {
                    return true;            // white space and separators
                }
                if (codepoint == 0x205f || codepoint == 0x2060) {
                    return true;            // white space and word joiner
                }
                if (codepoint >= 0x2066 && codepoint <= 0x206f) {
                    return true;            // bidirectional markers
                }
                return false;
            }
            if (codepoint < 0xe000) {
                if (codepoint == 0x3000) {
                    return true;            // ideographic space
                }
                if (codepoint >= 0xd7fc) {
                    // D7FC - D7FF are currently unassigned.
                    // D800 - DFFF are high and low surrogates, technically illegal.
                    return true;            // Treat as needing to be escaped
                }
                return false;
            }
            if (codepoint < 0xf900) {
                return true;            // private use
            }
            if (codepoint >= 0xfe00 && codepoint <= 0xfe0f) {
                return true;            // variation selectors
            }
            if (codepoint == 0xfeff) {
                return true;            // zero width non-breaking space
            }
            if (codepoint >= 0xfff0 && codepoint <= 0xffff) {
                if ((codepoint == 0xfffc || codepoint == 0xfffd))
                    return false;
                return true;            // interlinear specials
            }
            return false;
        }

        /// \brief Emit a byte buffer to the stream as unicode characters.
        ///
        /// Characters are emitted until we reach a terminator character or \b count bytes is consumed.
        /// \param s is the output stream
        /// \param buf is the byte buffer
        /// \param count is the maximum number of bytes to consume
        /// \param charsize is 1 for UTF8, 2 for UTF16, or 4 for UTF32
        /// \param bigend is \b true for a big endian encoding of UTF elements
        /// \return \b true if we reach a terminator character
        protected bool escapeCharacterData(TextWriter s, byte[] buf, int count, int charsize, bool bigend)
        {
            int i = 0;
            int skip = charsize;
            int codepoint = 0;
            while (i < count) {
                codepoint = StringManager.getCodepoint(buf + i, charsize, bigend, skip);
                if (codepoint == 0 || codepoint == -1) break;
                printUnicode(s, codepoint);
                i += skip;
            }
            return (codepoint == 0);
        }

        ///< Emit from the RPN stack as much as possible
        /// Any complete sub-expressions that are still on the RPN will get emitted.
        protected void recurse()
        {
            uint modsave = mods;
            int lastPending = pending;     // Already claimed
            pending = nodepend.size();  // Lay claim to the rest
            while (lastPending < pending) {
                Varnode vn = nodepend.back().vn;
                PcodeOp op = nodepend.back().op;
                mods = nodepend.back().vnmod;
                nodepend.pop_back();
                pending -= 1;
                if (vn.isImplied()) {
                    if (vn.hasImpliedField()) {
                        pushImpliedField(vn, op);
                    }
                    else {
                        PcodeOp defOp = vn.getDef();
                        defOp.getOpcode().push(this, defOp, op);
                    }
                }
                else
                    pushVnExplicit(vn, op);
                pending = nodepend.size();
            }
            mods = modsave;
        }

        ///< Push a binary operator onto the RPN stack
        /// Push an operator onto the stack that has a normal binary format.
        /// Both of its input expressions are also pushed.
        /// \param tok is the operator token to push
        /// \param op is the associated PcodeOp
        protected void opBinary(OpToken tok, PcodeOp op)
        {
            if (isSet(negatetoken)) {
                tok = tok.negate;
                unsetMod(negatetoken);
                if (tok == (OpToken)null)
                    throw new LowlevelError("Could not find fliptoken");
            }
            pushOp(tok, op);        // Push on reverse polish notation
                                    // implied vn's pushed on in reverse order for efficiency
                                    // see PrintLanguage::pushVnImplied
            pushVn(op.getIn(1), op, mods);
            pushVn(op.getIn(0), op, mods);
        }

        ///< Push a unary operator onto the RPN stack
        /// Push an operator onto the stack that has a normal unary format.
        /// Its input expression is also pushed.
        /// \param tok is the operator token to push
        /// \param op is the associated PcodeOp
        protected void opUnary(OpToken tok, PcodeOp op)
        {
            pushOp(tok, op);
            // implied vn's pushed on in reverse order for efficiency
            // see PrintLanguage::pushVnImplied
            pushVn(op.getIn(0), op, mods);
        }

        ///< Get the number of pending nodes yet to be put on the RPN stack
        protected int getPending() => pending;

        ///< Reset options to default for PrintLanguage
        protected void resetDefaultsInternal()
        {
            mods = 0;
            head_comment_type = Comment::header | Comment.comment_type.warningheader;
            line_commentindent = 20;
            namespc_strategy = MINIMAL_NAMESPACES;
            instr_comment_type = Comment::user2 | Comment.comment_type.warning;
        }

        /// \brief Print a single unicode character as a \e character \e constant for the high-level language
        ///
        /// For most languages, this prints the character surrounded by single quotes.
        /// \param s is the output stream
        /// \param onechar is the unicode code point of the character to print
        protected abstract void printUnicode(TextWriter s, int onechar);

        /// \brief Push a data-type name onto the RPN expression stack.
        ///
        /// The data-type is generally emitted as if for a cast.
        /// \param ct is the data-type to push
        protected abstract void pushType(Datatype ct);

        /// \brief Push a constant onto the RPN stack.
        ///
        /// The value is ultimately emitted based on its data-type and other associated mark-up
        /// \param val is the value of the constant
        /// \param ct is the data-type of the constant
        /// \param vn is the Varnode holding the constant (optional)
        /// \param op is the PcodeOp using the constant (optional)
        protected abstract void pushConstant(ulong val, Datatype ct, Varnode vn, PcodeOp op);

        /// \brief Push a constant marked up by and EquateSymbol onto the RPN stack
        ///
        /// The equate may substitute a name or force a conversion for the constant
        /// \param val is the value of the constant
        /// \param sz is the number of bytes to use for the encoding
        /// \param sym is the EquateSymbol that marks up the constant
        /// \param vn is the Varnode holding the constant (optional)
        /// \param op is the PcodeOp using the constant (optional)
        protected abstract bool pushEquate(ulong val, int sz, EquateSymbol sym, Varnode vn, PcodeOp op);

        /// \brief Push an address which is not in the normal data-flow.
        ///
        /// The given Varnode is treated as an address, which may or may not have a symbol name.
        /// \param vn is the annotation Varnode
        /// \param op is the PcodeOp which takes the annotation as input
        protected abstract void pushAnnotation(Varnode vn, PcodeOp op);

        /// \brief Push a specific Symbol onto the RPN stack
        ///
        /// \param sym is the given Symbol
        /// \param vn is the Varnode holding the Symbol value
        /// \param op is a PcodeOp associated with the Varnode
        protected abstract void pushSymbol(Symbol sym, Varnode vn, PcodeOp op);

        /// \brief Push an address as a substitute for a Symbol onto the RPN stack
        ///
        /// If there is no Symbol or other name source for an explicit variable,
        /// this method is used to print something to represent the variable based on its storage address.
        /// \param addr is the storage address
        /// \param vn is the Varnode representing the variable (if present)
        /// \param op is a PcodeOp associated with the variable
        protected abstract void pushUnnamedLocation(Address addr, Varnode vn, PcodeOp op);

        /// \brief Push a variable that represents only part of a symbol onto the RPN stack
        ///
        /// Generally \e member syntax specifying a field within a structure gets emitted.
        /// \param sym is the root Symbol
        /// \param off is the byte offset, within the Symbol, of the partial variable
        /// \param sz is the number of bytes in the partial variable
        /// \param vn is the Varnode holding the partial value
        /// \param op is a PcodeOp associate with the Varnode
        /// \param inslot is the input slot of \b vn with \b op, or -1 if \b op writes \b vn
        protected abstract void pushPartialSymbol(Symbol sym, int off, int sz, Varnode vn, PcodeOp op,
            int inslot);

        /// \brief Push an identifier for a variable that mismatches with its Symbol
        ///
        /// This happens when a Varnode overlaps, but is not contained by a Symbol.
        /// This most commonly happens when the size of a Symbol is unknown
        /// \param sym is the overlapped symbol
        /// \param off is the byte offset of the variable relative to the symbol
        /// \param sz is the size of the variable in bytes
        /// \param vn is the Varnode representing the variable
        /// \param op is a PcodeOp associated with the Varnode
        protected abstract void pushMismatchSymbol(Symbol sym, int off, int sz, Varnode vn, PcodeOp op);

        /// \brief Push the implied field of a given Varnode as an object member extraction operation
        ///
        /// If a Varnode is \e implied and has a \e union data-type, the particular read of the varnode
        /// may correspond to a particular field that needs to get printed as a token, even though the
        /// Varnode itself is printed directly.  This method pushes the field name token.
        /// \param vn is the given Varnode
        /// \param op is the particular PcodeOp reading the Varnode
        protected abstract void pushImpliedField(Varnode vn, PcodeOp op);

        ///< Emit a comment line
        /// The comment will get emitted as a single line using the high-level language's
        /// delimiters with the given indent level
        /// \param indent is the number of characters to indent
        /// \param comm is the Comment object containing the character data and associated markup info
        protected virtual void emitLineComment(int indent, Comment comm)
        {
            string text = comm.getText();
            AddrSpace spc = comm.getAddr().getSpace();
            ulong off = comm.getAddr().getOffset();
            if (indent < 0)
                indent = line_commentindent; // User specified default indent
            emit.tagLine(indent);
            int id = emit.startComment();
            // The comment delimeters should not be printed as
            // comment tags, so that they won't get filled
            emit.tagComment(commentstart, EmitMarkup::comment_color,
                      spc, off);
            int pos = 0;
            while (pos < text.size()) {
                char tok = text[pos++];
                if ((tok == ' ') || (tok == '\t')) {
                    int4 count = 1;
                    while (pos < text.size()) {
                        tok = text[pos];
                        if ((tok != ' ') && (tok != '\t')) break;
                        count += 1;
                        pos += 1;
                    }
                    emit.spaces(count);
                }
                else if (tok == '\n')
                    emit.tagLine();
                else if (tok == '\r') {
                }
                else if (tok == '{' && pos < text.size() && text[pos] == '@') {
                    // Comment annotation
                    int4 count = 1;
                    while (pos < text.size()) {
                        tok = text[pos];
                        count += 1;
                        pos += 1;
                        if (tok == '}') break;  // Search for brace ending the annotation
                    }
                    // Treat annotation as one token
                    string annote = text.Substring(pos - count, count);
                    emit.tagComment(annote, EmitMarkup::comment_color, spc, off);
                }
                else
                {
                    int4 count = 1;
                    while (pos < text.size())
                    {
                        tok = text[pos];
                        if (isspace(tok)) break;
                        count += 1;
                        pos += 1;
                    }
                    string sub = text.Substring(pos - count, count);
                    emit.tagComment(sub, EmitMarkup::comment_color, spc, off);
                }
            }
            if (commentend.size() != 0)
                emit.tagComment(commentend, EmitMarkup::comment_color, spc, off);
            emit.stopComment(id);
            comm.setEmitted(true);
        }

        /// \brief Emit a variable declaration
        ///
        /// This can be part of a full a statement, or just the declaration of a function parameter
        /// \param sym is the Symbol to be declared
        protected abstract void emitVarDecl(Symbol sym);

        /// \brief Emit a variable declaration statement
        ///
        /// \param sym is the Symbol to be declared
        protected abstract void emitVarDeclStatement(Symbol sym);

        /// \brief Emit all the variable declarations for a given scope
        ///
        /// A subset of all variables can be declared by specifying a category,
        /// 0 for parameters, -1 for everything.
        /// \param symScope is the given Scope
        /// \param cat is the category of variable to declare
        protected abstract bool emitScopeVarDecls(Scope symScope, int cat);

        /// \brief Emit a full expression
        ///
        /// This can be an assignment statement, if the given PcodeOp has an output Varnode,
        /// or it can be a statement with no left-hand side.
        /// \param op is the given PcodeOp performing the final operation of the expression
        protected abstract void emitExpression(PcodeOp op);

        /// \brief Emit a function declaration
        ///
        /// This prints the formal defining prototype for a function.
        /// \param fd is the Funcdata object representing the function to be emitted
        protected abstract void emitFunctionDeclaration(Funcdata fd);

        /// \brief Check whether a given boolean Varnode can be printed in negated form.
        ///
        /// In many situations a boolean value can be inverted by flipping the operator
        /// token producing it to a complementary token.
        /// \param vn is the given boolean Varnode
        /// \return \b true if the value can be easily inverted
        protected abstract bool checkPrintNegation(Varnode vn);

        /// \param g is the Architecture that owns and will use this PrintLanguage
        /// \param nm is the formal name of the language
        public PrintLanguage(Architecture g, string nm)
        {
            glb = g;
            castStrategy = (CastStrategy)null;
            name = nm;
            curscope = (Scope)null;
            emit = new EmitPrettyPrint();

            pending = 0;
            resetDefaultsInternal();
        }

        ~PrintLanguage()
        {
            delete emit;
            if (castStrategy != (CastStrategy)null)
                delete castStrategy;
        }

        ///< Get the language name
        public string getName() => name;

        ///< Get the casting strategy for the language
        public CastStrategy getCastStrategy() => castStrategy;

        ///< Get the output stream being emitted to
        public TextWriter getOutputStream() => emit.getOutputStream();

        ///< Set the output stream to emit to
        public void setOutputStream(TextWriter t)
        {
            emit.setOutputStream(t);
        }

        ///< Set the maximum number of characters per line
        public void setMaxLineSize(int mls)
        {
            emit.setMaxLineSize(mls);
        }

        ///< Set the number of characters to indent per level of code nesting
        public void setIndentIncrement(int inc)
        {
            emit.setIndentIncrement(inc);
        }

        ///< Set the number of characters to indent comment lines
        /// \param val is the number of characters
        public void setLineCommentIndent(int val)
        {
            if ((val < 0) || (val >= emit.getMaxLineSize()))
                throw new LowlevelError("Bad comment indent value");
            line_commentindent = val;
        }

        ///< Establish comment delimiters for the language
        /// By default, comments are indicated in the high-level language by preceding
        /// them with a specific sequence of delimiter characters, and optionally
        /// by ending the comment with another set of delimiter characters.
        /// \param start is the initial sequence of characters delimiting a comment
        /// \param stop if not empty is the sequence delimiting the end of the comment
        /// \param usecommentfill is \b true if the delimiter needs to be emitted after every line break
        public void setCommentDelimeter(string start, string stop, bool usecommentfill)
        {
            commentstart = start;
            commentend = stop;
            if (usecommentfill)
                emit.setCommentFill(start);
            else {
                string spaces;
                for (int4 i = 0; i < start.size(); ++i)
                    spaces += ' ';
                emit.setCommentFill(spaces);
            }
        }

        ///< Get the type of comments suitable within the body of a function
        public uint getInstructionComment() => instr_comment_type;

        ///< Set the type of comments suitable within the body of a function
        public void setInstructionComment(uint val)
        {
            instr_comment_type = val;
        }

        ///< Set how namespace tokens are displayed
        public void setNamespaceStrategy(namespace_strategy strat)
        {
            namespc_strategy = strat;
        }

        ///< Get the type of comments suitable for a function header
        public uint getHeaderComment() => head_comment_type;

        ///< Set the type of comments suitable for a function header
        public void setHeaderComment(uint val)
        {
            head_comment_type = val;
        }

        ///< Does the low-level emitter, emit markup
        public bool emitsMarkup() => emit.emitsMarkup();

        ///< Set whether the low-level emitter, emits markup
        /// Tell the emitter whether to emit just the raw tokens or if additional mark-up should be provided.
        /// \param val is \b true for additional mark-up
        public void setMarkup(bool val)
        {
            ((EmitPrettyPrint)emit).setMarkup(val);
        }

        ///< Set whether nesting code structure should be emitted
        /// Emitting formal code structuring can be turned off, causing all control-flow
        /// to be represented as \e goto statements and \e labels.
        /// \param val is \b true if no code structuring should be emitted
        public void setFlat(bool val)
        {
            if (val)
                mods |= flat;
            else
                mods &= ~flat;
        }

        ///< Initialize architecture specific aspects of printer
        public abstract void initializeFromArchitecture();

        ///< Set basic data-type information for p-code operators
        public abstract void adjustTypeOperators();

        ///< Set printing options to their default value
        public virtual void resetDefaults()
        {
            emit.resetDefaults();
            resetDefaultsInternal();
        }

        ///< Clear the RPN stack and the low-level emitter
        public virtual void clear()
        {
            emit.clear();
            if (!modstack.empty()) {
                mods = modstack.front();
                modstack.Clear();
            }
            scopestack.Clear();
            curscope = (Scope)null;
            revpol.Clear();
            pending = 0;

            nodepend.Clear();
        }

        ///< Set the default integer format
        /// This determines how integers are displayed by default. Possible
        /// values are "hex" and "dec" to force a given format, or "best" can
        /// be used to let the decompiler select what it thinks best for each individual integer.
        /// \param nm is "hex", "dec", or "best"
        public virtual void setIntegerFormat(string nm)
        {
            uint mod;
            if (nm.compare(0, 3, "hex") == 0)
                mod = force_hex;
            else if (nm.compare(0, 3, "dec") == 0)
                mod = force_dec;
            else if (nm.compare(0, 4, "best") == 0)
                mod = 0;
            else
                throw new LowlevelError("Unknown integer format option: " + nm);
            mods &= ~((uint4)(force_hex | force_dec)); // Turn off any pre-existing force
            mods |= mod;            // Set any new force
        }

        /// \brief Set the way comments are displayed in decompiler output
        /// This method can either be provided a formal name or a \e sample of the initial delimiter,
        /// then it will choose from among the schemes it knows
        /// \param nm is the configuration description
        public abstract void setCommentStyle(string nm);

        /// \brief Emit definitions of data-types
        /// \param typegrp is the container for the data-types that should be defined
        public abstract void docTypeDefinitions(TypeFactory typegrp);

        /// \brief Emit declarations of global variables
        public abstract void docAllGlobals();

        /// \brief Emit the declaration for a single (global) Symbol
        /// \param sym is the Symbol to declare
        public abstract void docSingleGlobal(Symbol sym);

        /// \brief Emit the declaration (and body) of a function
        /// \param fd is the function to emit
        public abstract void docFunction(Funcdata fd);

        ///< Emit statements in a basic block
        public abstract void emitBlockBasic(BlockBasic bb);

        ///< Emit (an unspecified) list of blocks
        public abstract void emitBlockGraph(BlockGraph bl);

        ///< Emit a basic block (with any labels)
        public abstract void emitBlockCopy(BlockCopy bl);

        ///< Emit a block ending with a goto statement
        public abstract void emitBlockGoto(BlockGoto bl);

        ///< Emit a sequence of blocks
        public abstract void emitBlockLs(BlockList bl);

        ///< Emit a conditional statement
        public abstract void emitBlockCondition(BlockCondition bl);

        ///< Emit an if/else style construct
        public abstract void emitBlockIf(BlockIf bl);

        ///< Emit a loop structure, check at top
        public abstract void emitBlockWhileDo(BlockWhileDo bl);

        ///< Emit a loop structure, check at bottom
        public abstract void emitBlockDoWhile(BlockDoWhile bl);

        ///< Emit an infinite loop structure
        public abstract void emitBlockInfLoop(BlockInfLoop bl);

        ///< Emit a switch structure
        public abstract void emitBlockSwitch(BlockSwitch bl);

        ///< Emit a COPY operator
        public abstract void opCopy(PcodeOp op);

        ///< Emit a LOAD operator
        public abstract void opLoad(PcodeOp op);

        ///< Emit a STORE operator
        public abstract void opStore(PcodeOp op);

        ///< Emit a BRANCH operator
        public abstract void opBranch(PcodeOp op);

        ///< Emit a CBRANCH operator
        public abstract void opCbranch(PcodeOp op);

        ///< Emit a BRANCHIND operator
        public abstract void opBranchind(PcodeOp op);

        ///< Emit a CALL operator
        public abstract void opCall(PcodeOp op);

        ///< Emit a CALLIND operator
        public abstract void opCallind(PcodeOp op);

        ///< Emit a CALLOTHER operator
        public abstract void opCallother(PcodeOp op);

        ///< Emit an operator constructing an object
        public abstract void opConstructor(PcodeOp op, bool withNew);

        ///< Emit a RETURN operator
        public abstract void opReturn(PcodeOp op);

        ///< Emit a INT_EQUAL operator
        public abstract void opIntEqual(PcodeOp op);

        ///< Emit a INT_NOTEQUAL operator
        public abstract void opIntNotEqual(PcodeOp op);

        ///< Emit a INT_SLESS operator
        public abstract void opIntSless(PcodeOp op);

        ///< Emit a INT_SLESSEQUAL operator
        public abstract void opIntSlessEqual(PcodeOp op);

        ///< Emit a INT_LESS operator
        public abstract void opIntLess(PcodeOp op);

        ///< Emit a INT_LESSEQUAL operator
        public abstract void opIntLessEqual(PcodeOp op);

        ///< Emit a INT_ZEXT operator
        public abstract void opIntZext(PcodeOp op, PcodeOp readOp);

        ///< Emit a INT_SEXT operator
        public abstract void opIntSext(PcodeOp op, PcodeOp readOp);

        ///< Emit a INT_ADD operator
        public abstract void opIntAdd(PcodeOp op);

        ///< Emit a INT_SUB operator
        public abstract void opIntSub(PcodeOp op);

        ///< Emit a INT_CARRY operator
        public abstract void opIntCarry(PcodeOp op);

        ///< Emit a INT_SCARRY operator
        public abstract void opIntScarry(PcodeOp op);

        ///< Emit a INT_SBORROW operator
        public abstract void opIntSborrow(PcodeOp op);

        ///< Emit a INT_2COMP operator
        public abstract void opInt2Comp(PcodeOp op);

        ///< Emit a INT_NEGATE operator
        public abstract void opIntNegate(PcodeOp op);

        ///< Emit a INT_XOR operator
        public abstract void opIntXor(PcodeOp op);

        ///< Emit a INT_AND operator
        public abstract void opIntAnd(PcodeOp op);

        ///< Emit a INT_OR operator
        public abstract void opIntOr(PcodeOp op);

        ///< Emit a INT_LEFT operator
        public abstract void opIntLeft(PcodeOp op);

        ///< Emit a INT_RIGHT operator
        public abstract void opIntRight(PcodeOp op);

        ///< Emit a INT_SRIGHT operator
        public abstract void opIntSright(PcodeOp op);

        ///< Emit a INT_MULT operator
        public abstract void opIntMult(PcodeOp op);

        ///< Emit a INT_DIV operator
        public abstract void opIntDiv(PcodeOp op);

        ///< Emit a INT_SDIV operator
        public abstract void opIntSdiv(PcodeOp op);

        ///< Emit a INT_REM operator
        public abstract void opIntRem(PcodeOp op);

        ///< Emit a INT_SREM operator
        public abstract void opIntSrem(PcodeOp op);

        ///< Emit a BOOL_NEGATE operator
        public abstract void opBoolNegate(PcodeOp op);

        ///< Emit a BOOL_XOR operator
        public abstract void opBoolXor(PcodeOp op);

        ///< Emit a BOOL_AND operator
        public abstract void opBoolAnd(PcodeOp op);

        ///< Emit a BOOL_OR operator
        public abstract void opBoolOr(PcodeOp op);

        ///< Emit a FLOAT_EQUAL operator
        public abstract void opFloatEqual(PcodeOp op);

        ///< Emit a FLOAT_NOTEQUAL operator
        public abstract void opFloatNotEqual(PcodeOp op);

        ///< Emit a FLOAT_LESS operator
        public abstract void opFloatLess(PcodeOp op);

        ///< Emit a FLOAT_LESSEQUAL operator
        public abstract void opFloatLessEqual(PcodeOp op);

        ///< Emit a FLOAT_NAN operator
        public abstract void opFloatNan(PcodeOp op);

        ///< Emit a FLOAT_ADD operator
        public abstract void opFloatAdd(PcodeOp op);

        ///< Emit a FLOAT_DIV operator
        public abstract void opFloatDiv(PcodeOp op);

        ///< Emit a FLOAT_MULT operator
        public abstract void opFloatMult(PcodeOp op);

        ///< Emit a FLOAT_SUB operator
        public abstract void opFloatSub(PcodeOp op);

        ///< Emit a FLOAT_NEG operator
        public abstract void opFloatNeg(PcodeOp op);

        ///< Emit a FLOAT_ABS operator
        public abstract void opFloatAbs(PcodeOp op);

        ///< Emit a FLOAT_SQRT operator
        public abstract void opFloatSqrt(PcodeOp op);

        ///< Emit a FLOAT_INT2FLOAT operator
        public abstract void opFloatInt2Float(PcodeOp op);

        ///< Emit a FLOAT_FLOAT2FLOAT operator
        public abstract void opFloatFloat2Float(PcodeOp op);

        ///< Emit a FLOAT_TRUNC operator
        public abstract void opFloatTrunc(PcodeOp op);

        ///< Emit a FLOAT_CEIL operator
        public abstract void opFloatCeil(PcodeOp op);

        ///< Emit a FLOAT_FLOOR operator
        public abstract void opFloatFloor(PcodeOp op);

        ///< Emit a FLOAT_ROUND operator
        public abstract void opFloatRound(PcodeOp op);

        ///< Emit a MULTIEQUAL operator
        public abstract void opMultiequal(PcodeOp op);

        ///< Emit a INDIRECT operator
        public abstract void opIndirect(PcodeOp op);

        ///< Emit a PIECE operator
        public abstract void opPiece(PcodeOp op);

        ///< Emit a SUBPIECE operator
        public abstract void opSubpiece(PcodeOp op);

        ///< Emit a CAST operator
        public abstract void opCast(PcodeOp op);

        ///< Emit a PTRADD operator
        public abstract void opPtradd(PcodeOp op);

        ///< Emit a PTRSUB operator
        public abstract void opPtrsub(PcodeOp op);

        ///< Emit a SEGMENTOP operator
        public abstract void opSegmentOp(PcodeOp op);

        ///< Emit a CPOOLREF operator
        public abstract void opCpoolRefOp(PcodeOp op);

        ///< Emit a NEW operator
        public abstract void opNewOp(PcodeOp op);

        ///< Emit an INSERT operator
        public abstract void opInsertOp(PcodeOp op);

        ///< Emit an EXTRACT operator
        public abstract void opExtractOp(PcodeOp op);

        ///< Emit a POPCOUNT operator
        public abstract void opPopcountOp(PcodeOp op);

        ///< Emit a LZCOUNT operator
        public abstract void opLzcountOp(PcodeOp op);

        ///< Generate an artificial field name
        /// This is used if a value is extracted from a structured data-type, but the natural name is not available.
        /// An artificial name is generated given just the offset into the data-type and the size in bytes.
        /// \param off is the byte offset into the data-type
        /// \param size is the number of bytes in the extracted value
        /// \return a string describing the artificial field
        public virtual string unnamedField(int off, int size) => $"_{off}_{size}_";

        ///< Determine the most natural base for an integer
        /// Count '0' and '9' digits base 10. Count '0' and 'f' digits base 16.
        /// The highest count is the preferred base.
        /// \param val is the given integer
        /// \return 10 for decimal or 16 for hexidecimal
        public static int mostNaturalBase(ulong val)
        {
            int countdec = 0;      // Count 0's and 9's

            ulong tmp = val;
            int dig, setdig;
            if (tmp == 0) return 10;
            setdig = (int)(tmp % 10);
            if ((setdig == 0) || (setdig == 9)) {
                countdec += 1;
                tmp /= 10;
                while (tmp != 0) {
                    dig = (int)(tmp % 10);
                    if (dig == setdig)
                        countdec += 1;
                    else
                        break;
                    tmp /= 10;
                }
            }
            switch (countdec) {
                case 0:
                    return 16;
                case 1:
                    if ((tmp > 1) || (setdig == 9)) return 16;
                    break;
                case 2:
                    if (tmp > 10) return 16;
                    break;
                case 3:
                case 4:
                    if (tmp > 100) return 16;
                    break;
                default:
                    if (tmp > 1000) return 16;
                    break;
            }

            int counthex = 0;      // Count 0's and f's

            tmp = val;
            setdig = (int)(tmp & 0xf);
            if ((setdig == 0) || (setdig == 0xf)) {
                counthex += 1;
                tmp >>= 4;
                while (tmp != 0) {
                    dig = (int)(tmp & 0xf);
                    if (dig == setdig)
                        counthex += 1;
                    else
                        break;
                    tmp >>= 4;
                }
            }

            return (countdec > counthex) ? 10 : 16;
        }

        ///< Print a number in binary form
        /// Print a string a '0' and '1' characters representing the given value
        /// \param s is the output stream
        /// \param val is the given value
        public static void formatBinary(TextWriter s, ulong val)
        {
            int pos = Globals.mostsigbit_set(val);
            if (pos < 0) {
                s.Write('0');
                return;
            }
            else if (pos <= 7)
                pos = 7;
            else if (pos <= 15)
                pos = 15;
            else if (pos <= 31)
                pos = 31;
            else
                pos = 63;
            ulong mask = 1;
            mask <<= pos;
            while (mask != 0) {
                s.Write(((mask & val) != 0) ? '1' : '0');
                mask >>= 1;
            }
        }
    }
}
