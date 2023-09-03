using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Sla.EXTRA;
using System.Diagnostics.CodeAnalysis;

using TrackedSet = System.Collections.Generic.List<Sla.CORE.TrackedContext>;
using static System.Diagnostics.Activity;

namespace Sla.CORE
{
    /// \brief An in-memory implementation of the ContextDatabase interface
    /// Context blobs are held in a partition map on addresses.  Any address within the map
    /// indicates a \e split point, where the value of a context variable was explicitly changed.
    /// Sets of tracked registers are held in a separate partition map.
    public class ContextInternal : ContextDatabase
    {
        /// \brief A context blob, holding context values across some range of code addresses
        /// This is an internal object that allocates the actual "array of words" for a context blob.
        /// An associated mask array holds 1-bits for context variables that were explicitly set for the
        /// specific split point.
        public struct FreeArray
        {
            /// The "array of words" holding context variable values
            public uint[] array;
            /// The mask array indicating which variables are explicitly set
            public uint[] mask;
            /// The number of words in the array
            public int size;

            /// Construct an empty context blob
            public FreeArray()
            {
                size = 0;
                array = new uint[0];
                mask = new uint[0];
            }

            /////< Destructor
            //public ~FreeArray()
            //{
            //    //if (size != 0) {
            //    //    delete[] array;
            //    //    delete[] mask;
            //    //}
            //}

            /// Resize the context blob, preserving old values
            /// The "array of words" and mask array are resized to the given value. Old values are preserved,
            /// chopping off the last values, or appending zeroes, as needed.
            /// \param sz is the new number of words to resize array to
            public void reset(int sz)
            {
                uint[] newarray = new uint[sz];
                uint[] newmask = new uint[sz];
                if (sz != 0) {
                    newarray = new uint[sz];
                    newmask = new uint[sz];
                    Array.Copy(array, newarray, array.Length);
                    Array.Copy(mask, newmask, newmask.Length);
                }
                array = newarray;
                mask = newmask;
                size = sz;
            }

            // TODO : Looks like this assignment operator is never used. Should it be
            // we must transform it to a non static CopyFrom method and make sure to
            // apply it were appropriate. Suggestion : recompile original library and
            // throw an error in this method, so that we could spot were effectively used.
            /////< Assignment operator
            ///// Clone a context blob into \b this.
            ///// \param op2 is the context blob being cloned/copied
            ///// \return a reference to \b this
            //public static FreeArray operator=(FreeArray op2)
            //{
            //    if (size != 0) {
            //        delete[] array;
            //        delete[] mask;
            //    }
            //    array = (uint*)0;
            //    mask = (uint*)0;
            //    size = op2.size;
            //    if (size != 0)
            //    {
            //        array = new uint[size];
            //        mask = new uint[size];
            //        for (int i = 0; i < size; ++i)
            //        {
            //            array[i] = op2.array[i];        // Copy value at split point
            //            mask[i] = 0;			// but not fact that value is being set
            //        }
            //    }
            //    return *this;
            //}
        }

        /// Number of words in a context blob (for this architecture)
        private int size;
        /// Map from context variable name to description object
        private Dictionary<string, ContextBitRange> variables;
        /// Partition map of context blobs (FreeArray)
        private partmap<Address, FreeArray> database;
        /// Partition map of tracked register sets
        private partmap<Address, TrackedSet> trackbase;

        /// \brief Encode a single context block to a stream
        /// The blob is broken up into individual values and written out as a series
        /// of \<set> elements within a parent \<context_pointset> element.
        /// \param encoder is the stream encoder
        /// \param addr is the address of the split point where the blob is valid
        /// \param vec is the array of words holding the blob values
        public void encodeContext(Sla.CORE.Encoder encoder, Address addr, uint[] vec)
        {
            encoder.openElement(ElementId.ELEM_CONTEXT_POINTSET);
            addr.getSpace().encodeAttributes(encoder, addr.getOffset());
            foreach (KeyValuePair<string, ContextBitRange> pair in variables) {
                uint val = pair.Value.getValue(vec);
                encoder.openElement(ElementId.ELEM_SET);
                encoder.writeString(AttributeId.ATTRIB_NAME, pair.Key);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_VAL, val);
                encoder.closeElement(ElementId.ELEM_SET);
            }
            encoder.closeElement(ElementId.ELEM_CONTEXT_POINTSET);
        }

        /// \brief Restore a context blob for given address range from a stream decoder
        /// Parse either a \<context_pointset> or \<context_set> element. In either case,
        /// children are parsed to get context variable values.  Then a context blob is
        /// reconstructed from the values.  The new blob is added to the interval map based
        /// on the address range.  If the start address is invalid, the default value of
        /// the context variables are painted.  The second address can be invalid, if
        /// only a split point is known.
        /// \param decoder is the stream decoder
        /// \param addr1 is the starting address of the given range
        /// \param addr2 is the ending address of the given range
        public void decodeContext(Decoder decoder, Address addr1, Address addr2)
        {
            while (true) {
                uint subId = decoder.openElement();
                if (subId != ElementId.ELEM_SET) {
                    break;
                }
                uint val = (uint)decoder.readUnsignedInteger(AttributeId.ATTRIB_VAL);
                ContextBitRange var = getVariable(decoder.readString(AttributeId.ATTRIB_NAME));
                List<uint[]> vec = new List<uint[]>();
                if (addr1.isInvalid()) {
                    // Invalid addr1, indicates we should set default value
                    uint[] defaultBuffer = getDefaultValue();
                    for (int i = 0; i < size; ++i) {
                        defaultBuffer[i] = 0;
                    }
                    vec.Add(defaultBuffer);
                }
                else {
                    getRegionForSet(vec, addr1, addr2, var.getWord(), var.getMask() << var.getShift());
                }
                for (int i = 0; i < vec.Count; ++i) {
                    var.setValue(vec[i], val);
                }
                decoder.closeElement(subId);
            }
        }

        protected override ContextBitRange getVariable(string nm)
        {
            ContextBitRange? value;

            if (!variables.TryGetValue(nm, out value)) {
                throw new LowlevelError("Non-existent context variable: " + nm);
            }
            return value;
        }

        protected override void getRegionForSet(List<uint[]> res, Address addr1, Address addr2, int num,
            uint mask)
        {
            database.split(addr1);

            // WARNING : The returned enumerator is already set on the first relevant record.
            // DO NOT use MoveNext before reading
            IEnumerator<KeyValuePair<Address, FreeArray>>? aiter = database.begin(addr1);
            IEnumerator<KeyValuePair<Address, FreeArray>>? biter;
            if (!addr2.isInvalid()) {
                database.split(addr2);
                biter = database.begin(addr2);
            }
            else {
                biter = null;
            }
            if (null == aiter) {
                return;
            }
            do {
                // TODO Check the biter test is correctly set BEFORE marking
                if ((null != biter) && (biter.Current.Key == aiter.Current.Key)) {
                    break;
                }
                uint[] context = aiter.Current.Value.array;
                uint[] maskPtr = aiter.Current.Value.mask;
                res.Add(context);
                // Mark that this value is being definitely set
                maskPtr[num] |= mask;
            } while(aiter.MoveNext());
        }

        protected override void getRegionToChangePoint(List<uint[]> res, Address addr,
            int num, uint mask)
        {
            database.split(addr);
            // WARNING : The returned enumerator is already set on the first relevant record.
            // DO NOT use MoveNext before reading
            IEnumerator<KeyValuePair<Address, FreeArray>>? aiter = database.begin(addr);
            uint[] maskArray;
            if (null == aiter) {
                return;
            }
            uint[] vecArray = aiter.Current.Value.array;
            res.Add(vecArray);
            maskArray = aiter.Current.Value.mask;
            maskArray[num] |= mask;
            do {
                vecArray = vecArray = aiter.Current.Value.array;
                maskArray = aiter.Current.Value.mask;
                if ((maskArray[num] & mask) != 0) {
                    // Reached point where this value was definitively set before
                    break;
                }
                res.Add(vecArray);
            } while (aiter.MoveNext());
        }

        protected override uint[] getDefaultValue()
        {
            return database.defaultValue().array;
        }

        public ContextInternal()
        {
            size = 0;
        }

        //public ~ContextInternal()
        //{
        //}

        public override int getContextSize() => size;

        internal override void registerVariable(string nm, int sbit, int ebit)
        {
            if (!database.empty()) {
                throw new LowlevelError(
                    "Cannot register new context variables after database is initialized");
            }
            ContextBitRange bitrange = new ContextBitRange(sbit, ebit);
            int sz = sbit / (8 * sizeof(uint)) + 1;
            if ((ebit / (8 * sizeof(uint)) + 1) != sz) {
                throw new LowlevelError("Context variable does not fit in one word");
            }
            if (sz > size) {
                size = sz;
                database.defaultValue().reset(size);
            }
            variables[nm] = bitrange;
        }

        internal override uint[] getContext(Address addr) => database.getValue(addr).array;

        internal override uint[] getContext(Address addr, out ulong first, out ulong last)
        {
            int valid;
            Address? before = null;
            Address? after = null;
            uint[] res = database.bounds(addr, ref before, ref after, out valid).array;
            if (((valid & 1) != 0) || (before.getSpace() != addr.getSpace())) {
                first = 0;
            }
            else {
                first = before.getOffset();
            }
            last = (((valid & 2) != 0) || (after.getSpace() != addr.getSpace()))
                ? addr.getSpace().getHighest()
                : after.getOffset() - 1;
            return res;
        }

        internal override ref TrackedSet getTrackedDefault()
        {
            return ref trackbase.defaultValue();
        }

        internal override TrackedSet getTrackedSet(Address addr) => trackbase.getValue(addr);

        internal override TrackedSet createSet(Address addr1, Address addr2)
        {
            TrackedSet res = trackbase.clearRange(addr1, addr2);
            if (null == res) {
                throw new BugException();
            }
            res.Clear();
            return res;
        }

        internal override void encode(Sla.CORE.Encoder encoder)
        {
            if (database.empty() && trackbase.empty()) {
                return;
            }
            encoder.openElement(ElementId.ELEM_CONTEXT_POINTS);
            IEnumerator<KeyValuePair<Address, FreeArray>>? fiter = database.begin();
            if (null != fiter) {
                do {
                    // Save context at each changepoint
                    encodeContext(encoder, fiter.Current.Key, fiter.Current.Value.array);
                } while (fiter.MoveNext());
            }
            IEnumerator<KeyValuePair<Address, TrackedSet>>? titer = trackbase.begin();
            if (null != titer) {
                do {
                    encodeTracked(encoder, titer.Current.Key, titer.Current.Value);
                } while (titer.MoveNext());
            }
            encoder.closeElement(ElementId.ELEM_CONTEXT_POINTS);
        }

        internal override void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_CONTEXT_POINTS);
            while(true) {
                uint subId = decoder.openElement();
                if (subId == 0) {
                    break;
                }
                if (subId == ElementId.ELEM_CONTEXT_POINTSET) {
                    AttributeId attribId = decoder.getNextAttributeId();
                    decoder.rewindAttributes();
                    if (attribId == 0) {
                        // Restore the default value
                        decodeContext(decoder, new Address(), new Address());
                    }
                    else {
                        VarnodeData vData = VarnodeData.decodeFromAttributes(decoder);
                        decodeContext(decoder, vData.getAddr(), new Address());
                    }
                }
                else if (subId == ElementId.ELEM_TRACKED_POINTSET) {
                    VarnodeData vData = VarnodeData.decodeFromAttributes(decoder);
                    decodeTracked(decoder, trackbase.split(vData.getAddr()));
                }
                else {
                    throw new LowlevelError("Bad <context_points> tag");
                }
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        internal override void decodeFromSpec(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_CONTEXT_DATA);
            while(true) {
                uint subId = decoder.openElement();
                if (subId == 0) {
                    break;
                }
                Range range = new Range();
                // There MUST be a range
                range.decodeFromAttributes(decoder);
                Address addr1 = range.getFirstAddr();
                Address addr2 = range.getLastAddrOpen(decoder.getAddrSpaceManager());
                if (subId == ElementId.ELEM_CONTEXT_SET) {
                    decodeContext(decoder, addr1, addr2);
                }
                else if (subId == ElementId.ELEM_TRACKED_SET) {
                    decodeTracked(decoder, createSet(addr1, addr2));
                }
                else {
                    throw new LowlevelError("Bad <context_data> tag");
                }
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }
    }
}
