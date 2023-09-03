using Sla.CORE;

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
            space = new ConstTpl(ConstTpl.const_type.handle, hand, ConstTpl.v_field.v_space);
            offset = new ConstTpl(ConstTpl.const_type.handle, hand, ConstTpl.v_field.v_offset);
            size = new ConstTpl(ConstTpl.const_type.handle, hand, ConstTpl.v_field.v_size);
                // Varnode built from a handle
                        // if zerosize is true, set the size constant to zero
            if (zerosize)
                size = new ConstTpl(ConstTpl.const_type.real, 0);
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
            space = sp;
            offset = off;
            size = sz;
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
            if (offset.getType() != ConstTpl.const_type.handle) return false;
            // Technically we should probably check all three
            // ConstTpls for dynamic handles, but in all cases
            // if there is any dynamic piece then the offset is
            FixedHandle hand = walker.getFixedHandle(offset.getHandleIndex());
            return (hand.offset_space != (AddrSpace)null);
        }

        public int transfer(List<HandleTpl> @params)
        {
            bool doesOffsetPlus = false;
            int handleIndex = 0;
            int plus = 0;
            if (   (offset.getType() == ConstTpl.const_type.handle)
                && (offset.getSelect() == ConstTpl.v_field.v_offset_plus))
            {
                handleIndex = offset.getHandleIndex();
                plus = (int)offset.getReal();
                doesOffsetPlus = true;
            }
            space.transfer (@params);
            offset.transfer (@params);
            size.transfer (@params);
            if (doesOffsetPlus) {
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
            if (!(op1.space == op2.space)) return (op1.space < op2.space);
            if (!(op1.offset == op2.offset)) return (op1.offset < op2.offset);
            if (!(op1.size == op2.size)) return (op1.size < op2.size);
            return false;
        }

        public static bool operator >(VarnodeTpl op1, VarnodeTpl op2)
        {
            if (!(op1.space == op2.space)) return (op1.space > op2.space);
            if (!(op1.offset == op2.offset)) return (op1.offset > op2.offset);
            if (!(op1.size == op2.size)) return (op1.size > op2.size);
            return false;
        }

        public void setOffset(ulong constVal)
        {
            offset = new ConstTpl(ConstTpl.const_type.real, constVal);
        }

        public void setRelative(ulong constVal)
        {
            offset = new ConstTpl(ConstTpl.const_type.j_relative, constVal);
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
            if (space.getType() != ConstTpl.const_type.spaceid) return false;
            if (space.getSpace().getType() != spacetype.IPTR_INTERNAL) return false;
            return true;
        }

        public bool isRelative()
        {
            return (offset.getType() == ConstTpl.const_type.j_relative);
        }

        public void changeHandleIndex(List<int> handmap)
        {
            space.changeHandleIndex(handmap);
            offset.changeHandleIndex(handmap);
            size.changeHandleIndex(handmap);
        }

        public bool adjustTruncation(int sz, bool isbigendian)
        {
            // We know this.offset is an offset_plus, check that the truncation is in bounds (given -sz-)
            // adjust plus for endianness if necessary
            // return true if truncation is in bounds
            if (size.getType() != ConstTpl.const_type.real)
                return false;
            int numbytes = (int)size.getReal();
            int byteoffset = (int)offset.getReal();
            if (numbytes + byteoffset > sz) return false;

            // Encode the original truncation amount with the plus value
            ulong val = (ulong)byteoffset;
            val <<= 16;
            val |= (isbigendian) 
                ? (ulong)(sz - (numbytes + byteoffset))
                : (ulong)byteoffset;
            offset = new ConstTpl(ConstTpl.const_type.handle, offset.getHandleIndex(),
                ConstTpl.v_field.v_offset_plus, val);
            return true;
        }

        public void saveXml(TextWriter s)
        {
            s.Write("<varnode_tpl>");
            space.saveXml(s);
            offset.saveXml(s);
            size.saveXml(s);
            s.WriteLine("</varnode_tpl>");
        }

        public void restoreXml(Element el, AddrSpaceManager manage)
        {
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            if (!iter.MoveNext()) throw new BugException();
            space.restoreXml(iter.Current, manage);
            if (!iter.MoveNext()) throw new BugException();
            offset.restoreXml(iter.Current, manage);
            if (!iter.MoveNext()) throw new BugException();
            size.restoreXml(iter.Current, manage);
        }
    }
}
