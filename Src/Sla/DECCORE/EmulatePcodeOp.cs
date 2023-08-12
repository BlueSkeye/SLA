using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Emulation based on (existing) PcodeOps and Varnodes.
    /// This is still an abstract class.  It does most of the work of emulating
    /// p-code using PcodeOp and Varnode objects (as opposed to PcodeOpRaw and VarnodeData).
    /// This class leaves implementation of control-flow to the derived class. This class
    /// implements most operations by going through new virtual methods:
    ///    - getVarnodeValue()
    ///    - setVarnodeValue()
    ///    - getLoadImageValue()
    ///
    /// The default executeLoad() implementation pulls values from the underlying LoadImage
    /// object. The following p-code ops are provided \e NULL implementations, as some tasks
    /// don't need hard emulation of them:
    ///   - STORE
    ///   - CPOOLREF
    ///   - NEW
    internal abstract class EmulatePcodeOp : Emulate
    {
        protected Architecture glb;        ///< The underlying Architecture for the program being emulated
        protected PcodeOp currentOp;     ///< Current PcodeOp being executed
        protected PcodeOp lastOp;        ///< Last PcodeOp that was executed

        /// \brief Pull a value from the load-image given a specific address
        /// A contiguous chunk of memory is pulled from the load-image and returned as a
        /// constant value, respecting the endianess of the address space. The default implementation
        /// of this method pulls the value directly from the LoadImage object.
        /// \param spc is the address space to pull the value from
        /// \param offset is the starting address offset (from within the space) to pull the value from
        /// \param sz is the number of bytes to pull from memory
        /// \return indicated bytes arranged as a constant value
        protected virtual ulong getLoadImageValue(AddrSpace spc, ulong offset, int sz)
        {
            LoadImage loadimage = glb.loader ?? throw new BugException();
            ulong res;

            loadimage.loadFill((byte*)&res, sizeof(ulong), new Address(spc, offset));

            if ((HOST_ENDIAN == 1) != spc.isBigEndian())
                res = Globals.byte_swap(res, sizeof(ulong));
            if (spc.isBigEndian() && (sz < sizeof(ulong)))
                res >>= (sizeof(ulong) - sz) * 8;
            else
                res &= Globals.calc_mask((uint)sz);
            return res;
        }

        protected override void executeUnary()
        {
            ulong in1 = getVarnodeValue(currentOp.getIn(0));
            ulong @out = currentBehave.evaluateUnary(currentOp.getOut().getSize(),
                                 currentOp.getIn(0).getSize(), in1);
            setVarnodeValue(currentOp.getOut(), @out);
        }

        protected override void executeBinary()
        {
            ulong in1 = getVarnodeValue(currentOp.getIn(0));
            ulong in2 = getVarnodeValue(currentOp.getIn(1));
            ulong @out = currentBehave.evaluateBinary(currentOp.getOut().getSize(),
                                  currentOp.getIn(0).getSize(), in1, in2);
            setVarnodeValue(currentOp.getOut(), @out);
        }

        protected override void executeLoad()
        {
            // op will be null, use current_op
            ulong off = getVarnodeValue(currentOp.getIn(1));
            AddrSpace spc = currentOp.getIn(0).getSpaceFromConst();
            off = AddrSpace.addressToByte(off, spc.getWordSize());
            int sz = currentOp.getOut().getSize();
            ulong res = getLoadImageValue(spc, off, sz);
            setVarnodeValue(currentOp.getOut(), res);
        }

        protected override void executeStore()
        {
            // There is currently nowhere to store anything since the memstate is null
            //  ulong val = getVarnodeValue(current_op.getIn(2)); // Value being stored
            //  ulong off = getVarnodeValue(current_op.getIn(1));
            //  AddrSpace *spc = current_op.getIn(0).getSpaceFromConst();
        }

        //  virtual void executeBranch(void)=0;
        protected override bool executeCbranch()
        {
            // op will be null, use current_op
            ulong cond = getVarnodeValue(currentOp.getIn(1));
            // We must take into account the booleanflip bit with pcode from the syntax tree
            return ((cond != 0) != currentOp.isBooleanFlip());
        }

        //  virtual void executeBranchind(void)=0;
        //  virtual void executeCall(void)=0;
        //  virtual void executeCallind(void)=0;
        //  virtual void executeCallother(void)=0;

        protected override void executeMultiequal()
        {
            // op will be null, use current_op
            int i;
            FlowBlock bl = currentOp.getParent();
            FlowBlock last_bl = lastOp.getParent();

            for (i = 0; i < bl.sizeIn(); ++i)
                if (bl.getIn(i) == last_bl) break;
            if (i == bl.sizeIn())
                throw new LowlevelError("Could not execute MULTIEQUAL");
            ulong val = getVarnodeValue(currentOp.getIn(i));
            setVarnodeValue(currentOp.getOut(), val);
        }

        protected override void executeIndirect()
        {
            // We could probably safely ignore this in the
            // context we are using it (jumptable recovery)
            // But we go ahead and assume it is equivalent to copy
            ulong val = getVarnodeValue(currentOp.getIn(0));
            setVarnodeValue(currentOp.getOut(), val);
        }

        protected override void executeSegmentOp()
        {
            SegmentOp segdef = glb.userops.getSegmentOp(currentOp.getIn(0).getSpaceFromConst().getIndex());
            if (segdef == (SegmentOp)null)
                throw new LowlevelError("Segment operand missing definition");

            ulong in1 = getVarnodeValue(currentOp.getIn(1));
            ulong in2 = getVarnodeValue(currentOp.getIn(2));
            List<ulong> bindlist = new List<ulong>();
            bindlist.Add(in1);
            bindlist.Add(in2);
            ulong res = segdef.execute(bindlist);
            setVarnodeValue(currentOp.getOut(), res);
        }

        protected override void executeCpoolRef()
        {
            // Ignore references to constant pool
        }

        protected override void executeNew()
        {
            // Ignore new operations
        }

        //  virtual void fallthruOp(void)=0;

        /// \param g is the Architecture providing the LoadImage
        public EmulatePcodeOp(Architecture g)
        {
            glb = g;
            currentOp = (PcodeOp)null;
            lastOp = (PcodeOp)null;
        }

        /// \brief Establish the current PcodeOp being emulated
        /// \param op is the PcodeOp that will next be executed via executeCurrentOp()
        public void setCurrentOp(PcodeOp op)
        {
            currentOp = op;
            currentBehave = op.getOpcode().getBehavior();
        }

        public override Address getExecuteAddress() => currentOp.getAddr();

        /// \brief Given a specific Varnode, set the given value for it in the current machine state
        /// This is the placeholder internal operation for setting a Varnode value during emulation.
        /// The value is \e stored using the Varnode as the \e address and \e storage \e size.
        /// \param vn is the specific Varnode
        /// \param val is the constant value to store
        public abstract void setVarnodeValue(Varnode vn, ulong val);

        /// \brief Given a specific Varnode, retrieve the current value for it from the machine state
        /// This is the placeholder internal operation for obtaining a Varnode value during emulation.
        /// The value is \e loaded using the Varnode as the \e address and \e storage \e size.
        /// \param vn is the specific Varnode
        /// \return the corresponding value from the machine state
        public abstract ulong getVarnodeValue(Varnode vn);
    }
}
