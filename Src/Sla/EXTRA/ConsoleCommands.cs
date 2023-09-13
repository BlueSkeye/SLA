using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief A console command run as part of a test sequence
    internal class ConsoleCommands : IfaceStatus
    {
        // Sequence of commands
        private List<string> commands = new List<string>();
        // Position of next command to execute
        private int pos;
        
        protected override void readLine(out string line)
        {
            if (pos >= commands.size()) {
                line = string.Empty;
                return;
            }
            line = commands[pos];
            pos += 1;
        }

        /// \param s is the stream where command output is printed
        /// \param comms is the list of commands to be issued
        public ConsoleCommands(TextWriter s, List<string> comms)
            : base("> ", s)
        {
            commands = new List<string>(comms);
            pos = 0;
            IfaceCapability.registerAllCommands(this);
        }

        ///< Reset console for a new program
        public override void reset()
        {
            pos = 0;
            inerror = false;
            done = false;
        }

        public override bool isStreamFinished() => (pos == commands.size());
    }
}
