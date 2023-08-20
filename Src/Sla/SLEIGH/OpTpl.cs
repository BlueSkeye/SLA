using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class OpTpl
    {
        private VarnodeTpl? output;
        private OpCode opc;
        private List<VarnodeTpl> input;
        
        public OpTpl()
        {
        }
        
        public OpTpl(OpCode oc)
        {
            opc = oc;
            output = (VarnodeTpl)null;
        }
        
        ~OpTpl()
        {               // An OpTpl owns its varnode_tpls
            //if (output != (VarnodeTpl)null)
            //    delete output;
            //foreach (VarnodeTpl template in input)
            //    delete* iter;
        }

        public VarnodeTpl getOut() => output;

        public int numInput() => input.size();

        public VarnodeTpl getIn(int i) => input[i];

        public OpCode getOpcode() => opc;

        public bool isZeroSize()
        {
            // Return if any input or output has zero size
            if (output != (VarnodeTpl)null)
                if (output.isZeroSize()) return true;
            IEnumerator<VarnodeTpl> iter = input.begin();
            while (iter.MoveNext())
                if (iter.Current.isZeroSize()) return true;
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
            // delete output;
            output = (VarnodeTpl)null;
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
        {
            // Remove the indicated input
            // delete input[index];
            for (int i = index; i < input.size() - 1; ++i)
                input[i] = input[i + 1];
            input.RemoveLastItem();
        }

        public void changeHandleIndex(List<int> handmap)
        {
            if (output != (VarnodeTpl)null)
                output.changeHandleIndex(handmap);
            foreach (VarnodeTpl template in input)
                template.changeHandleIndex(handmap);
        }

        public void saveXml(TextWriter s)
        {
            s.Write($"<op_tpl code=\"{Globals.get_opname(opc)}\">");
            if (output == (VarnodeTpl)null)
                s.WriteLine("<null/>");
            else
                output.saveXml(s);
            for (int i = 0; i < input.size(); ++i)
                input[i].saveXml(s);
            s.WriteLine("</op_tpl>");
        }

        public void restoreXml(Element el, AddrSpaceManager manage)
        {
            opc = get_opcode(el.getAttributeValue("code"));
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            if (!iter.MoveNext()) throw new ApplicationException();
            if (iter.Current.getName() == "null")
                output = (VarnodeTpl)null;
            else {
                output = new VarnodeTpl();
                output.restoreXml(iter.Current, manage);
            }
            while (iter.MoveNext()) {
                VarnodeTpl vn = new VarnodeTpl();
                vn.restoreXml(iter.Current, manage);
                input.Add(vn);
            }
        }
    }
}
