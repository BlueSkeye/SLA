﻿using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the CPOOLREF op-code
    internal class TypeOpCpoolref : TypeOp
    {
        ///< The constant pool container
        private ConstantPool cpool;

        public TypeOpCpoolref(TypeFactory t)
            : base(t, CPUI_CPOOLREF, "cpoolref")
        {
            cpool = t->getArch()->cpool;
            opflags = PcodeOp::special | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_CPOOLREF, false, true); // Dummy behavior
        }

        // Never needs casting
        public override Datatype getOutputLocal(PcodeOp op)
        {
            vector<uintb> refs;
            for (int4 i = 1; i < op->numInput(); ++i)
                refs.push_back(op->getIn(i)->getOffset());
            CPoolRecord* rec = cpool->getRecord(refs);
            if (rec == (CPoolRecord*)0)
                return TypeOp::getOutputLocal(op);
            if (rec->getTag() == CPoolRecord::instance_of)
                return tlst->getBase(1, TYPE_BOOL);
            return rec->getType();
        }

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            return (Datatype*)0;
        }

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
        {
            return tlst->getBase(op->getIn(slot)->getSize(), TYPE_INT);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opCpoolRefOp(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op->getOut() != (Varnode*)0)
            {
                Varnode::printRaw(s, op->getOut());
                s << " = ";
            }
            s << getOperatorName(op);
            vector<uintb> refs;
            for (int4 i = 1; i < op->numInput(); ++i)
                refs.push_back(op->getIn(i)->getOffset());
            CPoolRecord* rec = cpool->getRecord(refs);
            if (rec != (CPoolRecord*)0)
                s << '_' << rec->getToken();
            s << '(';
            Varnode::printRaw(s, op->getIn(0));
            for (int4 i = 2; i < op->numInput(); ++i)
            {
                s << ',';
                Varnode::printRaw(s, op->getIn(i));
            }
            s << ')';
        }
    }
}