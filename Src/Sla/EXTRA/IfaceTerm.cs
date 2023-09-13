using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.EXTRA
{
    /// \brief Implement the command-line interface on top of a specific input stream
    ///
    /// An initial input stream is provided as the base stream to parse for commands.
    /// Additional input streams can be stacked by invoking scripts.
    /// If the stream supports it, the stream parser recognizes special command-line editing
    /// and completion keys.
    internal class IfaceTerm : IfaceStatus
    {
#if __TERMINAL__
        // True if the input stream is a terminal
        private bool is_terminal;
        // Underlying file descriptor
        private int ifd;
        // Original terminal settings
        private struct termios itty;
#endif
        // The base input stream for the interface
        private TextReader sptr;
        // Stack of nested input streams
        private List<TextReader> inputstack;

        // 'Complete' the current command line
        // Respond to a TAB key press and try to 'complete' any existing tokens.
        // The method is handed the current state of the command-line in a string, and
        // it updates the command-line in place.
        //
        // \param line is current command-line and will hold the final completion
        // \param cursor is the current position of the cursor
        // \return the (possibly new) position of the cursor, after completion
        private int doCompletion(string line, int cursor)
        {
            List<string> fullcommand = new List<string>();
            TextReader s = new StringReader(line);
            string tok;
            IEnumerator<IfaceCommand> first, last;
            int oldsize, match;

            IEnumerator<IfaceCommand> first = comlist.GetEnumerator();
            IEnumerator<IfaceCommand> last = comlist.GetEnumerator();
            // Try to expand the command
            match = expandCom(fullcommand, s, first, last);
            if (match == 0) {
                optr.WriteLine();
                optr.WriteLine("Invalid command");
                // No change to command line
                return cursor;
            }

            // At least one match
            oldsize = line.Length;
            wordsToString(out line, fullcommand);
            if (match < 0)
                match = -match;
            else
                // Provide extra space if command word is complete
                line += ' ';
            if (!s.EofReached()) {
                // Read any additional parameters back to command line
                tok = s.ReadString();
                s.ReadSpaces();
                // Assume first space is present before extra params
                line += tok;
            }
            while (!s.EofReached()) {
                // Provide space between extra parameters
                line += ' ';
                tok = s.ReadString();
                s.ReadSpaces();
                line += tok;
            }
            if (oldsize < line.Length)
                // If we have expanded at all
                // Just display expansion
                return line.Length;

            if (match > 1) {
                // If more than one possible command
                // Display all possible completions
                string complete;
                optr.WriteLine();
                while (first.MoveNext()) {
                    // Get possible completion
                    first.Current.commandString(complete);
                    optr.WriteLine(complete);
                }
            }
            else {
                // Command is unique and expanded
                optr.WriteLine();
                optr.WriteLine("Command is complete");
            }
            return line.Length;
        }

        protected override void readLine(out string line)
        {
            char val;
            int escval;
            int cursor, lastlen, i;
            bool onecharecho;
            int hist;
            string saveline;

            line = string.Empty;
            cursor = 0;
            hist = 0;
            do {
                onecharecho = false;
                lastlen = line.Length;
                val = sptr.get();
                if (sptr.eof())
                    val = '\n';
                switch (val) {
                    case 0x01:          // C-a
                        cursor = 0;     // Jump to beginning
                        break;
                    case 0x02:          // C-b
                        if (cursor > 0)
                            cursor -= 1;        // back up one char
                        break;
                    case 0x03:          // C-c
                        line = string.Empty;       // As if we cleared the line and hit return
                        cursor = 0;
                        val = 0x0a;
                        onecharecho = true;
                        break;
                    case 0x04:          // C-d
                        line.erase(cursor, 1);  // Delete character cursor is on
                        break;
                    case 0x05:          // C-e
                        cursor = line.Length;   // Jump to end
                        break;
                    case 0x06:          // C-f
                        if (cursor < line.Length)
                            cursor += 1;        // Move forward one character
                        break;
                    case 0x07:          // C-g
                        break;          // do nothing
                    case 0x09:          // TAB
                        cursor = doCompletion(line, cursor);
                        break;
                    case 0x0a:          // Newline
                    case 0x0d:          // Carriage return
                        cursor = line.Length;
                        onecharecho = true;
                        break;
                    case 0x0b:          // C-k
                        line.erase(cursor);
                        break;
                    case 0x0c:          // C-l
                        break;
                    case 0x0e:          // C-n
                        if (hist > 0) {
                            hist -= 1;      // Get more recent history
                            if (hist > 0)
                                getHistory(line, hist - 1);
                            else
                                line = saveline;
                            cursor = line.Length;
                        }
                        break;
                    case 0x10:          // C-p
                        if (hist < getHistorySize())
                        {
                            hist += 1;      // Get more ancient history
                            if (hist == 1)
                                saveline = line;
                            getHistory(line, hist - 1);
                            cursor = line.Length;
                        }
                        break;
                    case 0x12:          // C-r
                        break;
                    case 0x15:          // C-u
                        line.erase(0, cursor);  // Erase up to cursor
                        cursor = 0;
                        break;
                    case 0x1b:          // Escape character
                        escval = sptr.get();
                        escval <<= 8;
                        escval += sptr.get();
                        switch (escval) {
                            case 0x4f44:        // left arrow
                                if (cursor > 0)
                                    cursor -= 1;
                                break;
                            case 0x4f43:        // right arrow
                                if (cursor < line.Length)
                                    cursor += 1;
                                break;
                        }
                        break;
                    case 0x08:
                    case 0x7f:          // delete
                        if (cursor != 0)
                            line.erase(--cursor, 1);
                        break;
                    default:
                        line.insert(cursor++, 1, val); // Insert single character
                        if (cursor == line.Length)
                            onecharecho = true;
                        break;
                }
                if (onecharecho)
                    optr.put(val);     // Echo most characters
                else {
                    optr.put('\r');        // Ontop of old line
                    writePrompt();
                    optr << line;      // print new line
                    for (i = line.Length; i < lastlen; ++i)
                        optr.put(' ');     // Erase any old characters
                    for (i = i - cursor; i > 0; --i)
                        optr.put('\b');    // Put cursor in the right place
                }
            } while (val != '\n');
        }

        public IfaceTerm(string prmpt, TextReader @is, TextWriter os)
            : base(prmpt, os)
        {
            sptr = @is;
#if __TERMINAL__
            struct termios ittypass;

            is_terminal = true;
            //  ifd = fileno( (FILE *)sptr.rdbuf() );
            ifd = 0;			// The above line doesn't work on some systems
				        // and since ifd will almost always refer to stdin...
  
            if (0>tcgetattr(ifd,&itty)) { // Get original terminal settings 
            if (errno == EBADF)
                throw IfaceError("Bad input file descriptor passed to iface");
            else if (errno == ENOTTY) {
                is_terminal = false;
                return;
            }
            throw IfaceError("Unknown error with input file stream");
            }
            // Build terminal settings for entering passphrase 
            ittypass = itty;
            ittypass.c_lflag &= ~((tcflag_t)ECHO); // Turn off echo 
            ittypass.c_lflag &= ~((tcflag_t)ICANON); // Turn of buffered input 
            ittypass.c_cc[VMIN] = 1;    // Buffer only one character at a time 
            ittypass.c_cc[VTIME] = 0;   // Do not time out 

            if (0 > tcsetattr(ifd, TCSANOW, &ittypass))
                throw IfaceError("Unable to set terminal attributes");
#endif
        }

        ~IfaceTerm()
        {
            while (!inputstack.empty()) {
                // delete sptr;
                sptr = inputstack.GetLastItem();
                inputstack.RemoveLastItem();
            }
#if __TERMINAL__
            if (is_terminal)
            {
                tcsetattr(ifd, TCSANOW, &itty); // Restore original terminal settings
            }
#endif
        }

        public override void pushScript(TextReader iptr, string newprompt)
        {
            inputstack.Add(sptr);
            sptr = iptr;
            base.pushScript(iptr, newprompt);
        }

        public override void popScript()
        {
            // delete sptr;
            sptr = inputstack.GetLastItem();
            inputstack.RemoveLastItem();
            base.popScript();
        }

        public override bool isStreamFinished()
        {
            if (done || inerror) return true;
            return sptr.eof();
        }
    }
}
