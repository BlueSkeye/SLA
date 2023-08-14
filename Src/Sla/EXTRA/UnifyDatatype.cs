using Sla.CORE;
using Sla.DECCORE;
using static Sla.DECCORE.FlowBlock;
using static Sla.DECCORE.TokenSplit;
using static Sla.SLEIGH.ConstTpl;

namespace Sla.EXTRA
{
    internal class UnifyDatatype
    {
        public enum TypeKind
        {
            op_type,
            var_type,
            const_type,
            block_type
        }
        
        private TypeKind type;

        private struct /*union*/ Store
        {
            internal PcodeOp op;
            internal Varnode vn;
            internal ulong cn;
            internal BlockBasic bl;
        }
        private Store storespot;

        public UnifyDatatype()
        {
            type = TypeKind.op_type;
        }

        public UnifyDatatype(UnifyDatatype.TypeKind tp)
        {
            type = tp;
            switch (type) {
                case TypeKind.op_type:
                case TypeKind.var_type:
                case TypeKind.block_type:
                    break;
                case TypeKind.const_type:
                    storespot.cn = 0UL;
                    break;
                default:
                    throw new LowlevelError("Bad unify datatype");
            }
        }

        public UnifyDatatype(UnifyDatatype op2)
        {
            type = op2.type;
            switch (type) {
                case TypeKind.op_type:
                case TypeKind.var_type:
                case TypeKind.block_type:
                    break;
                case TypeKind.const_type:
                    storespot.cn = 0; // Copy needs its own memory
                    break;
                default:
                    throw new LowlevelError("Bad unify datatype");
            }
        }

        public UnifyDatatype operator=(UnifyDatatype op2)
        {
            switch (type) {
                case TypeKind.op_type:
                case TypeKind.var_type:
                case TypeKind.block_type:
                    break;
                case TypeKind.const_type:
                    // delete storespot.cn;
                    break;
                default:
                    throw new LowlevelError("Bad unify datatype");
            }
            type = op2.type;
            switch (type) {
                case TypeKind.op_type:
                case TypeKind.var_type:
                case TypeKind.block_type:
                    break;
                case TypeKind.const_type:
                    storespot.cn = 0; // Copy needs its own memory
                    break;
                default:
                    throw new LowlevelError("Bad unify datatype");
            }
            return *this;
        }

        ~UnifyDatatype()
        {
            switch (type)
            {
                case TypeKind.op_type:
                case TypeKind.var_type:
                case TypeKind.block_type:
                    break;
                case TypeKind.const_type:
                    delete storespot.cn;
                    break;
                default:
                    break;
            }
        }

        public TypeKind getType() => type;

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

        public void setConstant(ulong val)
        {
            storespot.cn = val;
        }

        public ulong getConstant() => storespot.cn;

        public void printVarDecl(TextWriter s, int id, UnifyCPrinter cprinter)
        {
            cprinter.printIndent(s);
            switch (type) {
                case TypeKind.op_type:
                    s.WriteLine($"PcodeOp *{cprinter.getName(id)};");
                    break;
                case TypeKind.var_type:
                    s.WriteLine($"Varnode *{cprinter.getName(id)};");
                    break;
                case TypeKind.block_type:
                    s.WriteLine("BlockBasic *{cprinter.getName(id)};");
                    break;
                case TypeKind.const_type:
                    s.WriteLine($"ulong {cprinter.getName(id)};");
                    break;
                default:
                    throw new LowlevelError("Bad unify datatype");
            }
        }

        public string getBaseName()
        {
            switch (type) {
                case TypeKind.op_type:
                    return "op";
                case TypeKind.var_type:
                    return "vn";
                case TypeKind.block_type:
                    return "bl";
                case TypeKind.const_type:
                    return "cn";
                default:
                    throw new LowlevelError("Bad unify datatype");
            }
        }
    }
}
