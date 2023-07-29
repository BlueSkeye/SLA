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
        private List<uint4> flagstack = new List<uint4>();
        /// The current command prompt
        private string prompt;
        /// Maximum number of command lines to store in history
        private int4 maxhistory;
        /// Most recent history
        private int4 curhistory;
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
        /// \param first will hold an iterator to the first command in the range
        /// \param last will hold an iterator (one after) the last command in the range
        /// \param input is the list of command tokens to match on
        private void restrictCom(List<IfaceCommand>::const_iterator first,
            List<IfaceCommand>::const_iterator last, List<string> input)
        {
            List<IfaceCommand*>::const_iterator newfirst, newlast;
            IfaceCommandDummy dummy;

            dummy.addWords(input);
            newfirst = lower_bound(first, last, &dummy, compare_ifacecommand);
            dummy.removeWord();
            string temp(input.back() ); // Make copy of last word
            temp[temp.size() - 1] += 1; // temp will now be greater than any word
                                        // whose first letters match input.back()
            dummy.addWord(temp);
            newlast = upper_bound(first, last, &dummy, compare_ifacecommand);
            first = newfirst;
            last = newlast;
        }

        /// \brief Read the next command line
        /// \param line is filled in with the next command to execute
        protected abstract void readLine(string line);

        /// Store the given command line into \e history
        /// The line is saved in a circular history buffer
        /// \param line is the command line to save
        private void saveHistory(string line)
        {
            if (history.size() < maxhistory)
                history.push_back(line);
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
        /// \param first will hold the beginning of the matching range of commands
        /// \param last will hold the end of the matching range of commands
        /// \return the number of matching commands
        protected int4 expandCom(List<string> expand, TextReader s,
              List<IfaceCommand>::const_iterator first,
              List<IfaceCommand>::const_iterator last)
        {
            int4 pos;           // Which word are we currently expanding
            string tok;
            bool res;

            expand.clear();     // Make sure command list is empty
            res = true;
            if (first == last)      // If subrange is empty, return 0
                return 0;
            for (pos = 0; ; ++pos)
            {
                s >> ws;            // Skip whitespace
                if (first == (last - 1))
                {   // If subrange is unique
                    if (s.eof())        // If no more input
                        for (; pos < (*first).numWords(); ++pos) // Automatically provide missing words
                            expand.push_back((*first).getCommandWord(pos));
                    if ((*first).numWords() == pos) // If all words are matched
                        return 1;       // Finished
                }
                if (!res)
                {           // Last word was ambiguous
                    if (!s.eof())
                        return (last - first);
                    return (first - last);  // Negative number to indicate last word incomplete
                }
                if (s.eof())
                {       // if no other words
                    if (expand.empty())
                        return (first - last);
                    return (last - first);  // return number of matches
                }
                s >> tok;           // Get next token
                expand.push_back(tok);
                restrictCom(first, last, expand);
                if (first == last)      // If subrange is empty, return 0
                    return 0;
                res = maxmatch(tok, (*first).getCommandWord(pos), (*(last - 1)).getCommandWord(pos));
                expand.back() = tok;
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
        public IfaceStatus(string prmpt, TextWriter os, int4 mxhist = 10)
        {
            optr = &os;
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
            if (optr != fileoptr)
            {
                ((ofstream*)fileoptr).close();
                delete fileoptr;
            }
            while (!promptstack.empty())
                popScript();
            for (int4 i = 0; i < comlist.size(); ++i)
                delete comlist[i];
            map<string, IfaceData*>::const_iterator iter;
            for (iter = datamap.begin(); iter != datamap.end(); ++iter)
                if ((*iter).second != (IfaceData*)0)
                    delete(*iter).second;
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
            ifstream* s = new ifstream(filename.c_str());
            if (!*s)
                throw IfaceParseError("Unable to open script file: " + filename);
            pushScript(s, newprompt);
        }

        /// \brief Provide a new input stream to execute, with an associated command prompt
        ///
        /// The new stream is added to a stack and becomes the primary source for parsing new commands.
        /// Once commands from the stream are exhausted, parsing will resume in the previous stream.
        /// \param iptr is the new input stream
        /// \param newprompt is the command line prompt to associate with the new stream
        public override void pushScript(TextReader iptr, string newprompt)
        {
            promptstack.push_back(prompt);
            uint4 flags = 0;
            if (errorisdone)
                flags |= 1;
            flagstack.push_back(flags);
            errorisdone = true;     // Abort on first exception in a script
            prompt = newprompt;
        }

        /// \brief Return to processing the parent stream
        ///
        /// The current input stream, as established by a script, is popped from the stack,
        /// along with its command prompt, and processing continues with the previous stream.
        public override void popScript()
        {
            prompt = promptstack.back();
            promptstack.pop_back();
            uint4 flags = flagstack.back();
            flagstack.pop_back();
            errorisdone = ((flags & 1) != 0);
            inerror = false;
        }

        /// Pop any existing script streams and return to processing from the base stream
        public override void reset()
        {
            while (!promptstack.empty())
                popScript();
            errorisdone = false;
            done = false;
        }

        /// Get depth of script nesting
        public int4 getNumInputStreamSize() => promptstack.size();

        /// Write the current command prompt to the current output stream
        public void writePrompt()
        {
            *optr << prompt;
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
        public void registerCom(IfaceCommand fptr, string nm1, string nm2 = null, string nm3 = null,
            string nm4 = null, string nm5 = null)
        {
            fptr.addWord(nm1);
            if (nm2 != (char*)0)
                fptr.addWord(nm2);
            if (nm3 != (char*)0)
                fptr.addWord(nm3);
            if (nm4 != (char*)0)
                fptr.addWord(nm4);
            if (nm5 != (char*)0)
                fptr.addWord(nm5);

            comlist.push_back(fptr);    // Enter new command
            sorted = false;

            string nm(fptr.getModule()); // Name of module this command belongs to
            map<string, IfaceData*>::const_iterator iter = datamap.find(nm);
            IfaceData* data;
            if (iter == datamap.end())
            {
                data = fptr.createData();
                datamap[nm] = data;
            }
            else
                data = (*iter).second;
            fptr.setData(this, data);  // Inform command of its data
        }

        /// Get data associated with a IfaceCommand module
        /// Commands (IfaceCommand) are associated with a particular module that has
        /// a formal name and a data object associated with it.  This method
        /// retrieves the module specific data object by name.
        /// \param nm is the name of the module
        /// \return the IfaceData object or null
        public IfaceData getData(string nm)
        {
            map<string, IfaceData*>::const_iterator iter = datamap.find(nm);
            if (iter == datamap.end())
                return (IfaceData*)0;
            return (*iter).second;
        }

        /// Run the next command
        /// A single command line is read (via readLine) and executed.
        /// If the command is successfully executed, the command line is
        /// committed to history and \b true is returned.
        /// \return \b true if a command successfully executes
        public bool runCommand()
        {
            string line;            // Next line from input stream

            if (!sorted)
            {
                sort(comlist.begin(), comlist.end(), compare_ifacecommand);
                sorted = true;
            }
            readLine(line);
            if (line.empty()) return false;
            saveHistory(line);

            List<string> fullcommand;
            List<IfaceCommand*>::const_iterator first = comlist.begin();
            List<IfaceCommand*>::const_iterator last = comlist.end();
            istringstream is (line);
            int4 match;

            match = expandCom(fullcommand, is, first, last); // Try to expand the command
            if (match == 0)
            {
                *optr << "ERROR: Invalid command" << endl;
                return false;
            }
            else if (fullcommand.size() == 0) // Nothing useful typed
                return false;
            else if (match > 1)
            {
                if ((*first).numWords() != fullcommand.size())
                { // Check for complete but not unique
                    *optr << "ERROR: Incomplete command" << endl;
                    return false;
                }
            }
            else if (match < 0)
                *optr << "ERROR: Incomplete command" << endl;

            (*first).execute(is);  // Try to execute the (first) command
            return true;            // Indicate a command was executed
        }

        /// Get the i-th command line from history
        /// A command line is selected by specifying how many steps in time
        /// to go back through the list of successful command lines.
        /// \param line will hold the selected command line from history
        /// \param i is the number of steps back to go
        public void getHistory(string line, int4 i)
        {
            if (i >= history.size())
                return; // No change to line if history too far back

            i = curhistory - 1 - i;
            if (i < 0) i += maxhistory;
            line = history[i];
        }

        /// Get the number of command lines in history
        public int4 getHistorySize() => history.size();

        /// Return \b true if the current stream is finished
        public abstract bool isStreamFinished();

        /// Return \b true if the last command failed
        public bool isInError() => inerror;

        /// Adjust which stream to process based on last error
        // The last command has failed, decide if we are completely abandoning this stream
        public void evaluateError()
        {
            if (errorisdone)
            {
                *optr << "Aborting process" << endl;
                inerror = true;
                done = true;
                return;
            }
            if (getNumInputStreamSize() != 0)
            { // we have something to pop
                *optr << "Aborting " << prompt << endl;
                inerror = true;
                return;
            }
            inerror = false;
        }

        /// Concatenate tokens
        /// Concatenate a list of tokens into a single string, separated by a space character
        public static void wordsToString(string res, List<string> list)
        {
            List<string>::const_iterator iter;

            res.erase();
            for (iter = list.begin(); iter != list.end(); ++iter)
            {
                if (iter != list.begin())
                    res += ' ';
                res += *iter;
            }
        }

        private static bool maxmatch(string res, string op1, string op2)
        {               // Set res to maximum characters in common
                        // at the beginning of op1 and op2
            int4 len;

            len = (op1.size() < op2.size()) ? op1.size() : op2.size();

            res.erase();
            for (int4 i = 0; i < len; ++i) {
                if (op1[i] == op2[i])
                    res += op1[i];
                else
                    return false;
            }
            return true;
        }
    }
}
