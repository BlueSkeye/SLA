using ghidra;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Manager/container for description objects (UserPcodeOp) of user defined p-code ops
    ///
    /// The description objects are referenced by the CALLOTHER constant id, (or by name during initialization).
    /// During initialize(), every user defined p-code op presented by the Architecture is
    /// assigned a default UnspecializedPcodeOp description.  Further processing of the .cspec or .pspec
    /// may reassign a more specialized description object by parsing specific tags using
    /// on of \b this class's parse* methods.
    internal class UserOpManage
    {
        /// Description objects indexed by CALLOTHER constant id
        private List<UserPcodeOp> useroplist;
        /// A map from the name of the user defined operation to a description object
        private Dictionary<string, UserPcodeOp> useropmap;
        /// Segment operations supported by this Architecture
        private List<SegmentOp> segmentop;
        /// (Single) volatile read operation
        private VolatileReadOp vol_read;
        /// (Single) volatile write operation
        private VolatileWriteOp vol_write;

        /// Insert a new UserPcodeOp description object in the map(s)
        /// Add the description to the mapping by index and the mapping by name. Make same basic
        /// sanity checks for conflicting values and duplicate operations and throw an
        /// exception if there's a problem.
        /// \param op is the new description object
        private void registerOp(UserPcodeOp op)
        {
            int4 ind = op.getIndex();
            if (ind < 0) throw new LowlevelError("UserOp not assigned an index");

            map<string, UserPcodeOp*>::iterator iter;
            iter = useropmap.find(op.getName());
            if (iter != useropmap.end())
            {
                UserPcodeOp* other = (*iter).second;
                if (other.getIndex() != ind)
                    throw new LowlevelError("Conflicting indices for userop name " + op.getName());
            }

            while (useroplist.size() <= ind)
                useroplist.push_back((UserPcodeOp*)0);
            if (useroplist[ind] != (UserPcodeOp*)0)
            {
                if (useroplist[ind].getName() != op.getName())
                    throw new LowlevelError("User op " + op.getName() + " has same index as " + useroplist[ind].getName());
                // We assume this registration customizes an existing userop
                delete useroplist[ind];     // Delete the old spec
            }
            useroplist[ind] = op;       // Index crossref
            useropmap[op.getName()] = op; // Name crossref

            SegmentOp* s_op = dynamic_cast<SegmentOp*>(op);
            if (s_op != (SegmentOp*)0)
            {
                int4 index = s_op.getSpace().getIndex();

                while (segmentop.size() <= index)
                    segmentop.push_back((SegmentOp*)0);

                if (segmentop[index] != (SegmentOp*)0)
                    throw new LowlevelError("Multiple segmentops defined for same space");
                segmentop[index] = s_op;
                return;
            }
            VolatileReadOp* tmpVolRead = dynamic_cast<VolatileReadOp*>(op);
            if (tmpVolRead != (VolatileReadOp*)0)
            {
                if (vol_read != (VolatileReadOp*)0)
                    throw new LowlevelError("Multiple volatile reads registered");
                vol_read = tmpVolRead;
                return;
            }
            VolatileWriteOp* tmpVolWrite = dynamic_cast<VolatileWriteOp*>(op);
            if (tmpVolWrite != (VolatileWriteOp*)0)
            {
                if (vol_write != (VolatileWriteOp*)0)
                    throw new LowlevelError("Multiple volatile writes registered");
                vol_write = tmpVolWrite;
            }
        }

        /// Construct an empty manager
        public UserOpManage()
        {
            vol_read = (VolatileReadOp*)0;
            vol_write = (VolatileWriteOp*)0;
        }

        ~UserOpManage()
        {
            List<UserPcodeOp*>::iterator iter;

            for (iter = useroplist.begin(); iter != useroplist.end(); ++iter)
            {
                UserPcodeOp* userop = *iter;
                if (userop != (UserPcodeOp*)0)
                    delete userop;
            }
        }

        /// Initialize description objects for all user defined ops
        /// Every user defined p-code op is initially assigned an UnspecializedPcodeOp description,
        /// which may get overridden later.
        /// \param glb is the Architecture from which to draw user defined operations
        public void initialize(Architecture glb)
        {
            List<string> basicops;
            glb.translate.getUserOpNames(basicops);
            for (uint4 i = 0; i < basicops.size(); ++i)
            {
                if (basicops[i].size() == 0) continue;
                UserPcodeOp* userop = new UnspecializedPcodeOp(glb, basicops[i], i);
                registerOp(userop);
            }
        }

        /// Create any required operations if they weren't explicitly defined
        /// Establish defaults for necessary operators not already defined.
        /// Currently this forces volatile read/write operations to exist.
        /// \param glb is the owning Architecture
        public void setDefaults(Architecture glb)
        {
            if (vol_read == (VolatileReadOp*)0)
            {
                VolatileReadOp* volread = new VolatileReadOp(glb, "read_volatile", useroplist.size(), false);
                registerOp(volread);
            }
            if (vol_write == (VolatileWriteOp*)0)
            {
                VolatileWriteOp* volwrite = new VolatileWriteOp(glb, "write_volatile", useroplist.size(), false);
                registerOp(volwrite);
            }
        }

        /// Number of segment operations supported
        public int4 numSegmentOps() => segmentop.size();

        /// Retrieve a user-op description object by index
        /// \param i is the index
        /// \return the indicated user-op description
        public UserPcodeOp getOp(int4 i)
        {
            if (i >= useroplist.size()) return (UserPcodeOp*)0;
            return useroplist[i];
        }

        /// Retrieve description by name
        /// \param nm is the low-level operation name
        /// \return the matching description object or NULL
        public UserPcodeOp getOp(string nm)
        {
            map<string, UserPcodeOp*>::const_iterator iter;
            iter = useropmap.find(nm);
            if (iter == useropmap.end()) return (UserPcodeOp*)0;
            return (*iter).second;
        }

        /// Retrieve a segment-op description object by index
        /// \param i is the index
        /// \return the indicated segment-op description
        public SegmentOp getSegmentOp(int4 i)
        {
            if (i >= segmentop.size()) return (SegmentOp*)0;
            return segmentop[i];
        }

        /// Get (the) volatile read description
        public VolatileReadOp getVolatileRead() => vol_read;

        /// Get (the) volatile write description
        public VolatileWriteOp getVolatileWrite() => vol_write;

        /// Parse a \<segmentop> element
        /// Create a SegmentOp description object based on the element and
        /// register it with \b this manager.
        /// \param decoder is the stream decoder
        /// \param glb is the owning Architecture
        public void decodeSegmentOp(Decoder decoder, Architecture glb)
        {
            SegmentOp* s_op;
            s_op = new SegmentOp(glb, "", useroplist.size());
            try
            {
                s_op.decode(decoder);
                registerOp(s_op);
            }
            catch (LowlevelError err) {
                delete s_op;
                throw err;
            }
        }

        /// Parse a \<volatile> element
        /// Create either a VolatileReadOp or VolatileWriteOp description object based on
        /// the element and register it with \b this manager.
        /// \param decoder is the stream decoder
        /// \param glb is the owning Architecture
        public void decodeVolatile(Decoder decoder, Architecture glb)
        {
            string readOpName;
            string writeOpName;
            bool functionalDisplay = false;
            for (; ; )
            {
                uint4 attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_INPUTOP)
                {
                    readOpName = decoder.readString();
                }
                else if (attribId == ATTRIB_OUTPUTOP)
                {
                    writeOpName = decoder.readString();
                }
                else if (attribId == ATTRIB_FORMAT)
                {
                    string format = decoder.readString();
                    if (format == "functional")
                        functionalDisplay = true;
                }
            }
            if (readOpName.size() == 0 || writeOpName.size() == 0)
                throw new LowlevelError("Missing inputop/outputop attributes in <volatile> element");
            VolatileReadOp* vr_op = new VolatileReadOp(glb, readOpName, useroplist.size(), functionalDisplay);
            try
            {
                registerOp(vr_op);
            }
            catch (LowlevelError err) {
                delete vr_op;
                throw err;
            }
            VolatileWriteOp* vw_op = new VolatileWriteOp(glb, writeOpName, useroplist.size(), functionalDisplay);
            try
            {
                registerOp(vw_op);
            }
            catch (LowlevelError err) {
                delete vw_op;
                throw err;
            }
        }

        /// Parse a \<callotherfixup> element
        /// Create an InjectedUserOp description object based on the element
        /// and register it with \b this manager.
        /// \param decoder is the stream decoder
        /// \param glb is the owning Architecture
        public void decodeCallOtherFixup(Decoder decoder, Architecture glb)
        {
            InjectedUserOp* op = new InjectedUserOp(glb, "", 0, 0);
            try
            {
                op.decode(decoder);
                registerOp(op);
            }
            catch (LowlevelError err) {
                delete op;
                throw err;
            }
        }

        /// Parse a \<jumpassist> element
        /// Create a JumpAssistOp description object based on the element
        /// and register it with \b this manager.
        /// \param decoder is the stream decoder
        /// \param glb is the owning Architecture
        public void decodeJumpAssist(Decoder decoder, Architecture glb)
        {
            JumpAssistOp* op = new JumpAssistOp(glb);
            try
            {
                op.decode(decoder);
                registerOp(op);
            }
            catch (LowlevelError err) {
                delete op;
                throw err;
            }
        }

        /// \brief Manually install an InjectedUserOp given just names of the user defined op and the p-code snippet
        ///
        /// An alternate way to attach a call-fixup to user defined p-code ops, without using XML. The
        /// p-code to inject is presented as a raw string to be handed to the p-code parser.
        /// \param useropname is the name of the user defined op
        /// \param outname is the name of the output variable in the snippet
        /// \param inname is the list of input variable names in the snippet
        /// \param snippet is the raw p-code source snippet
        /// \param glb is the owning Architecture
        public void manualCallOtherFixup(string useropname, string outname, List<string> inname,
            string snippet, Architecture glb)
        {
            UserPcodeOp* userop = getOp(useropname);
            if (userop == (UserPcodeOp*)0)
                throw new LowlevelError("Unknown userop: " + useropname);
            if (dynamic_cast<UnspecializedPcodeOp*>(userop) == (UnspecializedPcodeOp*)0)
                throw new LowlevelError("Cannot fixup userop: " + useropname);

            int4 injectid = glb.pcodeinjectlib.manualCallOtherFixup(useropname, outname, inname, snippet);
            InjectedUserOp* op = new InjectedUserOp(glb, useropname, userop.getIndex(), injectid);
            try
            {
                registerOp(op);
            }
            catch (LowlevelError err) {
                delete op;
                throw err;
            }
        }
    }
}
