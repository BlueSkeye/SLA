using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleLoadVarnode : Rule
    {
        // friend class RuleStoreVarnode;
        /// \brief Return associated space if given Varnode is an \e active spacebase.
        ///
        /// The Varnode should be a spacebase register input to the function or a
        /// constant, and it should get loaded from the correct space.
        /// \param glb is the address space manager
        /// \param vn is the given Varnode
        /// \param spc is the address space being loaded from
        /// \return the associated space or NULL if the Varnode is not of the correct form
        private static AddrSpace? correctSpacebase(Architecture glb, Varnode vn, AddrSpace spc)
        {
            if (!vn.isSpacebase()) return (AddrSpace)null;
            if (vn.isConstant())       // We have a global pseudo spacebase
                return spc;         // Associate with load/stored space
            if (!vn.isInput()) return (AddrSpace)null;
            AddrSpace assoc = glb.getSpaceBySpacebase(vn.getAddr(), vn.getSize());
            if (assoc.getContain() != spc) // Loading off right space?
                return (AddrSpace)null;
            return assoc;
        }

        /// \brief Check if given Varnode is spacebase + a constant
        ///
        /// If it is, pass back the constant and return the associated space
        /// \param glb is the address space manager
        /// \param vn is the given Varnode
        /// \param val is the reference for passing back the constant
        /// \param spc is the space being loaded from
        /// \return the associated space or NULL
        private static AddrSpace? vnSpacebase(Architecture glb, Varnode vn, out ulong val, AddrSpace spc)
        {
            AddrSpace? retspace = correctSpacebase(glb, vn, spc);
            if (retspace != (AddrSpace)null) {
                val = 0;
                return retspace;
            }
            if (!vn.isWritten()) {
                val = 0;
                return (AddrSpace)null;
            }
            PcodeOp op = vn.getDef() ?? throw new BugException();
            if (op.code() != OpCode.CPUI_INT_ADD) {
                val = 0;
                return (AddrSpace)null;
            }
            Varnode vn1 = op.getIn(0);
            Varnode vn2 = op.getIn(1);
            retspace = correctSpacebase(glb, vn1, spc);
            if (retspace != (AddrSpace)null) {
                if (vn2.isConstant()) {
                    val = vn2.getOffset();
                    return retspace;
                }
                val = 0;
                return (AddrSpace)null;
            }
            retspace = correctSpacebase(glb, vn2, spc);
            if (retspace != (AddrSpace)null) {
                if (vn1.isConstant()) {
                    val = vn1.getOffset();
                    return retspace;
                }
            }
            val = 0;
            return (AddrSpace)null;
        }

        /// \brief Check if STORE or LOAD is off of a spacebase + constant
        ///
        /// If so return the associated space and pass back the offset
        /// \param glb is the address space manager
        /// \param op is the STORE or LOAD PcodeOp
        /// \param offoff is a reference to where the offset should get passed back
        /// \return the associated space or NULL
        internal static AddrSpace? checkSpacebase(Architecture glb, PcodeOp op, out ulong offoff)
        {

            Varnode offvn = op.getIn(1);       // Address offset
            AddrSpace loadspace = op.getIn(0).getSpaceFromConst(); // Space being loaded/stored
                                                           // Treat segmentop as part of load/store
            if (offvn.isWritten() && (offvn.getDef().code() == OpCode.CPUI_SEGMENTOP)) {
                offvn = offvn.getDef().getIn(2);
                // If we are looking for a spacebase (i.e. stackpointer)
                // Then currently we COMPLETELY IGNORE the base part of the
                // segment. We assume it is all correct.
                // If the segmentop inner is constant, we are NOT looking
                // for a spacebase, and we do not igore the @base. If the
                // base is also constant, we let RuleSegmentOp reduce
                // the whole segmentop to a constant.  If the base
                // is not constant, we are not ready for a fixed address.
                if (offvn.isConstant()) {
                    offoff = 0;
                    return (AddrSpace)null;
                }
            }
            else if (offvn.isConstant()) {
                // Check for constant
                offoff = offvn.getOffset();
                return loadspace;
            }
            return vnSpacebase(glb, offvn, out offoff, loadspace);
        }

        public RuleLoadVarnode(string g)
            : base(g, 0, "loadvarnode")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleLoadVarnode(getGroup());
        }

        /// \class RuleLoadVarnode
        /// \brief Convert LOAD operations using a constant offset to COPY
        ///
        /// The pointer can either be a constant offset into the LOAD's specified address space,
        /// or it can be a \e spacebase register plus an offset, in which case it points into
        /// the \e spacebase register's address space.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_LOAD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int size;
            Varnode newvn;
            ulong offoff;

            AddrSpace? baseoff = checkSpacebase(data.getArch(), op, out offoff);
            if (baseoff == (AddrSpace)null) return 0;

            size = op.getOut().getSize();
            offoff = AddrSpace.addressToByte(offoff, baseoff.getWordSize());
            newvn = data.newVarnode(size, baseoff, offoff);
            data.opSetInput(op, newvn, 0);
            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            Varnode refvn = op.getOut();
            if (refvn.isSpacebasePlaceholder()) {
                refvn.clearSpacebasePlaceholder(); // Clear the trigger
                PcodeOp? placeOp = refvn.loneDescend();
                if (placeOp != (PcodeOp)null) {
                    FuncCallSpecs fc = data.getCallSpecs(placeOp);
                    if (fc != (FuncCallSpecs)null)
                        fc.resolveSpacebaseRelative(data, refvn);
                }
            }
            return 1;
        }
    }
}
