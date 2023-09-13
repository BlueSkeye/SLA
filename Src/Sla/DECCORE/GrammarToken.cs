using Sla.CORE;
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

        private Token type;

        private struct /*union*/ tokenvalue
        {
            internal ulong integer;
            internal string stringval;
        }

        private tokenvalue value;
        private int lineno;            // Line number containing this token
        private int colno;         // Column where this token starts
        private int filenum;           // Which file were we in

        internal void set(Token tp)
        {
            type = tp;
        }

        // ADDED offset
        internal void set(Token tp, char[] ptr, int offset, int len)
        {
            type = tp;
            switch (tp) {
                case Token.integer:
                    string charstring = new string(ptr, offset, len);
                    long val = long.Parse(charstring);
                    value.integer = (ulong)val;
                    break;
                case Token.identifier:
                case Token.stringval:
                    value.stringval = new string(ptr, offset, len);
                    break;
                case Token.charconstant:
                    if (len == 1)
                        value.integer = (byte)ptr[offset];
                    else {
                        // Backslash
                        switch (ptr[1]) {
                            case 'n':
                                value.integer = 10;
                                break;
                            case '0':
                                value.integer = 0;
                                break;
                            case 'a':
                                value.integer = 7;
                                break;
                            case 'b':
                                value.integer = 8;
                                break;
                            case 't':
                                value.integer = 9;
                                break;
                            case 'v':
                                value.integer = 11;
                                break;
                            case 'f':
                                value.integer = 12;
                                break;
                            case 'r':
                                value.integer = 13;
                                break;
                            default:
                                value.integer = (byte)ptr[offset + 1];
                                break;
                        }
                    }
                    break;
                default:
                    throw new LowlevelError("Bad internal grammar token set");
            }
        }

        internal void setPosition(int file, int line, int col)
        {
            filenum = file;
            lineno = line;
            colno = col;
        }
    
        public GrammarToken()
        {
            type = 0;
            value.integer = 0;
            lineno = -1;
            colno = -1;
            filenum = -1;
        }

        public Token getType() => type;

        public ulong getInteger() => value.integer;

        public string getString() => value.stringval;

        public int getLineNo() => lineno;

        public int getColNo() => colno;

        public int getFileNum() => filenum;
    }
}
