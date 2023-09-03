using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.SLEIGH
{
    internal class ConstTpl
    {
        public enum const_type
        {
            real = 0,
            handle = 1,
            j_start = 2,
            j_next = 3,
            j_next2 = 4,
            j_curspace = 5,
            j_curspace_size = 6,
            spaceid = 7,
            j_relative = 8,
            j_flowref = 9,
            j_flowref_size = 10,
            j_flowdest = 11,
            j_flowdest_size = 12
        }
        
        public enum v_field
        {
            v_space = 0,
            v_offset = 1,
            v_size = 2,
            v_offset_plus = 3
        }
        
        private const_type type;

        private class /*union*/ ValueKind
        {
            //    ulong real;			// an actual constant
            internal AddrSpace spaceid; // Id (pointer) for registered space
            internal int handle_index;      // Place holder for run-time determined value
        }
    
        private ValueKind value;
        private ulong value_real;
        private v_field select;     // Which part of handle to use as constant

        private static void printHandleSelector(TextWriter s, v_field val)
        {
            switch (val) {
                case v_field.v_space:
                    s.Write("space");
                    break;
                case v_field.v_offset:
                    s.Write("offset");
                    break;
                case v_field.v_size:
                    s.Write("size");
                    break;
                case v_field.v_offset_plus:
                    s.Write("offset_plus");
                    break;
            }
        }

        private static v_field readHandleSelector(string name)
        {
            if (name == "space")
                return v_field.v_space;
            if (name == "offset")
                return v_field.v_offset;
            if (name == "size")
                return v_field.v_size;
            if (name == "offset_plus")
                return v_field.v_offset_plus;
            throw new LowlevelError("Bad handle selector");
        }

        public ConstTpl()
        {
            type = const_type.real;
            value_real = 0;
        }
    
        public ConstTpl(ConstTpl op2)
        {
            type = op2.type;
            value = op2.value;
            value_real = op2.value_real;
            select = op2.select;
        }

        public ConstTpl(const_type tp, ulong val)
        {
            // Constructor for real constants
            type = tp;
            value_real = val;
            value.handle_index = 0;
            select = v_field.v_space;
        }

        public ConstTpl(const_type tp)
        {
            // Constructor for relative jump constants and uniques
            type = tp;
        }

        public ConstTpl(AddrSpace sid)
        {
            type = const_type.spaceid;
            value.spaceid = sid;
        }

        public ConstTpl(const_type tp, int ht, v_field vf)
        {
            // Constructor for handle constant
            type = const_type.handle;
            value.handle_index = ht;
            select = vf;
            value_real = 0;
        }

        public ConstTpl(const_type tp, int ht, v_field vf, ulong plus)
        {
            type = const_type.handle;
            value.handle_index = ht;
            select = vf;
            value_real = plus;
        }

        public bool isConstSpace()
        {
            if (type == const_type.spaceid)
                return (value.spaceid.getType() == spacetype.IPTR_CONSTANT);
            return false;
        }

        public bool isUniqueSpace()
        {
            if (type == const_type.spaceid)
                return (value.spaceid.getType() == spacetype.IPTR_INTERNAL);
            return false;
        }

        public static bool operator !=(ConstTpl op1, ConstTpl op2)
        {
            return !(op1 == op2);
        }

        public static bool operator ==(ConstTpl op1, ConstTpl op2)
        {
            if (op1.type != op2.type) return false;
            switch (op1.type) {
                case const_type.real:
                    return (op1.value_real == op2.value_real);
                case const_type.handle:
                    if (op1.value.handle_index != op2.value.handle_index)
                        return false;
                    if (op1.select != op2.select)
                        return false;
                    break;
                case const_type.spaceid:
                    return (op1.value.spaceid == op2.value.spaceid);
                default:
                    // Nothing additional to compare
                    break;
            }
            return true;
        }

        public static bool operator <(ConstTpl op1, ConstTpl op2)
        {
            if (op1.type != op2.type) return (op1.type < op2.type);
            switch (op1.type)
            {
                case const_type.real:
                    return (op1.value_real < op2.value_real);
                case const_type.handle:
                    if (op1.value.handle_index != op2.value.handle_index)
                        return (op1.value.handle_index < op2.value.handle_index);
                    if (op1.select != op2.select) return (op1.select < op2.select);
                    break;
                case const_type.spaceid:
                    return (op1.value.spaceid < op2.value.spaceid);
                default:            // Nothing additional to compare
                    break;
            }
            return false;
        }

        public static bool operator >(ConstTpl op1, ConstTpl op2)
        {
            if (op1.type != op2.type) return (op1.type > op2.type);
            switch (op1.type) {
                case const_type.real:
                    return (op1.value_real > op2.value_real);
                case const_type.handle:
                    if (op1.value.handle_index != op2.value.handle_index)
                        return (op1.value.handle_index > op2.value.handle_index);
                    if (op1.select != op2.select) return (op1.select > op2.select);
                    break;
                case const_type.spaceid:
                    return (op1.value.spaceid > op2.value.spaceid);
                default:
                    // Nothing additional to compare
                    break;
            }
            return false;
        }

        public ulong getReal() => value_real;

        public AddrSpace getSpace() => value.spaceid;

        public int getHandleIndex() => value.handle_index;

        public const_type getType() => type;

        public v_field getSelect() => select;

        public ulong fix(ParserWalker walker)
        { // Get the value of the ConstTpl in context
          // NOTE: if the property is dynamic this returns the property
          // of the temporary storage
            switch (type)
            {
                case const_type.j_start:
                    return walker.getAddr().getOffset(); // Fill in starting address placeholder with real address
                case const_type.j_next:
                    return walker.getNaddr().getOffset(); // Fill in next address placeholder with real address
                case const_type.j_next2:
                    return walker.getN2addr().getOffset(); // Fill in next2 address placeholder with real address
                case const_type.j_flowref:
                    return walker.getRefAddr().getOffset();
                case const_type.j_flowref_size:
                    return (ulong)walker.getRefAddr().getAddrSize();
                case const_type.j_flowdest:
                    return walker.getDestAddr().getOffset();
                case const_type.j_flowdest_size:
                    return (ulong)walker.getDestAddr().getAddrSize();
                case const_type.j_curspace_size:
                    return walker.getCurSpace().getAddrSize();
                case const_type.j_curspace:
                    return (ulong)(ulong)walker.getCurSpace();
                case const_type.handle:
                    {
                        FixedHandle hand = walker.getFixedHandle(value.handle_index);
                        switch (select)
                        {
                            case v_field.v_space:
                                if (hand.offset_space == (AddrSpace)null)
                                    return (ulong)(ulong)hand.space;
                                return (ulong)(ulong)hand.temp_space;
                            case v_field.v_offset:
                                if (hand.offset_space == (AddrSpace)null)
                                    return hand.offset_offset;
                                return hand.temp_offset;
                            case v_field.v_size:
                                return hand.size;
                            case v_field.v_offset_plus:
                                if (hand.space != walker.getConstSpace()) {
                                    // If we are not a constant
                                    if (hand.offset_space == (AddrSpace)null)
                                        return hand.offset_offset + (value_real & 0xffff); // Adjust offset by truncation amount
                                    return hand.temp_offset + (value_real & 0xffff);
                                }
                                else {
                                    // If we are a constant, we want to return a shifted value
                                    ulong val;
                                    if (hand.offset_space == (AddrSpace)null)
                                        val = hand.offset_offset;
                                    else
                                        val = hand.temp_offset;
                                    val >>= 8UL * (value_real >> (int)16);
                                    return val;
                                }
                        }
                        break;
                    }
                case const_type.j_relative:
                case const_type.real:
                    return value_real;
                case const_type.spaceid:
                    return (ulong)(ulong)value.spaceid;
            }
            return 0;           // Should never reach here
        }

        public AddrSpace fixSpace(ParserWalker walker)
        {               // Get the value of the ConstTpl in context
                        // when we know it is a space
            switch (type) {
                case const_type.j_curspace:
                    return walker.getCurSpace();
                case const_type.handle:
                    {
                        FixedHandle hand = walker.getFixedHandle(value.handle_index);
                        switch (select)
                        {
                            case v_field.v_space:
                                if (hand.offset_space == (AddrSpace)null)
                                    return hand.space;
                                return hand.temp_space;
                            default:
                                break;
                        }
                        break;
                    }
                case const_type.spaceid:
                    return value.spaceid;
                case const_type.j_flowref:
                    return walker.getRefAddr().getSpace();
                default:
                    break;
            }
            throw new LowlevelError("ConstTpl is not a spaceid as expected");
        }

        public void transfer(List<HandleTpl> @params)
        {
            // Replace old handles with new handles
            if (type != const_type.handle) return;
            HandleTpl newhandle = @params[value.handle_index] ;

            switch (select) {
                case v_field.v_space:
                    *this = newhandle.getSpace();
                    break;
                case v_field.v_offset:
                    *this = newhandle.getPtrOffset();
                    break;
                case v_field.v_offset_plus:
                    {
                        ulong tmp = value_real;
                        *this = newhandle.getPtrOffset();
                        if (type == const_type.real)
                        {
                            value_real += (tmp & 0xffff);
                        }
                        else if ((type == const_type.handle) && (select == v_field.v_offset))
                        {
                            select = v_field.v_offset_plus;
                            value_real = tmp;
                        }
                        else
                            throw new LowlevelError("Cannot truncate macro input in this way");
                        break;
                    }
                case v_field.v_size:
                    *this = newhandle.getSize();
                    break;
            }
        }

        public bool isZero() => ((type == const_type.real) && (value_real == 0));

        public void changeHandleIndex(List<int> handmap)
        {
            if (type == const_type.handle)
                value.handle_index = handmap[value.handle_index];
        }

        public void fillinSpace(FixedHandle hand, ParserWalker walker)
        {
            // Fill in the space portion of a FixedHandle, base on this ConstTpl
            switch (type) {
                case const_type.j_curspace:
                    hand.space = walker.getCurSpace();
                    return;
                case const_type.handle:
                    {
                        FixedHandle otherhand = walker.getFixedHandle(value.handle_index);
                        switch (select) {
                            case v_field.v_space:
                                hand.space = otherhand.space;
                                return;
                            default:
                                break;
                        }
                        break;
                    }
                case const_type.spaceid:
                    hand.space = value.spaceid;
                    return;
                default:
                    break;
            }
            throw new LowlevelError("ConstTpl is not a spaceid as expected");
        }

        public void fillinOffset(FixedHandle hand, ParserWalker walker)
        {
            // Fillin the offset portion of a FixedHandle, based on this ConstTpl
            // If the offset value is dynamic, indicate this in the handle
            // we don't just fill in the temporary variable offset
            // we assume hand.space is already filled in
            if (type == const_type.handle) {
                FixedHandle otherhand = walker.getFixedHandle(value.handle_index);
                hand.offset_space = otherhand.offset_space;
                hand.offset_offset = otherhand.offset_offset;
                hand.offset_size = otherhand.offset_size;
                hand.temp_space = otherhand.temp_space;
                hand.temp_offset = otherhand.temp_offset;
            }
            else {
                hand.offset_space = (AddrSpace)null;
                hand.offset_offset = hand.space.wrapOffset(fix(walker));
            }
        }

        public void saveXml(TextWriter s)
        {
            s.Write("<const_tpl type=\"");
            switch (type) {
                case const_type.real:
                    s.Write($"real\" val=\"0x{value_real:X}\"/>");
                    break;
                case const_type.handle:
                    s.Write($"handle\" val=\"{value.handle_index}\" s=\"");
                    printHandleSelector(s, select);
                    s.Write("\"");
                    if (select == v_field.v_offset_plus)
                        s.Write($" plus=\"0x{value_real:X}\"");
                    s.Write("/>");
                    break;
                case const_type.j_start:
                    s.Write("start\"/>");
                    break;
                case const_type.j_next:
                    s.Write("next\"/>");
                    break;
                case const_type.j_next2:
                    s.Write("next2\"/>");
                    break;
                case const_type.j_curspace:
                    s.Write("curspace\"/>");
                    break;
                case const_type.j_curspace_size:
                    s.Write("curspace_size\"/>");
                    break;
                case const_type.spaceid:
                    s.Write($"spaceid\" name=\"{value.spaceid.getName()}\"/>");
                    break;
                case const_type.j_relative:
                    s.Write($"relative\" val=\"0x{value_real:X}\"/>");
                    break;
                case const_type.j_flowref:
                    s.Write("flowref\"/>");
                    break;
                case const_type.j_flowref_size:
                    s.Write("flowref_size\"/>");
                    break;
                case const_type.j_flowdest:
                    s.Write("flowdest\"/>");
                    break;
                case const_type.j_flowdest_size:
                    s.Write("flowdest_size\"/>");
                    break;
            }
        }

        public void restoreXml(Element el, AddrSpaceManager manage)
        {
            string typestring = el.getAttributeValue("type");
            if (typestring == "real") {
                value_real = ulong.Parse(el.getAttributeValue("val"));
            }
            else if (typestring == "handle") {
                type = const_type.handle;
                value.handle_index = int.Parse(el.getAttributeValue("val"));
                select = readHandleSelector(el.getAttributeValue("s"));
                if (select == v_field.v_offset_plus) {
                    value_real = ulong.Parse(el.getAttributeValue("plus"));
                }
            }
            else if (typestring == "start") {
                type = const_type.j_start;
            }
            else if (typestring == "next") {
                type = const_type.j_next;
            }
            else if (typestring == "next2") {
                type = const_type.j_next2;
            }
            else if (typestring == "curspace") {
                type = const_type.j_curspace;
            }
            else if (typestring == "curspace_size") {
                type = const_type.j_curspace_size;
            }
            else if (typestring == "spaceid") {
                type = const_type.spaceid;
                value.spaceid = manage.getSpaceByName(el.getAttributeValue("name"));
            }
            else if (typestring == "relative") {
                type = const_type.j_relative;
                value_real = ulong.Parse(el.getAttributeValue("val"));
            }
            else if (typestring == "flowref") {
                type = const_type.j_flowref;
            }
            else if (typestring == "flowref_size") {
                type = const_type.j_flowref_size;
            }
            else if (typestring == "flowdest") {
                type = const_type.j_flowdest;
            }
            else if (typestring == "flowdest_size") {
                type = const_type.j_flowdest_size;
            }
            else
                throw new LowlevelError("Bad constant type");
        }
    }
}
