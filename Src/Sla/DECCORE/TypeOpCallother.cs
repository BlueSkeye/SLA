﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the CALLOTHER op-code (user defined p-code operations)
    internal class TypeOpCallother : TypeOp
    {
        public TypeOpCallother(TypeFactory t)
        {
            opflags = PcodeOp::special | PcodeOp::call | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_CALLOTHER, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opCallother(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op->getOut() != (Varnode*)0)
            {
                Varnode::printRaw(s, op->getOut());
                s << " = ";
            }
            s << getOperatorName(op);
            if (op->numInput() > 1)
            {
                s << '(';
                Varnode::printRaw(s, op->getIn(1));
                for (int4 i = 2; i < op->numInput(); ++i)
                {
                    s << ',';
                    Varnode::printRaw(s, op->getIn(i));
                }
                s << ')';
            }
        }

        public override string getOperatorName(PcodeOp op)
        {
            const BlockBasic* bb = op->getParent();
            if (bb != (BlockBasic*)0)
            {
                Architecture* glb = bb->getFuncdata()->getArch();
                int4 index = op->getIn(0)->getOffset();
                UserPcodeOp* userop = glb->userops.getOp(index);
                if (userop != (UserPcodeOp*)0)
                    return userop->getOperatorName(op);
            }
            ostringstream res;
            res << TypeOp::getOperatorName(op) << '[';
            op->getIn(0)->printRaw(res);
            res << ']';
            return res.str();
        }

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
        {
            if (!op->doesSpecialPropagation())
                return TypeOp::getInputLocal(op, slot);
            Architecture* glb = tlst->getArch();
            VolatileWriteOp* vw_op = glb->userops.getVolatileWrite(); // Check if this a volatile write op
            if ((vw_op->getIndex() == op->getIn(0)->getOffset()) && (slot == 2))
            { // And we are requesting slot 2
                const Address &addr(op->getIn(1)->getAddr()); // Address of volatile memory
                int4 size = op->getIn(2)->getSize(); // Size of memory being written
                uint4 vflags = 0;
                SymbolEntry* entry = glb->symboltab->getGlobalScope()->queryProperties(addr, size, op->getAddr(), vflags);
                if (entry != (SymbolEntry*)0)
                {
                    Datatype* res = entry->getSizedType(addr, size);
                    if (res != (Datatype*)0)
                        return res;
                }
            }
            return TypeOp::getInputLocal(op, slot);
        }

        public override Datatype getOutputLocal(PcodeOp op)
        {
            if (!op->doesSpecialPropagation())
                return TypeOp::getOutputLocal(op);
            Architecture* glb = tlst->getArch();
            VolatileReadOp* vr_op = glb->userops.getVolatileRead(); // Check if this a volatile read op
            if (vr_op->getIndex() == op->getIn(0)->getOffset())
            {
                const Address &addr(op->getIn(1)->getAddr()); // Address of volatile memory
                int4 size = op->getOut()->getSize(); // Size of memory being written
                uint4 vflags = 0;
                SymbolEntry* entry = glb->symboltab->getGlobalScope()->queryProperties(addr, size, op->getAddr(), vflags);
                if (entry != (SymbolEntry*)0)
                {
                    Datatype* res = entry->getSizedType(addr, size);
                    if (res != (Datatype*)0)
                        return res;
                }
            }
            return TypeOp::getOutputLocal(op);
        }
    }
}