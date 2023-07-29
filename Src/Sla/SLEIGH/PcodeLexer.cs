using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.SLEIGH
{
    internal class PcodeLexer
    {
        public enum State
        {
            // Lexer states
            start,
            special2,           // Middle of special 2 character operator
            special3,                   // First character of special 3 character operator
            special32,          // Second character of special 3 character operator
            comment,            // Middle of an endofline comment
            punctuation,        // Punctuation character
            identifier,         // Middle of an identifier
            hexstring,          // Middle of a hexidecimal number
            decstring,          // Middle of a decimal number
            endstream,          // Reached end of stream
            illegal         // Scanned an illegal character
        }

        private static readonly IdentRec[] idents = {
            // Sorted list of identifiers
            new IdentRec( "!=", OP_NOTEQUAL ),
            new IdentRec( "&&", OP_BOOL_AND ),
            new IdentRec( "<<", OP_LEFT ),
            new IdentRec( "<=", OP_LESSEQUAL ),
            new IdentRec( "==", OP_EQUAL ),
            new IdentRec( ">=", OP_GREATEQUAL ),
            new IdentRec( ">>", OP_RIGHT ),
            new IdentRec( "^^", OP_BOOL_XOR ),
            new IdentRec( "||", OP_BOOL_OR ),
            new IdentRec( "abs", OP_ABS ),
            new IdentRec( "borrow", OP_BORROW ),
            new IdentRec( "call", CALL_KEY ),
            new IdentRec( "carry", OP_CARRY ),
            new IdentRec( "ceil", OP_CEIL ),
            new IdentRec( "f!=", OP_FNOTEQUAL ),
            new IdentRec( "f*", OP_FMULT ),
            new IdentRec( "f+", OP_FADD ),
            new IdentRec( "f-", OP_FSUB ),
            new IdentRec( "f/", OP_FDIV ),
            new IdentRec( "f<", OP_FLESS ),
            new IdentRec( "f<=", OP_FLESSEQUAL ),
            new IdentRec( "f==", OP_FEQUAL ),
            new IdentRec( "f>", OP_FGREAT ),
            new IdentRec( "f>=", OP_FGREATEQUAL ),
            new IdentRec( "float2float", OP_FLOAT2FLOAT ),
            new IdentRec( "floor", OP_FLOOR ),
            new IdentRec( "goto", GOTO_KEY ),
            new IdentRec( "if", IF_KEY ),
            new IdentRec( "int2float", OP_INT2FLOAT ),
            new IdentRec( "local", LOCAL_KEY ),
            new IdentRec( "nan", OP_NAN ),
            new IdentRec( "return", RETURN_KEY ),
            new IdentRec( "round", OP_ROUND ),
            new IdentRec( "s%", OP_SREM ),
            new IdentRec( "s/", OP_SDIV ),
            new IdentRec( "s<", OP_SLESS ),
            new IdentRec( "s<=", OP_SLESSEQUAL ),
            new IdentRec( "s>", OP_SGREAT ),
            new IdentRec( "s>=", OP_SGREATEQUAL ),
            new IdentRec( "s>>",OP_SRIGHT ),
            new IdentRec( "sborrow", OP_SBORROW ),
            new IdentRec( "scarry", OP_SCARRY ),
            new IdentRec( "sext", OP_SEXT ),
            new IdentRec( "sqrt", OP_SQRT ),
            new IdentRec( "trunc", OP_TRUNC ),
            new IdentRec( "zext", OP_ZEXT )
        };
        private int4 curstate;
        private char curchar;
        private char lookahead1;
        private char lookahead2;
        private string curtoken;
        private int4 tokpos;
        private bool endofstream;
        private bool endofstreamsent;
        private istream s;
        private string curidentifier;
        private uintb curnum;

        private void starttoken()
        {
            curtoken[0] = curchar;
            tokpos = 1;
        }

        private void advancetoken()
        {
            curtoken[tokpos++] = curchar;
        }

        private bool isIdent(char c)
        {
            return (isalnum(c)||(c=='_')||(c=='.'));
        }

        private bool isHex(char c) => isxdigit(c);

        private bool isDec(char c) => isdigit(c);

        private int4 findIdentifier(string str)
        {
            int4 low = 0;
            int4 high = IDENTREC_SIZE - 1;
            int4 comp;
            do
            {
                int4 targ = (low + high) / 2;
                comp = str.compare(idents[targ].nm);
                if (comp < 0)       // str comes before targ
                    high = targ - 1;
                else if (comp > 0)      // str comes after targ
                    low = targ + 1;
                else
                    return targ;
            } while (low <= high);
            return -1;
        }

        private int4 moveState()
        {
            switch (curstate)
            {
                case start:
                    switch (curchar)
                    {
                        case '#':
                            curstate = comment;
                            return start;
                        case '|':
                            if (lookahead1 == '|')
                            {
                                starttoken();
                                curstate = special2;
                                return start;
                            }
                            return punctuation;
                        case '&':
                            if (lookahead1 == '&')
                            {
                                starttoken();
                                curstate = special2;
                                return start;
                            }
                            return punctuation;
                        case '^':
                            if (lookahead1 == '^')
                            {
                                starttoken();
                                curstate = special2;
                                return start;
                            }
                            return punctuation;
                        case '>':
                            if ((lookahead1 == '>') || (lookahead1 == '='))
                            {
                                starttoken();
                                curstate = special2;
                                return start;
                            }
                            return punctuation;
                        case '<':
                            if ((lookahead1 == '<') || (lookahead1 == '='))
                            {
                                starttoken();
                                curstate = special2;
                                return start;
                            }
                            return punctuation;
                        case '=':
                            if (lookahead1 == '=')
                            {
                                starttoken();
                                curstate = special2;
                                return start;
                            }
                            return punctuation;
                        case '!':
                            if (lookahead1 == '=')
                            {
                                starttoken();
                                curstate = special2;
                                return start;
                            }
                            return punctuation;
                        case '(':
                        case ')':
                        case ',':
                        case ':':
                        case '[':
                        case ']':
                        case ';':
                        case '+':
                        case '-':
                        case '*':
                        case '/':
                        case '%':
                        case '~':
                            return punctuation;
                        case 's':
                        case 'f':
                            if (curchar == 's')
                            {
                                if ((lookahead1 == '/') || (lookahead1 == '%'))
                                {
                                    starttoken();
                                    curstate = special2;
                                    return start;
                                }
                                else if (lookahead1 == '<')
                                {
                                    starttoken();
                                    if (lookahead2 == '=')
                                        curstate = special3;
                                    else
                                        curstate = special2;
                                    return start;
                                }
                                else if (lookahead1 == '>')
                                {
                                    starttoken();
                                    if ((lookahead2 == '>') || (lookahead2 == '='))
                                        curstate = special3;
                                    else
                                        curstate = special2;
                                    return start;
                                }
                            }
                            else
                            {           // curchar == 'f'
                                if ((lookahead1 == '+') || (lookahead1 == '-') || (lookahead1 == '*') || (lookahead1 == '/'))
                                {
                                    starttoken();
                                    curstate = special2;
                                    return start;
                                }
                                else if (((lookahead1 == '=') || (lookahead1 == '!')) && (lookahead2 == '='))
                                {
                                    starttoken();
                                    curstate = special3;
                                    return start;
                                }
                                else if ((lookahead1 == '<') || (lookahead1 == '>'))
                                {
                                    starttoken();
                                    if (lookahead2 == '=')
                                        curstate = special3;
                                    else
                                        curstate = special2;
                                    return start;
                                }
                            }
                        // fall through here, treat 's' and 'f' as ordinary characters
                        case 'a':
                        case 'b':
                        case 'c':
                        case 'd':
                        case 'e':
                        case 'g':
                        case 'h':
                        case 'i':
                        case 'j':
                        case 'k':
                        case 'l':
                        case 'm':
                        case 'n':
                        case 'o':
                        case 'p':
                        case 'q':
                        case 'r':
                        case 't':
                        case 'u':
                        case 'v':
                        case 'w':
                        case 'x':
                        case 'y':
                        case 'z':
                        case 'A':
                        case 'B':
                        case 'C':
                        case 'D':
                        case 'E':
                        case 'F':
                        case 'G':
                        case 'H':
                        case 'I':
                        case 'J':
                        case 'K':
                        case 'L':
                        case 'M':
                        case 'N':
                        case 'O':
                        case 'P':
                        case 'Q':
                        case 'R':
                        case 'S':
                        case 'T':
                        case 'U':
                        case 'V':
                        case 'W':
                        case 'X':
                        case 'Y':
                        case 'Z':
                        case '_':
                        case '.':
                            starttoken();
                            if (isIdent(lookahead1))
                            {
                                curstate = identifier;
                                return start;
                            }
                            curstate = start;
                            return identifier;
                        case '0':
                            starttoken();
                            if (lookahead1 == 'x')
                            {
                                curstate = hexstring;
                                return start;
                            }
                            if (isDec(lookahead1))
                            {
                                curstate = decstring;
                                return start;
                            }
                            curstate = start;
                            return decstring;
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            starttoken();
                            if (isDec(lookahead1))
                            {
                                curstate = decstring;
                                return start;
                            }
                            curstate = start;
                            return decstring;
                        case '\n':
                        case ' ':
                        case '\t':
                        case '\v':
                        case '\r':
                            return start;       // Ignore whitespace
                        case '\0':
                            curstate = endstream;
                            return endstream;
                        default:
                            curstate = illegal;
                            return illegal;
                    }
                    break;
                case special2:
                    advancetoken();
                    curstate = start;
                    return identifier;
                case special3:
                    advancetoken();
                    curstate = special32;
                    return start;
                case special32:
                    advancetoken();
                    curstate = start;
                    return identifier;
                case comment:
                    if (curchar == '\n')
                        curstate = start;
                    else if (curchar == '\0')
                    {
                        curstate = endstream;
                        return endstream;
                    }
                    return start;
                case identifier:
                    advancetoken();
                    if (isIdent(lookahead1))
                        return start;
                    curstate = start;
                    return identifier;
                case hexstring:
                    advancetoken();
                    if (isHex(lookahead1))
                        return start;
                    curstate = start;
                    return hexstring;
                case decstring:
                    advancetoken();
                    if (isDec(lookahead1))
                        return start;
                    curstate = start;
                    return decstring;
                default:
                    curstate = endstream;
            }
            return endstream;
        }

        public PcodeLexer()
        {
            s = (istream*)0;
        }

        public void initialize(istream t)
        { // Set up for new lex
            s = t;
            curstate = start;
            tokpos = 0;
            endofstream = false;
            endofstreamsent = false;
            lookahead1 = 0;
            lookahead2 = 0;
            s.get(lookahead1);     // Buffer the first two characters
            if (!(*s))
            {
                endofstream = true;
                lookahead1 = 0;
                return;
            }
            s.get(lookahead2);
            if (!(*s))
            {
                endofstream = true;
                lookahead2 = 0;
                return;
            }
        }

        public int4 getNextToken()
        { // Will return either: identifier, punctuation, hexstring, decstring, endstream, or illegal
          // If identifier, hexstring, or decstring,  curtoken will be filled with the characters
            int4 tok;
            do
            {
                curchar = lookahead1;
                lookahead1 = lookahead2;
                if (endofstream)
                    lookahead2 = '\0';
                else
                {
                    s.get(lookahead2);
                    if (!(*s))
                    {
                        endofstream = true;
                        lookahead2 = '\0';
                    }
                }
                tok = moveState();
            } while (tok == start);
            if (tok == identifier)
            {
                curtoken[tokpos] = '\0';    // Append null terminator
                curidentifier = curtoken;
                int4 num = findIdentifier(curidentifier);
                if (num < 0)            // Not a keyword
                    return STRING;
                return idents[num].id;
            }
            else if ((tok == hexstring) || (tok == decstring))
            {
                curtoken[tokpos] = '\0';
                istringstream s1(curtoken);
                s1.unsetf(ios::dec | ios::hex | ios::oct);
                s1 >> curnum;
                if (!s1)
                    return BADINTEGER;
                return INTEGER;
            }
            else if (tok == endstream)
            {
                if (!endofstreamsent)
                {
                    endofstreamsent = true;
                    return ENDOFSTREAM; // Send 'official' end of stream token
                }
                return 0;           // 0 means end of file to parser
            }
            else if (tok == illegal)
                return 0;
            return (int4)curchar;
        }

        public string getIdentifier() => curidentifier;

        public uintb getNumber() => curnum;
    }
}
