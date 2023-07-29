using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
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
        
        private uint type;

        private struct /*union*/ tokenvalue
        {
            internal ulong integer;
            internal string stringval;
        }

        private tokenvalue value;
        private int lineno;            // Line number containing this token
        private int colno;         // Column where this token starts
        private int filenum;           // Which file were we in

        private void set(uint tp);
        private void set(uint tp, char ptr, int len);

        private void setPosition(int file, int line, int col)
        {
            filenum = file;
            lineno = line;
            colno = col;
        }
    
        public GrammarToken();

        public uint getType() => type;

        public ulong getInteger() => value.integer;

        public string getString() => value.stringval;

        public int getLineNo() => lineno;

        public int getColNo() => colno;

        public int getFileNum() => filenum;
    }
}
