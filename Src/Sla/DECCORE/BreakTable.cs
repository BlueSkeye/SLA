using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A collection of breakpoints for the emulator
    ///
    /// A BreakTable keeps track of an arbitrary number of breakpoints for an emulator.
    /// Breakpoints are either associated with a particular user-defined pcode op,
    /// or with a specific machine address (as in a standard debugger). Through the BreakTable
    /// object, an emulator can invoke breakpoints through the two methods
    ///  - doPcodeOpBreak()
    ///  - doAddressBreak()
    ///
    /// depending on the type of breakpoint they currently want to invoke
    internal abstract class BreakTable
    {
        ~BreakTable()
        {
        }

        /// \brief Associate a particular emulator with breakpoints in this table
        /// Breakpoints may need access to the context in which they are invoked. This
        /// routine provides the context for all breakpoints in the table.
        /// \param emu is the Emulate context
        public abstract void setEmulate(Emulate emu);

        /// \brief Invoke any breakpoints associated with this particular pcodeop
        /// Within the table, the first breakpoint which is designed to work with this particular
        /// kind of pcode operation is invoked.  If there was a breakpoint and it was designed
        /// to \e replace the action of the pcode op, then \b true is returned.
        /// \param curop is the instance of a pcode op to test for breakpoints
        /// \return \b true if the action of the pcode op is performed by the breakpoint
        public abstract bool doPcodeOpBreak(PcodeOpRaw curop);

        /// \brief Invoke any breakpoints associated with this machine address
        /// Within the table, the first breakpoint which is designed to work with at this address
        /// is invoked.  If there was a breakpoint, and if it was designed to \e replace
        /// the action of the machine instruction, then \b true is returned.
        /// \param addr is address to test for breakpoints
        /// \return \b true if the machine instruction has been replaced by a breakpoint
        public abstract bool doAddressBreak(Address addr);
    }
}
