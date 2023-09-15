using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// Command line processing is started with mainloop(), which prints a command prompt,
    /// allows command line editing, including command completion and history, and executes
    /// the corresponding IfaceComman::execute() callback.
    /// Command words only have to match enough to disambiguate it from other commands.

    /// A Custom history size and command prompt can be passed to the constructor.
    /// Applications should inherit from base class IfaceStatus in order to
    ///   - Override the readLine() method
    ///   - Override pushScript() and popScript() to allow command scripts
    ///   - Get custom data into IfaceCommand callbacks
    internal abstract class IfaceStatus
    {
        /// Stack of command prompts corresponding to script nesting level
        private List<string> promptstack = new List<string>();
        /// Stack of flag state corresponding to script nesting level
        private List<uint> flagstack = new List<uint>();
        /// The current command prompt
        private string prompt;
        /// Maximum number of command lines to store in history
        private int maxhistory;
        /// Most recent history
        private int curhistory;
        /// History of commands executed through this interface
        private List<string> history = new List<string>();
        /// Set to \b true if commands are sorted
        private bool sorted;
        /// Set to \b true if any error terminates the process
        private bool errorisdone;

        /// \brief Restrict range of possible commands given a list of command line tokens
        ///
        /// Given a set of tokens partially describing a command, provide the most narrow
        /// range of IfaceCommand objects that could be referred to.
        /// REPLACED first, last -> commands + commandIndex
        /// \param first will hold an iterator to the first command in the range
        /// \param last will hold an iterator (one after) the last command in the range
        /// \param input is the list of command tokens to match on
        private void restrictCom(List<IfaceCommand> commands, ref int commandIndex,
            List<string> input)
        {
            IEnumerator<IfaceCommand> newfirst, newlast;
            IfaceCommandDummy dummy = new IfaceCommandDummy();

            dummy.addWords(input);
            newfirst = lower_bound(first, last, dummy, compare_ifacecommand);
            dummy.removeWord();
            // Make copy of last word
            string temp = input.GetLastItem();
            // temp will now be greater than any word
            // whose first letters match input.GetLastItem()
            temp[temp.Length - 1] += 1;
            dummy.addWord(temp);
            newlast = upper_bound(first, last, dummy, compare_ifacecommand);
            first = newfirst;
            last = newlast;
        }

        /// \brief Read the next command line
        /// \param line is filled in with the next command to execute
        protected abstract void readLine(out string line);

        /// Store the given command line into \e history
        /// The line is saved in a circular history buffer
        /// \param line is the command line to save
        private void saveHistory(string line)
        {
            if (history.size() < maxhistory)
                history.Add(line);
            else
                history[curhistory] = line;
            curhistory += 1;
            if (curhistory == maxhistory)
                curhistory = 0;
        }

        /// Set to \b true if last command did not succeed
        protected bool inerror;
        /// List of registered commands
        protected List<IfaceCommand> comlist = new List<IfaceCommand>();
        /// Data associated with particular modules
        protected Dictionary<string, IfaceData> datamap = new Dictionary<string, IfaceData>();

        /// \brief Expand tokens from the given input stream to a full command
        ///
        /// A range of possible commands is returned. Processing of the stream
        /// stops as soon as at least one complete command is recognized.
        /// Tokens partially matching a command are expanded to the full command
        /// and passed back.
        /// \param expand will hold the list of expanded tokens
        /// \param s is the input stream tokens are read from
        /// REPLACED first, last -> commands
        /// \param first will hold the beginning of the matching range of commands
        /// \param last will hold the end of the matching range of commands
        /// \return the number of matching commands
        protected int expandCom(List<string> expand, TextReader s,
            List<IfaceCommand> commands)
        {
            // Make sure command list is empty
            expand.Clear();
            bool res = true;
            if (0 == commands.Count)
                // If subrange is empty, return 0
                return 0;
            int commandIndex = 0;
            // Which word are we currently expanding
            for (int pos = 0; ; ++pos) {
                // Skip whitespace
                s.ReadSpaces();
                if (commandIndex == (commands.Count - 1)) {
                    // If subrange is unique
                    IfaceCommand currentCommand = commands[commandIndex];
                    if (s.EofReached()) {
                        // If no more input
                        for (; pos < currentCommand.numWords(); ++pos)
                            // Automatically provide missing words
                            expand.Add(currentCommand.getCommandWord(pos));
                    }
                    if (currentCommand.numWords() == pos)
                        // If all words are matched
                        // Finished
                        return 1;
                }
                if (!res) {
                    // Last word was ambiguous
                    return (!s.EofReached())
                        ? (commands.Count - commandIndex)
                        // Negative number to indicate last word incomplete
                        : (commandIndex - commands.Count);
                }
                if (s.EofReached()) {
                    // if no other words
                    if (expand.empty())
                        return (commandIndex - commands.Count);
                    // return number of matches
                    return (commands.Count - commandIndex);
                }
                // Get next token
                string tok = s.ReadString();
                expand.Add(tok);
                restrictCom(commands, ref commandIndex, expand);
                // If subrange is empty, return 0
                if (commands.Count <= commandIndex)
                    return 0;
                res = maxmatch(tok, commands[commandIndex].getCommandWord(pos),
                    commands.Last().getCommandWord(pos));
                expand.SetLastItem(tok);
            }
        }

        /// Set to \b true (by a command) to indicate processing is finished
        public bool done;
        /// Where to put command line output
        public TextWriter optr;
        /// Where to put bulk output
        public TextWriter fileoptr;

        /// \param prmpt is the base command line prompt
        /// \param os is the base stream to write output to
        /// \param mxhist is the maximum number of lines to store in history
        public IfaceStatus(string prmpt, TextWriter os, int mxhist = 10)
        {
            optr = os;
            fileoptr = optr;        // Bulk out, defaults to command line output
            sorted = false;
            inerror = false;
            errorisdone = false;
            done = false;
            prompt = prmpt;
            maxhistory = mxhist;
            curhistory = 0;
        }

        ~IfaceStatus()
        {
            if (optr != fileoptr) {
                fileoptr.Close();
                // delete fileoptr;
            }
            while (!promptstack.empty())
                popScript();
            //for (int i = 0; i < comlist.size(); ++i)
            //    delete comlist[i];
            //Dictionary<string, IfaceData*>::const_iterator iter;
            //for (iter = datamap.begin(); iter != datamap.end(); ++iter)
            //    if ((*iter).second != (IfaceData*)0)
            //        delete(*iter).second;
        }

        /// Set if processing should terminate on an error
        public void setErrorIsDone(bool val)
        {
            errorisdone = val;
        }

        /// \brief Push a new file on the script stack
        ///
        /// Attempt to open the file, and if we succeed put the open stream onto the script stack.
        /// \param filename is the name of the script file
        /// \param newprompt is the command line prompt to associate with the file
        public void pushScript(string filename, string newprompt)
        {
            TextReader s;
            try { s = new StreamReader(File.OpenRead(filename)); }
            catch { throw new IfaceParseError($"Unable to open script file: {filename}"); }
            pushScript(s, newprompt);
        }

        /// \brief Provide a new input stream to execute, with an associated command prompt
        ///
        /// The new stream is added to a stack and becomes the primary source for parsing new commands.
        /// Once commands from the stream are exhausted, parsing will resume in the previous stream.
        /// \param iptr is the new input stream
        /// \param newprompt is the command line prompt to associate with the new stream
        public virtual void pushScript(TextReader iptr, string newprompt)
        {
            promptstack.Add(prompt);
            uint flags = 0;
            if (errorisdone)
                flags |= 1;
            flagstack.Add(flags);
            errorisdone = true;     // Abort on first exception in a script
            prompt = newprompt;
        }

        /// \brief Return to processing the parent stream
        ///
        /// The current input stream, as established by a script, is popped from the stack,
        /// along with its command prompt, and processing continues with the previous stream.
        public virtual void popScript()
        {
            prompt = promptstack.GetLastItem();
            promptstack.RemoveLastItem();
            uint flags = flagstack.GetLastItem();
            flagstack.RemoveLastItem();
            errorisdone = ((flags & 1) != 0);
            inerror = false;
        }

        /// Pop any existing script streams and return to processing from the base stream
        public virtual void reset()
        {
            while (!promptstack.empty())
                popScript();
            errorisdone = false;
            done = false;
        }

        /// Get depth of script nesting
        public int getNumInputStreamSize() => promptstack.size();

        /// Write the current command prompt to the current output stream
        public void writePrompt()
        {
            optr.Write(prompt);
        }

        /// \brief Register a command with this interface
        ///
        /// A command object is associated with one or more tokens on the command line.
        /// A string containing up to 5 tokens can be associated with the command.
        ///
        /// \param fptr is the IfaceCommand object
        /// \param nm1 is the first token representing the command
        /// \param nm2 is the second token (or null)
        /// \param nm3 is the third token (or null)
        /// \param nm4 is the fourth token (or null)
        /// \param nm5 is the fifth token (or null)
        public void registerCom(IfaceCommand fptr, string nm1, string? nm2 = null, string? nm3 = null,
            string? nm4 = null, string? nm5 = null)
        {
            fptr.addWord(nm1);
            if (nm2 != null)
                fptr.addWord(nm2);
            if (nm3 != null)
                fptr.addWord(nm3);
            if (nm4 != null)
                fptr.addWord(nm4);
            if (nm5 != null)
                fptr.addWord(nm5);

            comlist.Add(fptr);    // Enter new command
            sorted = false;

            string nm = fptr.getModule(); // Name of module this command belongs to
            IfaceData result;
            IfaceData data;
            if (!datamap.TryGetValue(nm, out result)) {
                data = fptr.createData();
                datamap[nm] = data;
            }
            else
                data = result;
            fptr.setData(this, data);  // Inform command of its data
        }

        /// Get data associated with a IfaceCommand module
        /// Commands (IfaceCommand) are associated with a particular module that has
        /// a formal name and a data object associated with it.  This method
        /// retrieves the module specific data object by name.
        /// \param nm is the name of the module
        /// \return the IfaceData object or null
        public IfaceData? getData(string nm)
        {
            IfaceData result;
            return datamap.TryGetValue(nm, out result) ? result : null;
        }

        /// Run the next command
        /// A single command line is read (via readLine) and executed.
        /// If the command is successfully executed, the command line is
        /// committed to history and \b true is returned.
        /// \return \b true if a command successfully executes
        public bool runCommand()
        {
            string line;            // Next line from input stream

            if (!sorted) {
                comlist.Sort(compare_ifacecommand);
                sorted = true;
            }
            readLine(out line);
            if (line.empty()) return false;
            saveHistory(line);

            List<string> fullcommand = new List<string>();
            IEnumerator<IfaceCommand> first = comlist.GetEnumerator();
            IEnumerator<IfaceCommand> last = comlist.end();
            TextReader @is = new StringReader(line);

            // Try to expand the command
            int match = expandCom(fullcommand, @is, first, last);
            if (match == 0) {
                optr.WriteLine("ERROR: Invalid command");
                return false;
            }
            else if (fullcommand.size() == 0) // Nothing useful typed
                return false;
            else if (match > 1) {
                if (first.Current.numWords() != fullcommand.size()) {
                    // Check for complete but not unique
                    optr.WriteLine("ERROR: Incomplete command");
                    return false;
                }
            }
            else if (match < 0)
                optr.WriteLine("ERROR: Incomplete command");

            first.Current.execute(@is);  // Try to execute the (first) command
            return true;            // Indicate a command was executed
        }

        /// Get the i-th command line from history
        /// A command line is selected by specifying how many steps in time
        /// to go back through the list of successful command lines.
        /// \param line will hold the selected command line from history
        /// \param i is the number of steps back to go
        public void getHistory(out string line, int i)
        {
            if (i >= history.size()) {
                line = string.Empty;
                // No change to line if history too far back
                return;
            }
            i = curhistory - 1 - i;
            if (i < 0) i += maxhistory;
            line = history[i];
        }

        /// Get the number of command lines in history
        public int getHistorySize() => history.size();

        /// Return \b true if the current stream is finished
        public abstract bool isStreamFinished();

        /// Return \b true if the last command failed
        public bool isInError() => inerror;

        /// Adjust which stream to process based on last error
        // The last command has failed, decide if we are completely abandoning this stream
        public void evaluateError()
        {
            if (errorisdone) {
                optr.WriteLine("Aborting process");
                inerror = true;
                done = true;
                return;
            }
            if (getNumInputStreamSize() != 0) {
                // we have something to pop
                optr.WriteLine($"Aborting {prompt}");
                inerror = true;
                return;
            }
            inerror = false;
        }

        /// Concatenate tokens
        /// Concatenate a list of tokens into a single string, separated by a space character
        public static void wordsToString(out string res, List<string> list)
        {
            res = string.Empty;
            bool firstItem = true;
            foreach (string item in list) {
                if (firstItem) firstItem = false;
                else res += ' ';
                res += item;
            }
        }

        private static bool maxmatch(string res, string op1, string op2)
        {
            // Set res to maximum characters in common
            // at the beginning of op1 and op2
            int len;

            len = (op1.Length < op2.Length) ? op1.Length : op2.Length;

            res = string.Empty;
            for (int i = 0; i < len; ++i) {
                if (op1[i] == op2[i])
                    res += op1[i];
                else
                    return false;
            }
            return true;
        }
    }
}
