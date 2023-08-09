using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A breakpoint object
    /// This is a base class for breakpoint objects in an emulator.  The breakpoints are implemented
    /// as callback method, which is overridden for the particular behavior needed by the emulator.
    /// Each derived class must override either
    ///   - pcodeCallback()
    ///   - addressCallback()
    /// depending on whether the breakpoint is tailored for a particular pcode op or for
    /// a machine address.
    internal class BreakCallBack
    {
        /// The emulator currently associated with this breakpoint
        protected Emulate? emulate;

        /// The emulator currently associated with this breakpoint
        /// The base breakpoint needs no initialization parameters, the setEmulate() method must be
        /// called before the breakpoint can be invoked
        public BreakCallBack()
        {
            emulate = null;
        }

        ~BreakCallBack()
        {
        }

        /// Call back method for pcode based breakpoints
        /// This routine is invoked during emulation, if this breakpoint has somehow been associated with
        /// this kind of pcode op.  The callback can perform any operation on the emulator context it wants.
        /// It then returns \b true if these actions are intended to replace the action of the pcode op itself.
        /// Or it returns \b false if the pcode op should still have its normal effect on the emulator context.
        /// \param op is the particular pcode operation where the break occurs.
        /// \return \b true if the normal pcode op action should not occur
        public virtual bool pcodeCallback(PcodeOpRaw op)
        {
            return true;
        }

        /// Call back method for address based breakpoints
        /// This routine is invoked during emulation, if this breakpoint has somehow been associated with
        /// this address.  The callback can perform any operation on the emulator context it wants. It then
        /// returns \b true if these actions are intended to replace the action of the \b entire machine
        /// instruction at this address. Or it returns \b false if the machine instruction should still be
        /// executed normally.
        /// \param addr is the address where the break has occurred
        /// \return \b true if the machine instruction should not be executed
        public virtual bool addressCallback(Address addr)
        {
            return true;
        }

        /// Associate a particular emulator with this breakpoint
        /// Breakpoints can be associated with one emulator at a time.
        /// \param emu is the emulator to associate this breakpoint with
        public void setEmulate(Emulate emu)
        {
            emulate = emu;
        }
    }
}
