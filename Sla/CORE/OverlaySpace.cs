using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief An overlay space.
    /// A different code and data layout that occupies the same memory as another address space.
    /// Some compilers use this concept to increase the logical size of a program without increasing
    /// its physical memory requirements.  An overlay space allows the same physical location to contain
    /// different code and be labeled with different symbols, depending on context.
    /// From the point of view of reverse engineering, the different code and symbols are viewed
    /// as a logically distinct space.
    public class OverlaySpace : AddrSpace
    {
        ///< Space being overlayed
        private AddrSpace baseSpace;

        /// \param m is the address space manager
        /// \param t is the processor translator
        public OverlaySpace(AddrSpaceManager m, Translate t)
            : base(m, t, spacetype.IPTR_PROCESSOR)
        {
            baseSpace = null;
            setFlags(Properties.overlay);
        }
        public virtual AddrSpace getContain()
        {
            return baseSpace;
        }

        public virtual void saveXml(StreamWriter s)
        {
            s.Write("<space_overlay");
            Globals.a_v(s, "name", name);
            Globals.a_v_i(s, "index", index);
            Globals.a_v(s, "base", baseSpace.getName());
            s.WriteLine("/>");
        }
        
        public virtual void decode(ref Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_SPACE_OVERLAY);
            name = decoder.readString(AttributeId.ATTRIB_NAME);
            index = (int)decoder.readSignedInteger(AttributeId.ATTRIB_INDEX);
            baseSpace = decoder.readSpace(AttributeId.ATTRIB_BASE);
            decoder.closeElement(elemId);
            addressSize = baseSpace.getAddrSize();
            wordsize = baseSpace.getWordSize();
            delay = baseSpace.getDelay();
            deadcodedelay = baseSpace.getDeadcodeDelay();
            calcScaleMask();
            if (baseSpace.isBigEndian()) {
                setFlags(Properties.big_endian);
            }
            if (baseSpace.hasPhysical()) {
                setFlags(Properties.hasphysical);
            }
        }
    }
}
