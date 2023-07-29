using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ghidra.FuncCallSpecs;

namespace Sla.DECCORE
{
    /// \brief A p-code emitter for building PcodeOp objects
    ///
    /// The emitter is attached to a specific Funcdata object.  Any p-code generated (by FlowInfo typically)
    /// will be instantiated as PcodeOp and Varnode objects and placed in the Funcdata \e dead list.
    internal class PcodeEmitFd : PcodeEmit
    {
        /// The Funcdata container to emit to
        private Funcdata fd;
        
        public override void dump(Address addr, OpCode opc, VarnodeData outvar,
            VarnodeData[] vars, int isize)
        {
            // Convert template data into a real PcodeOp
            PcodeOp* op;
            Varnode* vn;

            if (outvar != (VarnodeData*)0)
            {
                Address oaddr(outvar.space, outvar.offset);
                op = fd.newOp(isize, addr);
                fd.newVarnodeOut(outvar.size, oaddr, op);
            }
            else
                op = fd.newOp(isize, addr);
            fd.opSetOpcode(op, opc);
            int i = 0;
            if (op.isCodeRef())
            { // Is the first input parameter a code reference
                Address addrcode(vars[0].space, vars[0].offset);
                // addrcode.toPhysical()  // For backward compatibility with SLED
                fd.opSetInput(op, fd.newCodeRef(addrcode), 0);
                i += 1;
                // This is handled by FlowInfo
                //    if ((opc==CPUI_CALL)&&(addrcode==pos.getNaddr())) {
                // This is probably PIC code and the call is really a jump
                //      fd.op_setopcode(op,CPUI_BRANCH);
                //    }
            }
            for (; i < isize; ++i)
            {
                vn = fd.newVarnode(vars[i].size, vars[i].space, vars[i].offset);
                fd.opSetInput(op, vn, i);
            }
        }

        ///< Establish the container for \b this emitter
        public void setFuncdata(Funcdata f)
        {
            fd = f;
        }
    }
}
