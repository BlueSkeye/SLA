﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INDIRECT op-code
    internal class TypeOpIndirect : TypeOp
    {
        public TypeOpIndirect(TypeFactory t)
            : base(t, CPUI_INDIRECT,"[]")

        {
            opflags = PcodeOp::special | PcodeOp::marker | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_INDIRECT, false, true); // Dummy behavior
        }

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
        {
            Datatype* ct;

            if (slot == 0)
                return TypeOp::getInputLocal(op, slot);
            ct = tlst->getTypeCode();
            PcodeOp* iop = PcodeOp::getOpFromConst(op->getIn(1)->getAddr());
            AddrSpace* spc = iop->getAddr().getSpace();
            return tlst->getTypePointer(op->getIn(0)->getSize(), ct, spc->getWordSize()); // Second parameter is code pointer
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int4 inslot, int4 outslot)
        {
            if (op->isIndirectCreation()) return (Datatype*)0;
            if ((inslot == 1) || (outslot == 1)) return (Datatype*)0;
            if ((inslot != -1) && (outslot != -1)) return (Datatype*)0; // Must propagate input <-> output

            Datatype* newtype;
            if (invn->isSpacebase())
            {
                AddrSpace* spc = tlst->getArch()->getDefaultDataSpace();
                newtype = tlst->getTypePointer(alttype->getSize(), tlst->getBase(1, TYPE_UNKNOWN), spc->getWordSize());
            }
            else
                newtype = alttype;
            return newtype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opIndirect(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode::printRaw(s, op->getOut());
            s << " = ";
            if (op->isIndirectCreation())
            {
                s << "[create] ";
            }
            else
            {
                Varnode::printRaw(s, op->getIn(0));
                s << ' ' << getOperatorName(op) << ' ';
            }
            Varnode::printRaw(s, op->getIn(1));
        }
    }
}