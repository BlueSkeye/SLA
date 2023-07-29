﻿using System;
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
        private List<OpTpl> ops;    // flattened ops making up the expression
        private VarnodeTpl outvn;      // Output varnode of the expression
                                // If the last op has an output, -outvn- is
                                // a COPY of that varnode
        
        public ExprTree()
        {
            ops = (List<OpTpl*>*)0;
            outvn = (VarnodeTpl*)0;
        }

        public ExprTree(VarnodeTpl vn)
        {
            outvn = vn;
            ops = new vector<OpTpl*>;
        }

        public ExprTree(OpTpl op)
        {
            ops = new vector<OpTpl*>;
            ops->push_back(op);
            if (op->getOut() != (VarnodeTpl*)0)
                outvn = new VarnodeTpl(*op->getOut());
            else
                outvn = (VarnodeTpl*)0;
        }

        ~ExprTree()
        {
            if (outvn != (VarnodeTpl*)0)
                delete outvn;
            if (ops != (vector<OpTpl*>*)0)
            {
                for (int4 i = 0; i < ops->size(); ++i)
                    delete(*ops)[i];
                delete ops;
            }
        }

        public void setOutput(VarnodeTpl newout)
        {               // Force the output of the expression to be new out
                        // If the original output is named, this requires
                        // an extra COPY op
            OpTpl* op;
            if (outvn == (VarnodeTpl*)0)
                throw SleighError("Expression has no output");
            if (outvn->isUnnamed())
            {
                delete outvn;
                op = ops->back();
                op->clearOutput();
                op->setOutput(newout);
            }
            else
            {
                op = new OpTpl(CPUI_COPY);
                op->addInput(outvn);
                op->setOutput(newout);
                ops->push_back(op);
            }
            outvn = new VarnodeTpl(*newout);
        }

        public VarnodeTpl getOut() => outvn;

        public ConstTpl getSize() => outvn->getSize();

        public static List<OpTpl> appendParams(OpTpl op, List<ExprTree> param)
        {               // Create op expression with entire list of expression
                        // inputs
            vector<OpTpl*>* res = new vector<OpTpl*>;

            for (int4 i = 0; i < param->size(); ++i)
            {
                res->insert(res->end(), (*param)[i]->ops->begin(), (*param)[i]->ops->end());
                (*param)[i]->ops->clear();
                op->addInput((*param)[i]->outvn);
                (*param)[i]->outvn = (VarnodeTpl*)0;
                delete(*param)[i];
            }
            res->push_back(op);
            delete param;
            return res;
        }

        public static List<OpTpl> toVector(ExprTree expr)
        {               // Grab the op vector and delete the output expression
            vector<OpTpl*>* res = expr->ops;
            expr->ops = (vector<OpTpl*>*)0;
            delete expr;
            return res;
        }
    }
}