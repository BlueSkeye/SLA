using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class GrammarLexer
    {
        private Dictionary<int, string> filenamemap;  // All files ever seen
        private Dictionary<int, istream> streammap;
        private List<int> filestack; // Stack of current files
        private int buffersize;        // maximum characters in buffer
        private char[] buffer;           // Current line being processed
        private int bufstart;      // Next character to process
        private int bufend;            // Next open position in buffer
        private int curlineno;
        private istream @in;            // Current stream
        private bool endoffile;
        private uint state;            // State of parser
        private string error;
        private enum State
        {
            start,
            slash,
            dot1,
            dot2,
            dot3,
            punctuation,
            endofline_comment,
            c_comment,
            doublequote,
            doublequoteend,
            singlequote,
            singlequoteend,
            singlebackslash,
            number,
            identifier
        };

        private void bumpLine()
        {               // Keep track of a newline
            curlineno += 1;
            bufstart = 0;
            bufend = 0;
        }

        private uint moveState(char lookahead)
        { // Change finite state machine based on lookahead
            uint res;
            bool newline = false;

            if (lookahead < 32)
            {
                if ((lookahead == 9) || (lookahead == 11) || (lookahead == 12) ||
                (lookahead == 13))
                    lookahead = ' ';
                else if (lookahead == '\n')
                {
                    newline = true;
                    lookahead = ' ';
                }
                else
                {
                    setError("Illegal character");
                    return GrammarToken::badtoken;
                }
            }
            else if (lookahead >= 127)
            {
                setError("Illegal character");
                return GrammarToken::badtoken;
            }

            res = 0;
            bool syntaxerror = false;
            switch (state)
            {
                case start:
                    switch (lookahead)
                    {
                        case '/':
                            state = slash;
                            break;
                        case '.':
                            state = dot1;
                            break;
                        case '*':
                        case ',':
                        case '(':
                        case ')':
                        case '[':
                        case ']':
                        case '{':
                        case '}':
                        case ';':
                        case '=':
                            state = punctuation;
                            bufstart = bufend - 1;
                            break;
                        case '-':
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            state = number;
                            bufstart = bufend - 1;
                            break;
                        case ' ':
                            break;          // Ignore since we are already open
                        case '\"':
                            state = doublequote;
                            bufstart = bufend - 1;
                            break;
                        case '\'':
                            state = singlequote;
                            break;
                        case 'a':
                        case 'b':
                        case 'c':
                        case 'd':
                        case 'e':
                        case 'f':
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
                        case 's':
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
                            state = identifier;
                            bufstart = bufend - 1;
                            break;
                        default:
                            setError("Illegal character");
                            return GrammarToken::badtoken;
                    }
                    break;
                case slash:
                    if (lookahead == '*')
                        state = c_comment;
                    else if (lookahead == '/')
                        state = endofline_comment;
                    else
                        syntaxerror = true;
                    break;
                case dot1:
                    if (lookahead == '.')
                        state = dot2;
                    else
                        syntaxerror = true;
                    break;
                case dot2:
                    if (lookahead == '.')
                        state = dot3;
                    else
                        syntaxerror = true;
                    break;
                case dot3:
                    state = start;
                    res = GrammarToken::dotdotdot;
                    break;
                case punctuation:
                    state = start;
                    res = (uint)buffer[bufstart];
                    break;
                case endofline_comment:
                    if (newline)
                        state = start;
                    break;          // Anything else is part of comment
                case c_comment:
                    if (lookahead == '/')
                    {
                        if ((bufend > 1) && (buffer[bufend - 2] == '*'))
                            state = start;
                    }
                    break;          // Anything else is part of comment
                case doublequote:
                    if (lookahead == '\"')
                        state = doublequoteend;
                    break;          // Anything else is part of string
                case doublequoteend:
                    state = start;
                    res = GrammarToken::stringval;
                    break;
                case singlequote:
                    if (lookahead == '\\')
                        state = singlebackslash;
                    else if (lookahead == '\'')
                        state = singlequoteend;
                    break;          // Anything else is part of string
                case singlequoteend:
                    state = start;
                    res = GrammarToken::charconstant;
                    break;
                case singlebackslash:   // Seen backslash in a single quoted string
                    state = singlequote;
                    break;
                case number:
                    if (lookahead == 'x')
                    {
                        if (((bufend - bufstart) != 2) || (buffer[bufstart] != '0'))
                            syntaxerror = true; // x only allowed as 0x hex indicator
                    }
                    else if ((lookahead >= '0') && (lookahead <= '9'))
                    {
                    }
                    else if ((lookahead >= 'A') && (lookahead <= 'Z'))
                    {
                    }
                    else if ((lookahead >= 'a') && (lookahead <= 'z'))
                    {
                    }
                    else if (lookahead == '_')
                    {
                    }
                    else
                    {
                        state = start;
                        res = GrammarToken::integer;
                    }
                    break;
                case identifier:
                    if ((lookahead >= '0') && (lookahead <= '9'))
                    {
                    }
                    else if ((lookahead >= 'A') && (lookahead <= 'Z'))
                    {
                    }
                    else if ((lookahead >= 'a') && (lookahead <= 'z'))
                    {
                    }
                    else if (lookahead == '_' || lookahead == ':')
                    {
                    }
                    else
                    {
                        state = start;
                        res = GrammarToken::identifier;
                    }
                    break;
            }
            if (syntaxerror)
            {
                setError("Syntax error");
                return GrammarToken::badtoken;
            }
            if (newline) bumpLine();
            return res;
        }

        private void establishToken(GrammarToken token, uint val)
        {
            if (val < GrammarToken::integer)
                token.set(val);
            else
            {
                token.set(val, buffer + bufstart, (bufend - bufstart) - 1);
            }
            token.setPosition(filestack.GetLastItem(), curlineno, bufstart);
        }

        private void setError(string err)
        {
            error = err;
        }
    
        public GrammarLexer(int maxbuffer)
        {
            buffersize = maxbuffer;
            buffer = new char[maxbuffer];
            bufstart = 0;
            bufend = 0;
            curlineno = 0;
            state = start;
            @in = (istream*)0;
            endoffile = true;
        }

        ~GrammarLexer()
        {
            delete[] buffer;
        }

        public void clear()
        { // Clear lexer for a brand new parse
            filenamemap.clear();
            streammap.clear();
            filestack.clear();
            bufstart = 0;
            bufend = 0;
            curlineno = 0;
            state = start;
            @in = (istream*)0;
            endoffile = true;
            error.clear();
        }

        public istream getCurStream() => @in;

        public void pushFile(string filename, istream i)
        {
            int filenum = filenamemap.size();
            filenamemap[filenum] = filename;
            streammap[filenum] = i;
            filestack.Add(filenum);
            @in = i;
            endoffile = false;
        }

        public void popFile()
        {
            filestack.pop_back();
            if (filestack.empty())
            {
                endoffile = true;
                return;
            }
            int filenum = filestack.GetLastItem();
            @in = streammap[filenum];  // Get previous stream
        }

        public void getNextToken(GrammarToken token)
        { // Read next token, return true if end of stream
            char nextchar;
            uint tok = GrammarToken::badtoken;
            bool firsttimethru = true;

            if (endoffile)
            {
                token.set(GrammarToken::endoffile);
                return;
            }
            do
            {
                if ((!firsttimethru) || (bufend == 0))
                {
                    if (bufend >= buffersize)
                    {
                        setError("Line too long");
                        tok = GrammarToken::badtoken;
                        break;
                    }
                    @@in.get(nextchar);
                    if (!(*@in)) {
                        endoffile = true;
                        break;
                    }
                    buffer[bufend++] = nextchar;
                }
                else
                    nextchar = buffer[bufend - 1]; // Get old lookahead token
                tok = moveState(nextchar);
                firsttimethru = false;
            } while (tok == 0);
            if (endoffile)
            {
                buffer[bufend++] = ' '; // Simulate a space
                tok = moveState(' ');   // to let the final token resolve
                if ((tok == 0) && (state != start) && (state != endofline_comment))
                {
                    setError("Incomplete token");
                    tok = GrammarToken::badtoken;
                }
            }
            establishToken(token, tok);
        }

        public void writeLocation(ostream s, int line, int filenum)
        {
            s << " at line " << dec << line;
            s << " in " << filenamemap[filenum];
        }

        public void writeTokenLocation(ostream s, int line, int colno)
        {
            if (line != curlineno) return;  // Does line match current line in buffer
            for (int i = 0; i < bufend; ++i)
                s << buffer[i];
            s << '\n';
            for (int i = 0; i < colno; ++i)
                s << ' ';
            s << "^--\n";
        }

        public string getError() => error;
    }
}
