using Sla.SLACOMP;
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
            // Middle of special 2 character operator
            special2,
            // First character of special 3 character operator
            special3,
            // Second character of special 3 character operator
            special32,
            // Middle of an endofline comment
            comment,
            // Punctuation character
            punctuation,
            // Middle of an identifier
            identifier,
            // Middle of a hexidecimal number
            hexstring,
            // Middle of a decimal number
            decstring,
            // Reached end of stream
            endstream,
            // Scanned an illegal character
            illegal
        }

        private static readonly IdentRec[] idents = {
            // Sorted list of identifiers
            new IdentRec( "!=", sleightokentype.OP_NOTEQUAL ),
            new IdentRec( "&&", sleightokentype.OP_BOOL_AND ),
            new IdentRec( "<<", sleightokentype.OP_LEFT ),
            new IdentRec( "<=", sleightokentype.OP_LESSEQUAL ),
            new IdentRec( "==", sleightokentype.OP_EQUAL ),
            new IdentRec( ">=", sleightokentype.OP_GREATEQUAL ),
            new IdentRec( ">>", sleightokentype.OP_RIGHT ),
            new IdentRec( "^^", sleightokentype.OP_BOOL_XOR ),
            new IdentRec( "||", sleightokentype.OP_BOOL_OR ),
            new IdentRec( "abs", sleightokentype.OP_ABS ),
            new IdentRec( "borrow", sleightokentype.OP_BORROW ),
            new IdentRec( "call", sleightokentype.CALL_KEY ),
            new IdentRec( "carry", sleightokentype.OP_CARRY ),
            new IdentRec( "ceil", sleightokentype.OP_CEIL ),
            new IdentRec( "f!=", sleightokentype.OP_FNOTEQUAL ),
            new IdentRec( "f*", sleightokentype.OP_FMULT ),
            new IdentRec( "f+", sleightokentype.OP_FADD ),
            new IdentRec( "f-", sleightokentype.OP_FSUB ),
            new IdentRec( "f/", sleightokentype.OP_FDIV ),
            new IdentRec( "f<", sleightokentype.OP_FLESS ),
            new IdentRec( "f<=", sleightokentype.OP_FLESSEQUAL ),
            new IdentRec( "f==", sleightokentype.OP_FEQUAL ),
            new IdentRec( "f>", sleightokentype.OP_FGREAT ),
            new IdentRec( "f>=", sleightokentype.OP_FGREATEQUAL ),
            new IdentRec( "float2float", sleightokentype.OP_FLOAT2FLOAT ),
            new IdentRec( "floor", sleightokentype.OP_FLOOR ),
            new IdentRec( "goto", sleightokentype.GOTO_KEY ),
            new IdentRec( "if", sleightokentype.IF_KEY ),
            new IdentRec( "int2float", sleightokentype.OP_INT2FLOAT ),
            new IdentRec( "local", sleightokentype.LOCAL_KEY ),
            new IdentRec( "nan", sleightokentype.OP_NAN ),
            new IdentRec( "return", sleightokentype.RETURN_KEY ),
            new IdentRec( "round", sleightokentype.OP_ROUND ),
            new IdentRec( "s%", sleightokentype.OP_SREM ),
            new IdentRec( "s/", sleightokentype.OP_SDIV ),
            new IdentRec( "s<", sleightokentype.OP_SLESS ),
            new IdentRec( "s<=", sleightokentype.OP_SLESSEQUAL ),
            new IdentRec( "s>", sleightokentype.OP_SGREAT ),
            new IdentRec( "s>=", sleightokentype.OP_SGREATEQUAL ),
            new IdentRec( "s>>", sleightokentype.OP_SRIGHT ),
            new IdentRec( "sborrow", sleightokentype.OP_SBORROW ),
            new IdentRec( "scarry", sleightokentype.OP_SCARRY ),
            new IdentRec( "sext", sleightokentype.OP_SEXT ),
            new IdentRec( "sqrt", sleightokentype.OP_SQRT ),
            new IdentRec( "trunc", sleightokentype.OP_TRUNC ),
            new IdentRec( "zext", sleightokentype.OP_ZEXT )
        };

        private State curstate;
        private char curchar;
        private int lookahead1;
        private int lookahead2;
        private StringBuilder curtoken = new StringBuilder();
        private int tokpos;
        private bool endofstream;
        private bool endofstreamsent;
        private FileStream? s;
        private string curidentifier;
        private ulong curnum;

        private void starttoken()
        {
            curtoken.Clear();
            curtoken.Append(curchar);
            tokpos = 1;
        }

        private void advancetoken()
        {
            curtoken[tokpos++] = curchar;
        }

        private bool isIdent(char c)
        {
            return (char.IsLetterOrDigit(c) || (c == '_') || (c == '.'));
        }

        private bool isHex(char c) => isxdigit(c);

        private bool isDec(char c) => char.IsDigit(c);

        private int findIdentifier(string str)
        {
            int low = 0;
            int high = IDENTREC_SIZE - 1;
            int comp;
            do {
                int targ = (low + high) / 2;
                comp = string.Compare(str, idents[targ].nm);
                if (comp < 0)       // str comes before targ
                    high = targ - 1;
                else if (comp > 0)      // str comes after targ
                    low = targ + 1;
                else
                    return targ;
            } while (low <= high);
            return -1;
        }

        private State moveState()
        {
            switch (curstate) {
                case State.start:
                    switch (curchar) {
                        case '#':
                            curstate = State.comment;
                            return State.start;
                        case '|':
                            if (lookahead1 == '|') {
                                starttoken();
                                curstate = State.special2;
                                return State.start;
                            }
                            return State.punctuation;
                        case '&':
                            if (lookahead1 == '&') {
                                starttoken();
                                curstate = State.special2;
                                return State.start;
                            }
                            return State.punctuation;
                        case '^':
                            if (lookahead1 == '^') {
                                starttoken();
                                curstate = State.special2;
                                return State.start;
                            }
                            return State.punctuation;
                        case '>':
                            if ((lookahead1 == '>') || (lookahead1 == '=')) {
                                starttoken();
                                curstate = State.special2;
                                return State.start;
                            }
                            return State.punctuation;
                        case '<':
                            if ((lookahead1 == '<') || (lookahead1 == '=')) {
                                starttoken();
                                curstate = State.special2;
                                return State.start;
                            }
                            return State.punctuation;
                        case '=':
                            if (lookahead1 == '=') {
                                starttoken();
                                curstate = State.special2;
                                return State.start;
                            }
                            return State.punctuation;
                        case '!':
                            if (lookahead1 == '=') {
                                starttoken();
                                curstate = State.special2;
                                return State.start;
                            }
                            return State.punctuation;
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
                            return State.punctuation;
                        case 's':
                        case 'f':
                            if (curchar == 's') {
                                if ((lookahead1 == '/') || (lookahead1 == '%')) {
                                    starttoken();
                                    curstate = State.special2;
                                    return State.start;
                                }
                                else if (lookahead1 == '<') {
                                    starttoken();
                                    curstate = (lookahead2 == '=')
                                        ? State.special3
                                        : State.special2;
                                    return State.start;
                                }
                                else if (lookahead1 == '>') {
                                    starttoken();
                                    curstate = ((lookahead2 == '>') || (lookahead2 == '='))
                                        ? State.special3
                                        : State.special2;
                                    return State.start;
                                }
                            }
                            else {
                                // curchar == 'f'
                                if ((lookahead1 == '+') || (lookahead1 == '-') || (lookahead1 == '*') || (lookahead1 == '/')) {
                                    starttoken();
                                    curstate = State.special2;
                                    return State.start;
                                }
                                else if (((lookahead1 == '=') || (lookahead1 == '!')) && (lookahead2 == '=')) {
                                    starttoken();
                                    curstate = State.special3;
                                    return State.start;
                                }
                                else if ((lookahead1 == '<') || (lookahead1 == '>')) {
                                    starttoken();
                                    curstate = (lookahead2 == '=') ? State.special3 : State.special2;
                                    return State.start;
                                }
                            }
                            // fall through here, treat 's' and 'f' as ordinary characters
                            goto case 'a';
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
                            if (isIdent((char)lookahead1)) {
                                curstate = State.identifier;
                                return State.start;
                            }
                            curstate = State.start;
                            return State.identifier;
                        case '0':
                            starttoken();
                            if (lookahead1 == 'x') {
                                curstate = State.hexstring;
                                return State.start;
                            }
                            if (isDec((char)lookahead1)) {
                                curstate = State.decstring;
                                return State.start;
                            }
                            curstate = State.start;
                            return State.decstring;
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
                            if (isDec((char)lookahead1)) {
                                curstate = State.decstring;
                                return State.start;
                            }
                            curstate = State.start;
                            return State.decstring;
                        case '\n':
                        case ' ':
                        case '\t':
                        case '\v':
                        case '\r':
                            return State.start;       // Ignore whitespace
                        case '\0':
                            curstate = State.endstream;
                            return State.endstream;
                        default:
                            curstate = State.illegal;
                            return State.illegal;
                    }
                case State.special2:
                    advancetoken();
                    curstate = State.start;
                    return State.identifier;
                case State.special3:
                    advancetoken();
                    curstate = State.special32;
                    return State.start;
                case State.special32:
                    advancetoken();
                    curstate = State.start;
                    return State.identifier;
                case State.comment:
                    if (curchar == '\n')
                        curstate = State.start;
                    else if (curchar == '\0') {
                        curstate = State.endstream;
                        return State.endstream;
                    }
                    return State.start;
                case State.identifier:
                    advancetoken();
                    if (isIdent((char)lookahead1))
                        return State.start;
                    curstate = State.start;
                    return State.identifier;
                case State.hexstring:
                    advancetoken();
                    if (isHex((char)lookahead1))
                        return State.start;
                    curstate = State.start;
                    return State.hexstring;
                case State.decstring:
                    advancetoken();
                    if (isDec((char)lookahead1))
                        return State.start;
                    curstate = State.start;
                    return State.decstring;
                default:
                    curstate = State.endstream;
                    break;
            }
            return State.endstream;
        }

        public PcodeLexer()
        {
            s = (FileStream)null;
        }

        public void initialize(FileStream t)
        {
            // Set up for new lex
            s = t;
            curstate = State.start;
            tokpos = 0;
            endofstream = false;
            endofstreamsent = false;
            lookahead1 = 0;
            lookahead2 = 0;
            // Buffer the first two characters
            lookahead1 = s.ReadByte();
            if (-1 == lookahead1) {
                endofstream = true;
                lookahead1 = 0;
                return;
            }
            lookahead2 = s.ReadByte();
            if (-1 == lookahead2) {
                endofstream = true;
                lookahead2 = 0;
                return;
            }
        }

        public int getNextToken()
        {
            // Will return either: identifier, punctuation, hexstring, decstring, endstream, or illegal
            // If identifier, hexstring, or decstring,  curtoken will be filled with the characters
            State tok;
            do {
                curchar = (char)lookahead1;
                lookahead1 = lookahead2;
                if (endofstream)
                    lookahead2 = '\0';
                else {
                    lookahead2 = s.ReadByte();
                    if (-1 == lookahead2) {
                        endofstream = true;
                        lookahead2 = '\0';
                    }
                }
                tok = moveState();
            } while (tok == State.start);
            if (tok == State.identifier) {
                //curtoken[tokpos] = '\0';    // Append null terminator
                curidentifier = curtoken.ToString();
                int num = findIdentifier(curidentifier);
                if (num < 0)            // Not a keyword
                    return (int)sleightokentype.STRING;
                return (int)idents[num].id;
            }
            else if ((tok == State.hexstring) || (tok == State.decstring)) {
                // curtoken[tokpos] = '\0';
                curnum = ulong.Parse(curtoken.ToString());
                if (!s1)
                    return (int)sleightokentype.BADINTEGER;
                return (int)sleightokentype.INTEGER;
            }
            else if (tok == State.endstream) {
                if (!endofstreamsent) {
                    endofstreamsent = true;
                    return ENDOFSTREAM; // Send 'official' end of stream token
                }
                return 0;           // 0 means end of file to parser
            }
            else if (tok == State.illegal)
                return 0;
            return (int)curchar;
        }

        public string getIdentifier() => curidentifier;

        public ulong getNumber() => curnum;
    }
}
