using System;
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
    internal class RulePtrsubCharConstant : Rule
    {
        /// \brief Try to push constant pointer further
        ///
        /// Given a PTRSUB has been collapsed to a constant COPY of a string address,
        /// try to collapse descendant any PTRADD.
        /// \param data is the function being analyzed
        /// \param outtype is the data-type associated with the constant
        /// \param op is the putative descendant PTRADD
        /// \param slot is the input slot receiving the collapsed PTRSUB
        /// \param val is the constant pointer value
        /// \return \b true if the descendant was collapsed
        private bool pushConstFurther(Funcdata data, TypePointer outtype, PcodeOp op, int slot, ulong val)
        {
            if (op.code() != CPUI_PTRADD) return false;        // Must be a PTRADD
            if (slot != 0) return false;
            Varnode* vn = op.getIn(1);
            if (!vn.isConstant()) return false;            // that is adding a constant
            ulong addval = vn.getOffset();
            addval *= op.getIn(2).getOffset();
            val += addval;
            Varnode* newconst = data.newConstant(vn.getSize(), val);
            newconst.updateType(outtype, false, false);        // Put the pointer datatype on new constant
            data.opRemoveInput(op, 2);
            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, CPUI_COPY);
            data.opSetInput(op, newconst, 0);
            return true;
        }

        public RulePtrsubCharConstant(string g)
            : base(g, 0, "ptrsubcharconstant")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePtrsubCharConstant(getGroup());
        }

        /// \class RulePtrsubCharConstant
        /// \brief Cleanup: Set-up to print string constants
        ///
        /// If a SUBPIECE refers to a global symbol, the output of the SUBPIECE is a (char *),
        /// and the address is read-only, then get rid of the SUBPIECE in favor
        /// of printing a constant string.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_PTRSUB);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* sb = op.getIn(0);
            Datatype* sbType = sb.getTypeReadFacing(op);
            if (sbType.getMetatype() != TYPE_PTR) return 0;
            TypeSpacebase* sbtype = (TypeSpacebase*)((TypePointer*)sbType).getPtrTo();
            if (sbtype.getMetatype() != TYPE_SPACEBASE) return 0;
            Varnode* vn1 = op.getIn(1);
            if (!vn1.isConstant()) return 0;
            Varnode* outvn = op.getOut();
            TypePointer* outtype = (TypePointer*)outvn.getTypeDefFacing();
            if (outtype.getMetatype() != TYPE_PTR) return 0;
            Datatype* basetype = outtype.getPtrTo();
            if (!basetype.isCharPrint()) return 0;
            Address symaddr = sbtype.getAddress(vn1.getOffset(), vn1.getSize(), op.getAddr());
            Scope* scope = sbtype.getMap();
            if (!scope.isReadOnly(symaddr, 1, op.getAddr()))
                return 0;
            // Check if data at the address looks like a string
            if (!data.getArch().stringManager.isString(symaddr, basetype))
                return 0;

            // If we reach here, the PTRSUB should be converted to a (COPY of a) pointer constant.
            bool removeCopy = false;
            if (!outvn.isAddrForce())
            {
                removeCopy = true;      // Assume we can remove, unless we can't propagate to all descendants
                list<PcodeOp*>::const_iterator iter, enditer;
                iter = outvn.beginDescend();
                enditer = outvn.endDescend();
                while (iter != enditer)
                {
                    PcodeOp* subop = *iter; // Give each descendant of op a chance to further propagate the constant
                    ++iter;
                    if (!pushConstFurther(data, outtype, subop, subop.getSlot(outvn), vn1.getOffset()))
                        removeCopy = false; // If the descendant does NOT propagate const, do NOT remove op
                }
            }
            if (removeCopy)
            {
                data.opDestroy(op);
            }
            else
            {   // Convert the original PTRSUB to a COPY of the constant
                Varnode* newvn = data.newConstant(outvn.getSize(), vn1.getOffset());
                newvn.updateType(outtype, false, false);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, newvn, 0);
                data.opSetOpcode(op, CPUI_COPY);
            }
            return 1;
        }
    }
}
