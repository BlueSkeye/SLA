using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal class GrammarToken
    {
        // friend class GrammarLexer;
        public enum Token
        {
            openparen = 0x28,
            closeparen = 0x29,
            star = 0x2a,
            comma = 0x2c,
            semicolon = 0x3b,
            openbracket = 0x5b,
            closebracket = 0x5d,
            openbrace = 0x7b,
            closebrace = 0x7d,

            badtoken = 0x100,
            endoffile = 0x101,
            dotdotdot = 0x102,

            integer = 0x103,
            charconstant = 0x104,
            identifier = 0x105,
            stringval = 0x106,
        }
        
        private uint4 type;

        private struct /*union*/ tokenvalue
        {
            internal uintb integer;
            internal string stringval;
        }

        private tokenvalue value;
        private int4 lineno;            // Line number containing this token
        private int4 colno;         // Column where this token starts
        private int4 filenum;           // Which file were we in

        private void set(uint4 tp);
        private void set(uint4 tp, char ptr, int4 len);

        private void setPosition(int4 file, int4 line, int4 col)
        {
            filenum = file;
            lineno = line;
            colno = col;
        }
    
        public GrammarToken();

        public uint4 getType() => type;

        public uintb getInteger() => value.integer;

        public string getString() => value.stringval;

        public int4 getLineNo() => lineno;

        public int4 getColNo() => colno;

        public int4 getFileNum() => filenum;
    }
}
