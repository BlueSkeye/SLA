using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class HandleTpl
    {
        private ConstTpl space;
        private ConstTpl size;
        private ConstTpl ptrspace;
        private ConstTpl ptroffset;
        private ConstTpl ptrsize;
        private ConstTpl temp_space;
        private ConstTpl temp_offset;
        
        public HandleTpl()
        {
        }
        
        public HandleTpl(VarnodeTpl vn)
        {               // Build handle which indicates given varnode
            space = vn.getSpace();
            size = vn.getSize();
            ptrspace = ConstTpl(ConstTpl.const_type.real, 0);
            ptroffset = vn.getOffset();
        }

        public HandleTpl(ConstTpl spc, ConstTpl sz, VarnodeTpl vn, AddrSpace t_space,ulong t_offset)
        {
            // Build handle to thing being pointed at by -vn-
            space = new ConstTpl(spc);
            size = new ConstTpl(sz);
            ptrspace = new ConstTpl(vn.getSpace());
            ptroffset = new ConstTpl(vn.getOffset());
            ptrsize = new ConstTpl(vn.getSize());
            temp_space = new ConstTpl(t_space);
            temp_offset = new ConstTpl(ConstTpl.const_type.real, t_offset);
        }

        public ConstTpl getSpace() => space;

        public ConstTpl getPtrSpace() => ptrspace;

        public ConstTpl getPtrOffset() => ptroffset;

        public ConstTpl getPtrSize() => ptrsize;

        public ConstTpl getSize() => size;

        public ConstTpl getTempSpace() => temp_space;

        public ConstTpl getTempOffset() => temp_offset;

        public void setSize(ConstTpl sz)
        {
            size = sz;
        }

        public void setPtrSize(ConstTpl sz)
        {
            ptrsize = sz;
        }

        public void setPtrOffset(ulong val)
        {
            ptroffset = new ConstTpl(ConstTpl.const_type.real, val);
        }

        public void setTempOffset(ulong val)
        {
            temp_offset = new ConstTpl(ConstTpl.const_type.real, val);
        }

        public void fix(FixedHandle hand, ParserWalker walker)
        {
            if (ptrspace.getType() == ConstTpl.const_type.real)
            {
                // The export is unstarred, but this doesn't mean the varnode
                // being exported isn't dynamic
                space.fillinSpace(hand, walker);
                hand.size = (int?)size.fix(walker);
                ptroffset.fillinOffset(hand, walker);
            }
            else
            {
                hand.space = space.fixSpace(walker);
                hand.size = (uint)size.fix(walker);
                hand.offset_offset = ptroffset.fix(walker);
                hand.offset_space = ptrspace.fixSpace(walker);
                if (hand.offset_space.getType() == spacetype.IPTR_CONSTANT)
                {
                    // Handle could have been dynamic but wasn't
                    hand.offset_space = (AddrSpace)null;
                    hand.offset_offset = AddrSpace.addressToByte(hand.offset_offset, hand.space.getWordSize());
                    hand.offset_offset = hand.space.wrapOffset(hand.offset_offset);
                }
                else
                {
                    hand.offset_size = (uint)ptrsize.fix(walker);
                    hand.temp_space = temp_space.fixSpace(walker);
                    hand.temp_offset = temp_offset.fix(walker);
                }
            }
        }

        public void changeHandleIndex(List<int> handmap)
        {
            space.changeHandleIndex(handmap);
            size.changeHandleIndex(handmap);
            ptrspace.changeHandleIndex(handmap);
            ptroffset.changeHandleIndex(handmap);
            ptrsize.changeHandleIndex(handmap);
            temp_space.changeHandleIndex(handmap);
            temp_offset.changeHandleIndex(handmap);
        }

        public void saveXml(TextWriter s)
        {
            s.Write("<handle_tpl>");
            space.saveXml(s);
            size.saveXml(s);
            ptrspace.saveXml(s);
            ptroffset.saveXml(s);
            ptrsize.saveXml(s);
            temp_space.saveXml(s);
            temp_offset.saveXml(s);
            s.WriteLine("</handle_tpl>");
        }

        public void restoreXml(Element el, AddrSpaceManager manage)
        {
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            space.restoreXml(*iter, manage);
            ++iter;
            size.restoreXml(*iter, manage);
            ++iter;
            ptrspace.restoreXml(*iter, manage);
            ++iter;
            ptroffset.restoreXml(*iter, manage);
            ++iter;
            ptrsize.restoreXml(*iter, manage);
            ++iter;
            temp_space.restoreXml(*iter, manage);
            ++iter;
            temp_offset.restoreXml(*iter, manage);
        }
    }
}
