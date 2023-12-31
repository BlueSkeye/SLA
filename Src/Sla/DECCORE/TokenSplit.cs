﻿using Sla.CORE;
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
        private EmitMarkup.syntax_highlight hl;
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
            tagtype = tag_type.docu_b;
            delimtype = printclass.begin;
            size = 0;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end document" command
        ///
        /// \param id is the id associated with the document (as returned by beginDocument)
        public void endDocument(int id)
        {
            tagtype = tag_type.docu_e;
            delimtype = printclass.end;
            size = 0;
            count = id;
        }

        /// \brief Create a "begin function body" command
        ///
        /// \return an id associated with the function body
        public int beginFunction(Funcdata f)
        {
            tagtype = tag_type.func_b;
            delimtype = printclass.begin;
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
            tagtype = tag_type.func_e;
            delimtype = printclass.end;
            size = 0;
            count = id;
        }

        /// \brief Create a "begin control-flow element" command
        ///
        /// \param b is the block structure object associated with the section
        /// \return an id associated with the section
        public int beginBlock(FlowBlock b)
        {
            tagtype = tag_type.bloc_b;
            delimtype = printclass.ignore;
            ptr_second.bl = b;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end control-flow element" command
        ///
        /// \param id is the id associated with the section (as returned by beginBlock)
        public void endBlock(int id)
        {
            tagtype = tag_type.bloc_e;
            delimtype = printclass.ignore;
            count = id;
        }

        /// \brief Create a "begin return type declaration" command
        ///
        /// \param v (if non-null) is the storage location for the return value
        /// \return an id associated with the return type
        public int beginReturnType(Varnode v)
        {
            tagtype = tag_type.rtyp_b;
            delimtype = printclass.begin;
            ptr_second.vn = v;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end return type declaration" command
        ///
        /// \param id is the id associated with the return type (as returned by beginReturnType)
        public void endReturnType(int id)
        {
            tagtype = tag_type.rtyp_e;
            delimtype = printclass.end;
            count = id;
        }

        /// \brief Create a "begin variable declaration" command
        ///
        /// \param sym is the symbol being declared
        /// \return an id associated with the declaration
        public int beginVarDecl(Symbol sym)
        {
            tagtype = tag_type.vard_b;
            delimtype = printclass.begin;
            ptr_second.symbol = sym;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end variable declaration" command
        ///
        /// \param id is the id associated with the declaration (as returned by beginVarDecl)
        public void endVarDecl(int id)
        {
            tagtype = tag_type.vard_e;
            delimtype = printclass.end;
            count = id;
        }

        /// \brief Create a "begin source code statement" command
        ///
        /// \param o is the root p-code operation of the statement
        /// \return an id associated with the statement
        public int beginStatement(PcodeOp o)
        {
            tagtype = tag_type.stat_b;
            delimtype = printclass.begin;
            op = o;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end source code statement" command
        ///
        /// \param id is the id associated with the statement (as returned by beginStatement)
        public void endStatement(int id)
        {
            tagtype = tag_type.stat_e;
            delimtype = printclass.end;
            count = id;
        }

        /// \brief Create a "begin function prototype declaration" command
        ///
        /// \return an id associated with the prototype
        public int beginFuncProto()
        {
            tagtype = tag_type.prot_b;
            delimtype = printclass.begin;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end function prototype declaration" command
        ///
        /// \param id is the id associated with the prototype (as returned by beginFuncProto)
        public void endFuncProto(int id)
        {
            tagtype = tag_type.prot_e;
            delimtype = printclass.end;
            count = id;
        }

        /// \brief Create a variable identifier token
        ///
        /// \param name is the character data for the identifier
        /// \param h indicates how the identifier should be highlighted
        /// \param v is the Varnode representing the variable within the syntax tree
        /// \param o is a p-code operation related to the use of the variable (may be null)
        public void tagVariable(string name, EmitMarkup.syntax_highlight h, Varnode v, PcodeOp o)
        {
            tok = name;
            size = tok.Length;
            tagtype = tag_type.vari_t;
            delimtype = printclass.tokenstring;
            hl = h;
            ptr_second.vn = v;
            op = o;
        }

        /// \brief Create an operator token
        ///
        /// \param name is the character data for the emitted representation
        /// \param h indicates how the token should be highlighted
        /// \param o is the PcodeOp object associated with the operation with the syntax tree
        public void tagOp(string name, EmitMarkup.syntax_highlight h, PcodeOp o)
        {
            tok = name;
            size = tok.Length;
            tagtype = tag_type.op_t;
            delimtype = printclass.tokenstring;
            hl = h;
            op = o;
        }

        /// \brief Create a function identifier token
        ///
        /// \param name is the character data for the identifier
        /// \param h indicates how the identifier should be highlighted
        /// \param f is the function
        /// \param o is the CALL operation associated within the syntax tree or null for a declaration
        public void tagFuncName(string name, EmitMarkup.syntax_highlight h, Funcdata f, PcodeOp o)
        {
            tok = name;
            size = tok.Length;
            tagtype = tag_type.fnam_t;
            delimtype = printclass.tokenstring;
            hl = h;
            ptr_second.fd = f;
            op = o;
        }

        /// \brief Create a data-type identifier token
        ///
        /// \param name is the character data for the identifier
        /// \param h indicates how the identifier should be highlighted
        /// \param ct is the data-type description object
        public void tagType(string name, EmitMarkup.syntax_highlight h, Datatype ct)
        {
            tok = name;
            size = tok.Length;
            tagtype = tag_type.type_t;
            delimtype = printclass.tokenstring;
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
        public void tagField(string name, EmitMarkup.syntax_highlight h, Datatype ct, int o, PcodeOp inOp)
        {
            tok = name;
            size = tok.Length;
            tagtype = tag_type.field_t;
            delimtype = printclass.tokenstring;
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
        public void tagComment(string name, EmitMarkup.syntax_highlight h, AddrSpace s, ulong o)
        {
            tok = name;
            size = tok.Length;
            ptr_second.spc = s;
            off = o;
            tagtype = tag_type.comm_t;
            delimtype = printclass.tokenstring;
            hl = h;
        }

        /// \brief Create a code label identifier token
        ///
        /// \param name is the character data of the label
        /// \param h indicates how the label should be highlighted
        /// \param s is the address space of the code address being labeled
        /// \param o is the offset of the code address being labeled
        public void tagLabel(string name, EmitMarkup.syntax_highlight h, AddrSpace s, ulong o)
        {
            tok = name;
            size = tok.Length;
            ptr_second.spc = s;
            off = o;
            tagtype = tag_type.label_t;
            delimtype = printclass.tokenstring;
            hl = h;
        }

        /// \brief Create a token for other (more unusual) syntax in source code
        ///
        /// \param data is the character data of the syntax being emitted
        /// \param h indicates how the syntax should be highlighted
        public void print(string data, EmitMarkup.syntax_highlight h)
        {
            tok = data;
            size = tok.Length;
            tagtype = tag_type.synt_t;
            delimtype = printclass.tokenstring;
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
            tagtype = tag_type.opar_t;
            delimtype = printclass.tokenstring;
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
            tagtype = tag_type.cpar_t;
            delimtype = printclass.tokenstring;
            count = id;
        }

        /// \brief Create a "start a printing group" command
        ///
        /// \return an id associated with the group
        public int openGroup()
        {
            tagtype = tag_type.oinv_t;
            delimtype = printclass.begin;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end a printing group" command
        ///
        /// \param id is the id associated with the group (as returned by openGroup)
        public void closeGroup(int id)
        {
            tagtype = tag_type.cinv_t;
            delimtype = printclass.end;
            count = id;
        }

        /// \brief Create a "start a new indent level" command
        ///
        /// \param bump the number of additional characters to indent
        /// \return an id associated with the nesting
        public int startIndent(int bump)
        {
            tagtype = tag_type.bump_t;
            delimtype = printclass.begin_indent;
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
            tagtype = tag_type.bump_t;
            delimtype = printclass.end_indent;
            size = 0;
            count = id;
        }

        /// \brief Create a "start a comment block" command
        ///
        /// \return an id associated with the comment block
        public int startComment()
        {
            tagtype = tag_type.oinv_t;
            delimtype = printclass.begin_comment;
            count = countbase++;
            return count;
        }

        /// \brief Create an "end a comment block" command
        ///
        /// \param id is the id associated with the block (as returned by startComment)
        public void stopComment(int id)
        {
            tagtype = tag_type.cinv_t;
            delimtype = printclass.end_comment;
            count = id;
        }

        /// \brief Create a whitespace token
        ///
        /// \param num is the number of space characters to emit
        /// \param bump is the number of characters to indent if the spaces force a line break
        public void spaces(int num, int bump)
        {
            tagtype = tag_type.spac_t;
            delimtype = printclass.tokenbreak;
            numspaces = num;
            indentbump = bump;
        }

        /// \brief Create a line break token
        public void tagLine()
        {
            tagtype = tag_type.bump_t;
            delimtype = printclass.tokenbreak;
            numspaces = 999999;
            indentbump = 0;
        }

        /// \brief Create a line break token with special indentation
        public void tagLine(int indent)
        {
            tagtype = tag_type.line_t;
            delimtype = printclass.tokenbreak;
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
            switch (tagtype) {
                case tag_type.docu_b:    // beginDocument
                    emit.beginDocument();
                    break;
                case tag_type.docu_e:    // endDocument
                    emit.endDocument(count);
                    break;
                case tag_type.func_b:    // beginFunction
                    emit.beginFunction(ptr_second.fd);
                    break;
                case tag_type.func_e:    // endFunction
                    emit.endFunction(count);
                    break;
                case tag_type.bloc_b:    // beginBlock
                    emit.beginBlock(ptr_second.bl);
                    break;
                case tag_type.bloc_e:    // endBlock
                    emit.endBlock(count);
                    break;
                case tag_type.rtyp_b:    // beginReturnType
                    emit.beginReturnType(ptr_second.vn);
                    break;
                case tag_type.rtyp_e:    // endReturnType
                    emit.endReturnType(count);
                    break;
                case tag_type.vard_b:    // beginVarDecl
                    emit.beginVarDecl(ptr_second.symbol);
                    break;
                case tag_type.vard_e:    // endVarDecl
                    emit.endVarDecl(count);
                    break;
                case tag_type.stat_b:    // beginStatement
                    emit.beginStatement(op);
                    break;
                case tag_type.stat_e:    // endStatement
                    emit.endStatement(count);
                    break;
                case tag_type.prot_b:    // beginFuncProto
                    emit.beginFuncProto();
                    break;
                case tag_type.prot_e:    // endFuncProto
                    emit.endFuncProto(count);
                    break;
                case tag_type.vari_t:    // tagVariable
                    emit.tagVariable(tok, hl, ptr_second.vn, op);
                    break;
                case tag_type.op_t:      // tagOp
                    emit.tagOp(tok, hl, op);
                    break;
                case tag_type.fnam_t:    // tagFuncName
                    emit.tagFuncName(tok, hl, ptr_second.fd, op);
                    break;
                case tag_type.type_t:    // tagType
                    emit.tagType(tok, hl, ptr_second.ct);
                    break;
                case tag_type.field_t: // tagField
                    emit.tagField(tok, hl, ptr_second.ct, (int)off, op);
                    break;
                case tag_type.comm_t:    // tagComment
                    emit.tagComment(tok, hl, ptr_second.spc, off);
                    break;
                case tag_type.label_t:   // tagLabel
                    emit.tagLabel(tok, hl, ptr_second.spc, off);
                    break;
                case tag_type.synt_t:    // print
                    emit.print(tok, hl);
                    break;
                case tag_type.opar_t:    // openParen
                    emit.openParen(tok, count);
                    break;
                case tag_type.cpar_t:    // closeParen
                    emit.closeParen(tok, count);
                    break;
                case tag_type.oinv_t:    // Invisible open
                    break;
                case tag_type.cinv_t:    // Invisible close
                    break;
                case tag_type.spac_t:    // Spaces
                    emit.spaces(numspaces);
                    break;
                case tag_type.line_t:    // tagLine
                case tag_type.bump_t:
                    throw new LowlevelError("Should never get called");
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
          case tag_type.docu_b:	// beginDocument
            s.Write("docu_b");
            break;
          case tag_type.docu_e:	// endDocument
            s.Write("docu_e");
            break;
          case tag_type.func_b:	// beginFunction
            s.Write("func_b");
            break;
          case tag_type.func_e:	// endFunction
            s.Write("func_e");
            break;
          case tag_type.bloc_b:	// beginBlock
            s.Write("bloc_b");
            break;
          case tag_type.bloc_e:	// endBlock
            s.Write("bloc_e");
            break;
          case tag_type.rtyp_b:	// beginReturnType
            s.Write("rtyp_b");
            break;
          case tag_type.rtyp_e:	// endReturnType
            s.Write("rtyp_e");
            break;
          case tag_type.vard_b:	// beginVarDecl
            s.Write("vard_b");
            break;
          case tag_type.vard_e:	// endVarDecl
            s.Write("vard_e");
            break;
          case tag_type.stat_b:	// beginStatement
            s.Write("stat_b");
            break;
          case tag_type.stat_e:	// endStatement
            s.Write("stat_e");
            break;
          case tag_type.prot_b:	// beginFuncProto
            s.Write("prot_b");
            break;
          case tag_type.prot_e:	// endFuncProto
            s.Write("prot_e");
            break;
          case tag_type.vari_t:	// tagVariable
            s.Write("vari_t");
            break;
          case tag_type.op_t:		// tagOp
            s.Write("op_t");
            break;
          case tag_type.fnam_t:	// tagFuncName
            s.Write("fnam_t");
            break;
          case tag_type.type_t:	// tagType
            s.Write("type_t");
            break;
          case tag_type.field_t: // tagField
            s.Write("field_t");
            break;
          case tag_type.comm_t:	// tagComment
            s.Write("comm_t");
            break;
          case tag_type.label_t:	// tagLabel
            s.Write("label_t");
            break;
          case tag_type.synt_t:	// print
            s.Write("synt_t");
            break;
          case tag_type.opar_t:	// openParen
            s.Write("opar_t");
            break;
          case tag_type.cpar_t:	// closeParen
            s.Write("cpar_t");
            break;
          case tag_type.oinv_t:	// Invisible open
            s.Write("oinv_t");
            break;
          case tag_type.cinv_t:	// Invisible close
            s.Write("cinv_t");
            break;
          case tag_type.spac_t:	// Spaces
            s.Write("spac_t");
            break;
          case tag_type.line_t:	// tagLine
            s.Write("line_t");
            break;
          case tag_type.bump_t:
            s.Write("bump_t");
            break;
          }
        }
#endif
    }
}
