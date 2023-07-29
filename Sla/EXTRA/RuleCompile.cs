using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
 #if CPUI_RULECOMPILE
   internal class RuleCompile
    {
        private TextWriter error_stream;
        private int4 errors;
        private RuleLexer lexer;
        private Dictionary<string, int4> namemap = new Dictionary<string, int4>();
        private ConstraintGroup finalrule;
        private List<OpBehavior> inst;
        
        public RuleCompile()
        {
            DummyTranslate dummy;
            error_stream = (ostream*)0;
            errors = 0;
            finalrule = (ConstraintGroup*)0;
            OpBehavior::registerInstructions(inst, &dummy);
        }

        ~RuleCompile()
        {
            if (finalrule != (ConstraintGroup*)0)
                delete finalrule;
            for (int4 i = 0; i < inst.size(); ++i)
            {
                OpBehavior* t_op = inst[i];
                if (t_op != (OpBehavior*)0)
                    delete t_op;
            }
        }

        public void ruleError(string s)
        {
            if (error_stream != (ostream*)0)
            {
                *error_stream << "Error at line " << dec << lexer.getLineNo() << endl;
                *error_stream << "   " << s << endl;
            }
            errors += 1;
        }

        public int4 numErrors() => errors;

        public int4 getLineNo() => lexer.getLineNo();

        public void setFullRule(ConstraintGroup full)
        {
            finalrule = full;
        }

        public ConstraintGroup getRule() => finalrule;

        public ConstraintGroup releaseRule()
        {
            ConstraintGroup res = finalrule;
            finalrule = (ConstraintGroup*)0;
            return res;
        }

        public Dictionary<string, int4> getNameMap() => namemap;

        public int4 findIdentifier(string nm)
        {
            int4 resid;
            map<string, int4>::const_iterator iter;
            iter = namemap.find(*nm);
            if (iter == namemap.end())
            {
                resid = namemap.size();
                namemap[*nm] = resid;
            }
            else
                resid = (*iter).second;
            delete nm;
            return resid;
        }

        public ConstraintGroup newOp(int4 id)
        {
            ConstraintGroup* res = new ConstraintGroup();
            res->addConstraint(new DummyOpConstraint(id));
            return res;
        }

        public ConstraintGroup newVarnode(int4 id)
        {
            ConstraintGroup* res = new ConstraintGroup();
            res->addConstraint(new DummyVarnodeConstraint(id));
            return res;
        }

        public ConstraintGroup newConst(int4 id)
        {
            ConstraintGroup* res = new ConstraintGroup();
            res->addConstraint(new DummyConstConstraint(id));
            return res;
        }

        public ConstraintGroup opCopy(ConstraintGroup @base, int4 opid)
        {
            int4 opindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintOpCopy(opindex, opid);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup opInput(ConstraintGroup @base, int8 slot, int4 varid)
        {
            int4 ourslot = (int4) * slot;
            delete slot;
            int4 opindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintOpInput(opindex, varid, ourslot);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup opInputAny(ConstraintGroup @base, int4 varid)
        {
            int4 opindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintOpInputAny(opindex, varid);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup opInputConstVal(ConstraintGroup @base, int8 slot, RHSConstant rhs)
        {
            int4 ourslot = (int4) * slot;
            delete slot;
            int4 opindex = base->getBaseIndex();
            UnifyConstraint* newconstraint;
            ConstantAbsolute* myconst = dynamic_cast<ConstantAbsolute*>(val);
            if (myconst != (ConstantAbsolute*)0)
            {
                newconstraint = new ConstraintParamConstVal(opindex, ourslot, myconst->getVal());
            }
            else
            {
                ConstantNamed* mynamed = dynamic_cast<ConstantNamed*>(val);
                if (mynamed != (ConstantNamed*)0)
                {
                    newconstraint = new ConstraintParamConst(opindex, ourslot, mynamed->getId());
                }
                else
                {
                    ruleError("Can only use absolute constant here");
                    newconstraint = new ConstraintParamConstVal(opindex, ourslot, 0);
                }
            }
            delete val;
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup opOutput(ConstraintGroup @base, int4 varid)
        {
            int4 opindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintOpOutput(opindex, varid);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup varCopy(ConstraintGroup @base, int4 varid)
        {
            int4 varindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintVarnodeCopy(varid, varindex);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup varConst(ConstraintGroup @base, RHSConstant ex, RHSConstant sz)
        {
            int4 varindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintVarConst(varindex, ex, sz);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup varDef(ConstraintGroup @base, int4 opid)
        {
            int4 varindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintDef(opid, varindex);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup varDescend(ConstraintGroup @base, int4 opid)
        {
            int4 varindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintDescend(opid, varindex);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup varUniqueDescend(ConstraintGroup @base, int4 opid)
        {
            int4 varindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintLoneDescend(opid, varindex);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup opCodeConstraint(ConstraintGroup @base, List<OpCode> oplist)
        {
            if (oplist->size() != 1)
                throw LowlevelError("Not currently supporting multiple opcode constraints");
            int4 opindex = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintOpcode(opindex, *oplist);
            delete oplist;
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup opCompareConstraint(ConstraintGroup @base, int4 opid, OpCode opc)
        {
            int4 op1index = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintOpCompare(op1index, opid, (opc == CPUI_INT_EQUAL));
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup varCompareConstraint(ConstraintGroup @base, int4 varid, OpCode opc)
        {
            int4 var1index = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintVarCompare(var1index, varid, (opc == CPUI_INT_EQUAL));
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup constCompareConstraint(ConstraintGroup @base, int4 constid, OpCode opc)
        {
            int4 const1index = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintConstCompare(const1index, constid, opc);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup constNamedExpression(int4 id, RHSConstant expr)
        {
            ConstraintGroup* res = new ConstraintGroup();
            res->addConstraint(new ConstraintNamedExpression(id, expr));
            return res;
        }

        public ConstraintGroup emptyGroup() => new ConstraintGroup();

        public ConstraintGroup emptyOrGroup() => new ConstraintOr();

        public ConstraintGroup mergeGroups(ConstraintGroup a, ConstraintGroup b)
        {
            a->mergeIn(b);
            return a;
        }

        public ConstraintGroup addOr(ConstraintGroup @base, ConstraintGroup newor)
        {
            base->addConstraint(newor);
            return base;
        }

        public ConstraintGroup opCreation(int4 newid, OpCode oc, bool iafter, int4 oldid)
        {
            OpBehavior* behave = inst[oc];
            int4 numparms = behave->isUnary() ? 1 : 2;
            UnifyConstraint* newconstraint = new ConstraintNewOp(newid, oldid, oc, iafter, numparms);
            ConstraintGroup* res = new ConstraintGroup();
            res->addConstraint(newconstraint);
            return res;
        }

        public ConstraintGroup newUniqueOut(ConstraintGroup @base, int4 varid, int4 sz)
        {
            UnifyConstraint* newconstraint = new ConstraintNewUniqueOut(base->getBaseIndex(), varid, sz);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup newSetInput(ConstraintGroup @base, RHSConstant slot, int4 varid)
        {
            UnifyConstraint* newconstraint = new ConstraintSetInput(base->getBaseIndex(), slot, varid);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup newSetInputConstVal(ConstraintGroup @base, RHSConstant slot, RHSConstant val,
            RHSConstant sz)
        {
            UnifyConstraint* newconstraint = new ConstraintSetInputConstVal(base->getBaseIndex(), slot, val, sz);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup removeInput(ConstraintGroup @base, RHSConstant slot)
        {
            UnifyConstraint* newconstraint = new ConstraintRemoveInput(base->getBaseIndex(), slot);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup newSetOpcode(ConstraintGroup @base, OpCode opc)
        {
            int4 opid = base->getBaseIndex();
            UnifyConstraint* newconstraint = new ConstraintSetOpcode(opid, opc);
            base->addConstraint(newconstraint);
            return base;
        }

        public ConstraintGroup booleanConstraint(bool ist, RHSConstant expr)
        {
            ConstraintGroup * base = new ConstraintGroup();
            UnifyConstraint* newconstraint = new ConstraintBoolean(ist, expr);
            base->addConstraint(newconstraint);
            return base;
        }

        public RHSConstant constNamed(int4 id)
        {
            RHSConstant* res = new ConstantNamed(id);
            return res;
        }

        public RHSConstant constAbsolute(int8 val)
        {
            RHSConstant* res = new ConstantAbsolute(*val);
            delete val;
            return res;
        }

        public RHSConstant constBinaryExpression(RHSConstant ex1, OpCode opc, RHSConstant ex2)
        {
            RHSConstant* res = new ConstantExpression(ex1, ex2, opc);
            return res;
        }

        public RHSConstant constVarnodeSize(int4 varindex)
        {
            RHSConstant* res = new ConstantVarnodeSize(varindex);
            return res;
        }

        public RHSConstant dotIdentifier(int4 id, string str)
        {
            RHSConstant* res;
            if ((*str) == "offset")
                res = new ConstantOffset(id);
            else if ((*str) == "size")
                res = new ConstantVarnodeSize(id);
            else if ((*str) == "isconstant")
                res = new ConstantIsConstant(id);
            else if ((*str) == "heritageknown")
                res = new ConstantHeritageKnown(id);
            else if ((*str) == "consume")
                res = new ConstantConsumed(id);
            else if ((*str) == "nzmask")
                res = new ConstantNZMask(id);
            else
            {
                string errmsg = "Unknown variable attribute: " + *str;
                ruleError(errmsg.c_str());
                res = new ConstantAbsolute(0);
            }
            delete str;
            return res;
        }

        public int4 nextToken() => lexer.nextToken();

        public void setErrorStream(TextWriter t)
        {
            error_stream = t;
        }

        public void run(TextWriter s, bool debug)
        {
#if YYDEBUG
            ruleparsedebug = debug ? 1 : 0;
#endif

            if (!s)
            {
                if (error_stream != (ostream*)0)
                    *error_stream << "Bad input stream to rule compiler" << endl;
                return;
            }
            errors = 0;
            if (finalrule != (ConstraintGroup*)0)
            {
                delete finalrule;
                finalrule = (ConstraintGroup*)0;
            }
            lexer.initialize(s);

            rulecompile = this;     // Setup the global pointer
            int4 parseres = ruleparseparse(); // Try to parse
            if (parseres != 0)
            {
                errors += 1;
                if (error_stream != (ostream*)0)
                    *error_stream << "Parsing error" << endl;
            }

            if (errors != 0)
            {
                if (error_stream != (ostream*)0)
                    *error_stream << "Parsing incomplete" << endl;
            }
        }

        public void postProcess()
        {
            int4 id = 0;
            finalrule->removeDummy();
            finalrule->setId(id);       // Set id for everybody
        }

        public int4 postProcessRule(List<OpCode> opcodelist)
        { // Do normal post processing but also remove initial opcode check
            finalrule->removeDummy();
            if (finalrule->numConstraints() == 0)
                throw LowlevelError("Cannot postprocess empty rule");
            ConstraintOpcode* subconst = dynamic_cast<ConstraintOpcode*>(finalrule->getConstraint(0));
            if (subconst == (ConstraintOpcode*)0)
                throw LowlevelError("Rule does not start with opcode constraint");
            opcodelist = subconst->getOpCodes();
            int4 opinit = subconst->getMaxNum();
            finalrule->deleteConstraint(0);
            int4 id = 0;
            finalrule->setId(id);
            return opinit;
        }

        public static ConstraintGroup buildUnifyer(string rule, List<string> idlist, List<int4> res)
        {
            RuleCompile ruler;
            istringstream s(rule);
            ruler.run(s, false);
            if (ruler.numErrors() != 0)
                throw LowlevelError("Could not build rule");
            ConstraintGroup* resconst = ruler.releaseRule();
            for (int4 i = 0; i < idlist.size(); ++i)
            {
                char initc;
                int4 id = -1;
                map<string, int4>::const_iterator iter;
                if (idlist[i].size() != 0)
                {
                    initc = idlist[i][0];
                    if ((initc == 'o') || (initc == 'O') || (initc == 'v') || (initc == 'V') || (initc == '#'))
                    {
                        iter = ruler.namemap.find(idlist[i]);
                        if (iter != ruler.namemap.end())
                            id = (*iter).second;
                    }
                }
                if (id == -1)
                    throw LowlevelError("Bad initializer name: " + idlist[i]);
                res.push_back(id);
            }
            return resconst;
        }
    }
#endif
}
