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
        protected HandleTpl? result;

        internal void setOpvec(List<OpTpl> opvec)
        {
            vec = opvec;
        }

        internal void setNumLabels(uint val)
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
            //List<OpTpl*>::iterator oiter;
            //for (oiter = vec.begin(); oiter != vec.end(); ++oiter)
            //    delete* oiter;
            //if (result != (HandleTpl)null)
            //    delete result;
        }

        public uint delaySlot() => delayslot;

        public uint numLabels() => numlabels;

        public List<OpTpl> getOpvec() => vec;

        public HandleTpl? getResult() => result;

        public bool addOp(OpTpl ot)
        {
            if (ot.getOpcode() == OpCode.DELAY_SLOT) {
                if (delayslot != 0)
                    // Cannot have multiple delay slots
                    return false;
                delayslot = (uint)ot.getIn(0).getOffset().getReal();
            }
            else if (ot.getOpcode() == OpCode.LABELBUILD)
                // Count labels
                numlabels += 1;
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
            IEnumerator<OpTpl> iter;
            VarnodeTpl indvn;

            foreach (OpTpl op in vec) {
                if (op.getOpcode() == OpCode.BUILD) {
                    int index = (int)op.getIn(0).getOffset().getReal();
                    if (check[index] != 0)
                        return check[index];    // Duplicate BUILD statement or non-subtable
                    check[index] = 1;       // Mark to avoid future duplicate build
                }
            }
            for (int i = 0; i < check.size(); ++i) {
                if (check[i] == 0) {
                    // Didn't see a BUILD statement
                    OpTpl op = new OpTpl(OpCode.BUILD);
                    indvn = new VarnodeTpl(new ConstTpl(const_space),
                        new ConstTpl(ConstTpl.const_type.real, (uint)i),
                        new ConstTpl(ConstTpl.const_type.real, 4));
                    op.addInput(indvn);
                    vec.Insert(0, op);
                }
            }
            return 0;
        }

        public bool buildOnly()
        {
            foreach (OpTpl op in vec) {
                if (op.getOpcode() != OpCode.BUILD)
                    return false;
            }
            return true;
        }

        public void changeHandleIndex(List<int> handmap)
        {
            foreach (OpTpl op in vec) {
                if (op.getOpcode() == OpCode.BUILD) {
                    int index = (int)op.getIn(0).getOffset().getReal();
                    index = handmap[index];
                    op.getIn(0).setOffset((ulong)index);
                }
                else
                    op.changeHandleIndex(handmap);
            }
            if (result != (HandleTpl)null)
                result.changeHandleIndex(handmap);
        }

        public void setInput(VarnodeTpl vn, int index, int slot)
        {
            // set the VarnodeTpl input for a particular op
            // for use with optimization routines
            OpTpl op = vec[index];
            VarnodeTpl? oldvn = op.getIn(slot);
            op.setInput(vn, slot);
            //if (oldvn != (VarnodeTpl)null)
            //    delete oldvn;
        }

        public void setOutput(VarnodeTpl vn, int index)
        {
            // set the VarnodeTpl output for a particular op
            // for use with optimization routines
            OpTpl op = vec[index];
            VarnodeTpl oldvn = op.getOut();
            op.setOutput(vn);
            //if (oldvn != (VarnodeTpl)null)
            //    delete oldvn;
        }

        public void deleteOps(List<int> indices)
        {
            // delete a particular set of ops
            for (int i = 0; i < indices.size(); ++i) {
                // delete vec[indices[i]];
                vec[indices[i]] = (OpTpl)null;
            }
            int poscur = 0;
            for (uint i = 0; i < vec.size(); ++i) {
                OpTpl? op = vec[(int)i];
                if (op != (OpTpl)null) {
                    vec[poscur] = op;
                    poscur += 1;
                }
            }
            while (vec.size() > poscur)
                vec.RemoveLastItem();
        }

        public void saveXml(TextWriter s, int sectionid)
        {
            s.Write("<construct_tpl");
            if (sectionid >= 0)
                s.Write($" section=\"{sectionid}");
            if (delayslot != 0)
                s.Write($" delay=\"{delayslot}\"");
            if (numlabels != 0)
                s.Write($" labels=\"{numlabels}\"");
            s.WriteLine(">");
            if (result != (HandleTpl)null)
                result.saveXml(s);
            else
                s.Write("<null/>");
            for (int i = 0; i < vec.size(); ++i)
                vec[i].saveXml(s);
            s.WriteLine("</construct_tpl>");
        }

        public int restoreXml(Element el, AddrSpaceManager manage)
        {
            int sectionid = -1;
            for (int i = 0; i < el.getNumAttributes(); ++i) {
                if (el.getAttributeName(i) == "delay")
                    delayslot = uint.Parse(el.getAttributeValue(i));
                else if (el.getAttributeName(i) == "labels") {
                    numlabels = uint.Parse(el.getAttributeValue(i));
                }
                else if (el.getAttributeName(i) == "section") {
                    sectionid = int.Parse(el.getAttributeValue(i));
                }
            }
            List<Element> list = el.getChildren();
            IEnumerator<Element> iter =  list.GetEnumerator();
            if (!iter.MoveNext()) throw new BugException();
            if (iter.Current.getName() == "null")
                result = (HandleTpl)null;
            else {
                result = new HandleTpl();
                result.restoreXml(iter.Current, manage);
            }
            while (iter.MoveNext()) {
                OpTpl op = new OpTpl();
                op.restoreXml(iter.Current, manage);
                vec.Add(op);
            }
            return sectionid;
        }
    }
}
