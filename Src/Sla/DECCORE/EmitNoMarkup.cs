using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A trivial emitter that outputs syntax straight to the stream
    ///
    /// This emitter does neither pretty printing nor markup.  It dumps any tokens
    /// straight to the final output stream.  It can be used as the low-level back-end
    /// for EmitPrettyPrint.
    internal class EmitNoMarkup : Emit
    {
        private TextWriter? s;             ///< The stream to output tokens to
        
        public EmitNoMarkup()
            : base()
        {
            s = null;
        }

        public override int beginDocument() => 0;

        public override void endDocument(int id)
        {
        }

        public override int beginFunction(Funcdata fd) => 0;

        public override void endFunction(int id)
        {
        }

        public override int beginBlock(FlowBlock bl) => 0;

        public override void endBlock(int id)
        {
        }

        public override void tagLine()
        {
            s.WriteLine();
            for (int i = indentlevel; i > 0; --i) s.Write(' ');
        }

        public override void tagLine(int indent)
        {
            s.WriteLine(); for (int i = indent; i > 0; --i) s.Write(' ');
        }

        public override int beginReturnType(Varnode vn) => 0;

        public override void endReturnType(int id)
        {
        }

        public override int beginVarDecl(Symbol sym) => 0;

        public override void endVarDecl(int id)
        {
        }

        public override int beginStatement(PcodeOp op) => 0;

        public override void endStatement(int id)
        {
        }

        public override int beginFuncProto() => 0;

        public override void endFuncProto(int id)
        {
        }

        public override void tagVariable(string name, syntax_highlight hl, Varnode vn, PcodeOp op)
        {
            s.Write(name);
        }

        public override void tagOp(string name, syntax_highlight hl, PcodeOp op)
        {
            s.Write(name);
        }

        public override void tagFuncName(string name, syntax_highlight hl, Funcdata fd, PcodeOp op)
        {
            s.Write(name);
        }

        public override void tagType(string name, syntax_highlight hl, Datatype ct)
        {
            s.Write(name);
        }

        public override void tagField(string name, syntax_highlight hl, Datatype ct, int off, PcodeOp op)
        {
            s.Write(name);
        }

        public override void tagComment(string name, syntax_highlight hl, AddrSpace spc, ulong off)
        {
            s.Write(name);
        }

        public override void tagLabel(string name, syntax_highlight hl, AddrSpace spc, ulong off)
        {
            s.Write(name);
        }

        public override void print(string data, syntax_highlight hl = syntax_highlight.no_color)
        {
            s.Write(data);
        }

        public override int openParen(string paren, int id = 0)
        {
            s.Write(paren);
            parenlevel += 1;
            return id;
        }

        public override void closeParen(string paren, int id)
        {
            s.Write(paren);
            parenlevel -= 1;
        }

        public override void setOutputStream(TextWriter t)
        {
            s = t;
        }

        public override TextWriter getOutputStream() => s;

        public override bool emitsMarkup() => false;
    }
}
