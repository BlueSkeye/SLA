using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if CPUI_RULECOMPILE
    internal class RuleLexer
    {
        // 1 is identifier, 2 is digit, 4=namechar
        private static int4[] identlist = new int4[256] {
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 0, 0, 0, 0, 0, 0,
          0, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
          5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0, 0, 0, 0, 5,
          0, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
          5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
          0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        private Dictionary<string, int4> keywordmap = new Dictionary<string, int4>();
        private TextReader s;
        private char[] identifier = new char[256];
        private int4 identlength;
        private int4[] lookahead = new int4[4];
        private int4 pos;
        bool endofstream;
        private int4 lineno;

        private int4 getNextChar()
        {
            char c;
            int4 ret = lookahead[pos];
            if (!endofstream)
            {
                (*s).get(c);
                if ((*s).eof() || (c == '\0'))
                {
                    endofstream = true;
                    lookahead[pos] = '\n';
                }
                else
                    lookahead[pos] = c;
            }
            else
                lookahead[pos] = -1;
            pos = (pos + 1) & 3;
            return ret;
        }

        private int4 next(int4 i) => lookahead[(pos + i) & 3];

        private int4 scanIdentifier()
        {
            int4 i = 0;
            identifier[i] = (char)getNextChar(); // Scan at least the first character
            i += 1;
            do
            {
                if ((identlist[next(0)] & 1) != 0)
                {
                    identifier[i] = (char)getNextChar();
                    i += 1;
                }
                else
                    break;
            } while (i < 255);
            if ((i == 255) || (i == 0))
                return -1;          // Identifier is too long
            identifier[i] = '\0';
            identlength = i;

            if ((identlist[(int4)identifier[0]] & 2) != 0) // First number is digit
                return scanNumber();

            switch (identifier[0])
            {
                case 'o':
                    return buildString(OP_IDENTIFIER);
                case 'v':
                    return buildString(VAR_IDENTIFIER);
                case '#':
                    return buildString(CONST_IDENTIFIER);
                case 'O':
                    return buildString(OP_NEW_IDENTIFIER);
                case 'V':
                    return buildString(VAR_NEW_IDENTIFIER);
                case '.':
                    return buildString(DOT_IDENTIFIER);
                default:
                    return otherIdentifiers();
            }
        }

        private int4 scanNumber()
        {
            istringstream s(identifier);
            s.unsetf(ios::dec | ios::hex | ios::oct);
            uint8 val;
            s >> val;
            if (!s)
                return BADINTEGER;
            ruleparselval.big = new int8(val);
            return INTB;
        }

        private int4 buildString(int4 tokentype)
        {
            if (identlength <= 1) return -1;
            for (int4 i = 1; i < identlength; ++i)
            {
                if ((identlist[(int4)identifier[i]] & 4) == 0) return -1;
            }

            if (identifier[0] == '.')
            {
                ruleparselval.str = new string(identifier + 1);
                return tokentype;
            }

            if (identifier[0] == '#')
                identifier[0] = 'c';
            ruleparselval.str = new string(identifier);
            return tokentype;
        }

        private int4 otherIdentifiers()
        {
            map<string, int4>::const_iterator iter;
            iter = keywordmap.find(string(identifier));
            if (iter != keywordmap.end())
                return (*iter).second;
            return -1;
        }

        private void initKeywords()
        {
            keywordmap["COPY"] = OP_COPY;
            keywordmap["ZEXT"] = OP_INT_ZEXT;
            keywordmap["CARRY"] = OP_INT_CARRY;
            keywordmap["SCARRY"] = OP_INT_SCARRY;
            keywordmap["SEXT"] = OP_INT_SEXT;
            keywordmap["SBORROW"] = OP_INT_SBORROW;
            keywordmap["NAN"] = OP_FLOAT_NAN;
            keywordmap["ABS"] = OP_FLOAT_ABS;
            keywordmap["SQRT"] = OP_FLOAT_SQRT;
            keywordmap["CEIL"] = OP_FLOAT_CEIL;
            keywordmap["FLOOR"] = OP_FLOAT_FLOOR;
            keywordmap["ROUND"] = OP_FLOAT_ROUND;
            keywordmap["INT2FLOAT"] = OP_FLOAT_INT2FLOAT;
            keywordmap["FLOAT2FLOAT"] = OP_FLOAT_FLOAT2FLOAT;
            keywordmap["TRUNC"] = OP_FLOAT_TRUNC;
            keywordmap["GOTO"] = OP_BRANCH;
            keywordmap["GOTOIND"] = OP_BRANCHIND;
            keywordmap["CALL"] = OP_CALL;
            keywordmap["CALLIND"] = OP_CALLIND;
            keywordmap["RETURN"] = OP_RETURN;
            keywordmap["CBRANCH"] = OP_CBRANCH;
            keywordmap["USEROP"] = OP_CALLOTHER;
            keywordmap["LOAD"] = OP_LOAD;
            keywordmap["STORE"] = OP_STORE;
            keywordmap["CONCAT"] = OP_PIECE;
            keywordmap["SUBPIECE"] = OP_SUBPIECE;
            keywordmap["before"] = BEFORE_KEYWORD;
            keywordmap["after"] = AFTER_KEYWORD;
            keywordmap["remove"] = REMOVE_KEYWORD;
            keywordmap["set"] = SET_KEYWORD;
            keywordmap["istrue"] = ISTRUE_KEYWORD;
            keywordmap["isfalse"] = ISFALSE_KEYWORD;
        }

        public RuleLexer()
        {
            initKeywords();
        }

        public void initialize(TextReader t)
        {
            s = &t;
            pos = 0;
            endofstream = false;
            lineno = 1;
            getNextChar();
            getNextChar();
            getNextChar();
            getNextChar();      // Fill lookahead buffer
        }

        public int4 getLineNo() => lineno;

        public int4 nextToken()
        {
            for (; ; )
            {
                int4 mychar = next(0);
                switch (mychar)
                {
                    case '(':
                    case ')':
                    case ',':
                    case '[':
                    case ']':
                    case ';':
                    case '{':
                    case '}':
                    case ':':
                        getNextChar();
                        ruleparselval.ch = (char)mychar;
                        return mychar;
                    case '\r':
                    case ' ':
                    case '\t':
                    case '\v':
                        getNextChar();
                        break;
                    case '\n':
                        getNextChar();
                        lineno += 1;
                        break;
                    case '-':
                        getNextChar();
                        if (next(0) == '>')
                        {
                            getNextChar();
                            return RIGHT_ARROW;
                        }
                        else if (next(0) == '-')
                        {
                            getNextChar();
                            if (next(0) == '>')
                            {
                                getNextChar();
                                return DOUBLE_RIGHT_ARROW;
                            }
                            return ACTION_TICK;
                        }
                        return OP_INT_SUB;
                    case '<':
                        getNextChar();
                        if (next(0) == '-')
                        {
                            getNextChar();
                            if (next(0) == '-')
                            {
                                getNextChar();
                                return DOUBLE_LEFT_ARROW;
                            }
                            return LEFT_ARROW;
                        }
                        else if (next(0) == '<')
                        {
                            getNextChar();
                            return OP_INT_LEFT;
                        }
                        else if (next(0) == '=')
                        {
                            getNextChar();
                            return OP_INT_LESSEQUAL;
                        }
                        return OP_INT_LESS;
                    case '|':
                        getNextChar();
                        if (next(0) == '|')
                        {
                            getNextChar();
                            return OP_BOOL_OR;
                        }
                        return OP_INT_OR;
                    case '&':
                        getNextChar();
                        if (next(0) == '&')
                        {
                            getNextChar();
                            return OP_BOOL_AND;
                        }
                        return OP_INT_AND;
                    case '^':
                        getNextChar();
                        if (next(0) == '^')
                        {
                            getNextChar();
                            return OP_BOOL_XOR;
                        }
                        return OP_INT_XOR;
                    case '>':
                        if (next(1) == '>')
                        {
                            getNextChar();
                            getNextChar();
                            return OP_INT_RIGHT;
                        }
                        return -1;
                    case '=':
                        getNextChar();
                        if (next(0) == '=')
                        {
                            getNextChar();
                            return OP_INT_EQUAL;
                        }
                        ruleparselval.ch = (char)mychar;
                        return mychar;
                    case '!':
                        getNextChar();
                        if (next(0) == '=')
                        {
                            getNextChar();
                            return OP_INT_NOTEQUAL;
                        }
                        return OP_BOOL_NEGATE;
                    case 's':
                        if (next(1) == '/')
                        {
                            getNextChar();
                            getNextChar();
                            return OP_INT_SDIV;
                        }
                        else if (next(1) == '%')
                        {
                            getNextChar();
                            getNextChar();
                            return OP_INT_SREM;
                        }
                        else if ((next(1) == '>') && (next(2) == '>'))
                        {
                            getNextChar();
                            getNextChar();
                            getNextChar();
                            return OP_INT_SRIGHT;
                        }
                        else if (next(1) == '<')
                        {
                            getNextChar();
                            getNextChar();
                            if (next(0) == '=')
                            {
                                getNextChar();
                                return OP_INT_SLESSEQUAL;
                            }
                            return OP_INT_SLESS;
                        }
                        return scanIdentifier();
                    case 'f':
                        if (next(1) == '+')
                        {
                            getNextChar();
                            getNextChar();
                            return OP_FLOAT_ADD;
                        }
                        else if (next(1) == '-')
                        {
                            getNextChar();
                            getNextChar();
                            return OP_FLOAT_SUB;
                        }
                        else if (next(1) == '*')
                        {
                            getNextChar();
                            getNextChar();
                            return OP_FLOAT_MULT;
                        }
                        else if (next(1) == '/')
                        {
                            getNextChar();
                            getNextChar();
                            return OP_FLOAT_DIV;
                        }
                        else if ((next(1) == '=') && (next(2) == '='))
                        {
                            getNextChar();
                            getNextChar();
                            getNextChar();
                            return OP_FLOAT_EQUAL;
                        }
                        else if ((next(1) == '!') && (next(2) == '='))
                        {
                            getNextChar();
                            getNextChar();
                            getNextChar();
                            return OP_FLOAT_NOTEQUAL;
                        }
                        else if (next(1) == '<')
                        {
                            getNextChar();
                            getNextChar();
                            if (next(0) == '=')
                            {
                                getNextChar();
                                return OP_FLOAT_LESSEQUAL;
                            }
                            return OP_FLOAT_LESS;
                        }
                        return -1;
                    case '+':
                        getNextChar();
                        return OP_INT_ADD;
                    case '*':
                        getNextChar();
                        return OP_INT_MULT;
                    case '/':
                        getNextChar();
                        return OP_INT_DIV;
                    case '%':
                        getNextChar();
                        return OP_INT_REM;
                    case '~':
                        getNextChar();
                        return OP_INT_NEGATE;
                    case '#':
                        if ((identlist[next(1)] & 6) == 4)
                            return scanIdentifier();
                        getNextChar();
                        ruleparselval.ch = (char)mychar; // Return '#' as single token
                        return mychar;
                    default:
                        return scanIdentifier();
                }
            }
            return -1;
        }
    }
#endif
}
