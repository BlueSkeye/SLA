using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ConstructTpl
    {
        // friend class SleighCompile;
        
        protected uint delayslot;
        protected uint numlabels;        // Number of label templates
        protected List<OpTpl> vec;
        protected HandleTpl result;

        protected void setOpvec(List<OpTpl> opvec)
        {
            vec = opvec;
        }

        protected void setNumLabels(uint val)
        {
            numlabels = val;
        }
        
        public ConstructTpl()
        {
            delayslot = 0;
            numlabels = 0;
            result = (HandleTpl)null;
        }
        
        ~ConstructTpl()
        {               // Constructor owns its ops and handles
            List<OpTpl*>::iterator oiter;
            for (oiter = vec.begin(); oiter != vec.end(); ++oiter)
                delete* oiter;
            if (result != (HandleTpl)null)
                delete result;
        }

        public uint delaySlot() => delayslot;

        public uint numLabels() => numlabels;

        public List<OpTpl> getOpvec() => vec;

        public HandleTpl getResult() => result;

        public bool addOp(OpTpl ot)
        {
            if (ot.getOpcode() == DELAY_SLOT)
            {
                if (delayslot != 0)
                    return false;       // Cannot have multiple delay slots
                delayslot = ot.getIn(0).getOffset().getReal();
            }
            else if (ot.getOpcode() == LABELBUILD)
                numlabels += 1;     // Count labels
            vec.Add(ot);
            return true;
        }

        public bool addOpList(List<OpTpl> oplist)
        {
            for (int i = 0; i < oplist.size(); ++i)
                if (!addOp(oplist[i]))
                    return false;
            return true;
        }

        public void setResult(HandleTpl t)
        {
            result = t;
        }

        public int fillinBuild(List<int> check, AddrSpace const_space)
        { // Make sure there is a build statement for all subtable params
          // Return 0 upon success, 1 if there is a duplicate BUILD, 2 if there is a build for a non-subtable
            List<OpTpl*>::iterator iter;
            OpTpl* op;
            VarnodeTpl* indvn;

            for (iter = vec.begin(); iter != vec.end(); ++iter)
            {
                op = *iter;
                if (op.getOpcode() == BUILD)
                {
                    int index = op.getIn(0).getOffset().getReal();
                    if (check[index] != 0)
                        return check[index];    // Duplicate BUILD statement or non-subtable
                    check[index] = 1;       // Mark to avoid future duplicate build
                }
            }
            for (int i = 0; i < check.size(); ++i)
            {
                if (check[i] == 0)
                {   // Didn't see a BUILD statement
                    op = new OpTpl(BUILD);
                    indvn = new VarnodeTpl(ConstTpl(const_space),
                                ConstTpl(ConstTpl.const_type.real, i),
                                ConstTpl(ConstTpl.const_type.real, 4));
                    op.addInput(indvn);
                    vec.insert(vec.begin(), op);
                }
            }
            return 0;
        }

        public bool buildOnly()
        {
            List<OpTpl*>::const_iterator iter;
            OpTpl* op;
            for (iter = vec.begin(); iter != vec.end(); ++iter)
            {
                op = *iter;
                if (op.getOpcode() != BUILD)
                    return false;
            }
            return true;
        }

        public void changeHandleIndex(List<int> handmap)
        {
            List<OpTpl*>::const_iterator iter;
            OpTpl* op;

            for (iter = vec.begin(); iter != vec.end(); ++iter)
            {
                op = *iter;
                if (op.getOpcode() == BUILD)
                {
                    int index = op.getIn(0).getOffset().getReal();
                    index = handmap[index];
                    op.getIn(0).setOffset(index);
                }
                else
                    op.changeHandleIndex(handmap);
            }
            if (result != (HandleTpl)null)
                result.changeHandleIndex(handmap);
        }

        public void setInput(VarnodeTpl vn, int index, int slot)
        { // set the VarnodeTpl input for a particular op
          // for use with optimization routines
            OpTpl* op = vec[index];
            VarnodeTpl* oldvn = op.getIn(slot);
            op.setInput(vn, slot);
            if (oldvn != (VarnodeTpl*)0)
                delete oldvn;
        }

        public void setOutput(VarnodeTpl vn, int index)
        { // set the VarnodeTpl output for a particular op
          // for use with optimization routines
            OpTpl* op = vec[index];
            VarnodeTpl* oldvn = op.getOut();
            op.setOutput(vn);
            if (oldvn != (VarnodeTpl*)0)
                delete oldvn;
        }

        public void deleteOps(List<int> indices)
        { // delete a particular set of ops
            for (uint i = 0; i < indices.size(); ++i)
            {
                delete vec[indices[i]];
                vec[indices[i]] = (OpTpl*)0;
            }
            uint poscur = 0;
            for (uint i = 0; i < vec.size(); ++i)
            {
                OpTpl* op = vec[i];
                if (op != (OpTpl*)0)
                {
                    vec[poscur] = op;
                    poscur += 1;
                }
            }
            while (vec.size() > poscur)
                vec.RemoveLastItem();
        }

        public void saveXml(TextWriter s, int sectionid)
        {
            s << "<construct_tpl";
            if (sectionid >= 0)
                s << " section=\"" << dec << sectionid << "\"";
            if (delayslot != 0)
                s << " delay=\"" << dec << delayslot << "\"";
            if (numlabels != 0)
                s << " labels=\"" << dec << numlabels << "\"";
            s << ">\n";
            if (result != (HandleTpl)null)
                result.saveXml(s);
            else
                s << "<null/>";
            for (int i = 0; i < vec.size(); ++i)
                vec[i].saveXml(s);
            s << "</construct_tpl>\n";
        }

        public int restoreXml(Element el, AddrSpaceManager manage)
        {
            int sectionid = -1;
            for (int i = 0; i < el.getNumAttributes(); ++i)
            {
                if (el.getAttributeName(i) == "delay")
                {
                    istringstream s = new istringstream(el.getAttributeValue(i));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> delayslot;
                }
                else if (el.getAttributeName(i) == "labels")
                {
                    istringstream s = new istringstream(el.getAttributeValue(i));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> numlabels;
                }
                else if (el.getAttributeName(i) == "section")
                {
                    istringstream s = new istringstream(el.getAttributeValue(i));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> sectionid;
                }
            }
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            if ((*iter).getName() == "null")
                result = (HandleTpl)null;
            else
            {
                result = new HandleTpl();
                result.restoreXml(*iter, manage);
            }
            ++iter;
            while (iter != list.end())
            {
                OpTpl* op = new OpTpl();
                op.restoreXml(*iter, manage);
                vec.Add(op);
                ++iter;
            }
            return sectionid;
        }
    }
}
