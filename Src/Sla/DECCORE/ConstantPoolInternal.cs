using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An implementation of the ConstantPool interface storing records internally in RAM
    /// The CPoolRecord objects are held directly in a map container. This class can be used
    /// as a stand-alone ConstantPool that holds all its records in RAM. Or, it can act as
    /// a local CPoolRecord cache for some other implementation.
    internal class ConstantPoolInternal : ConstantPool
    {
        /// \brief A cheap (efficient) placeholder for a \e reference to a constant pool record
        /// A \b reference can be an open-ended number of (1 or more) integers. In practice, the
        /// most integers we see in a reference is two.  So this is a slightly more efficient
        /// container than an open-ended List.
        /// The field \b a is the first integer, the field \b b is the second integer, or zero
        /// if there is no second integer. The references are ordered lexicographically.
        /// The class also serves to serialize/deserialize references from XML
        internal class CheapSorter
        {
            /// The first integer in a \e reference
            public ulong a;
            /// The second integer in a \e reference (or zero)
            public ulong b;

            /// Construct a zero reference
            public CheapSorter()
            {
                a = 0;
                b = 0;
            }

            /// Copy constructor
            public CheapSorter(CheapSorter op2)
            {
                a = op2.a;
                b = op2.b;
            }

            /// Construct from an array of integers
            public CheapSorter(List<ulong> refs)
            {
                a = refs[0];
                b = (refs.Count > 1) ? refs[1] : 0;
            }

            /// \brief Lexicographic comparison
            /// \param op2 is the reference to compare with \b this
            /// \return \b true if \b this should be ordered before the other reference
            public static bool operator <(CheapSorter op1, CheapSorter op2)
            {
                return (op1.a != op2.a) ? (op1.a <op2.a) : (op1.b <op2.b);
            }

            /// \brief Convert the reference back to a formal array of integers
            /// \param refs is the provided container of integers
            public void apply(List<ulong> refs)
            {
                refs.Add(a);
                refs.Add(b);
            }

            /// Encode the \e reference to a stream
            public void encode(Sla.CORE.Encoder encoder)
            {
                encoder.openElement(ElementId.ELEM_REF);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_A, a);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_B, b);
                encoder.closeElement(ElementId.ELEM_REF);
            }

            /// Decode the \e reference from a stream
            /// Restore \b this \e reference from a \<ref> element
            /// \param decoder is the stream decoder
            public void decode(Sla.CORE.Decoder decoder)
            {
                uint elemId = decoder.openElement(ElementId.ELEM_REF);
                a = decoder.readUnsignedInteger(AttributeId.ATTRIB_A);
                b = decoder.readUnsignedInteger(AttributeId.ATTRIB_B);
                decoder.closeElement(elemId);
            }
        }

        /// A map from \e reference to constant pool record
        private Dictionary<CheapSorter, CPoolRecord> cpoolMap =
            new Dictionary<CheapSorter, CPoolRecord>();

        private override CPoolRecord createRecord(List<ulong> refs)
        {
            CheapSorter sorter(refs);
            pair<Dictionary<CheapSorter, CPoolRecord>::iterator, bool> res;
            res = cpoolMap.emplace(piecewise_construct, forward_as_tuple(sorter), forward_as_tuple());
            if (res.second == false)
                throw new LowlevelError("Creating duplicate entry in constant pool: " + (*res.first).second.getToken());
            return &(*res.first).second;
        }

        public override CPoolRecord getRecord(List<ulong> refs)
        {
            CheapSorter sorter(refs);
            Dictionary<CheapSorter, CPoolRecord>::const_iterator iter = cpoolMap.find(sorter);
            if (iter == cpoolMap.end())
                return (CPoolRecord*)0;
            return &(*iter).second;
        }

        public override bool empty() => cpoolMap.empty();

        public override void clear()
        {
            cpoolMap.Clear();
        }

        public override void encode(Sla.CORE.Encoder encoder)
        {
            Dictionary<CheapSorter, CPoolRecord>::const_iterator iter;
            encoder.openElement(ElementId.ELEM_CONSTANTPOOL);
            for (iter = cpoolMap.begin(); iter != cpoolMap.end(); ++iter)
            {
                iter.Current.Key.encode(encoder);
                (*iter).second.encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_CONSTANTPOOL);
        }

        public override void decode(Sla.CORE.Decoder decoder, TypeFactory typegrp)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_CONSTANTPOOL);
            while (decoder.peekElement() != 0)
            {
                CheapSorter sorter;
                sorter.decode(decoder);
                List<ulong> refs;
                sorter.apply(refs);
                CPoolRecord* newrec = createRecord(refs);
                newrec.decode(decoder, typegrp);
            }
            decoder.closeElement(elemId);
        }
    }
}
