using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Sla.DECCORE.FlowBlock;
using static Sla.DECCORE.TokenSplit;
using static Sla.SLEIGH.ConstTpl;

namespace Sla.EXTRA
{
    internal class UnifyDatatype
    {
        public enum TypeKind
        {
            op_type, var_type, const_type, block_type
        }
        
        private TypeKind type;

        private struct /*union*/ Store
        {
            internal PcodeOp op;
            internal Varnode vn;
            internal uintb cn;
            internal BlockBasic bl;
        }
        private Store storespot;

        public UnifyDatatype()
        {
            type = op_type;
        }

        public UnifyDatatype(uint4 tp)
        {
            type = tp;
            switch (type)
            {
                case op_type:
                case var_type:
                case block_type:
                    break;
                case const_type:
                    storespot.cn = new uintb;
                    break;
                default:
                    throw LowlevelError("Bad unify datatype");
            }
        }

        public UnifyDatatype(UnifyDatatype op2)
        {
            type = op2.type;
            switch (type)
            {
                case op_type:
                case var_type:
                case block_type:
                    break;
                case const_type:
                    storespot.cn = new uintb; // Copy needs its own memory
                    break;
                default:
                    throw LowlevelError("Bad unify datatype");
            }
        }

        public UnifyDatatype operator=(UnifyDatatype op2)
        {
            switch (type)
            {
                case op_type:
                case var_type:
                case block_type:
                    break;
                case const_type:
                    delete storespot.cn;
                    break;
                default:
                    throw LowlevelError("Bad unify datatype");
            }
            type = op2.type;
            switch (type)
            {
                case op_type:
                case var_type:
                case block_type:
                    break;
                case const_type:
                    storespot.cn = new uintb; // Copy needs its own memory
                    break;
                default:
                    throw LowlevelError("Bad unify datatype");
            }
            return *this;
        }

        ~UnifyDatatype()
        {
            switch (type)
            {
                case op_type:
                case var_type:
                case block_type:
                    break;
                case const_type:
                    delete storespot.cn;
                    break;
                default:
                    break;
            }
        }

        public uint4 getType() => type;

        public void setOp(PcodeOp o)
        {
            storespot.op = o;
        }

        public PcodeOp getOp() => storespot.op;

        public void setVarnode(Varnode v)
        {
            storespot.vn = v;
        }

        public Varnode getVarnode() => storespot.vn;

        public void setBlock(BlockBasic b)
        {
            storespot.bl = b;
        }

        public BlockBasic getBlock() => storespot.bl;

        public void setConstant(uintb val)
        {
            storespot.cn = val;
        }

        public uintb getConstant() => storespot.cn;

        public void printVarDecl(TextWriter s, int4 id, UnifyCPrinter cprinter)
        {
            cprinter.printIndent(s);
            switch (type)
            {
                case op_type:
                    s << "PcodeOp *" << cprinter.getName(id) << ';' << endl;
                    break;
                case var_type:
                    s << "Varnode *" << cprinter.getName(id) << ';' << endl;
                    break;
                case block_type:
                    s << "BlockBasic *" << cprinter.getName(id) << ';' << endl;
                    break;
                case const_type:
                    s << "uintb " << cprinter.getName(id) << ';' << endl;
                    break;
                default:
                    throw LowlevelError("Bad unify datatype");
            }
        }

        public string getBaseName()
        {
            switch (type)
            {
                case op_type:
                    return "op";
                case var_type:
                    return "vn";
                case block_type:
                    return "bl";
                case const_type:
                    return "cn";
                default:
                    throw LowlevelError("Bad unify datatype");
            }
        }
    }
}
