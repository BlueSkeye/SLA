using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A token/command object in the pretty printing stream
    ///
    /// The pretty printing algorithm (see EmitPrettyPrint) works on the stream of
    /// tokens, constituting the content actually being output, plus additional
    /// embedded commands made up begin/end or open/close pairs that delimit the
    /// (hierarchy of) groups of tokens that should be printed as a unit. Instances
    /// of this class represent all the possible elements of this stream.
    ///
    /// All instances exhibit a broad \e printclass that generally reflects whether
    /// the token is one of the begin/end delimiters or is actual content.
    /// Instances also have a \e tag_type that indicate the specific function of the
    /// token within the stream, which mirror the begin/end/open/close/tag methods
    /// on the emitter classes (EmitMarkup).
    internal class TokenSplit
    {
        /// \brief An enumeration denoting the general class of a token
        public enum printclass
        {
            /// A token that starts a printing group
            begin,
            /// A token that ends a printing group
            end,
            /// A token representing actual content
            tokenstring,
            /// White space (where line breaks can be inserted)
            tokenbreak,
            /// Start of a new nesting level
            begin_indent,
            /// End of a nesting level
            end_indent,
            /// Start of a comment block
            begin_comment,
            /// End of a comment block
            end_comment,
            /// Mark-up that doesn't affect pretty printing
            ignore
        }

        /// \brief The exhaustive list of possible token types
        public enum tag_type
        {
            /// Start of a document
            docu_b,
            /// End of a document
            docu_e,
            /// Start of a function body
            func_b,
            /// End of a function body
            func_e,
            /// Start of a control-flow section
            bloc_b,
            /// End of a control-flow section
            bloc_e,
            /// Start of a return type declaration
            rtyp_b,
            /// End of a return type declaration
            rtyp_e,
            /// Start of a variable declaration
            vard_b,
            /// End of a variable declaration
            vard_e,
            /// Start of a statement
            stat_b,
            /// End of a statement
            stat_e,
            /// Start of a function prototype
            prot_b,
            /// End of a function prototype
            prot_e,
            /// A variable identifier
            vari_t,
            /// An operator
            op_t,
            /// A function identifier
            fnam_t,
            /// A data-type identifier
            type_t,
            /// A field name for a structured data-type
            field_t,
            /// Part of a comment block
            comm_t,
            /// A code label
            label_t,
            /// Other unspecified syntax
            synt_t,
            /// Open parenthesis
            opar_t,
            /// Close parenthesis
            cpar_t,
            /// Start of an arbitrary (invisible) grouping
            oinv_t,
            /// End of an arbitrary (invisible) grouping
            cinv_t,
            /// White space
            spac_t,
            /// Required line break
            bump_t,
            /// Required line break with one-time indent level
            line_t
        }

        /// Type of token
        private tag_type tagtype;
        /// The general class of the token
        private printclass delimtype;
        /// Characters of token (if any)
        private string tok;
        /// Highlighting for token
        private EmitMarkup::syntax_highlight hl;
        // Additional markup elements for token
        /// Pcode-op associated with \b this token
        private PcodeOp op;
        
        private /* union */ struct AdditionalMarkup
        {
            /// Associated Varnode
            internal Varnode vn;
            /// Associated Control-flow
            internal FlowBlock bl;
            /// Associated Function
            internal Funcdata fd;
            /// Associated Data-type
            internal Datatype ct;
            /// Associated Address
            internal AddrSpace spc;
            /// Associated Symbol being displayed
            internal Symbol symbol;
        }
        /// Additional markup associated with the token
        private AdditionalMarkup ptr_second;
        /// Offset associated either with address or field markup
        private ulong off;
        /// Amount to indent if a line breaks
        private int indentbump;
        /// Number of spaces in a whitespace token (\e tokenbreak)
        private int numspaces;
        /// Number of content characters or other size information
        private int size;
        /// Associated id (for matching begin/end pairs)
        private int count;
        /// Static counter for uniquely assigning begin/end pair ids.
        private static int countbase = 0;

        public TokenSplit()
        {
        }

        /// \brief Create a "begin document" command
        /// \return an id associated with the document
        public int beginDocument()
        {
            tagtype = docu_b;
            delimtype = begin;
            size = 0;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end document" command
        ///
        /// \param id is the id associated with the document (as returned by beginDocument)
        public void endDocument(int id)
        {
            tagtype = docu_e;
            delimtype = end;
            size = 0;
            count = id;
        }

        /// \brief Create a "begin function body" command
        ///
        /// \return an id associated with the function body
        public int beginFunction(Funcdata f)
        {
            tagtype = func_b;
            delimtype = begin;
            size = 0;
            ptr_second.fd = f;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end function body" command
        ///
        /// \param id is the id associated with the function body (as returned by beginFunction)
        public void endFunction(int id)
        {
            tagtype = func_e;
            delimtype = end;
            size = 0;
            count = id;
        }

        /// \brief Create a "begin control-flow element" command
        ///
        /// \param b is the block structure object associated with the section
        /// \return an id associated with the section
        public int beginBlock(FlowBlock b)
        {
            tagtype = bloc_b;
            delimtype = ignore;
            ptr_second.bl = b;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end control-flow element" command
        ///
        /// \param id is the id associated with the section (as returned by beginBlock)
        public void endBlock(int id)
        {
            tagtype = bloc_e;
            delimtype = ignore;
            count = id;
        }

        /// \brief Create a "begin return type declaration" command
        ///
        /// \param v (if non-null) is the storage location for the return value
        /// \return an id associated with the return type
        public int beginReturnType(Varnode v)
        {
            tagtype = rtyp_b;
            delimtype = begin;
            ptr_second.vn = v;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end return type declaration" command
        ///
        /// \param id is the id associated with the return type (as returned by beginReturnType)
        public void endReturnType(int id)
        {
            tagtype = rtyp_e;
            delimtype = end;
            count = id;
        }

        /// \brief Create a "begin variable declaration" command
        ///
        /// \param sym is the symbol being declared
        /// \return an id associated with the declaration
        public int beginVarDecl(Symbol sym)
        {
            tagtype = vard_b;
            delimtype = begin;
            ptr_second.symbol = sym;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end variable declaration" command
        ///
        /// \param id is the id associated with the declaration (as returned by beginVarDecl)
        public void endVarDecl(int id)
        {
            tagtype = vard_e;
            delimtype = end;
            count = id;
        }

        /// \brief Create a "begin source code statement" command
        ///
        /// \param o is the root p-code operation of the statement
        /// \return an id associated with the statement
        public int beginStatement(PcodeOp o)
        {
            tagtype = stat_b;
            delimtype = begin;
            op = o;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end source code statement" command
        ///
        /// \param id is the id associated with the statement (as returned by beginStatement)
        public void endStatement(int id)
        {
            tagtype = stat_e;
            delimtype = end;
            count = id;
        }

        /// \brief Create a "begin function prototype declaration" command
        ///
        /// \return an id associated with the prototype
        public int beginFuncProto()
        {
            tagtype = prot_b;
            delimtype = begin;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end function prototype declaration" command
        ///
        /// \param id is the id associated with the prototype (as returned by beginFuncProto)
        public void endFuncProto(int id)
        {
            tagtype = prot_e;
            delimtype = end;
            count = id;
        }

        /// \brief Create a variable identifier token
        ///
        /// \param name is the character data for the identifier
        /// \param h indicates how the identifier should be highlighted
        /// \param v is the Varnode representing the variable within the syntax tree
        /// \param o is a p-code operation related to the use of the variable (may be null)
        public void tagVariable(string name, EmitMarkup::syntax_highlight h, Varnode v, PcodeOp o)
        {
            tok = name;
            size = tok.size();
            tagtype = vari_t;
            delimtype = tokenstring;
            hl = h;
            ptr_second.vn = v;
            op = o;
        }

        /// \brief Create an operator token
        ///
        /// \param name is the character data for the emitted representation
        /// \param h indicates how the token should be highlighted
        /// \param o is the PcodeOp object associated with the operation with the syntax tree
        public void tagOp(string name, EmitMarkup::syntax_highlight h, PcodeOp o)
        {
            tok = name;
            size = tok.size();
            tagtype = op_t;
            delimtype = tokenstring;
            hl = h;
            op = o;
        }

        /// \brief Create a function identifier token
        ///
        /// \param name is the character data for the identifier
        /// \param h indicates how the identifier should be highlighted
        /// \param f is the function
        /// \param o is the CALL operation associated within the syntax tree or null for a declaration
        public void tagFuncName(string name, EmitMarkup::syntax_highlight h, Funcdata f, PcodeOp o)
        {
            tok = name;
            size = tok.size();
            tagtype = fnam_t;
            delimtype = tokenstring;
            hl = h;
            ptr_second.fd = f;
            op = o;
        }

        /// \brief Create a data-type identifier token
        ///
        /// \param name is the character data for the identifier
        /// \param h indicates how the identifier should be highlighted
        /// \param ct is the data-type description object
        public void tagType(string name, EmitMarkup::syntax_highlight h, Datatype ct)
        {
            tok = name;
            size = tok.size();
            tagtype = type_t;
            delimtype = tokenstring;
            hl = h;
            ptr_second.ct = ct;
        }

        /// \brief Create an identifier for a field within a structured data-type
        ///
        /// \param name is the character data for the identifier
        /// \param h indicates how the identifier should be highlighted
        /// \param ct is the data-type associated with the field
        /// \param o is the (byte) offset of the field within its structured data-type
        /// \param inOp is the PcodeOp associated with the field (usually PTRSUB or SUBPIECE)
        public void tagField(string name, EmitMarkup::syntax_highlight h, Datatype ct, int o, PcodeOp inOp)
        {
            tok = name;
            size = tok.size();
            tagtype = field_t;
            delimtype = tokenstring;
            hl = h;
            ptr_second.ct = ct;
            off = (ulong)o;
            op = inOp;
        }

        /// \brief Create a comment string in the generated source code
        ///
        /// \param name is the character data for the comment
        /// \param h indicates how the comment should be highlighted
        /// \param s is the address space of the address where the comment is attached
        /// \param o is the offset of the address where the comment is attached
        public void tagComment(string name, EmitMarkup::syntax_highlight h, AddrSpace s, ulong o)
        {
            tok = name;
            size = tok.size(); 
            ptr_second.spc = s;
            off = o;
            tagtype = comm_t;
            delimtype = tokenstring;
            hl = h;
        }

        /// \brief Create a code label identifier token
        ///
        /// \param name is the character data of the label
        /// \param h indicates how the label should be highlighted
        /// \param s is the address space of the code address being labeled
        /// \param o is the offset of the code address being labeled
        public void tagLabel(string name, EmitMarkup::syntax_highlight h, AddrSpace s, ulong o)
        {
            tok = name;
            size = tok.size();
            ptr_second.spc = s;
            off = o;
            tagtype = label_t;
            delimtype = tokenstring;
            hl = h;
        }

        /// \brief Create a token for other (more unusual) syntax in source code
        ///
        /// \param data is the character data of the syntax being emitted
        /// \param h indicates how the syntax should be highlighted
        public void print(string data, EmitMarkup::syntax_highlight h)
        {
            tok = data;
            size = tok.size();
            tagtype = synt_t;
            delimtype = tokenstring;
            hl = h;
        }

        /// \brief Create an open parenthesis
        ///
        /// \param paren is the open parenthesis character to emit
        /// \param id is an id to associate with the parenthesis
        public void openParen(string paren, int id)
        {
            tok = paren;
            size = 1;
            tagtype = opar_t;
            delimtype = tokenstring;
            count = id;
        }

        /// \brief Create a close parenthesis
        ///
        /// \param paren is the close parenthesis character to emit
        /// \param id is the id associated with the matching open parenthesis (as returned by openParen)
        public void closeParen(string paren, int id)
        {
            tok = paren;
            size = 1;
            tagtype = cpar_t;
            delimtype = tokenstring;
            count = id;
        }

        /// \brief Create a "start a printing group" command
        ///
        /// \return an id associated with the group
        public int openGroup()
        {
            tagtype = oinv_t;
            delimtype = begin;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end a printing group" command
        ///
        /// \param id is the id associated with the group (as returned by openGroup)
        public void closeGroup(int id)
        {
            tagtype = cinv_t;
            delimtype = end;
            count = id;
        }

        /// \brief Create a "start a new indent level" command
        ///
        /// \param bump the number of additional characters to indent
        /// \return an id associated with the nesting
        public int startIndent(int bump)
        {
            tagtype = bump_t;
            delimtype = begin_indent;
            indentbump = bump;
            size = 0;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end an indent level" command
        ///
        /// \param id is the id associated with the nesting (as returned by startIndent)
        public void stopIndent(int id)
        {
            tagtype = bump_t;
            delimtype = end_indent;
            size = 0;
            count = id;
        }

        /// \brief Create a "start a comment block" command
        ///
        /// \return an id associated with the comment block
        public int startComment()
        {
            tagtype = oinv_t;
            delimtype = begin_comment;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end a comment block" command
        ///
        /// \param id is the id associated with the block (as returned by startComment)
        public void stopComment(int id)
        {
            tagtype = cinv_t;
            delimtype = end_comment;
            count = id;
        }

        /// \brief Create a whitespace token
        ///
        /// \param num is the number of space characters to emit
        /// \param bump is the number of characters to indent if the spaces force a line break
        public void spaces(int num, int bump)
        {
            tagtype = spac_t;
            delimtype = tokenbreak;
            numspaces = num;
            indentbump = bump;
        }

        /// \brief Create a line break token
        public void tagLine()
        {
            tagtype = bump_t;
            delimtype = tokenbreak;
            numspaces = 999999;
            indentbump = 0;
        }

        /// \brief Create a line break token with special indentation
        public void tagLine(int indent)
        {
            tagtype = line_t;
            delimtype = tokenbreak;
            numspaces = 999999;
            indentbump = indent;
        }

        /// Send \b this token to emitter
        /// Emit markup or content corresponding to \b this token on a low-level emitter.
        /// The API method matching the token type is called, feeding it content contained in
        /// the object.
        /// \param emit is the low-level emitter to output to
        public void print(Emit emit)
        {
            switch (tagtype)
            {
                case docu_b:    // beginDocument
                    emit.beginDocument();
                    break;
                case docu_e:    // endDocument
                    emit.endDocument(count);
                    break;
                case func_b:    // beginFunction
                    emit.beginFunction(ptr_second.fd);
                    break;
                case func_e:    // endFunction
                    emit.endFunction(count);
                    break;
                case bloc_b:    // beginBlock
                    emit.beginBlock(ptr_second.bl);
                    break;
                case bloc_e:    // endBlock
                    emit.endBlock(count);
                    break;
                case rtyp_b:    // beginReturnType
                    emit.beginReturnType(ptr_second.vn);
                    break;
                case rtyp_e:    // endReturnType
                    emit.endReturnType(count);
                    break;
                case vard_b:    // beginVarDecl
                    emit.beginVarDecl(ptr_second.symbol);
                    break;
                case vard_e:    // endVarDecl
                    emit.endVarDecl(count);
                    break;
                case stat_b:    // beginStatement
                    emit.beginStatement(op);
                    break;
                case stat_e:    // endStatement
                    emit.endStatement(count);
                    break;
                case prot_b:    // beginFuncProto
                    emit.beginFuncProto();
                    break;
                case prot_e:    // endFuncProto
                    emit.endFuncProto(count);
                    break;
                case vari_t:    // tagVariable
                    emit.tagVariable(tok, hl, ptr_second.vn, op);
                    break;
                case op_t:      // tagOp
                    emit.tagOp(tok, hl, op);
                    break;
                case fnam_t:    // tagFuncName
                    emit.tagFuncName(tok, hl, ptr_second.fd, op);
                    break;
                case type_t:    // tagType
                    emit.tagType(tok, hl, ptr_second.ct);
                    break;
                case field_t: // tagField
                    emit.tagField(tok, hl, ptr_second.ct, (int)off, op);
                    break;
                case comm_t:    // tagComment
                    emit.tagComment(tok, hl, ptr_second.spc, off);
                    break;
                case label_t:   // tagLabel
                    emit.tagLabel(tok, hl, ptr_second.spc, off);
                    break;
                case synt_t:    // print
                    emit.print(tok, hl);
                    break;
                case opar_t:    // openParen
                    emit.openParen(tok, count);
                    break;
                case cpar_t:    // closeParen
                    emit.closeParen(tok, count);
                    break;
                case oinv_t:    // Invisible open
                    break;
                case cinv_t:    // Invisible close
                    break;
                case spac_t:    // Spaces
                    emit.spaces(numspaces);
                    break;
                case line_t:    // tagLine
                case bump_t:
                    throw new LowlevelError("Should never get called");
                    break;
            }
        }

        /// Get the extra indent after a line break
        public int getIndentBump() => indentbump;

        /// Get the number of characters of whitespace
        public int getNumSpaces() => numspaces;

        /// Get the number of content characters
        public int getSize() => size;

        /// Set the number of content characters
        public void setSize(int sz)
        {
            size = sz;
        }

        /// Get the print class of \b this
        public printclass getClass() => delimtype;

        /// Get \b this tag type
        public tag_type getTag() => tagtype;

#if PRETTY_DEBUG
        /// Get the delimiter id
        public int getCount() => count;
        
        /// Print \b this token to stream for debugging
        public void printDebug(TextWriter s)
        {
          switch(tagtype) {
          case docu_b:	// beginDocument
            s << "docu_b";
            break;
          case docu_e:	// endDocument
            s << "docu_e";
            break;
          case func_b:	// beginFunction
            s << "func_b";
            break;
          case func_e:	// endFunction
            s << "func_e";
            break;
          case bloc_b:	// beginBlock
            s << "bloc_b";
            break;
          case bloc_e:	// endBlock
            s << "bloc_e";
            break;
          case rtyp_b:	// beginReturnType
            s << "rtyp_b";
            break;
          case rtyp_e:	// endReturnType
            s << "rtyp_e";
            break;
          case vard_b:	// beginVarDecl
            s << "vard_b";
            break;
          case vard_e:	// endVarDecl
            s << "vard_e";
            break;
          case stat_b:	// beginStatement
            s << "stat_b";
            break;
          case stat_e:	// endStatement
            s << "stat_e";
            break;
          case prot_b:	// beginFuncProto
            s << "prot_b";
            break;
          case prot_e:	// endFuncProto
            s << "prot_e";
            break;
          case vari_t:	// tagVariable
            s << "vari_t";
            break;
          case op_t:		// tagOp
            s << "op_t";
            break;
          case fnam_t:	// tagFuncName
            s << "fnam_t";
            break;
          case type_t:	// tagType
            s << "type_t";
            break;
          case field_t: // tagField
            s << "field_t";
            break;
          case comm_t:	// tagComment
            s << "comm_t";
            break;
          case label_t:	// tagLabel
            s << "label_t";
            break;
          case synt_t:	// print
            s << "synt_t";
            break;
          case opar_t:	// openParen
            s << "opar_t";
            break;
          case cpar_t:	// closeParen
            s << "cpar_t";
            break;
          case oinv_t:	// Invisible open
            s << "oinv_t";
            break;
          case cinv_t:	// Invisible close
            s << "cinv_t";
            break;
          case spac_t:	// Spaces
            s << "spac_t";
            break;
          case line_t:	// tagLine
            s << "line_t";
            break;
          case bump_t:
            s << "bump_t";
            break;
          }
        }
#endif
    }
}
