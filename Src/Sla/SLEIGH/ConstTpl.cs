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
            real = 0, handle = 1, j_start = 2, j_next = 3, j_next2 = 4, j_curspace = 5,
            j_curspace_size = 6, spaceid = 7, j_relative = 8,
            j_flowref = 9, j_flowref_size = 10, j_flowdest = 11, j_flowdest_size = 12
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
            //    uintb real;			// an actual constant
            internal AddrSpace spaceid; // Id (pointer) for registered space
            internal int4 handle_index;      // Place holder for run-time determined value
        }
    
        private ValueKind value;
        private uintb value_real;
        private v_field select;     // Which part of handle to use as constant

        private static void printHandleSelector(ostream &s, v_field val)
        {
            switch (val)
            {
                case v_space:
                    s << "space";
                    break;
                case v_offset:
                    s << "offset";
                    break;
                case v_size:
                    s << "size";
                    break;
                case v_offset_plus:
                    s << "offset_plus";
                    break;
            }
        }

        private static v_field readHandleSelector(string name)
        {
            if (name == "space")
                return v_space;
            if (name == "offset")
                return v_offset;
            if (name == "size")
                return v_size;
            if (name == "offset_plus")
                return v_offset_plus;
            throw new LowlevelError("Bad handle selector");
        }

        public ConstTpl()
        {
            type = real;
            value_real = 0;
        }
    
        public ConstTpl(ConstTpl op2)
        {
            type = op2.type;
            value = op2.value;
            value_real = op2.value_real;
            select = op2.select;
        }

        public ConstTpl(const_type tp, uintb val)
        {               // Constructor for real constants
            type = tp;
            value_real = val;
            value.handle_index = 0;
            select = v_space;
        }

        public ConstTpl(const_type tp)
        {               // Constructor for relative jump constants and uniques
            type = tp;
        }

        public ConstTpl(AddrSpace sid)
        {
            type = spaceid;
            value.spaceid = sid;
        }

        public ConstTpl(const_type tp, int4 ht, v_field vf)
        {               // Constructor for handle constant
            type = handle;
            value.handle_index = ht;
            select = vf;
            value_real = 0;
        }

        public ConstTpl(const_type tp, int4 ht, v_field vf, uintb plus)
        {
            type = handle;
            value.handle_index = ht;
            select = vf;
            value_real = plus;
        }

        public bool isConstSpace()
        {
            if (type == spaceid)
                return (value.spaceid->getType() == IPTR_CONSTANT);
            return false;
        }

        public bool isUniqueSpace()
        {
            if (type == spaceid)
                return (value.spaceid->getType() == IPTR_INTERNAL);
            return false;
        }

        public static bool operator ==(ConstTpl op1, ConstTpl op2)
        {
            if (type != op2.type) return false;
            switch (type)
            {
                case real:
                    return (value_real == op2.value_real);
                case handle:
                    if (value.handle_index != op2.value.handle_index) return false;
                    if (select != op2.select) return false;
                    break;
                case spaceid:
                    return (value.spaceid == op2.value.spaceid);
                default:            // Nothing additional to compare
                    break;
            }
            return true;
        }

        public static bool operator <(ConstTpl op1, ConstTpl op2)
        {
            if (type != op2.type) return (type < op2.type);
            switch (type)
            {
                case real:
                    return (value_real < op2.value_real);
                case handle:
                    if (value.handle_index != op2.value.handle_index)
                        return (value.handle_index < op2.value.handle_index);
                    if (select != op2.select) return (select < op2.select);
                    break;
                case spaceid:
                    return (value.spaceid < op2.value.spaceid);
                default:            // Nothing additional to compare
                    break;
            }
            return false;
        }

        public uintb getReal() => value_real;

        public AddrSpace getSpace() => value.spaceid;

        public int4 getHandleIndex() => value.handle_index;

        public const_type getType() => type;

        public v_field getSelect() => select;

        public uintb fix(ParserWalker walker)
        { // Get the value of the ConstTpl in context
          // NOTE: if the property is dynamic this returns the property
          // of the temporary storage
            switch (type)
            {
                case j_start:
                    return walker.getAddr().getOffset(); // Fill in starting address placeholder with real address
                case j_next:
                    return walker.getNaddr().getOffset(); // Fill in next address placeholder with real address
                case j_next2:
                    return walker.getN2addr().getOffset(); // Fill in next2 address placeholder with real address
                case j_flowref:
                    return walker.getRefAddr().getOffset();
                case j_flowref_size:
                    return walker.getRefAddr().getAddrSize();
                case j_flowdest:
                    return walker.getDestAddr().getOffset();
                case j_flowdest_size:
                    return walker.getDestAddr().getAddrSize();
                case j_curspace_size:
                    return walker.getCurSpace()->getAddrSize();
                case j_curspace:
                    return (uintb)(uintp)walker.getCurSpace();
                case handle:
                    {
                        const FixedHandle &hand(walker.getFixedHandle(value.handle_index));
                        switch (select)
                        {
                            case v_space:
                                if (hand.offset_space == (AddrSpace*)0)
                                    return (uintb)(uintp)hand.space;
                                return (uintb)(uintp)hand.temp_space;
                            case v_offset:
                                if (hand.offset_space == (AddrSpace*)0)
                                    return hand.offset_offset;
                                return hand.temp_offset;
                            case v_size:
                                return hand.size;
                            case v_offset_plus:
                                if (hand.space != walker.getConstSpace())
                                { // If we are not a constant
                                    if (hand.offset_space == (AddrSpace*)0)
                                        return hand.offset_offset + (value_real & 0xffff); // Adjust offset by truncation amount
                                    return hand.temp_offset + (value_real & 0xffff);
                                }
                                else
                                {           // If we are a constant, we want to return a shifted value
                                    uintb val;
                                    if (hand.offset_space == (AddrSpace*)0)
                                        val = hand.offset_offset;
                                    else
                                        val = hand.temp_offset;
                                    val >>= 8 * (value_real >> 16);
                                    return val;
                                }
                        }
                        break;
                    }
                case j_relative:
                case real:
                    return value_real;
                case spaceid:
                    return (uintb)(uintp)value.spaceid;
            }
            return 0;           // Should never reach here
        }

        public AddrSpace fixSpace(ParserWalker walker)
        {               // Get the value of the ConstTpl in context
                        // when we know it is a space
            switch (type)
            {
                case j_curspace:
                    return walker.getCurSpace();
                case handle:
                    {
                        const FixedHandle &hand(walker.getFixedHandle(value.handle_index));
                        switch (select)
                        {
                            case v_space:
                                if (hand.offset_space == (AddrSpace*)0)
                                    return hand.space;
                                return hand.temp_space;
                            default:
                                break;
                        }
                        break;
                    }
                case spaceid:
                    return value.spaceid;
                case j_flowref:
                    return walker.getRefAddr().getSpace();
                default:
                    break;
            }
            throw new LowlevelError("ConstTpl is not a spaceid as expected");
        }

        public void transfer(List<HandleTpl*> @params)
        {               // Replace old handles with new handles
            if (type != handle) return;
            HandleTpl* newhandle = params[value.handle_index] ;

            switch (select)
            {
                case v_space:
                    *this = newhandle->getSpace();
                    break;
                case v_offset:
                    *this = newhandle->getPtrOffset();
                    break;
                case v_offset_plus:
                    {
                        uintb tmp = value_real;
                        *this = newhandle->getPtrOffset();
                        if (type == real)
                        {
                            value_real += (tmp & 0xffff);
                        }
                        else if ((type == handle) && (select == v_offset))
                        {
                            select = v_offset_plus;
                            value_real = tmp;
                        }
                        else
                            throw new LowlevelError("Cannot truncate macro input in this way");
                        break;
                    }
                case v_size:
                    *this = newhandle->getSize();
                    break;
            }
        }

        public bool isZero() => ((type==real)&& (value_real == 0));

        public void changeHandleIndex(List<int4> handmap)
        {
            if (type == handle)
                value.handle_index = handmap[value.handle_index];
        }

        public void fillinSpace(FixedHandle hand, ParserWalker walker)
        { // Fill in the space portion of a FixedHandle, base on this ConstTpl
            switch (type)
            {
                case j_curspace:
                    hand.space = walker.getCurSpace();
                    return;
                case handle:
                    {
                        const FixedHandle &otherhand(walker.getFixedHandle(value.handle_index));
                        switch (select)
                        {
                            case v_space:
                                hand.space = otherhand.space;
                                return;
                            default:
                                break;
                        }
                        break;
                    }
                case spaceid:
                    hand.space = value.spaceid;
                    return;
                default:
                    break;
            }
            throw new LowlevelError("ConstTpl is not a spaceid as expected");
        }

        public void fillinOffset(FixedHandle hand, ParserWalker walker)
        { // Fillin the offset portion of a FixedHandle, based on this ConstTpl
          // If the offset value is dynamic, indicate this in the handle
          // we don't just fill in the temporary variable offset
          // we assume hand.space is already filled in
            if (type == handle)
            {
                const FixedHandle &otherhand(walker.getFixedHandle(value.handle_index));
                hand.offset_space = otherhand.offset_space;
                hand.offset_offset = otherhand.offset_offset;
                hand.offset_size = otherhand.offset_size;
                hand.temp_space = otherhand.temp_space;
                hand.temp_offset = otherhand.temp_offset;
            }
            else
            {
                hand.offset_space = (AddrSpace*)0;
                hand.offset_offset = hand.space->wrapOffset(fix(walker));
            }
        }

        public void saveXml(TextWriter s)
        {
            s << "<const_tpl type=\"";
            switch (type)
            {
                case real:
                    s << "real\" val=\"0x" << hex << value_real << "\"/>";
                    break;
                case handle:
                    s << "handle\" val=\"" << dec << value.handle_index << "\" ";
                    s << "s=\"";
                    printHandleSelector(s, select);
                    s << "\"";
                    if (select == v_offset_plus)
                        s << " plus=\"0x" << hex << value_real << "\"";
                    s << "/>";
                    break;
                case j_start:
                    s << "start\"/>";
                    break;
                case j_next:
                    s << "next\"/>";
                    break;
                case j_next2:
                    s << "next2\"/>";
                    break;
                case j_curspace:
                    s << "curspace\"/>";
                    break;
                case j_curspace_size:
                    s << "curspace_size\"/>";
                    break;
                case spaceid:
                    s << "spaceid\" name=\"" << value.spaceid->getName() << "\"/>";
                    break;
                case j_relative:
                    s << "relative\" val=\"0x" << hex << value_real << "\"/>";
                    break;
                case j_flowref:
                    s << "flowref\"/>";
                    break;
                case j_flowref_size:
                    s << "flowref_size\"/>";
                    break;
                case j_flowdest:
                    s << "flowdest\"/>";
                    break;
                case j_flowdest_size:
                    s << "flowdest_size\"/>";
                    break;
            }
        }

        public void restoreXml(Element el, AddrSpaceManager manage)
        {
            const string &typestring(el->getAttributeValue("type"));
            if (typestring == "real")
            {
                type = real;
                istringstream s(el->getAttributeValue("val"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> value_real;
            }
            else if (typestring == "handle")
            {
                type = handle;
                istringstream s(el->getAttributeValue("val"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> value.handle_index;
                select = readHandleSelector(el->getAttributeValue("s"));
                if (select == v_offset_plus)
                {
                    istringstream s2(el->getAttributeValue("plus"));
                    s2.unsetf(ios::dec | ios::hex | ios::oct);
                    s2 >> value_real;
                }
            }
            else if (typestring == "start")
            {
                type = j_start;
            }
            else if (typestring == "next")
            {
                type = j_next;
            }
            else if (typestring == "next2")
            {
                type = j_next2;
            }
            else if (typestring == "curspace")
            {
                type = j_curspace;
            }
            else if (typestring == "curspace_size")
            {
                type = j_curspace_size;
            }
            else if (typestring == "spaceid")
            {
                type = spaceid;
                value.spaceid = manage->getSpaceByName(el->getAttributeValue("name"));
            }
            else if (typestring == "relative")
            {
                type = j_relative;
                istringstream s(el->getAttributeValue("val"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> value_real;
            }
            else if (typestring == "flowref")
            {
                type = j_flowref;
            }
            else if (typestring == "flowref_size")
            {
                type = j_flowref_size;
            }
            else if (typestring == "flowdest")
            {
                type = j_flowdest;
            }
            else if (typestring == "flowdest_size")
            {
                type = j_flowdest_size;
            }
            else
                throw new LowlevelError("Bad constant type");
        }
    }
}
