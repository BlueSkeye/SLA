
namespace Sla.DECCORE
{
    internal class GrammarLexer
    {
        // All files ever seen
        private Dictionary<int, string> filenamemap = new Dictionary<int, string>();
        private Dictionary<int, TextReader> streammap = new Dictionary<int, TextReader>();
        // Stack of current files
        private List<int> filestack = new List<int>();
        // maximum characters in buffer
        private int buffersize;
        // Current line being processed
        private char[] buffer;
        // Next character to process
        private int bufstart;
        // Next open position in buffer
        private int bufend;
        private int curlineno;
        // Current stream
        private TextReader? @in;
        private bool endoffile;
        // State of parser
        private State state;
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
        {
            // Keep track of a newline
            curlineno += 1;
            bufstart = 0;
            bufend = 0;
        }

        private GrammarToken.Token moveState(char lookahead)
        {
            // Change finite state machine based on lookahead
            GrammarToken.Token res;
            bool newline = false;

            if (lookahead < 32) {
                if ((lookahead == 9) || (lookahead == 11) || (lookahead == 12) ||
                (lookahead == 13))
                    lookahead = ' ';
                else if (lookahead == '\n') {
                    newline = true;
                    lookahead = ' ';
                }
                else {
                    setError("Illegal character");
                    return GrammarToken.Token.badtoken;
                }
            }
            else if (lookahead >= 127) {
                setError("Illegal character");
                return GrammarToken.Token.badtoken;
            }

            res = 0;
            bool syntaxerror = false;
            switch (state) {
                case State.start:
                    switch (lookahead) {
                        case '/':
                            state = State.slash;
                            break;
                        case '.':
                            state = State.dot1;
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
                            state = State.punctuation;
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
                            state = State.number;
                            bufstart = bufend - 1;
                            break;
                        case ' ':
                            // Ignore since we are already open
                            break;
                        case '\"':
                            state = State.doublequote;
                            bufstart = bufend - 1;
                            break;
                        case '\'':
                            state = State.singlequote;
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
                            state = State.identifier;
                            bufstart = bufend - 1;
                            break;
                        default:
                            setError("Illegal character");
                            return GrammarToken.Token.badtoken;
                    }
                    break;
                case State.slash:
                    if (lookahead == '*')
                        state = State.c_comment;
                    else if (lookahead == '/')
                        state = State.endofline_comment;
                    else
                        syntaxerror = true;
                    break;
                case State.dot1:
                    if (lookahead == '.')
                        state = State.dot2;
                    else
                        syntaxerror = true;
                    break;
                case State.dot2:
                    if (lookahead == '.')
                        state = State.dot3;
                    else
                        syntaxerror = true;
                    break;
                case State.dot3:
                    state = State.start;
                    res = GrammarToken.Token.dotdotdot;
                    break;
                case State.punctuation:
                    state = State.start;
                    res = (GrammarToken.Token)buffer[bufstart];
                    break;
                case State.endofline_comment:
                    if (newline)
                        state = State.start;
                    // Anything else is part of comment
                    break;
                case State.c_comment:
                    if (lookahead == '/') {
                        if ((bufend > 1) && (buffer[bufend - 2] == '*'))
                            state = State.start;
                    }
                    // Anything else is part of comment
                    break;
                case State.doublequote:
                    if (lookahead == '\"')
                        state = State.doublequoteend;
                    // Anything else is part of string
                    break;
                case State.doublequoteend:
                    state = State.start;
                    res = GrammarToken.Token.stringval;
                    break;
                case State.singlequote:
                    if (lookahead == '\\')
                        state = State.singlebackslash;
                    else if (lookahead == '\'')
                        state = State.singlequoteend;
                    // Anything else is part of string
                    break;
                case State.singlequoteend:
                    state = State.start;
                    res = GrammarToken.Token.charconstant;
                    break;
                case State.singlebackslash:
                    // Seen backslash in a single quoted string
                    state = State.singlequote;
                    break;
                case State.number:
                    if (lookahead == 'x') {
                        if (((bufend - bufstart) != 2) || (buffer[bufstart] != '0'))
                            // x only allowed as 0x hex indicator
                            syntaxerror = true;
                    }
                    else if ((lookahead >= '0') && (lookahead <= '9')) {
                    }
                    else if ((lookahead >= 'A') && (lookahead <= 'Z')) {
                    }
                    else if ((lookahead >= 'a') && (lookahead <= 'z')) {
                    }
                    else if (lookahead == '_') {
                    }
                    else {
                        state = State.start;
                        res = GrammarToken.Token.integer;
                    }
                    break;
                case State.identifier:
                    if ((lookahead >= '0') && (lookahead <= '9')) {
                    }
                    else if ((lookahead >= 'A') && (lookahead <= 'Z')) {
                    }
                    else if ((lookahead >= 'a') && (lookahead <= 'z')) {
                    }
                    else if (lookahead == '_' || lookahead == ':') {
                    }
                    else {
                        state = State.start;
                        res = GrammarToken.Token.identifier;
                    }
                    break;
            }
            if (syntaxerror) {
                setError("Syntax error");
                return GrammarToken.Token.badtoken;
            }
            if (newline) bumpLine();
            return res;
        }

        private void establishToken(GrammarToken token, GrammarToken.Token val)
        {
            if (val < GrammarToken.Token.integer)
                token.set(val);
            else {
                token.set(val, buffer, bufstart, (bufend - bufstart) - 1);
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
            state = State.start;
            @in = (TextReader)null;
            endoffile = true;
        }

        ~GrammarLexer()
        {
            // delete[] buffer;
        }

        public void clear()
        {
            // Clear lexer for a brand new parse
            filenamemap.Clear();
            streammap.Clear();
            filestack.Clear();
            bufstart = 0;
            bufend = 0;
            curlineno = 0;
            state = State.start;
            @in = (TextReader)null;
            endoffile = true;
            error = string.Empty;
        }

        public TextReader? getCurStream() => @in;

        public void pushFile(string filename, TextReader i)
        {
            int filenum = filenamemap.Count();
            filenamemap[filenum] = filename;
            streammap[filenum] = i;
            filestack.Add(filenum);
            @in = i;
            endoffile = false;
        }

        public void popFile()
        {
            filestack.RemoveLastItem();
            if (filestack.empty()) {
                endoffile = true;
                return;
            }
            int filenum = filestack.GetLastItem();
            // Get previous stream
            @in = streammap[filenum];
        }

        public void getNextToken(GrammarToken token)
        {
            // Read next token, return true if end of stream
            GrammarToken.Token tok = GrammarToken.Token.badtoken;
            bool firsttimethru = true;

            if (endoffile) {
                token.set(GrammarToken.Token.endoffile);
                return;
            }
            if (@in == null) { throw new InvalidOperationException(); }
            do {
                char? nextchar;
                if ((!firsttimethru) || (bufend == 0)) {
                    if (bufend >= buffersize) {
                        setError("Line too long");
                        tok = GrammarToken.Token.badtoken;
                        break;
                    }
                    nextchar = @in.ReadCharacter();
                    if (nextchar == null) {
                        endoffile = true;
                        break;
                    }
                    else {
                        buffer[bufend++] = nextchar.Value;
                    }
                }
                else {
                    // Get old lookahead token
                    nextchar = buffer[bufend - 1];
                }
                tok = moveState(nextchar.Value);
                firsttimethru = false;
            } while (tok == 0);
            if (endoffile) {
                // Simulate a space
                buffer[bufend++] = ' ';
                // to let the final token resolve
                tok = moveState(' ');
                if ((tok == 0) && (state != State.start) && (state != State.endofline_comment)) {
                    setError("Incomplete token");
                    tok = GrammarToken.Token.badtoken;
                }
            }
            establishToken(token, tok);
        }

        public void writeLocation(TextWriter s, int line, int filenum)
        {
            s.Write($" at line {line} in {filenamemap[filenum]}");
        }

        public void writeTokenLocation(TextWriter s, int line, int colno)
        {
            if (line != curlineno) return;  // Does line match current line in buffer
            for (int i = 0; i < bufend; ++i)
                s.Write(buffer[i]);
            s.WriteLine();
            for (int i = 0; i < colno; ++i)
                s.Write(' ');
            s.Write("^--\n");
        }

        public string getError() => error;
    }
}
