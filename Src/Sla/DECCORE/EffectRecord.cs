using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Description of the indirect effect a sub-function has on a memory range
    /// This object applies only to the specific memory range, which is seen from the
    /// point of view of the calling function as a particular
    /// sub-function gets called. The main enumeration below lists the possible effects.
    internal class EffectRecord
    {
        public enum EffectType
        {
            unaffected = 1, ///< The sub-function does not change the value at all
            killedbycall = 2,   ///< The memory is changed and is completely unrelated to its original value
            return_address = 3, ///< The memory is being used to store the return address
            unknown_effect = 4  ///< An unknown effect (indicates the absence of an EffectRecord)
        }

        private VarnodeData range;        ///< The memory range affected
        private EffectType type;         ///< The type of effect

        /// Constructor for use with decode()
        public EffectRecord()
        {
        }

        /// Copy constructor
        public EffectRecord(EffectRecord op2)
        {
            range = op2.range;
            type = op2.type;
        }

        /// Construct a memory range with an unknown effect
        /// The type is set to \e unknown_effect
        /// \param addr is the start of the memory range
        /// \param size is the number of bytes in the memory range
        public EffectRecord(Address addr, int size)
        {
            range.space = addr.getSpace();
            range.offset = addr.getOffset();
            range.size = (uint)size;
            type = EffectType.unknown_effect;
        }

        /// Construct an effect on a parameter storage location
        /// \param entry is a model of the parameter storage
        /// \param t is the effect type
        public EffectRecord(ParamEntry entry, EffectType t)
        {
            range.space = entry.getSpace();
            range.offset = entry.getBase();
            range.size = (uint)entry.getSize();
            type = t;
        }

        /// Construct an effect on a memory range
        /// \param data is the memory range affected
        /// \param t is the effect type
        public EffectRecord(VarnodeData data, EffectType t)
        {
            range = data;
            type = t;
        }

        /// Get the type of effect
        public EffectType getType() => type;

        /// Get the starting address of the affected range
        public Address getAddress() => new Address(range.space, range.offset);

        /// Get the size of the affected range
        public int getSize() => (int)range.size;

        /// Equality operator
        public static bool operator ==(EffectRecord op1, EffectRecord op2)
        {
            if (op1.range != op2.range) return false;
            return (op1.type == op2.type);
        }

        /// Inequality operator
        public static bool operator !=(EffectRecord op1, EffectRecord op2)
        {
            if (op1.range != op2.range) return true;
            return (op1.type != op2.type);
        }

        /// Encode the record to a stream
        /// Encode just an \<addr> element.  The effect type is indicated by the parent element.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            Address addr = new Address(range.space, range.offset);
            if (   (type == EffectType.unaffected)
                || (type == EffectType.killedbycall)
                || (type == EffectType.return_address))
                addr.encode(encoder, (int)range.size);
            else
                throw new LowlevelError("Bad EffectRecord type");
        }

        /// Decode the record from a stream
        /// Parse an \<addr> element to get the memory range. The effect type is inherited from the parent.
        /// \param grouptype is the effect inherited from the parent
        /// \param decoder is the stream decoder
        public void decode(EffectType grouptype, Sla.CORE.Decoder decoder)
        {
            type = grouptype;
            range = VarnodeData.decode(decoder);
        }

        /// \brief Compare two EffectRecords by their start Address
        /// \param op1 is the first record to compare
        /// \param op2 is the other record to compare
        /// \return \b true if \b this should be ordered before the other record
        public static int compareByAddress(EffectRecord op1, EffectRecord op2)
        {
            return (op1.range.space != op2.range.space)
                ? op1.range.space.getIndex().CompareTo(op2.range.space.getIndex())
                : op1.range.offset.CompareTo(op2.range.offset);
        }
    }
}
