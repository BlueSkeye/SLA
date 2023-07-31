using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class OpTpl
    {
        private VarnodeTpl output;
        private OpCode opc;
        private List<VarnodeTpl> input;
        
        public OpTpl()
        {
        }
        
        public OpTpl(OpCode oc)
        {
            opc = oc;
            output = (VarnodeTpl*)0;
        }
        
        ~OpTpl()
        {               // An OpTpl owns its varnode_tpls
            if (output != (VarnodeTpl*)0)
                delete output;
            List<VarnodeTpl*>::iterator iter;
            for (iter = input.begin(); iter != input.end(); ++iter)
                delete* iter;
        }

        public VarnodeTpl getOut() => output;

        public int numInput() => input.size();

        public VarnodeTpl getIn(int i) => input[i];

        public OpCode getOpcode() => opc;

        public bool isZeroSize()
        {               // Return if any input or output has zero size
            List<VarnodeTpl*>::const_iterator iter;

            if (output != (VarnodeTpl*)0)
                if (output.isZeroSize()) return true;
            for (iter = input.begin(); iter != input.end(); ++iter)
                if ((*iter).isZeroSize()) return true;
            return false;
        }

        public void setOpcode(OpCode o)
        {
            opc = o;
        }

        public void setOutput(VarnodeTpl vt)
        {
            output = vt;
        }

        public void clearOutput()
        {
            delete output;
            output = (VarnodeTpl*)0;
        }

        public void addInput(VarnodeTpl vt)
        {
            input.Add(vt);
        }

        public void setInput(VarnodeTpl vt, int slot)
        {
            input[slot] = vt;
        }

        public void removeInput(int index)
        { // Remove the indicated input
            delete input[index];
            for (int i = index; i < input.size() - 1; ++i)
                input[i] = input[i + 1];
            input.RemoveLastItem();
        }

        public void changeHandleIndex(List<int> handmap)
        {
            if (output != (VarnodeTpl*)0)
                output.changeHandleIndex(handmap);
            List<VarnodeTpl*>::const_iterator iter;

            for (iter = input.begin(); iter != input.end(); ++iter)
                (*iter).changeHandleIndex(handmap);
        }

        public void saveXml(TextWriter s)
        {
            s << "<op_tpl code=\"" << Globals.get_opname(opc) << "\">";
            if (output == (VarnodeTpl*)0)
                s << "<null/>\n";
            else
                output.saveXml(s);
            for (int i = 0; i < input.size(); ++i)
                input[i].saveXml(s);
            s << "</op_tpl>\n";
        }

        public void restoreXml(Element el, AddrSpaceManager manage)
        {
            opc = get_opcode(el.getAttributeValue("code"));
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            if ((*iter).getName() == "null")
                output = (VarnodeTpl*)0;
            else
            {
                output = new VarnodeTpl();
                output.restoreXml(*iter, manage);
            }
            ++iter;
            while (iter != list.end())
            {
                VarnodeTpl* vn = new VarnodeTpl();
                vn.restoreXml(*iter, manage);
                input.Add(vn);
                ++iter;
            }
        }
    }
}
