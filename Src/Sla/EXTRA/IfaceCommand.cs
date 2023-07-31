using Sla.EXTRA;
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
    /// \brief A command that can be executed from the command line
    ///
    /// The command has data associated with it (via setData()) and is executed
    /// via the execute() method.  The command can get additional parameters from
    /// the command line by reading the input stream passed to it.
    /// The command is associated with a specific sequence of words (tokens)
    /// that should appear at the start of the command line.
    internal abstract class IfaceCommand
    {
        /// The token sequence associated with the command
        private List<string> com = new List<string>();
        
        ~IfaceCommand()
        {
        }

        /// \brief Associate a specific data object with this command.
        ///
        /// \param root is the interface object this command is registered with
        /// \param data is the data object the command should use
        public abstract void setData(IfaceStatus root, IfaceData data);

        /// Execute this command. Additional state can be read from the given command line stream.
        /// Otherwise, the command gets its data from its registered IfaceData object
        /// \param s is the input stream from the command line
        public abstract void execute(TextReader s);

        /// \brief Get the formal module name to which this command belongs
        ///
        /// Commands in the same module share data through their registered IfaceData object
        /// \return the formal module name
        public abstract string getModule();

        /// \brief Create a specialized data object for \b this command (and its module)
        ///
        /// This method is only called once per module
        /// \return the newly created data object for the module
        public abstract IfaceData createData();

        /// \brief Add a token to the command line string associated with this command
        ///
        /// \param temp is the new token to add
        public void addWord(string temp)
        {
            com.Add(temp);
        }

        /// Remove the last token from the associated command line string
        public void removeWord()
        {
            com.RemoveLastItem();
        }

        ///< Get the i-th command token
        public string getCommandWord(int i) => com[i];

        ///< Add words to the associated command line string
        public void addWords(List<string> wordlist)
        {
            List<string>::const_iterator iter;

            for (iter = wordlist.begin(); iter != wordlist.end(); ++iter)
                com.Add(*iter);
        }

        public int numWords() => com.size();   ///< Return the number of tokens in the command line string

        ///< Get the complete command line string
        /// \param res is overwritten with the full command line string
        public void commandString(string res)
        {
            IfaceStatus::wordsToString(res, com);
        }

        ///< Order two commands by their command line strings
        /// The commands are ordered lexicographically and alphabetically by
        /// the comparing tokens in their respective command line strings
        /// \param op2 is the other command to compare with \b this
        /// \return -1, 0, 1 if \b this is earlier, equal to, or after to the other command
        public int compare(IfaceCommand op2)
        {
            int res;
            List<string>::const_iterator iter1, iter2;

            for (iter1 = com.begin(), iter2 = op2.com.begin(); ; ++iter1, ++iter2)
            {
                if (iter1 == com.end())
                {
                    if (iter2 == op2.com.end())
                        return 0;
                    return -1;      // This is less
                }
                if (iter2 == op2.com.end())
                    return 1;
                res = (*iter1).compare(*iter2);
                if (res != 0)
                    return res;
            }
            return 0;           // Never reaches here
        }

        /// \brief Compare to commands as pointers
        ///
        /// \param a is a pointer to the first command
        /// \param b is a pointer to the second command
        /// \return \b true if the first pointer is ordered before the second
        internal static bool compare_ifacecommand(IfaceCommand a, IfaceCommand b) {
            return (0 > a.compare(*b));
        }
    }
}
