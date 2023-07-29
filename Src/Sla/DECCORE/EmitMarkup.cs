using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Emitter that associates markup with individual tokens
    ///
    /// Variable and operation tokens are associated with their corresponding Varnode or PcodeOp object in
    /// the data-flow graph of the decompiled function.
    ///
    /// Explicit descriptions of various token groups is emitted, including:
    ///  - Function prototypes
    ///  - Variable declarations
    ///  - Control-flow blocks
    ///  - Statements
    ///
    /// Tokens are emitted with syntax highlighting information.
    ///
    /// This class can be used as the low-level back-end to EmitPrettyPrint to provide a solution
    /// that does both pretty printing and markup.
    internal class EmitMarkup : Emit
    {
        /// Stream being emitted to
        protected TextWriter s;
        /// How markup is encoded to the output stream
        protected Encoder encoder;
        
        public EmitMarkup() 
            : base()
        {
            s = (ostream*)0;
            encoder = (Encoder*)0;
        }

        ~EmitMarkup()
        {
            if (encoder != (Encoder*)0)
                delete encoder;
        }

        public override int beginDocument()
        {
            encoder.openElement(ELEM_CLANG_DOCUMENT);
            return 0;
        }

        public override void endDocument(int id)
        {
            encoder.closeElement(ELEM_CLANG_DOCUMENT);
        }

        public override int beginFunction(Funcdata fd)
        {
            encoder.openElement(ELEM_FUNCTION);
            return 0;
        }

        public override void endFunction(int id)
        {
            encoder.closeElement(ELEM_FUNCTION);
        }

        public override int beginBlock(FlowBlock bl)
        {
            encoder.openElement(ELEM_BLOCK);
            encoder.writeSignedInteger(ATTRIB_BLOCKREF, bl.getIndex());
            return 0;
        }

        public override void endBlock(int id)
        {
            encoder.closeElement(ELEM_BLOCK);
        }

        public override void tagLine()
        {
            emitPending();
            encoder.openElement(ELEM_BREAK);
            encoder.writeSignedInteger(ATTRIB_INDENT, indentlevel);
            encoder.closeElement(ELEM_BREAK);
        }


        public override void tagLine(int indent)
        {
            emitPending();
            encoder.openElement(ELEM_BREAK);
            encoder.writeSignedInteger(ATTRIB_INDENT, indent);
            encoder.closeElement(ELEM_BREAK);
        }

        public override int beginReturnType(Varnode vn)
        {
            encoder.openElement(ELEM_RETURN_TYPE);
            if (vn != (Varnode*)0)
                encoder.writeUnsignedInteger(ATTRIB_VARREF, vn.getCreateIndex());
            return 0;
        }

        public override void endReturnType(int id)
        {
            encoder.closeElement(ELEM_RETURN_TYPE);
        }

        public override int beginVarDecl(Symbol sym)
        {
            encoder.openElement(ELEM_VARDECL);
            encoder.writeUnsignedInteger(ATTRIB_SYMREF, sym.getId());
            return 0;
        }

        public override void endVarDecl(int id)
        {
            encoder.closeElement(ELEM_VARDECL);
        }

        public override int beginStatement(PcodeOp op)
        {
            encoder.openElement(ELEM_STATEMENT);
            if (op != (PcodeOp*)0)
                encoder.writeUnsignedInteger(ATTRIB_OPREF, op.getTime());
            return 0;
        }

        public override void endStatement(int id)
        {
            encoder.closeElement(ELEM_STATEMENT);
        }

        public override int beginFuncProto()
        {
            encoder.openElement(ELEM_FUNCPROTO);
            return 0;
        }

        public override void endFuncProto(int id)
        {
            encoder.closeElement(ELEM_FUNCPROTO);
        }

        public override void tagVariable(string name, syntax_highlight hl, Varnode vn, PcodeOp op)
        {
            encoder.openElement(ELEM_VARIABLE);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            if (vn != (Varnode*)0)
                encoder.writeUnsignedInteger(ATTRIB_VARREF, vn.getCreateIndex());
            if (op != (PcodeOp*)0)
                encoder.writeUnsignedInteger(ATTRIB_OPREF, op.getTime());
            encoder.writeString(ATTRIB_CONTENT, name);
            encoder.closeElement(ELEM_VARIABLE);
        }

        public override void tagOp(string name, syntax_highlight hl, PcodeOp op)
        {
            encoder.openElement(ELEM_OP);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            if (op != (PcodeOp*)0)
                encoder.writeUnsignedInteger(ATTRIB_OPREF, op.getTime());
            encoder.writeString(ATTRIB_CONTENT, name);
            encoder.closeElement(ELEM_OP);
        }

        public override void tagFuncName(string name, syntax_highlight hl, Funcdata fd, PcodeOp op)
        {
            encoder.openElement(ELEM_FUNCNAME);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            if (op != (PcodeOp*)0)
                encoder.writeUnsignedInteger(ATTRIB_OPREF, op.getTime());
            encoder.writeString(ATTRIB_CONTENT, name);
            encoder.closeElement(ELEM_FUNCNAME);
        }

        public override void tagType(string name, syntax_highlight hl, Datatype ct)
        {
            encoder.openElement(ELEM_TYPE);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            if (ct.getId() != 0)
            {
                encoder.writeUnsignedInteger(ATTRIB_ID, ct.getId());
            }
            encoder.writeString(ATTRIB_CONTENT, name);
            encoder.closeElement(ELEM_TYPE);
        }

        public override void tagField(string name, syntax_highlight hl, Datatype ct, int off, PcodeOp op)
        {
            encoder.openElement(ELEM_FIELD);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            if (ct != (Datatype*)0) {
                encoder.writeString(ATTRIB_NAME, ct.getName());
                if (ct.getId() != 0)
                {
                    encoder.writeUnsignedInteger(ATTRIB_ID, ct.getId());
                }
                encoder.writeSignedInteger(ATTRIB_OFF, o);
                if (op != (PcodeOp*)0)
                    encoder.writeUnsignedInteger(ATTRIB_OPREF, op.getTime());
            }
            encoder.writeString(ATTRIB_CONTENT, name);
            encoder.closeElement(ELEM_FIELD);
        }

        public override void tagComment(string name, syntax_highlight hl, AddrSpace spc, ulong off)
        {
            encoder.openElement(ELEM_COMMENT);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            encoder.writeSpace(ATTRIB_SPACE, spc);
            encoder.writeUnsignedInteger(ATTRIB_OFF, off);
            encoder.writeString(ATTRIB_CONTENT, name);
            encoder.closeElement(ELEM_COMMENT);
        }

        public override void tagLabel(string name, syntax_highlight hl, AddrSpace spc, ulong off)
        {
            encoder.openElement(ELEM_LABEL);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            encoder.writeSpace(ATTRIB_SPACE, spc);
            encoder.writeUnsignedInteger(ATTRIB_OFF, off);
            encoder.writeString(ATTRIB_CONTENT, name);
            encoder.closeElement(ELEM_LABEL);
        }

        public override void print(string data, syntax_highlight hl = no_color)
        {
            encoder.openElement(ELEM_SYNTAX);
            if (hl != no_color)
                encoder.writeUnsignedInteger(ATTRIB_COLOR, hl);
            encoder.writeString(ATTRIB_CONTENT, data);
            encoder.closeElement(ELEM_SYNTAX);
        }

        public override int openParen(string paren, int id = 0)
        {
            encoder.openElement(ELEM_SYNTAX);
            encoder.writeSignedInteger(ATTRIB_OPEN, id);
            encoder.writeString(ATTRIB_CONTENT, paren);
            encoder.closeElement(ELEM_SYNTAX);
            parenlevel += 1;
            return 0;
        }

        public override void closeParen(string paren,int id)
        {
            encoder.openElement(ELEM_SYNTAX);
            encoder.writeSignedInteger(ATTRIB_CLOSE, id);
            encoder.writeString(ATTRIB_CONTENT, paren);
            encoder.closeElement(ELEM_SYNTAX);
            parenlevel -= 1;
        }

        public override void setOutputStream(TextWriter t)
        {
            if (encoder != (Encoder*)0)
                delete encoder;
            s = t;
            encoder = new PackedEncode(*s);
        }

        public override TexWriter getOutputStream() => s;

        public override bool emitsMarkup() => true;
    }
}
