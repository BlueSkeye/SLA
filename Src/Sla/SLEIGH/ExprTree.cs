using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ExprTree
    {
        // A flattened expression tree
        // friend class PcodeCompile;
        // flattened ops making up the expression
        private List<OpTpl>? ops;
        // Output varnode of the expression
        // If the last op has an output, -outvn- is
        // a COPY of that varnode
        private VarnodeTpl outvn;
        
        public ExprTree()
        {
            ops = (List<OpTpl>)nulll;
            outvn = (VarnodeTpl)null;
        }

        public ExprTree(VarnodeTpl vn)
        {
            outvn = vn;
            ops = new List<OpTpl>();
        }

        public ExprTree(OpTpl op)
        {
            ops = new List<OpTpl>();
            ops.Add(op);
            if (op.getOut() != (VarnodeTpl)null)
                outvn = new VarnodeTpl(op.getOut());
            else
                outvn = (VarnodeTpl)null;
        }

        ~ExprTree()
        {
            //if (outvn != (VarnodeTpl)null)
            //    delete outvn;
            if (ops != (List<OpTpl>)null) {
                //for (int i = 0; i < ops.size(); ++i)
                //    delete(*ops)[i];
                //delete ops;
            }
        }

        public void setOutput(VarnodeTpl newout)
        {
            // Force the output of the expression to be new out
            // If the original output is named, this requires
            // an extra COPY op
            OpTpl op;
            if (outvn == (VarnodeTpl)null)
                throw new SleighError("Expression has no output");
            if (outvn.isUnnamed()) {
                // delete outvn;
                op = ops.GetLastItem();
                op.clearOutput();
                op.setOutput(newout);
            }
            else {
                op = new OpTpl(CORE.OpCode.CPUI_COPY);
                op.addInput(outvn);
                op.setOutput(newout);
                ops.Add(op);
            }
            outvn = new VarnodeTpl(newout);
        }

        public VarnodeTpl getOut() => outvn;

        public ConstTpl getSize() => outvn.getSize();

        public static List<OpTpl?> appendParams(OpTpl op, List<ExprTree> param)
        {
            // Create op expression with entire list of expression inputs
            List<OpTpl?> res = new List<OpTpl?>();

            for (int i = 0; i < param.Count; ++i) {
                res.AddRange(param[i].ops);
                param[i].ops.Clear();
                op.addInput(param[i].outvn);
                param[i].outvn = (VarnodeTpl)null;
                // delete(*param)[i];
            }
            res.Add(op);
            // delete param;
            return res;
        }

        public static List<OpTpl>? toVector(ExprTree expr)
        {
            // Grab the op List and delete the output expression
            List<OpTpl>? res = expr.ops;
            expr.ops = (List<OpTpl>)null;
            // delete expr;
            return res;
        }
    }
}
