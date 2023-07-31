using ghidra;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A jump-table model assisted by pseudo-op directives in the code
    ///
    /// This model looks for a special \e jumpassist pseudo-op near the branch site, which contains
    /// p-code models describing how to parse a jump-table for case labels and addresses.
    /// It views the switch table calculation as a two-stage process:
    ///    - case2index:    convert the switchvar to an index into a table
    ///    - index2address: convert the index to an address
    ///
    /// The pseudo-op holds:
    ///    - the table address, size (number of indices)
    ///    - exemplar p-code for inverting the case2index part of the calculation
    ///    - exemplar p-code for calculating index2address
    internal class JumpAssisted : JumpModel
    {
        /// The \e jumpassist PcodeOp
        private PcodeOp assistOp;
        /// The \e jumpassist p-code models
        private JumpAssistOp userop;
        /// Total number of indices in the table (not including the defaultaddress)
        private int sizeIndices;
        /// The switch variable
        private Varnode switchvn;
        
        public JumpAssisted(JumpTable jt)
            : base(jt)
        {
            assistOp = (PcodeOp)null;
            switchvn = (Varnode)null;
            sizeIndices = 0;
        }

        //  virtual ~JumpAssisted(void);
        
        public override bool isOverride() => false;

        public override int getTableSize() => sizeIndices+1;

        public override bool recoverModel(Funcdata fd, PcodeOp indop, uint matchsize,
            uint maxtablesize)
        {
            // Look for the special "jumpassist" pseudo-op
            Varnode* addrVn = indop.getIn(0);
            if (!addrVn.isWritten()) return false;
            assistOp = addrVn.getDef();
            if (assistOp == (PcodeOp)null) return false;
            if (assistOp.code() != OpCode.CPUI_CALLOTHER) return false;
            if (assistOp.numInput() < 3) return false;
            int index = assistOp.getIn(0).getOffset();
            userop = dynamic_cast<JumpAssistOp*>(fd.getArch().userops.getOp(index));
            if (userop == (JumpAssistOp*)0) return false;

            switchvn = assistOp.getIn(1);      // The switch variable
            for (int i = 2; i < assistOp.numInput(); ++i)
                if (!assistOp.getIn(i).isConstant())
                    return false;               // All remaining params must be constant
            if (userop.getCalcSize() == -1)        // If no size script, first param after switch var is size
                sizeIndices = assistOp.getIn(2).getOffset();
            else
            {
                ExecutablePcode* pcodeScript = (ExecutablePcode*)fd.getArch().pcodeinjectlib.getPayload(userop.getCalcSize());
                List<ulong> inputs;
                int numInputs = assistOp.numInput() - 1;  // How many remaining varnodes after useropid
                if (pcodeScript.sizeInput() != numInputs)
                    throw new LowlevelError(userop.getName() + ": <size_pcode> has wrong number of parameters");
                for (int i = 0; i < numInputs; ++i)
                    inputs.Add(assistOp.getIn(i + 1).getOffset());
                sizeIndices = pcodeScript.evaluate(inputs);
            }
            if (matchsize != 0 && matchsize - 1 != sizeIndices) // matchsize has 1 added to it for the default case
                return false;           // Not matching the size we saw previously
            if (sizeIndices > maxtablesize)
                return false;

            return true;
        }

        public override void buildAddresses(Funcdata fd, PcodeOp indop, List<Address> addresstable,
            List<LoadTable> loadpoints)
        {
            if (userop.getIndex2Addr() == -1)
                throw new LowlevelError("Final index2addr calculation outside of jumpassist");
            ExecutablePcode* pcodeScript = (ExecutablePcode*)fd.getArch().pcodeinjectlib.getPayload(userop.getIndex2Addr());
            addresstable.clear();

            AddrSpace* spc = indop.getAddr().getSpace();
            List<ulong> inputs;
            int numInputs = assistOp.numInput() - 1;  // How many remaining varnodes after useropid
            if (pcodeScript.sizeInput() != numInputs)
                throw new LowlevelError(userop.getName() + ": <addr_pcode> has wrong number of parameters");
            for (int i = 0; i < numInputs; ++i)
                inputs.Add(assistOp.getIn(i + 1).getOffset());

            ulong mask = ~((ulong)0);
            int bit = fd.getArch().funcptr_align;
            if (bit != 0)
            {
                mask = (mask >> bit) << bit;
            }
            for (int index = 0; index < sizeIndices; ++index)
            {
                inputs[0] = index;
                ulong output = pcodeScript.evaluate(inputs);
                output &= mask;
                addresstable.Add(Address(spc, output));
            }
            ExecutablePcode* defaultScript = (ExecutablePcode*)fd.getArch().pcodeinjectlib.getPayload(userop.getDefaultAddr());
            if (defaultScript.sizeInput() != numInputs)
                throw new LowlevelError(userop.getName() + ": <default_pcode> has wrong number of parameters");
            inputs[0] = 0;
            ulong defaultAddress = defaultScript.evaluate(inputs);
            addresstable.Add(Address(spc, defaultAddress));       // Add default location to end of addresstable
        }

        public override void findUnnormalized(uint maxaddsub, uint maxleftright, uint maxext)
        {
        }

        public override void buildLabels(Funcdata fd, List<Address> addresstable,
            List<ulong> label, JumpModel orig)
        {
            if (((JumpAssisted*)orig).sizeIndices != sizeIndices)
    throw new LowlevelError("JumpAssisted table size changed during recovery");
            if (userop.getIndex2Case() == -1)
            {
                for (int i = 0; i < sizeIndices; ++i)
                    label.Add(i);     // The index is the label
            }
            else
            {
                ExecutablePcode* pcodeScript = (ExecutablePcode*)fd.getArch().pcodeinjectlib.getPayload(userop.getIndex2Case());
                List<ulong> inputs;
                int numInputs = assistOp.numInput() - 1;  // How many remaining varnodes after useropid
                if (numInputs != pcodeScript.sizeInput())
                    throw new LowlevelError(userop.getName() + ": <case_pcode> has wrong number of parameters");
                for (int i = 0; i < numInputs; ++i)
                    inputs.Add(assistOp.getIn(i + 1).getOffset());

                for (int index = 0; index < sizeIndices; ++index)
                {
                    inputs[0] = index;
                    ulong output = pcodeScript.evaluate(inputs);
                    label.Add(output);
                }
            }
            label.Add(0xBAD1ABE1);        // Add fake label to match the defaultAddress
        }

        public override Varnode foldInNormalization(Funcdata fd, PcodeOp indop)
        {
            // Replace all outputs of jumpassist op with switchvn (including BRANCHIND)
            Varnode* outvn = assistOp.getOut();
            list<PcodeOp*>::const_iterator iter = outvn.beginDescend();
            while (iter != outvn.endDescend())
            {
                PcodeOp* op = *iter;
                ++iter;
                fd.opSetInput(op, switchvn, 0);
            }
            fd.opDestroy(assistOp);        // Get rid of the assist op (it has served its purpose)
            return switchvn;
        }

        public override bool foldInGuards(Funcdata fd, JumpTable jump)
        {
            int origVal = jump.getDefaultBlock();
            jump.setLastAsMostCommon();            // Default case is always the last block
            return (origVal != jump.getDefaultBlock());
        }

        public override bool sanityCheck(Funcdata fd, PcodeOp indop, List<Address> addresstable)
            => true;

        public override JumpModel clone(JumpTable jt)
        {
            JumpAssisted* clone = new JumpAssisted(jt);
            clone.userop = userop;
            clone.sizeIndices = sizeIndices;
            return clone;
        }

        public override void clear()
        {
            assistOp = (PcodeOp)null;
            switchvn = (Varnode)null;
        }
    }
}
