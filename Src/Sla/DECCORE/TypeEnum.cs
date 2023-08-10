using ghidra;
using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief An enumerated Datatype object: an integer with named values.
    ///
    /// This supports combinations of the enumeration values (using logical OR and bit-wise complement)
    /// by defining independent \b bit-fields.
    internal class TypeEnum : TypeBase
    {
        // friend class TypeFactory;
        /// Map from integer to name
        protected Dictionary<ulong, string> namemap;
        /// Masks for each bitfield within the enum
        protected List<ulong> masklist;

        /// Establish the value . name map
        /// Set the map. Calculate the independent bit-fields within the named values of the enumeration
        /// Two bits are in the same bit-field if there is a name in the map whose value
        /// has those two bits set.  Bit-fields must be a contiguous range of bits.
        protected void setNameMap(Dictionary<ulong, string> nmap)
        {
            Dictionary<ulong, string>::const_iterator iter;
            ulong curmask, lastmask;
            int maxbit;
            int curmaxbit;
            bool fieldisempty;

            namemap = nmap;
            masklist.Clear();

            flags &= ~(Properties.poweroftwo);

            maxbit = 8 * size - 1;

            curmaxbit = 0;
            while (curmaxbit <= maxbit) {
                curmask = 1;
                curmask <<= curmaxbit;
                lastmask = 0;
                fieldisempty = true;
                while (curmask != lastmask) {
                    // Repeat until there is no change in the current mask
                    lastmask = curmask;     // Note changes from last time through

                    for (iter = namemap.begin(); iter != namemap.end(); ++iter) {
                        // For every named enumeration value
                        ulong val = iter.Current.Key;
                        if ((val & curmask) != 0) {
                            // If the value shares ANY bits in common with the current mask
                            curmask |= val;     // Absorb ALL defined bits of the value into the current mask
                            fieldisempty = false;
                        }
                    }

                    // Fill in any holes in the mask (bit field must consist of contiguous bits
                    int lsb = Globals.leastsigbit_set(curmask);
                    int msb = Globals.mostsigbit_set(curmask);
                    if (msb > curmaxbit)
                        curmaxbit = msb;

                    ulong mask1 = 1;
                    mask1 = (mask1 << lsb) - 1;     // every bit below lsb is set to 1
                    ulong mask2 = 1;
                    mask2 <<= msb;
                    mask2 <<= 1;
                    mask2 -= 1;                  // every bit below or equal to msb is set to 1
                    curmask = mask1 ^ mask2;
                }
                if (fieldisempty) {
                    // If no value hits this bit
                    if (!masklist.empty())
                        masklist[masklist.Count - 1] |= curmask; // Include the bit with the previous mask
                    else
                        masklist.Add(curmask);
                }
                else
                    masklist.Add(curmask);
                curmaxbit += 1;
            }
            if (masklist.size() > 1)
                flags |= Properties.poweroftwo;
        }

        /// Restore \b this enum data-type from a stream
        /// Parse a \<type> element with children describing each specific enumeration value.
        /// \param decoder is the stream decoder
        /// \param typegrp is the factory owning \b this data-type
        internal void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            //  uint elemId = decoder.openElement();
            decodeBasic(decoder);
            submeta = (metatype == type_metatype.TYPE_INT)
                ? sub_metatype.SUB_INT_ENUM
                : sub_metatype.SUB_UINT_ENUM;
            Dictionary<ulong, string> nmap;

            while (true) {
                uint childId = decoder.openElement();
                if (childId == 0) break;
                ulong val = 0;
                string nm;
                while(true)
                {
                    AttributeId  attrib = decoder.getNextAttributeId();
                    if (attrib == 0) break;
                    if (attrib == AttributeId.ATTRIB_VALUE) {
                        long valsign = decoder.readSignedInteger(); // Value might be negative
                        val = (ulong)valsign & Globals.calc_mask(size);
                    }
                    else if (attrib == AttributeId.ATTRIB_NAME)
                        nm = decoder.readString();
                }
                if (nm.size() == 0)
                    throw new LowlevelError(name + ": TypeEnum field missing name attribute");
                nmap[val] = nm;
                decoder.closeElement(childId);
            }
            setNameMap(nmap);
            //  decoder.closeElement(elemId);
        }

        /// Construct from another TypeEnum
        public TypeEnum(TypeEnum op)
            : base(op)
        {
            namemap = op.namemap;
            masklist = op.masklist;
            flags |= (op.flags & Properties.poweroftwo) | Properties.enumtype;
        }

        /// Construct from a size and meta-type (TYPE_INT or type_metatype.TYPE_UINT)
        public TypeEnum(int s, type_metatype m)
            : base(s, m)
        {
            flags |= enumtype;
            submeta = (m == type_metatype.TYPE_INT)
                ? sub_metatype.SUB_INT_ENUM
                : sub_metatype.SUB_UINT_ENUM;
        }

        /// Construct from a size, meta-type, and name
        public TypeEnum(int s, type_metatype m, string nm)
            : base(s, m, nm)
        {
            flags |= Properties.enumtype;
            submeta = (m == type_metatype.TYPE_INT)
                ? sub_metatype.SUB_INT_ENUM
                : sub_metatype.SUB_UINT_ENUM;
        }

        /// Beginning of name map
        public IEnumerator<KeyValuePair<ulong, string>> beginEnum() => namemap.GetEnumerator();

        ///// End of name map
        //public IEnumerator<KeyValuePair<ulong, string>> endEnum() => namemap.end();

        /// Recover the named representation
        /// Given a specific value of the enumeration, calculate the named representation of that value.
        /// The representation is returned as a list of names that must logically ORed and possibly complemented.
        /// If no representation is possible, no names will be returned.
        /// \param val is the value to find the representation for
        /// \param valnames will hold the returned list of names
        /// \return true if the representation needs to be complemented
        public bool getMatches(ulong val, List<string> matchname)
        {
            int count;

            for (count = 0; count < 2; ++count) {
                bool allmatch = true;
                if (val == 0) {
                    // Zero handled specially, it crosses all masks
                    string value;
                    if (namemap.TryGetValue(val, out value))
                        valnames.Add(value);
                    else
                        allmatch = false;
                }
                else {
                    for (int i = 0; i < masklist.size(); ++i) {
                        ulong maskedval = val & masklist[i];
                        if (maskedval == 0) // No component of -val- in this mask
                            continue;       // print nothing
                        string value;
                        if (namemap.TryGetValue(maskedval, out value))
                            valnames.Add(value); // Found name for this component
                        else {
                            // If no name for this component
                            allmatch = false;           // Give up on representation
                            break;              // Stop searching for other components
                        }
                    }
                }
                if (allmatch)           // If we have a complete representation
                    return (count == 1);        // Return whether we represented original value or complement
                val = val ^ Globals.calc_mask(size);    // Switch value we are trying to represent (to complement)
                valnames.Clear();           // Clear out old attempt
            }
            return false;   // If we reach here, no representation was possible, -valnames- is empty
        }

        public override int compare(Datatype op, int level)
        {
            return compareDependency(op);
        }

        public override int compareDependency(Datatype op)
        {
            int res = base.compareDependency(op); // Compare as basic types first
            if (res != 0) return res;

            TypeEnum te = (TypeEnum) op;
            Dictionary<ulong, string>::const_iterator iter1, iter2;

            if (namemap.Count != te.namemap.Count) {
                return (namemap.Count < te.namemap.Count) ? -1 : 1;
            }
            iter1 = namemap.begin();
            iter2 = te.namemap.begin();
            while (iter1 != namemap.end()) {
                if ((*iter1).first != (*iter2).first)
                    return ((*iter1).first < (*iter2).first) ? -1 : 1;
                if ((*iter1).second != (*iter2).second)
                    return ((*iter1).second < (*iter2).second) ? -1 : 1;
                ++iter1;
                ++iter2;
            }
            return 0;
        }

        public override Datatype clone() => new TypeEnum(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (typedefImm != (Datatype)null) {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            encoder.writeString(AttributeId.ATTRIB_ENUM, "true");
            foreach (KeyValuePair<ulong, string> pair in namemap) {
                encoder.openElement(ElementId.ELEM_VAL);
                encoder.writeString(AttributeId.ATTRIB_NAME, pair.Value);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_VALUE, pair.Key);
                encoder.closeElement(ElementId.ELEM_VAL);
            }
            encoder.closeElement(ElementId.ELEM_TYPE);
        }
    }
}
