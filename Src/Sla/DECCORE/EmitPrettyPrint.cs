﻿using Sla.CORE;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A generic source code pretty printer
    ///
    /// This pretty printer is based on the standard Derek C. Oppen pretty printing
    /// algorithm. It allows configurable indenting, spacing, and line breaks that enhances
    /// the readability of the high-level language output. It makes use of the extra
    /// information inherent in the AST to make decisions about how to best print language
    /// statements. It attempts to abstract the main formatting elements of imperative
    /// languages:  statements, code blocks, declarations, etc., and so should be largely
    /// language independent. In this way, the main language emitter doesn't have to worry
    /// about formatting issues.
    ///
    /// This emitter encapsulates a lower-level emitter that does the final emitting to
    /// stream and may add markup.
    internal class EmitPrettyPrint : Emit
    {
#if PRETTY_DEBUG
        private List<int> checkid;
#endif
        /// The low-level emitter
        private Emit lowlevel;
        /// Space available for currently active nesting levels
        private List<int> indentstack;
        /// Space remaining in current line
        private int spaceremain;
        /// Maximum number of characters allowed in a line
        private int maxlinesize;
        /// # of characters committed from the current line
        private int leftotal;
        /// # of characters yet to be committed from the current line
        private int rightotal;
        /// \b true if break needed before next token
        private bool needbreak;
        /// \b true if in the middle of a comment
        private bool commentmode;
        /// Used to fill comments if line breaks are forced
        private string commentfill;
        /// References to current \e open and \e whitespace tokens
        private circularqueue<int> scanqueue;
        /// The full stream of tokens
        private circularqueue<TokenSplit> tokqueue;

        /// Expand the stream buffer
        /// Increase the number of tokens that can be in the queue simultaneously.
        /// This is automatically called when the buffers are full.
        /// Given a fixed maximum line size for the pretty printer, the buffer should
        /// quickly reach a size that supports the biggest possible number of cached tokens.
        /// The current token queue is preserved and references into the queue are
        /// recalculated.
        private void expand()
        {
            int max = tokqueue.getMax();
            int left = tokqueue.bottomref();
            tokqueue.expand(200);
            // Expanding puts the leftmost element at reference 0
            // So we need to adjust references
            for (int i = 0; i < max; ++i)
                scanqueue.SetRef(i, (scanqueue.@ref(i) +max - left) % max);
            // The number of elements in scanqueue is always less than
            // or equal to the number of elements in tokqueue, so
            // if we keep scanqueue and tokqueue with the same max
            // we don't need to check for scanqueue overflow
            scanqueue.expand(200);
        }

        /// Enforce whitespace for a \e start token
        /// Make sure there is whitespace after the last content token, inserting a zero-sized
        /// whitespace token if necessary, before emitting a \e start token.
        private void checkstart()
        {
            if (needbreak) {
                TokenSplit tok = tokqueue.push();
                tok.spaces(0, 0);
                scan();
            }
            needbreak = false;
        }

        /// Enforce whitespace for an \e end token
        /// Make sure there is some content either in the current print group or following the
        /// last line break, inserting an empty string token if necessary, before emitting
        /// an \e end token.
        private void checkend()
        {
            if (!needbreak) {
                TokenSplit tok = tokqueue.push();
                tok.print(EMPTY_STRING, syntax_highlight.no_color); // Add a blank string
                scan();
            }
            needbreak = true;
        }

        /// Enforce whitespace for a \e content token
        /// Make sure there is whitespace after the last content token, inserting a zero-sized
        /// whitespace token if necessary, before emitting a \e content token.
        private void checkstring()
        {
            if (needbreak) {
                TokenSplit tok = tokqueue.push();
                tok.spaces(0, 0);
                scan();
            }
            needbreak = true;
        }

        /// Enforce whitespace for a line break
        /// Make sure there is some content either in the current print group or following the
        /// last line break, inserting an empty string token if necessary, before emitting
        /// a \e line \e break token.
        private void checkbreak()
        {
            if (!needbreak) {
                TokenSplit tok = tokqueue.push();
                tok.print(EMPTY_STRING, syntax_highlight.no_color); // Add a blank string
                scan();
            }
            needbreak = false;
        }

        /// Reset indenting levels to accommodate a token that won't fit
        /// (Permanently) adjust the current set of indent levels to guarantee a minimum
        /// amount of space and issue a line break.  This disrupts currently established indenting
        /// but makes sure that at least half the line is available for the next token.
        private void overflow()
        {
            int half = maxlinesize / 2;
            for (int i = indentstack.size() - 1; i >= 0; --i) {
                if (indentstack[i] < half)
                    indentstack[i] = half;
                else
                    break;
            }
            int newspaceremain;
            if (!indentstack.empty())
                newspaceremain = indentstack.GetLastItem();
            else
                newspaceremain = maxlinesize;
            if (newspaceremain == spaceremain)
                return;     // Line breaking doesn't give us any additional space
            if (commentmode && (newspaceremain == spaceremain + commentfill.Length))
                return;     // Line breaking doesn't give us any additional space
            spaceremain = newspaceremain;
            lowlevel.tagLine(maxlinesize - spaceremain);
            if (commentmode && (commentfill.Length != 0)) {
                lowlevel.print(commentfill, EmitMarkup.syntax_highlight.comment_color);
                spaceremain -= commentfill.Length;
            }
        }

        /// Output the given token to the low-level emitter
        /// Content and markup is sent to the low-level emitter if appropriate. The
        /// \e indentlevel stack is adjusted as necessary depending on the token.
        /// \param tok is the given token to emit.
        private void print(TokenSplit tok)
        {
            int val = 0;

            switch (tok.getClass()) {
                case TokenSplit.printclass.ignore:
                    tok.print(lowlevel);    // Markup or other that doesn't use space
                    break;
                case TokenSplit.printclass.begin_indent:
                    val = indentstack.GetLastItem() - tok.getIndentBump();
                    indentstack.Add(val);
#if PRETTY_DEBUG
                    checkid.Add(tok.getCount());
#endif
                    break;
                case TokenSplit.printclass.begin_comment:
                    commentmode = true;
                    // fallthru, treat as a group begin
                    goto case TokenSplit.printclass.begin;
                case TokenSplit.printclass.begin:
                    tok.print(lowlevel);
                    indentstack.Add(spaceremain);
#if PRETTY_DEBUG
                    checkid.Add(tok.getCount());
#endif
                    break;
                case TokenSplit.printclass.end_indent:
                    if (indentstack.empty())
                        throw new LowlevelError("indent error");
#if PRETTY_DEBUG
                    if (checkid.empty() || (checkid.GetLastItem() != tok.getCount()))
                        throw new LowlevelError("mismatch1");
                    checkid.RemoveLastItem();
                    if (indentstack.empty())
                        throw new LowlevelError("Empty indent stack");
#endif
                    indentstack.RemoveLastItem();
                    break;
                case TokenSplit.printclass.end_comment:
                    commentmode = false;
                    // fallthru, treat as a group end
                    goto case TokenSplit.printclass.end;
                case TokenSplit.printclass.end:
                    tok.print(lowlevel);
#if PRETTY_DEBUG
                    if (checkid.empty() || (checkid.GetLastItem() != tok.getCount()))
                        throw new LowlevelError("mismatch2");
                    checkid.RemoveLastItem();
                    if (indentstack.empty())
                        throw new LowlevelError("indent error");
#endif
                    indentstack.RemoveLastItem();
                    break;
                case TokenSplit.printclass.tokenstring:
                    if (tok.getSize() > spaceremain)
                        overflow();
                    tok.print(lowlevel);
                    spaceremain -= tok.getSize();
                    break;
                case TokenSplit.printclass.tokenbreak:
                    if (tok.getSize() > spaceremain)
                    {
                        if (tok.getTag() == TokenSplit.tag_type.line_t) // Absolute indent
                            spaceremain = maxlinesize - tok.getIndentBump();
                        else {
                            // relative indent
                            val = indentstack.GetLastItem() - tok.getIndentBump();
                            // If creating a line break doesn't save that much
                            // don't do the line break
                            if ((tok.getNumSpaces() <= spaceremain) &&
                                (val - spaceremain < 10))
                            {
                                lowlevel.spaces(tok.getNumSpaces());
                                spaceremain -= tok.getNumSpaces();
                                return;
                            }
                            indentstack.SetLastItem(val);
                            spaceremain = val;
                        }
                        lowlevel.tagLine(maxlinesize - spaceremain);
                        if (commentmode && (commentfill.Length != 0)) {
                            lowlevel.print(commentfill, syntax_highlight.comment_color);
                            spaceremain -= commentfill.Length;
                        }
                    }
                    else {
                        lowlevel.spaces(tok.getNumSpaces());
                        spaceremain -= tok.getNumSpaces();
                    }
                    break;
            }
        }

        /// Emit tokens that have been fully committed
        /// Groups of tokens that have been fully committed are sent to the
        /// low-level emitter and purged from the queue. Delimiter tokens that open a new
        /// printing group initially have a negative size, indicating the group is uncommitted
        /// and may need additional line breaks inserted.  As the ending delimiters are scanned
        /// and/or line breaks are forced.  The negative sizes are converted to positive and the
        /// corresponding group becomes \e committed, and the constituent content is emitted
        /// by this method.
        private void advanceleft()
        {
            int l = tokqueue.bottom().getSize();
            while (l >= 0) {
                TokenSplit tok = tokqueue.bottom();
                print(tok);
                switch (tok.getClass()) {
                    case TokenSplit.printclass.tokenbreak:
                        leftotal += tok.getNumSpaces();
                        break;
                    case TokenSplit.printclass.tokenstring:
                        leftotal += l;
                        break;
                    default:
                        break;
                }
                tokqueue.popbottom();
                if (tokqueue.empty()) break;
                l = tokqueue.bottom().getSize();
            }
        }

        /// Process a new token
        /// The token is assumed to be just added and at the top of the queue.
        /// This is the heart of the pretty printing algorithm.  The new token is assigned
        /// a size, the queue of open references and line breaks is updated. The amount
        /// of space currently available and the size of printing groups are updated.
        /// If the current line is going to overflow, a decision is made where in the uncommented
        /// tokens a line break needs to be inserted and what its indent level will be. If the
        /// leftmost print group closes without needing a line break, all the content it contains
        /// is \e committed and is sent to the low-level emitter.
        private void scan()
        {
            if (tokqueue.empty())       // If we managed to overflow queue
                expand();           // Expand it
                                    // Delay creating reference until after the possible expansion
            TokenSplit tok = tokqueue.top();
            switch (tok.getClass()) {
                case TokenSplit.printclass.begin_comment:
                case TokenSplit.printclass.begin:
                    if (scanqueue.empty()) {
                        leftotal = rightotal = 1;
                    }
                    tok.setSize(-rightotal);
                    scanqueue.push(tokqueue.topref());
                    break;
                case TokenSplit.printclass.end_comment:
                case TokenSplit.printclass.end:
                    tok.setSize(0);
                    if (!scanqueue.empty()) {
                        TokenSplit @ref = tokqueue.@ref(scanqueue.pop());
                        @ref.setSize(@ref.getSize() + rightotal);
                        if ((@ref.getClass() == TokenSplit.printclass.tokenbreak) && (!scanqueue.empty())) {
                            TokenSplit ref2 = tokqueue.@ref(scanqueue.pop());
                            ref2.setSize(ref2.getSize() + rightotal);
                        }
                        if (scanqueue.empty())
                            advanceleft();
                    }
                    break;
                case TokenSplit.printclass.tokenbreak:
                    if (scanqueue.empty()) {
                        leftotal = rightotal = 1;
                    }
                    else {
                        TokenSplit @ref = tokqueue.@ref(scanqueue.top());
                        if (@ref.getClass() == TokenSplit.printclass.tokenbreak) {
                            scanqueue.pop();
                            @ref.setSize(@ref.getSize() + rightotal);
                        }
                    }
                    tok.setSize(-rightotal);
                    scanqueue.push(tokqueue.topref());
                    rightotal += tok.getNumSpaces();
                    break;
                case TokenSplit.printclass.begin_indent:
                case TokenSplit.printclass.end_indent:
                case TokenSplit.printclass.ignore:
                    tok.setSize(0);
                    break;
                case TokenSplit.printclass.tokenstring:
                    if (!scanqueue.empty()) {
                        rightotal += tok.getSize();
                        while (rightotal - leftotal > spaceremain) {
                            TokenSplit @ref = tokqueue.@ref(scanqueue.popbottom());
                            @ref.setSize(999999);
                            advanceleft();
                            if (scanqueue.empty()) break;
                        }
                    }
                    break;
            }
        }

        /// Reset the defaults
        private void resetDefaultsPrettyPrint()
        {
            setMaxLineSize(100);
        }

        /// Construct with an initial maximum line size
        public EmitPrettyPrint()
            : base()
        {
            scanqueue = new circularqueue<int>(3 * 100);
            tokqueue = new circularqueue<TokenSplit>(3 * 100);
            lowlevel = new EmitNoMarkup();  // Do not emit xml by default
            spaceremain = maxlinesize;
            needbreak = false;
            commentmode = false;
            resetDefaultsPrettyPrint();
        }

        ~EmitPrettyPrint()
        {
            // delete lowlevel;
        }

        public override int beginDocument()
        {
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.beginDocument();
            scan();
            return id;
        }

        public override void endDocument(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.endDocument(id);
            scan();
        }

        public override int beginFunction(Funcdata fd)
        {
#if PRETTY_DEBUG
            if (!tokqueue.empty())
                throw new LowlevelError("Starting with non-empty token queue");
#endif
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.beginFunction(fd);
            scan();
            return id;
        }

        public override void endFunction(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.endFunction(id);
            scan();
        }

        public override int beginBlock(FlowBlock bl)
        {
            TokenSplit tok = tokqueue.push();
            int id = tok.beginBlock(bl);
            scan();
            return id;
        }

        public override void endBlock(int id)
        {
            TokenSplit tok = tokqueue.push();
            tok.endBlock(id);
            scan();
        }

        public override void tagLine()
        {
            emitPending();
            checkbreak();
            TokenSplit tok = tokqueue.push();
            tok.tagLine();
            scan();
        }

        public override void tagLine(int indent)
        {
            emitPending();
            checkbreak();
            TokenSplit tok = tokqueue.push();
            tok.tagLine(indent);
            scan();
        }

        public override int beginReturnType(Varnode vn)
        {
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.beginReturnType(vn);
            scan();
            return id;
        }

        public override void endReturnType(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.endReturnType(id);
            scan();
        }

        public override int beginVarDecl(Symbol sym)
        {
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.beginVarDecl(sym);
            scan();
            return id;
        }

        public override void endVarDecl(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.endVarDecl(id);
            scan();
        }

        public override int beginStatement(PcodeOp op)
        {
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.beginStatement(op);
            scan();
            return id;
        }

        public override void endStatement(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.endStatement(id);
            scan();
        }

        public override int beginFuncProto()
        {
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.beginFuncProto();
            scan();
            return id;
        }

        public override void endFuncProto(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.endFuncProto(id);
            scan();
        }

        public override void tagVariable(string name, syntax_highlight hl, Varnode vn, PcodeOp op)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.tagVariable(name, hl, vn, op);
            scan();
        }

        public override void tagOp(string name, syntax_highlight hl, PcodeOp op)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.tagOp(name, hl, op);
            scan();
        }

        public override void tagFuncName(string name,syntax_highlight hl, Funcdata fd, PcodeOp op)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.tagFuncName(name, hl, fd, op);
            scan();
        }

        public override void tagType(string name,syntax_highlight hl, Datatype ct)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.tagType(name, hl, ct);
            scan();
        }

        public override void tagField(string name,syntax_highlight hl, Datatype ct, int off, PcodeOp op)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.tagField(name, hl, ct, off, op);
            scan();
        }

        public override void tagComment(string name,syntax_highlight hl, AddrSpace spc, ulong off)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.tagComment(name, hl, spc, off);
            scan();
        }

        public override void tagLabel(string name,syntax_highlight hl, AddrSpace spc, ulong off)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.tagLabel(name, hl, spc, off);
            scan();
        }

        public override void print(string data, syntax_highlight hl = syntax_highlight.no_color)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.print(data, hl);
            scan();
        }

        public override int openParen(string paren,int id = 0)
        {
            id = openGroup();          // Open paren automatically opens group
            TokenSplit tok = tokqueue.push();
            tok.openParen(paren, id);
            scan();
            needbreak = true;
            return id;
        }

        public override void closeParen(string paren,int id)
        {
            checkstring();
            TokenSplit tok = tokqueue.push();
            tok.closeParen(paren, id);
            scan();
            closeGroup(id);
        }

        public override int openGroup()
        {
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.openGroup();
            scan();
            return id;
        }

        public override void closeGroup(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.closeGroup(id);
            scan();
        }

        public override void clear()
        {
            base.clear();
            lowlevel.clear();
            indentstack.Clear();
            scanqueue.clear();
            tokqueue.clear();
            leftotal = 1;
            rightotal = 1;
            needbreak = false;
            commentmode = false;
            spaceremain = maxlinesize;
        }

        public override void setOutputStream(TextWriter t)
        {
            lowlevel.setOutputStream(t);
        }

        public override TextWriter getOutputStream() => lowlevel.getOutputStream();

        public override void spaces(int num, int bump = 0)
        {
            checkbreak();
            TokenSplit tok = tokqueue.push();
            tok.spaces(num, bump);
            scan();
        }

        public override int startIndent()
        {
            TokenSplit tok = tokqueue.push();
            int id = tok.startIndent(indentincrement);
            scan();
            return id;
        }

        public override void stopIndent(int id)
        {
            TokenSplit tok = tokqueue.push();
            tok.stopIndent(id);
            scan();
        }

        public override int startComment()
        {
            checkstart();
            TokenSplit tok = tokqueue.push();
            int id = tok.startComment();
            scan();
            return id;
        }

        public override void stopComment(int id)
        {
            checkend();
            TokenSplit tok = tokqueue.push();
            tok.stopComment(id);
            scan();
        }

        public override void flush()
        {
            while (!tokqueue.empty()) {
                TokenSplit tok = tokqueue.popbottom();
                if (tok.getSize() < 0)
                    throw new LowlevelError("Cannot flush pretty printer. Missing group end");
                print(tok);
            }
            needbreak = false;
#if PRETTY_DEBUG
            if (!scanqueue.empty())
                throw new LowlevelError("prettyprint scanqueue did not flush");
            if (!indentstack.empty())
                throw new LowlevelError("prettyprint indentstack did not flush");
#endif
            lowlevel.flush();
        }

        public override void setMaxLineSize(int val)
        {
            if ((val < 20) || (val > 10000))
                throw new LowlevelError("Bad maximum line size");
            maxlinesize = val;
            scanqueue.setMax(3 * val);
            tokqueue.setMax(3 * val);
            spaceremain = maxlinesize;
            clear();
        }

        public override int getMaxLineSize() => maxlinesize;

        public override void setCommentFill(string fill)
        {
            commentfill = fill;
        }

        public override bool emitsMarkup() => lowlevel.emitsMarkup();

        public override void resetDefaults()
        {
            lowlevel.resetDefaults();
            resetDefaultsInternal();
            resetDefaultsPrettyPrint();
        }

        /// Toggle whether the low-level emitter emits markup or not
        /// This method toggles the low-level emitter between EmitMarkup and EmitNoMarkup depending
        /// on whether markup is desired.
        /// \param val is \b true if markup is desired
        public virtual void setMarkup(bool val)
        {
            TextWriter t = lowlevel.getOutputStream();
            // delete lowlevel;
            if (val)
                lowlevel = new EmitMarkup();
            else
                lowlevel = new EmitNoMarkup();
            lowlevel.setOutputStream(t);
        }
    }
}
