using Sla.CORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An interface to the pool of \b constant objects for byte-code languages
    ///
    /// This is an abstract base class that acts as a container for CPoolRecords.
    /// A \e reference (1 or more integer constants) maps to an individual CPoolRecord.
    /// A CPoolRecord object can be queried for using getRecord(), and a new object
    /// can be added with putRecord().  Internally, the actual CPoolRecord object
    /// is produced by createRecord().
    internal abstract class ConstantPool
    {
        /// \brief Allocate a new CPoolRecord object, given a \e reference to it
        /// The object will still need to be initialized but is already associated with the reference.
        /// Any issue with allocation (like a dupicate reference) causes an exception.
        /// \param refs is the \e reference of 1 or more identifying integers
        /// \return the new CPoolRecord
        internal abstract CPoolRecord createRecord(List<ulong> refs);
  
        ~ConstantPool()
        {
        }

        /// \brief Retrieve a constant pool record (CPoolRecord) given a \e reference to it
        /// \param refs is the \e reference (made up of 1 or more identifying integers)
        /// \return the matching CPoolRecord or NULL if none matches the reference

        public abstract CPoolRecord getRecord(List<ulong> refs);

        /// \brief A a new constant pool record to \b this database
        /// Given the basic constituents of the record, type, name, and data-type, create
        /// a new CPoolRecord object and associate it with the given \e reference.
        /// \param refs is the \e reference (made up of 1 or more identifying integers)
        /// \param tag is the type of record to create
        /// \param tok is the name associated with the object
        /// \param ct is the data-type associated with the object
        public void putRecord(List<ulong> refs, uint tag, string tok, Datatype ct)
        {
            CPoolRecord newrec = createRecord(refs);
            newrec.tag = tag;
            newrec.token = tok;
            newrec.type = ct;
        }

        /// \brief Restore a CPoolRecord given a \e reference and a stream decoder
        /// A \<cpoolrec> element initializes the new record which is immediately associated
        /// with the \e reference.
        /// \param refs is the \e reference (made up of 1 or more identifying integers)
        /// \param decoder is the given stream decoder
        /// \param typegrp is the TypeFactory used to resolve data-type references in XML
        /// \return the newly allocated and initialized CPoolRecord
        public CPoolRecord decodeRecord(List<ulong> refs, Decoder decoder, TypeFactory typegrp)
        {
            CPoolRecord* newrec = createRecord(refs);
            newrec.decode(decoder, typegrp);
            return newrec;
        }

        /// Is the container empty of records
        public abstract bool empty();

        /// Release any (local) resources
        public abstract void clear();

        /// \brief Encode all records in this container to a stream
        /// (If supported) A \<constantpool> element is written containing \<cpoolrec>
        /// child elements for each CPoolRecord in the container.
        /// \param encoder is the stream encoder
        public abstract void encode(Encoder encoder);

        /// \brief Restore constant pool records from the given stream decoder
        /// (If supported) The container is populated with CPoolRecords initialized
        /// from a \<constantpool> element.
        /// \param decoder is the given stream decoder
        /// \param typegrp is the TypeFactory used to resolve data-type references in the XML
        public abstract void decode(Decoder decoder, TypeFactory typegrp);
    }
}
