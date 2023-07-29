using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ghidra.Emit;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief A trivial emitter that outputs syntax straight to the stream
    ///
    /// This emitter does neither pretty printing nor markup.  It dumps any tokens
    /// straight to the final output stream.  It can be used as the low-level back-end
    /// for EmitPrettyPrint.
    internal class EmitNoMarkup : Emit
    {
        private TextWriter s;             ///< The stream to output tokens to
        
        public EmitNoMarkup()
            : base()
        {
            s = (ostream*)0;
        }

        public override int4 beginDocument() => 0;

        public override void endDocument(int4 id)
        {
        }

        public override int4 beginFunction(Funcdata fd) => 0;

        public override void endFunction(int4 id)
        {
        }

        public override int4 beginBlock(FlowBlock bl) => 0;

        public override void endBlock(int4 id)
        {
        }

        public override void tagLine()
        {
            *s << endl; for (int4 i = indentlevel; i > 0; --i) *s << ' ';
        }

        public override void tagLine(int4 indent)
        {
            *s << endl; for (int4 i = indent; i > 0; --i) *s << ' ';
        }

        public override int4 beginReturnType(Varnode vn) => 0;

        public override void endReturnType(int4 id)
        {
        }

        public override int4 beginVarDecl(Symbol sym) => 0;

        public override void endVarDecl(int4 id)
        {
        }

        public override int4 beginStatement(PcodeOp op) => 0;

        public override void endStatement(int4 id)
        {
        }

        public override int4 beginFuncProto() => 0;

        public override void endFuncProto(int4 id)
        {
        }

        public override void tagVariable(string name, syntax_highlight hl, Varnode vn, PcodeOp op)
        {
            *s << name;
        }

        public override void tagOp(string name, syntax_highlight hl, PcodeOp op)
        {
            *s << name;
        }

        public override void tagFuncName(string name, syntax_highlight hl, Funcdata fd, PcodeOp op)
        {
            *s << name;
        }

        public override void tagType(string name, syntax_highlight hl, Datatype ct)
        {
            *s << name;
        }

        public override void tagField(string name, syntax_highlight hl, Datatype ct, int4 off, PcodeOp op)
        {
            *s << name;
        }

        public override void tagComment(string name, syntax_highlight hl, AddrSpace spc, uintb off)
        {
            *s << name;
        }

        public override void tagLabel(string name, syntax_highlight hl, AddrSpace spc, uintb off)
        {
            *s << name;
        }

        public override void print(string data, syntax_highlight hl = no_color)
        {
            *s << data;
        }

        public override int4 openParen(string paren, int4 id = 0)
        {
            *s << paren; parenlevel += 1; return id;
        }

        public override void closeParen(string paren, int4 id)
        {
            *s << paren; parenlevel -= 1;
        }

        public override void setOutputStream(TextWriter t) { s = t; }

        public override TextWriter getOutputStream() => s;

        public override bool emitsMarkup() => false;
    }
}
