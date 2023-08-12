using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantExpression : RHSConstant
    {
        private RHSConstant expr1;
        private RHSConstant expr2;
        private OpCode opc;
        
        public ConstantExpression(RHSConstant e1, RHSConstant e2, OpCode oc)
        {
            expr1 = e1;
            expr2 = e2;
            opc = oc;
        }
        
        ~ConstantExpression()
        {
            delete expr1;
            if (expr2 != (RHSConstant)null)
                delete expr2;
        }

        public override RHSConstant clone()
        {
            RHSConstant* ecopy1 = expr1.clone();
            RHSConstant* ecopy2 = (RHSConstant)null;
            if (expr2 != (RHSConstant)null)
                ecopy2 = expr2.clone();
            return new ConstantExpression(ecopy1, ecopy2, opc);
        }

        public override ulong getConstant(UnifyState state)
        {
            OpBehavior* behavior = state.getBehavior(opc);
            if (behavior.isSpecial())
                throw new CORE.LowlevelError("Cannot evaluate special operator in constant expression");
            ulong res;
            if (behavior.isUnary())
            {
                ulong ourconst1 = expr1.getConstant(state);
                res = behavior.evaluateUnary(sizeof(ulong), sizeof(ulong), ourconst1);
            }
            else
            {
                ulong ourconst1 = expr1.getConstant(state);
                ulong ourconst2 = expr2.getConstant(state);
                res = behavior.evaluateBinary(sizeof(ulong), sizeof(ulong), ourconst1, ourconst2);
            }
            return res;
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            int type;          // 0=binary 1=unarypre 2=unarypost 3=func
            string name;            // name of operator
            switch (opc)
            {
                case OpCode.CPUI_INT_ADD:
                    type = 0;
                    name = " + ";
                    break;
                case OpCode.CPUI_INT_SUB:
                    type = 0;
                    name = " - ";
                    break;
                case OpCode.CPUI_INT_AND:
                    type = 0;
                    name = " & ";
                    break;
                case OpCode.CPUI_INT_OR:
                    type = 0;
                    name = " | ";
                    break;
                case OpCode.CPUI_INT_XOR:
                    type = 0;
                    name = " ^ ";
                    break;
                case OpCode.CPUI_INT_MULT:
                    type = 0;
                    name = " * ";
                    break;
                case OpCode.CPUI_INT_DIV:
                    type = 0;
                    name = " / ";
                    break;
                case OpCode.CPUI_INT_EQUAL:
                    type = 0;
                    name = " == ";
                    break;
                case OpCode.CPUI_INT_NOTEQUAL:
                    type = 0;
                    name = " != ";
                    break;
                //  case OpCode.CPUI_INT_SLESS:
                //  case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_LESS:
                    type = 0;
                    name = " < ";
                    break;
                case OpCode.CPUI_INT_LESSEQUAL:
                    type = 0;
                    name = " <= ";
                    break;
                //  case OpCode.CPUI_INT_ZEXT:
                //  case OpCode.CPUI_INT_SEXT:
                //  case OpCode.CPUI_INT_CARRY:
                //  case OpCode.CPUI_INT_SCARRY:
                //  case OpCode.CPUI_INT_SBORROW:
                case OpCode.CPUI_INT_LEFT:
                    type = 0;
                    name = " << ";
                    break;
                case OpCode.CPUI_INT_RIGHT:
                    type = 0;
                    name = " >> ";
                    break;
                //  case OpCode.CPUI_INT_SRIGHT:
                default:
                    throw new CORE.LowlevelError("Unable to generate C for this expression element");
            }
            if (type == 0)
            {
                s << '(';
                expr1.writeExpression(s, printstate);
                s << name;
                expr2.writeExpression(s, printstate);
                s << ')';
            }
            else if (type == 1)
            {
                s << '(' << name;
                expr1.writeExpression(s, printstate);
                s << ')';
            }
            else if (type == 2)
            {
                s << '(';
                expr1.writeExpression(s, printstate);
                s << name << ')';
            }
            else
            {
                s << name << '(';
                expr1.writeExpression(s, printstate);
                s << ')';
            }
        }
    }
}
