using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief A basic instantiation of a breakpoint table
    ///
    /// This object allows breakpoints to registered in the table via either
    ///   - registerPcodeCallback()  or
    ///   = registerAddressCallback()
    ///
    /// Breakpoints are stored in map containers, and the core BreakTable methods
    /// are implemented to search in these containers
    internal class BreakTableCallBack : BreakTable
    {
        /// The emulator associated with this table
        private Emulate? emulate;
        /// The translator 
        private Translate trans;
        /// a container of pcode based breakpoints
        private Dictionary<Address, BreakCallBack> addresscallback;
        /// a container of addressed based breakpoints
        private Dictionary<ulong, BreakCallBack> pcodecallback;

        /// Basic breaktable constructor
        /// The break table needs a translator object so user-defined pcode ops can be registered against
        /// by name.
        /// \param t is the translator object
        public BreakTableCallBack(Translate t)
        {
            emulate = null;
            trans = t;
        }

        /// Register a pcode based breakpoint
        /// Any time the emulator is about to execute a user-defined pcode op with the given name,
        /// the indicated breakpoint is invoked first. The break table does \e not assume responsibility
        /// for freeing the breakpoint object.
        /// \param name is the name of the user-defined pcode op
        /// \param func is the breakpoint object to associate with the pcode op
        public void registerPcodeCallback(string name, BreakCallBack func)
        {
            func.setEmulate(emulate);
            List<string> userops;
            trans.getUserOpNames(userops);
            for (int i = 0; i < userops.size(); ++i) {
                if (userops[i] == name) {
                    pcodecallback[(ulong)i] = func;
                    return;
                }
            }
            throw new LowlevelError($"Bad userop name: {name}");
        }

        /// Register an address based breakpoint
        /// Any time the emulator is about to execute (the pcode translation of) a particular machine
        /// instruction at this address, the indicated breakpoint is invoked first. The break table
        /// does \e not assume responsibility for freeing the breakpoint object.
        /// \param addr is the address associated with the breakpoint
        /// \param func is the breakpoint being registered
        public void registerAddressCallback(Address addr, BreakCallBack func)
        {
            func.setEmulate(emulate);
            addresscallback[addr] = func;
        }

        /// Associate an emulator with all breakpoints in the table
        /// This routine invokes the setEmulate method on each breakpoint currently in the table
        /// \param emu is the emulator to be associated with the breakpoints
        public override void setEmulate(Emulate emu)
        {
            // Make sure all callbbacks are aware of new emulator
            emulate = emu;

            foreach (BreakCallBack callBack in addresscallback.Values)
                callBack.setEmulate(emu);
            foreach (BreakCallBack callBack in pcodecallback.Values)
                callBack.setEmulate(emu);
        }

        /// Invoke any breakpoints for the given pcode op
        /// This routine examines the pcode-op based container for any breakpoints associated with the
        /// given op.  If one is found, its pcodeCallback method is invoked.
        /// \param curop is pcode op being checked for breakpoints
        /// \return \b true if the breakpoint exists and returns \b true, otherwise return \b false
        public override bool doPcodeOpBreak(PcodeOpRaw curop)
        {
            ulong val = curop.getInput(0).offset;
            BreakCallBack? callBack;
            return pcodecallback.TryGetValue(val, out callBack) && callBack.pcodeCallback(curop);
        }

        /// Invoke any breakpoints for the given address
        /// This routine examines the address based container for any breakpoints associated with the
        /// given address. If one is found, its addressCallback method is invoked.
        /// \param addr is the address being checked for breakpoints
        /// \return \b true if the breakpoint exists and returns \b true, otherwise return \b false
        public override bool doAddressBreak(Address addr)
        {
            BreakCallBack? callBack;
            return addresscallback.TryGetValue(addr, out callBack) && callBack.addressCallback(addr);
        }
    }
}
