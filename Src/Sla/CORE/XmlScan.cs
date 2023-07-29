using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief The XML character scanner
    /// Tokenize a byte stream suitably for the main XML parser.  The scanner expects an ASCII or UTF-8
    /// encoding.  Characters is XML tag and attribute names are restricted to ASCII "letters", but
    /// extended UTF-8 characters can be used in any other character data: attribute values, content, comments. 
    public class XmlScan
    {
        /// \brief Modes of the scanner
        public enum mode
        {
            CharDataMode, CDataMode, AttValueSingleMode,
            AttValueDoubleMode, CommentMode, CharRefMode,
            NameMode, SNameMode, SingleMode
        }
        /// \brief Additional tokens returned by the scanner, in addition to byte values 00-ff
        public enum token
        {
            CharDataToken = 258,
            CDataToken = 259,
            AttValueToken = 260,
            CommentToken = 261,
            CharRefToken = 262,
            NameToken = 263,
            SNameToken = 264,
            ElementBraceToken = 265,
            CommandBraceToken = 266
        }

        /// The current scanning mode
        private mode curmode;
        /// The stream being scanned
        private StreamReader s;
        /// Current string being built
        private string? lvalue;
        /// Lookahead into the byte stream
        private int[] lookahead = new int[4];
        /// Current position in the lookahead buffer
        private int pos;
        /// Has end of stream been reached
        private bool endofstream;

        public void Dispose()
        {
        }

        /// Clear the current token string
        private void clearlvalue()
        {
            //if (null != lvalue) {
            //    delete lvalue;
            //}
            lvalue = null;
        }

        /// \brief Get the next byte in the stream
        /// Maintain a lookahead of 4 bytes at all times so that we can check for special
        /// XML character sequences without consuming.
        /// \return the next byte value as an integer
        private int getxmlchar()
        {
            int ret = lookahead[pos];
            if (!endofstream) {
                int rawInput = s.Read();
                if ((-1 == rawInput) || (0 == rawInput)) {
                    endofstream = true;
                    lookahead[pos] = '\n';
                }
                else {
                    lookahead[pos] = (char)rawInput;
                }
            }
            else {
                lookahead[pos] = -1;
            }
            pos = (pos + 1) & 3;
            return ret;
        }

        /// Peek at the next (i-th) byte without consuming
        private int next(int i)
        {
            return lookahead[(pos + i) & 3];
        }

        /// Is the given byte a \e letter
        private bool isLetter(int val)
        {
            return (((val >= 'A') && (val <= 'Z')) || ((val >= 'a') && (val <= 'z')));
        }

        /// Is the given byte/character the valid start of an XML name
        private bool isInitialNameChar(int val)
        {
            return isLetter(val) || ((val == '_') || (val == ':'));
        }

        /// Is the given byte/character valid for an XML name	
        private bool isNameChar(int val)
        {
            return isLetter(val)
                || ((val >= '0') && (val <= '9'))
                || ((val == '.') || (val == '-') || (val == '_') || (val == ':'));
        }

        /// Is the given byte/character valid as an XML character
        private bool isChar(int val)
        {
            return (val >= 0x20)
                || (val == 0xd)
                || (val == 0xa)
                || (val == 0x9);
        }

        /// Scan for the next token in Single Character mode
        private token scanSingle()
        {
            int res = getxmlchar();
            return ('<' == res)
                ? isInitialNameChar(next(0))
                    ? token.ElementBraceToken
                    : token.CommandBraceToken
                : (token)res;
        }

        /// Scan for the next token is Character Data mode
        private token scanCharData()
        {
            clearlvalue();
            lvalue = string.Empty;

            while (true)
            {
                // look for '<' '&' or ']]>'
                switch (next(0))
                {
                    case -1:
                        break;
                    case '<':
                    case '&':
                        break;
                    case ']':
                        if ((']' != next(1)) || ('>' != next(2)))
                        {
                            goto default;
                        }
                        break;
                    default:
                        lvalue += getxmlchar();
                        continue;
                }
                break;
            }
            return (0 == lvalue.Length) ? scanSingle() : token.CharDataToken;
        }

        /// Scan for the next token in CDATA mode
        private token scanCData()
        {
            clearlvalue();
            lvalue = string.Empty;

            while (true)
            {
                // Look for "]]>" and non-Char
                switch (next(0))
                {
                    case -1:
                        break;
                    case ']':
                        if ((']' != next(1)) || ('>' != next(2)))
                        {
                            goto default;
                        }
                        break;
                    default:
                        if (!isChar(next(0)))
                        {
                            break;
                        }
                        lvalue += getxmlchar();
                        continue;
                }
                break;
            }
            // CData can be empty
            return token.CDataToken;
        }

        /// Scan for the next token in Attribute Value mode
        private token scanAttValue(int quote)
        {
            clearlvalue();
            lvalue = string.Empty;
            while (next(0) != -1)
            {
                if (next(0) == quote)
                {
                    break;
                }
                if (next(0) == '<')
                {
                    break;
                }
                if (next(0) == '&')
                {
                    break;
                }
                lvalue += getxmlchar();
            }
            while (true)
            {
                int scannedCharacter;
                switch (scannedCharacter = next(0))
                {
                    case -1:
                        break;
                    case '<':
                    case '&':
                        break;
                    default:
                        if (quote == scannedCharacter)
                        {
                            break;
                        }
                        lvalue += getxmlchar();
                        continue;
                }
                break;
            }
            if (0 == lvalue.Length)
            {
                return scanSingle();
            }
            return token.AttValueToken;
        }

        /// Scan for the next token in Character Reference mode
        private token scanCharRef()
        {
            int v;
            clearlvalue();
            lvalue = string.Empty;
            if (next(0) == 'x') {
                lvalue += getxmlchar();
                while (next(0) != -1) {
                    v = next(0);
                    if (((v >= '0') && (v <= '9'))
                        || ((v >= 'a') && (v <= 'f'))
                        || ((v >= 'A') && (v <= 'F')))
                    {
                        lvalue += getxmlchar();
                        continue;
                    }
                    break;
                }
                if (1 == lvalue.Length) {
                    // Must be at least 1 hex digit
                    return (token)'x';
                }
            }
            else {
                while (next(0) != -1) {
                    v = next(0);
                    if ((v < '0') || (v > '9')) {
                        break;
                    }
                    lvalue += getxmlchar();
                }
                if (lvalue.Length == 0) {
                    return (token)scanSingle();
                }
            }
            return token.CharRefToken;
        }

        /// Scan for the next token in Comment mode
        private token scanComment()
        {
            clearlvalue();
            lvalue = string.Empty;

            while (next(0) != -1) {
                if (next(0) == '-') {
                    if (next(1) == '-') {
                        break;
                    }
                }
                if (!isChar(next(0))) {
                    break;
                }
                lvalue += getxmlchar();
            }
            return token.CommentToken;
        }

        /// Scan a Name or return single non-name character
        private token scanName()
        {
            clearlvalue();
            lvalue = string.Empty;

            if (!isInitialNameChar(next(0)))
            {
                return scanSingle();
            }
            lvalue += getxmlchar();
            while (next(0) != -1)
            {
                if (!isNameChar(next(0)))
                {
                    break;
                }
                lvalue += getxmlchar();
            }
            return token.NameToken;
        }

        /// Scan Name, allow white space before
        private token scanSName()
        {
            int whitecount = 0;
            while ((next(0) == ' ') || (next(0) == '\n') || (next(0) == '\r') || (next(0) == '\t'))
            {
                whitecount += 1;
                getxmlchar();
            }
            clearlvalue();
            lvalue = string.Empty;
            if (!isInitialNameChar(next(0)))
            {
                // First non-whitespace is not Name char
                if (whitecount > 0)
                {
                    return (token)' ';
                }
                return scanSingle();
            }
            lvalue += getxmlchar();
            while (next(0) != -1)
            {
                if (!isNameChar(next(0)))
                {
                    break;
                }
                lvalue += getxmlchar();
            }
            return (whitecount > 0) ? token.SNameToken : token.NameToken;
        }

        /// Construct scanner given a stream
        public XmlScan(StreamReader t)
        {
            s = t;
            curmode = mode.SingleMode;
            lvalue = null;
            pos = 0;
            endofstream = false;
            // Fill lookahead buffer
            getxmlchar();
            getxmlchar();
            getxmlchar();
            getxmlchar();
        }

        ///< Destructor
        ~XmlScan()
        {
            clearlvalue();
        }

        ///< Set the scanning mode
        public void setmode(mode m)
        {
            curmode = m;
        }

        /// Get the next token
        public token nexttoken()
        {
            mode mymode = curmode;
            curmode = mode.SingleMode;
            switch (mymode)
            {
                case mode.CharDataMode:
                    return scanCharData();
                case mode.CDataMode:
                    return scanCData();
                case mode.AttValueSingleMode:
                    return scanAttValue('\'');
                case mode.AttValueDoubleMode:
                    return scanAttValue('"');
                case mode.CommentMode:
                    return scanComment();
                case mode.CharRefMode:
                    return scanCharRef();
                case mode.NameMode:
                    return scanName();
                case mode.SNameMode:
                    return scanSName();
                case mode.SingleMode:
                    return scanSingle();
                default:
                    return (token)(-1);
            }
        }

        /// Return the last \e lvalue string
        public string? lval()
        {
            string? ret = lvalue;
            lvalue = null;
            return ret;
        }
    }
}
