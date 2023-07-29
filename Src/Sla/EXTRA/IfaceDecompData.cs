using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief Common data shared by decompiler commands
    internal class IfaceDecompData : IfaceData
    {
        public Funcdata fd;     ///< Current function active in the console
        public Architecture conf; ///< Current architecture/program active in the console
        public CallGraph cgraph;  ///< Call-graph information for the program
        public FunctionTestCollection testCollection;     ///< Executable environment from a datatest

#if CPUI_RULECOMPILE
        public string experimental_file;   // File containing experimental rules
#endif
#if OPACTION_DEBUG
        public bool jumptabledebug;
#endif
        public IfaceDecompData()
        {
            conf = (Architecture*)0;
            fd = (Funcdata)null;
            cgraph = (CallGraph*)0;
            testCollection = (FunctionTestCollection*)0;
#if OPACTION_DEBUG
            jumptabledebug = false;
#endif
        }

        ~IfaceDecompData()
        {
            if (cgraph != (CallGraph*)0)
                delete cgraph;
            if (conf != (Architecture*)0)
                delete conf;
            if (testCollection != (FunctionTestCollection*)0)
                delete testCollection;
            // fd will get deleted with Database
        }

        ///< Allocate the call-graph object
        public void allocateCallGraph()
        {
            if (cgraph != (CallGraph*)0)
                delete cgraph;
            cgraph = new CallGraph(conf);
        }

        /// Clear references to current function
        /// This is called if a command throws a low-level error.
        /// It clears any analysis on the function, sets the current function
        /// to null, and issues a warning.
        /// \param s is the stream to write the warning to
        public void abortFunction(TextWriter s)
        {
            if (fd == (Funcdata)null) return;
            s << "Unable to proceed with function: " << fd.getName() << endl;
            conf.clearAnalysis(fd);
            fd = (Funcdata)null;
        }

        /// Free all resources for the current architecture/program
        public void clearArchitecture()
        {
            if (conf != (Architecture*)0)
                delete conf;
            conf = (Architecture*)0;
            fd = (Funcdata)null;
        }

        /// \brief Generate raw p-code for the current function
        ///
        /// Follow flow from the entry point of the function and generate the
        /// raw p-code ops for all instructions, up to \e return instructions.
        /// If a \e size in bytes is provided, it bounds the memory region where flow
        /// can be followed.  Otherwise, a zero \e size allows unbounded flow tracing.
        /// \param s is a output stream for reporting function details or errors
        /// \param size (if non-zero) is the maximum number of bytes to disassemble
        public void followFlow(TextWriter s, int size)
        {
#if OPACTION_DEBUG
            if (jumptabledebug)
                fd.enableJTCallback(jump_callback);
#endif
            try
            {
                if (size == 0)
                {
                    Address baddr = new Address(fd.getAddress().getSpace(),0);
                    Address eaddr = new Address(fd.getAddress().getSpace(), fd.getAddress().getSpace().getHighest());
                    fd.followFlow(baddr, eaddr);
                }
                else
                    fd.followFlow(fd.getAddress(), fd.getAddress() + size);
                s << "Function " << fd.getName() << ": ";
                fd.getAddress().printRaw(s);
                s << endl;
            }
            catch (RecovError err) {
                s << "Function " << fd.getName() << ": " << err.ToString() << endl;
            }
        }

        /// Read a varnode from the given stream
        /// The Varnode is selected from the \e current function.  It is specified as a
        /// storage location with info about its defining p-code in parantheses.
        ///   - `%EAX(r0x10000:0x65)`
        ///   - `%ECX(i)`
        ///   - `r0x10001000:4(:0x96)`
        ///   - `u0x00001100:1(:0x102)`
        ///   - `#0x1(0x10205:0x27)`
        ///
        /// The storage address space is given as the \e short-cut character followed by the
        /// address offset.  For register spaces, the name of the register can be given instead of the
        /// offset.  After the offset, a size can be specified with a ':' followed by the size in bytes.
        /// If size is not provided and there is no register name, a default word size is assigned based
        /// on the address space.
        ///
        /// The defining p-code op is specified either as:
        ///   - An address and sequence number: `%EAX(r0x10000:0x65)`
        ///   - Just a sequence number: `%EAX(:0x65)`  or
        ///   - An "i" token for inputs: `%EAX(i)`
        ///
        /// For a constant Varnode, the storage offset is the actual value of the constant, and
        /// the p-code address and sequence number must both be present and specify the p-code op
        /// that \e reads the constant.
        /// \param s is the given input stream
        /// \return the Varnode object
        public Varnode readVarnode(TextReader s)
        {
            uint uq;
            int defsize;
            Varnode vn = (Varnode)null;

            if (fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            Address pc;
            Address loc = new Address(parse_varnode(s, defsize, pc, uq,* conf.types));
            if (loc.getSpace().getType() == IPTR_CONSTANT)
            {
                if (pc.isInvalid() || (uq == uint.MaxValue))
                    throw new IfaceParseError("Missing p-code sequence number");
                SeqNum seq = new SeqNum(pc, uq);
                PcodeOp op = fd.findOp(seq);
                if (op != (PcodeOp)null)
                {
                    for (int i = 0; i < op.numInput(); ++i)
                    {
                        Varnode tmpvn = op.getIn(i);
                        if (tmpvn.getAddr() == loc)
                        {
                            vn = tmpvn;
                            break;
                        }
                    }
                }
            }
            else if (pc.isInvalid() && (uq == uint.MaxValue))
                vn = fd.findVarnodeInput(defsize, loc);
            else if ((!pc.isInvalid()) && (uq != uint.MaxValue))
                vn = fd.findVarnodeWritten(defsize, loc, pc, uq);
            else
            {
                VarnodeLocSet::const_iterator iter, enditer;
                iter = fd.beginLoc(defsize, loc);
                enditer = fd.endLoc(defsize, loc);
                while (iter != enditer)
                {
                    vn = *iter++;
                    if (vn.isFree()) continue;
                    if (vn.isWritten())
                    {
                        if ((!pc.isInvalid()) && (vn.getDef().getAddr() == pc)) break;
                        if ((uq != uint.MaxValue) && (vn.getDef().getTime() == uq)) break;
                    }
                }
            }

            if (vn == (Varnode)null)
                throw new IfaceExecutionError("Requested varnode does not exist");
            return vn;
        }

        ///< Find a symbol by name
        /// Find any symbols matching the given name in the current scope.  Scope is either the
        /// current function scope if a function is active, otherwise the global scope.
        /// \param name is the given name, either absolute or partial
        /// \param res will hold any matching symbols
        public void readSymbol(string name, List<Symbol> res)
        {
            Scope scope = (fd == (Funcdata)null) ? conf.symboltab.getGlobalScope() : fd.getScopeLocal();
            string basename;
            scope = conf.symboltab.resolveScopeFromSymbolName(name, "::", basename, scope);
            if (scope == (Scope)null)
                throw new IfaceParseError("Bad namespace for symbol: " + name);
            scope.queryByName(basename, res);
        }
    }
}
