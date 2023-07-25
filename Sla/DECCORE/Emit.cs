using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Interface for emitting the Decompiler's formal output: source code
    ///
    /// There are two basic functions being implemented through this interface:
    ///
    /// \b Markup: allows recording of the natural grouping of the high-level tokens
    /// and directly links the nodes of the abstract syntax tree to the emitted tokens.
    ///
    /// \b Pretty \b printing: Line breaks and additional white space characters are
    /// inserted within the emitted source code to enforce a maximum number of characters
    /// per line while minimizing breaks in important groups of syntax.
    /// Where extra line breaks are necessary, additional indenting is provided to
    /// reduce the impact on readability.
    ///
    /// All printing must be surrounded by at least one \e begin and \e end tag pair:
    ///   - beginDocument endDocument surrounds a whole document of code output
    ///   - beginFunction endFunction surrounds a whole declaration and body of a function
    ///   - beginBlock endBlock surrounds control-flow elements
    ///   - beginReturnType endReturnType
    ///   - beginVarDecl endVarDecl surrounds variable declarations
    ///   - beginStatement endStatement  surrounds a single statement
    ///   - beginFuncProto endFuncProto  surrounds a function prototype declaration
    ///
    /// Additional printing groups can be specified with tag pairs:
    ///   - openParen closeParen creates a unit surrounded by parentheses and treats
    ///   - openGroup closeGroup create an arbitrary printing unit
    ///   - startIndent stopIndent prints a unit at a given indent level
    ///   - startComment stopComment delimit comments for special indenting and filling
    ///
    /// The tag* functions, emit the actual language tokens, supplying appropriate markup.
    ///   - tagVariable to print variables
    ///   - tagOp to print operators
    ///   - tagFuncName to print a function identifiers
    ///   - tagType to print data-type identifiers
    ///   - tagField to print field identifiers for structured data-types
    ///   - tagComment to print words in a comment
    ///   - tagLabel to print control-flow labels
    ///
    ///   - print is used for any other syntax
    ///   - spaces is used to print whitespace
    ///   - tagLine forces a line break
    ///   - tagLine(indent) forces a line break with an indent override
    ///
    /// For an implementation that actually does pretty printing, see EmitPrettyPrint.
    internal abstract class Emit
    {
        /// An empty string
        public const string EMPTY_STRING = string.Empty;

        /// Current indent level (in fixed width characters)
        protected int4 indentlevel;
        /// Current depth of parentheses
        protected int4 parenlevel;
        /// Change in indentlevel per level of nesting
        protected int4 indentincrement;
        /// Pending print callback
        protected PendPrint pendPrint;

        /// Set options to default values for EmitMarkup
        protected void resetDefaultsInternal()
        {
            indentincrement = 2;
        }

        /// Emit any pending print commands
        protected void emitPending();
        
        public Emit()
        {
            indentlevel = 0;
            parenlevel = 0;
            pendPrint = (PendPrint*)0;
            resetDefaultsInternal();
        }

        public enum syntax_highlight
        {
            /// Keyword in the high-level language
            keyword_color = 0,
            /// Comments
            comment_color = 1,
            /// Data-type identifiers
            type_color = 2,
            /// Function identifiers
            funcname_color = 3,
            /// Local variable identifiers
            var_color = 4,
            /// Constant values
            const_color = 5,
            /// Function parameters
            param_color = 6,
            /// Global variable identifiers
            global_color = 7,
            /// Un-highlighted
            no_color = 8,
            /// Indicates a warning or error state
            error_color = 9,
            /// A token with special/highlighted meaning
            special_color = 10
        }
        
        ~Emit()
        {
        }

        /// \brief Begin a whole document of output
        /// Inform the emitter that generation of the source code document has begun
        /// \return an id associated with the document
        public abstract int4 beginDocument();

        /// \brief End a whole document of output
        ///
        /// Inform the emitter that generation of the source code document is finished
        /// \param id is the id associated with the document (as returned by beginDocument)
        public abstract void endDocument(int4 id);

        /// \brief Begin a whole declaration and body of a function
        ///
        /// Inform the emitter that generation of a function body has begun
        /// \return an id associated with the function body
        public abstract int4 beginFunction(Funcdata fd);

        /// \brief End a whole declaration and body of a function
        ///
        /// Inform the emitter that generation of a function body has ended
        /// \param id is the id associated with the function body (as returned by beginFunction)
        public abstract void endFunction(int4 id);

        /// \brief Begin a control-flow element
        ///
        /// Inform the emitter that a new control-flow section is starting. This is a source code unit
        /// usually surrounded with curly braces '{' and '}'.
        /// \param bl is the block structure object associated with the section
        /// \return an id associated with the section
        public abstract int4 beginBlock(FlowBlock bl);

        /// \brief End a control-flow element
        ///
        /// Inform the emitter that a control-flow section is ending.
        /// \param id is the id associated with the section (as returned by beginBlock)
        public abstract void endBlock(int4 id);

        /// \brief Force a line break
        ///
        /// Tell the emitter that a new line is desired at the current indent level
        public abstract void tagLine();

        /// \brief Force a line break and indent level
        ///
        /// Tell the emitter that a new line is desired at a specific indent level. The indent level
        /// is overridden only for the line, then it returns to its previous value.
        /// \param indent is the desired indent level for the new line
        public abstract void tagLine(int4 indent);

        /// \brief Begin a return type declaration
        ///
        /// Inform the emitter that generation of a function's return type is starting.
        /// \param vn (if non-null) is the storage location for the return value
        /// \return an id associated with the return type
        public abstract int4 beginReturnTypeVarnode vn);

        /// \brief End a return type declaration
        ///
        /// Inform the emitter that generation of a function's return type is ending.
        /// \param id is the id associated with the return type (as returned by beginReturnType)
        public abstract void endReturnType(int4 id);

        /// \brief Begin a variable declaration
        ///
        /// Inform the emitter that a variable declaration has started.
        /// \param sym is the symbol being declared
        /// \return an id associated with the declaration
        public abstract int4 beginVarDecl(Symbol sym);

        /// \brief End a variable declaration
        ///
        /// Inform the emitter that a variable declaration has ended.
        /// \param id is the id associated with the declaration (as returned by beginVarDecl)
        public abstract void endVarDecl(int4 id);

        /// \brief Begin a source code statement
        ///
        /// Inform the emitter that a source code statement is beginning.
        /// \param op is the root p-code operation of the statement
        /// \return an id associated with the statement
        public abstract int4 beginStatement(PcodeOp op);

        /// \brief End a source code statement
        ///
        /// Inform the emitter that a source code statement is ending.
        /// \param id is the id associated with the statement (as returned by beginStatement)
        public abstract void endStatement(int4 id);

        /// \brief Begin a function prototype declaration
        ///
        /// Inform the emitter that a function prototype is starting.
        /// \return an id associated with the prototype
        public abstract int4 beginFuncProto();

        /// \brief End a function prototype declaration
        ///
        /// Inform the emitter that a function prototype is ending.
        /// \param id is the id associated with the prototype (as returned by beginFuncProto)
        public abstract void endFuncProto(int4 id);

        /// \brief Emit a variable token
        ///
        /// An identifier string representing the variable is output, possibly with additional markup.
        /// \param name is the character data for the identifier
        /// \param hl indicates how the identifier should be highlighted
        /// \param vn is the Varnode representing the variable within the syntax tree
        /// \param op is a p-code operation related to the use of the variable (may be null)
        public abstract void tagVariable(string name, syntax_highlight hl, Varnode vn, PcodeOp op);

        /// \brief Emit an operation token
        ///
        /// The string representing the operation as appropriate for the source language is emitted,
        /// possibly with additional markup.
        /// \param name is the character data for the emitted representation
        /// \param hl indicates how the token should be highlighted
        /// \param op is the PcodeOp object associated with the operation with the syntax tree
        public abstract void tagOp(string name, syntax_highlight hl, PcodeOp op);

        /// \brief Emit a function identifier
        ///
        /// An identifier string representing the symbol name of the function is emitted, possible
        /// with additional markup.
        /// \param name is the character data for the identifier
        /// \param hl indicates how the identifier should be highlighted
        /// \param fd is the function
        /// \param op is the CALL operation associated within the syntax tree or null for a declaration
        public abstract void tagFuncName(string name, syntax_highlight hl, Funcdata fd, PcodeOp op);

        /// \brief Emit a data-type identifier
        ///
        /// A string representing the name of a data-type, as appropriate for the source language
        /// is emitted, possibly with additional markup.
        /// \param name is the character data for the identifier
        /// \param hl indicates how the identifier should be highlighted
        /// \param ct is the data-type description object
        public abstract void tagType(string name, syntax_highlight hl, Datatype ct);

        /// \brief Emit an identifier for a field within a structured data-type
        ///
        /// A string representing an individual component of a structured data-type is emitted,
        /// possibly with additional markup.
        /// \param name is the character data for the identifier
        /// \param hl indicates how the identifier should be highlighted
        /// \param ct is the data-type associated with the field
        /// \param off is the (byte) offset of the field within its structured data-type
        /// \param op is the PcodeOp associated with the field (usually PTRSUB or SUBPIECE)
        public abstract void tagField(string name, syntax_highlight hl, Datatype ct, int4 off, PcodeOp op);

        /// \brief Emit a comment string as part of the generated source code
        ///
        /// Individual comments can be broken up and emitted using multiple calls to this method,
        /// but ultimately the comment delimiters and the body of the comment are both emitted with
        /// this method, which may provide addition markup.
        /// \param name is the character data for the comment
        /// \param hl indicates how the comment should be highlighted
        /// \param spc is the address space of the address where the comment is attached
        /// \param off is the offset of the address where the comment is attached
        public abstract void tagComment(string name, syntax_highlight hl, AddrSpace spc, uintb off);

        /// \brief Emit a code label identifier
        ///
        /// A string describing a control-flow destination, as appropriate for the source language
        /// is output, possibly with additional markup.
        /// \param name is the character data of the label
        /// \param hl indicates how the label should be highlighted
        /// \param spc is the address space of the code address being labeled
        /// \param off is the offset of the code address being labeled
        public abstract void tagLabel(string name, syntax_highlight hl, AddrSpace spc, uintb off);

        /// \brief Emit other (more unusual) syntax as part of source code generation
        ///
        /// This method is used to emit syntax not covered by the other methods, such as
        /// spaces, semi-colons, braces, and other punctuation.
        /// \param data is the character data of the syntax being emitted
        /// \param hl indicates how the syntax should be highlighted
        public abstract void print(string data, syntax_highlight hl = no_color);

        /// \brief Emit an open parenthesis
        ///
        /// This method emits the parenthesis character itself and also starts a printing unit
        /// of the source code being surrounded by the parentheses.
        /// \param paren is the open parenthesis character to emit
        /// \param id is an id to associate with the parenthesis
        /// \return an id associated with the parenthesis
        public abstract int4 openParen(string paren, int4 id = 0);

        /// \brief Emit a close parenthesis
        ///
        /// This method emits the parenthesis character itself and ends the printing unit that
        /// was started by the matching open parenthesis.
        /// \param paren is the close parenthesis character to emit
        /// \param id is the id associated with the matching open parenthesis (as returned by openParen)
        public abstract void closeParen(string paren, int4 id);

        /// \brief Start a group of things that are printed together
        ///
        /// Inform the emitter that a new printing group is starting.
        /// \return an id associated with the group
        public virtual int4 openGroup()
        {
            return 0;
        }

        /// \brief End a group of things that are printed together
        ///
        /// Inform the emitter that a printing group is ending.
        /// \param id is the id associated with the group (as returned by openGroup)
        public virtual void closeGroup(int4 id)
        {
        }

        /// Reset the emitter to its initial state
        public virtual void clear()
        {
            parenlevel = 0;
            indentlevel = 0;
            pendPrint = (PendPrint*)0;
        }

        /// Set the output stream for the emitter
        public abstract void setOutputStream(TextWriter t);

        /// Get the current output stream
        public abstract TextWriter getOutputStream();

        /// \brief Emit a sequence of space characters as part of source code
        ///
        /// \param num is the number of space characters to emit
        /// \param bump is the number of characters to indent if the spaces force a line break
        public virtual void spaces(int4 num, int4 bump = 0)
        {
            static const string spacearray[] = { "", " ", "  ", "   ", "    ", "     ", "      ", "       ",
      "        ", "         ", "          " };
            if (num <= 10)
                print(spacearray[num]);
            else
            {
                string spc;
                for (int4 i = 0; i < num; ++i)
                    spc += ' ';
                print(spc);
            }
        }

        /// \brief Start a new indent level
        ///
        /// Inform the emitter that one level of nesting is being added.
        /// \return an id associated with the nesting
        public virtual int4 startIndent()
        {
            indentlevel += indentincrement;
            return 0;
        }

        /// \brief End an indent level
        ///
        /// Inform the emitter that the current nesting has ended, and we are returning to the
        /// previous level.
        /// \param id is the id associated with the nesting (as returned by startIndent)
        public virtual void stopIndent(int4 id)
        {
            indentlevel -= indentincrement;
        }

        /// \brief Start a comment block within the emitted source code
        ///
        /// Inform the emitter that a set of comment tokens/lines is starting.
        /// \return an id associated with the comment block
        public virtual int4 startComment()
        {
            return 0;
        }

        /// \brief End a comment block
        ///
        /// Inform the emitter that a set of comment tokens/lines is ending.
        /// \param id is the id associated with the block (as returned by startComment)
        public virtual void stopComment(int4 id)
        {
        }

        /// \brief Flush any remaining character data
        ///
        /// Depending on the particular emitter, tokens and syntax that have been submitted
        /// to the emitter may be held internally for a time before getting output to the
        /// final stream.  This routine makes sure submitted syntax is fully output.
        public virtual void flush()
        {
        }

        /// \brief Provide a maximum line size to the pretty printer
        ///
        /// The emitter may insert line breaks to enforce this maximum.
        /// \param mls is the number of characters to set for the maximum line size
        public virtual void setMaxLineSize(int4 mls)
        {
        }

        /// \brief Get the current maximum line size
        ///
        /// If the emitter respects a maximum line size, return that size.
        /// \return the maximum line size or -1 if the emitter does not have a maximum
        public virtual int4 getMaxLineSize()
        {
            return -1;
        }

        /// \brief Set the comment fill characters for when line breaks are forced
        ///
        /// If the pretty printer forces a line break in the middle of a comment, this
        /// string is emitted to provide proper syntax and indenting to continue the comment.
        /// \param fill is the set of fill characters
        public virtual void setCommentFill(string fill)
        {
        }

        /// \brief Determine if \b this is an XML markup emitter
        ///
        /// \return \b true if \b this produces an XML markup of its emitted source code
        public abstract bool emitsMarkup();

        /// \brief (Re)set the default emitting options
        public virtual void resetDefaults()
        {
            resetDefaultsInternal();
        }

        /// \brief Get the current parentheses depth
        ///
        /// \return the current number of open parenthetical groups
        public int4 getParenLevel() => parenlevel;

        /// \brief Get the number of characters indented per level of nesting
        ///
        /// \return the number of characters
        public int4 getIndentIncrement() => indentincrement;

        /// \brief Set the number of characters indented per level of nesting
        ///
        /// \param val is the desired number of characters to indent
        public void setIndentIncrement(int4 val)
        {
            indentincrement = val;
        }

        /// \brief Set a pending print callback
        ///
        /// The callback will be issued prior to the the next call to tagLine() unless
        /// a the method cancelPendingPrint() is called first.
        /// \param pend is the callback to be issued
        public void setPendingPrint(PendPrint pend)
        {
            pendPrint = pend;
        }

        /// \brief Cancel any pending print callback
        ///
        /// If there is any print callback pending, cancel it
        public void cancelPendingPrint()
        {
            pendPrint = (PendPrint*)0;
        }

        /// \brief Check if the given print callback is still pending
        ///
        /// \param pend is the given print callback to check
        /// \return \b true if the specific print callback is pending
        public bool hasPendingPrint(PendPrint pend) => (pendPrint == pend);
    }
}
