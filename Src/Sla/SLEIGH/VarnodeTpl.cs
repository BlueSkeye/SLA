using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class VarnodeTpl
    {
        //friend class OpTpl;
        //friend class HandleTpl;
        private ConstTpl space;
        private ConstTpl offset;
        private ConstTpl size;
        private bool unnamed_flag;
        
        public VarnodeTpl(int hand, bool zerosize)
        {
            space = new ConstTpl(ConstTpl::handle, hand, ConstTpl::v_space);
            offset = new ConstTpl(ConstTpl::handle, hand, ConstTpl::v_offset);
            size = new ConstTpl(ConstTpl::handle, hand, ConstTpl::v_size);
                // Varnode built from a handle
                        // if zerosize is true, set the size constant to zero
            if (zerosize)
                size = ConstTpl(ConstTpl::real, 0);
            unnamed_flag = false;
        }

        public VarnodeTpl()
        {
            space = new ConstTpl();
            offset = new ConstTpl();
            size = new ConstTpl();
            unnamed_flag = false;
        }
        
        public VarnodeTpl(ConstTpl sp, ConstTpl off, ConstTpl sz)
        {
            space = new ConstTpl(sp);
            offset = new ConstTpl(off);
            size = new ConstTpl(sz);
            unnamed_flag = false;
        }

        public VarnodeTpl(VarnodeTpl vn)
        {
            // A clone of the VarnodeTpl
            space = new ConstTpl(vn.space);
            offset = new ConstTpl(vn.offset);
            size = new ConstTpl(vn.size);

            unnamed_flag = vn.unnamed_flag;
        }

        public ConstTpl getSpace() => space;

        public ConstTpl getOffset() => offset;

        public ConstTpl getSize() => size;

        public bool isDynamic(ParserWalker walker)
        {
            if (offset.getType() != ConstTpl::handle) return false;
            // Technically we should probably check all three
            // ConstTpls for dynamic handles, but in all cases
            // if there is any dynamic piece then the offset is
            FixedHandle hand = walker.getFixedHandle(offset.getHandleIndex());
            return (hand.offset_space != (AddrSpace)null);
        }

        public int transfer(List<HandleTpl> @params)
        {
            bool doesOffsetPlus = false;
            int handleIndex;
            int plus;
            if ((offset.getType() == ConstTpl::handle) && (offset.getSelect() == ConstTpl::v_offset_plus))
            {
                handleIndex = offset.getHandleIndex();
                plus = (int)offset.getReal();
                doesOffsetPlus = true;
            }
            space.transfer (@params);
            offset.transfer (@params);
            size.transfer (@params);
            if (doesOffsetPlus)
            {
                if (isLocalTemp())
                    return plus;        // A positive number indicates truncation of a local temp
                if (@params[handleIndex].getSize().isZero())
                    return plus;      //    or a zerosize object
            }
            return -1;
        }

        public bool isZeroSize() => size.isZero();

        public static bool operator <(VarnodeTpl op1, VarnodeTpl op2)
        {
            if (!(space == op2.space)) return (space < op2.space);
            if (!(offset == op2.offset)) return (offset < op2.offset);
            if (!(size == op2.size)) return (size < op2.size);
            return false;
        }

        public void setOffset(ulong constVal)
        {
            offset = new ConstTpl(ConstTpl::real, constVal);
        }

        public void setRelative(ulong constVal)
        {
            offset = new ConstTpl(ConstTpl::j_relative, constVal);
        }

        public void setSize(ConstTpl sz )
        {
            size = sz;
        }

        public bool isUnnamed() => unnamed_flag;

        public void setUnnamed(bool val)
        {
            unnamed_flag = val;
        }

        public bool isLocalTemp()
        {
            if (space.getType() != ConstTpl::spaceid) return false;
            if (space.getSpace().getType() != IPTR_INTERNAL) return false;
            return true;
        }

        public bool isRelative()
        {
            return (offset.getType() == ConstTpl::j_relative);
        }

        public void changeHandleIndex(List<int> handmap)
        {
            space.changeHandleIndex(handmap);
            offset.changeHandleIndex(handmap);
            size.changeHandleIndex(handmap);
        }

        public bool adjustTruncation(int sz, bool isbigendian)
        { // We know this.offset is an offset_plus, check that the truncation is in bounds (given -sz-)
          // adjust plus for endianness if necessary
          // return true if truncation is in bounds
            if (size.getType() != ConstTpl::real)
                return false;
            int numbytes = (int)size.getReal();
            int byteoffset = (int)offset.getReal();
            if (numbytes + byteoffset > sz) return false;

            // Encode the original truncation amount with the plus value
            ulong val = byteoffset;
            val <<= 16;
            if (isbigendian)
            {
                val |= (ulong)(sz - (numbytes + byteoffset));
            }
            else
            {
                val |= (ulong)byteoffset;
            }


            offset = ConstTpl(ConstTpl::handle, offset.getHandleIndex(), ConstTpl::v_offset_plus, val);
            return true;
        }

        public void saveXml(TextWriter s)
        {
            s << "<varnode_tpl>";
            space.saveXml(s);
            offset.saveXml(s);
            size.saveXml(s);
            s << "</varnode_tpl>\n";
        }

        public void restoreXml(Element el, AddrSpaceManager manage)
        {
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            space.restoreXml(*iter, manage);
            ++iter;
            offset.restoreXml(*iter, manage);
            ++iter;
            size.restoreXml(*iter, manage);
        }
    }
}
