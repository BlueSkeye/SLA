using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleStructOffset0 : Rule
    {
        public RuleStructOffset0(string g)
            : base(g, 0, "structoffset0")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleStructOffset0(getGroup());
        }

        /// \class RuleStructOffset0
        /// \brief Convert a LOAD or STORE to the first element of a structure to a PTRSUB.
        ///
        /// Data-type propagation may indicate we have a pointer to a structure, but
        /// we really need a pointer to the first element of the structure. We can tell
        /// this is happening if we load or store too little data from the pointer, interpreting
        /// it as a pointer to the structure.  This Rule then applies a PTRSUB(,0) to the pointer
        /// to drill down to the first component.
        public override void getOpList(List<OpCode> oplist)
        {
            OpCode[] list = { OpCode.CPUI_LOAD, OpCode.CPUI_STORE };
            oplist.AddRange(list);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int movesize;          // Number of bytes being moved by load or store

            if (!data.hasTypeRecoveryStarted()) return 0;
            if (op.code() == OpCode.CPUI_LOAD) {
                movesize = op.getOut().getSize();
            }
            else if (op.code() == OpCode.CPUI_STORE) {
                movesize = op.getIn(2).getSize();
            }
            else
                return 0;

            Varnode ptrVn = op.getIn(1);
            Datatype ct = ptrVn.getTypeReadFacing(op);
            if (ct.getMetatype() != type_metatype.TYPE_PTR) return 0;
            Datatype baseType = ((TypePointer)ct).getPtrTo();
            ulong offset = 0;
            if (ct.isFormalPointerRel() && ((TypePointerRel)ct).evaluateThruParent(0)) {
                TypePointerRel ptRel = (TypePointerRel)ct;
                baseType = ptRel.getParent();
                if (baseType.getMetatype() != type_metatype.TYPE_STRUCT)
                    return 0;
                int iOff = ptRel.getPointerOffset();
                iOff = AddrSpace.addressToByteInt(iOff, ptRel.getWordSize());
                if (iOff >= baseType.getSize())
                    return 0;
                offset = (ulong)iOff;
            }
            if (baseType.getMetatype() == type_metatype.TYPE_STRUCT) {
                if (baseType.getSize() < movesize)
                    return 0;               // Moving something bigger than entire structure
                Datatype? subType = baseType.getSubType(offset, out offset); // Get field at pointer's offset
                if (subType == (Datatype)null) return 0;
                if (subType.getSize() < movesize) return 0;    // Subtype is too small to handle LOAD/STORE
                //    if (baseType.getSize() == movesize) {
                // If we reach here, move is same size as the structure, which is the same size as
                // the first element.
                //    }
            }
            else if (baseType.getMetatype() == type_metatype.TYPE_ARRAY) {
                if (baseType.getSize() < movesize)
                    return 0;               // Moving something bigger than entire array
                if (baseType.getSize() == movesize) {
                    // Moving something the size of entire array
                    if (((TypeArray)baseType).numElements() != 1)
                        return 0;
                    // If we reach here, moving something size of single element. Assume this is normal access.
                }
            }
            else
                return 0;

            PcodeOp newop = data.newOpBefore(op, OpCode.CPUI_PTRSUB, ptrVn, data.newConstant(ptrVn.getSize(), 0));
            if (ptrVn.getType().needsResolution())
                data.inheritResolution(ptrVn.getType(), newop, 0, op, 1);
            newop.setStopTypePropagation();
            data.opSetInput(op, newop.getOut(), 1);
            return 1;
        }
    }
}
