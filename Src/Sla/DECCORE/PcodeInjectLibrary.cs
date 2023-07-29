using ghidra;
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
    /// \brief A collection of p-code injection payloads
    ///
    /// This is a container of InjectPayload objects that can be applied for a
    /// specific Architecture.  Payloads can be read in via stream (decodeInject()) and manually
    /// via manualCallFixup() and manualCallOtherFixup().  Each payload is assigned an integer \e id
    /// when it is read in, and getPayload() fetches the payload during analysis. The library
    /// also associates the formal names of payloads with the id. Payloads of different types,
    /// CALLFIXUP_TYPE, CALLOTHERFIXUP_TYPE, etc., are stored in separate namespaces.
    ///
    /// This is an abstract base class. The derived classes determine the type of storage used
    /// by the payloads.  The library also provides a reusable InjectContext object to match
    /// the payloads, which can be obtained via getCachedContext().
    internal abstract class PcodeInjectLibrary
    {
        /// The Architecture to which the injection payloads apply
        protected Architecture glb;
        /// Offset within \e unique space for allocating temporaries within a payload
        protected uint tempbase;
        /// Registered injections
        protected List<InjectPayload> injection;
        /// Map of registered call-fixup names to injection id
        protected Dictionary<string, int> callFixupMap;
        /// Map of registered callother-fixup names to injection id
        protected Dictionary<string, int> callOtherFixupMap;
        /// Map of registered mechanism names to injection id
        protected Dictionary<string, int> callMechFixupMap;
        /// Map of registered script names to ExecutablePcode id
        protected Dictionary<string, int> scriptMap;
        /// Map from injectid to call-fixup name
        protected List<string> callFixupNames;
        /// Map from injectid to callother-fixup target-op name
        protected List<string> callOtherTarget;
        /// Map from injectid to call-mech name
        protected List<string> callMechTarget;
        /// Map from injectid to script name
        protected List<string> scriptNames;

        /// \brief Map a \e call-fixup name to a payload id
        ///
        /// \param fixupName is the formal name of the call-fixup
        /// \param injectid is the integer id
        protected void registerCallFixup(string fixupName, int injectid/* , List<string> targets */)
        {
            pair<Dictionary<string, int>::iterator, bool> check;
            check = callFixupMap.insert(pair<string, int>(fixupName, injectid));
            if (!check.second)      // This symbol is already mapped
                throw new LowlevelError("Duplicate <callfixup>: " + fixupName);
            while (callFixupNames.size() <= injectid)
                callFixupNames.Add("");
            callFixupNames[injectid] = fixupName;
        }

        /// \brief Map a \e callother-fixup name to a payload id
        ///
        /// \param fixupName is the formal name of the callother-fixup
        /// \param injectid is the integer id
        protected void registerCallOtherFixup(string fixupName, int injectid)
        {
            pair<Dictionary<string, int>::iterator, bool> check;
            check = callOtherFixupMap.insert(pair<string, int>(fixupName, injectid));
            if (!check.second)      // This symbol is already mapped
                throw new LowlevelError("Duplicate <callotherfixup>: " + fixupName);
            while (callOtherTarget.size() <= injectid)
                callOtherTarget.Add("");
            callOtherTarget[injectid] = fixupName;
        }

        /// \brief Map a \e call \e mechanism name to a payload id
        ///
        /// \param fixupName is the formal name of the call mechanism
        /// \param injectid is the integer id
        protected void registerCallMechanism(string fixupName,int injectid)
        {
            pair<Dictionary<string, int>::iterator, bool> check;
            check = callMechFixupMap.insert(pair<string, int>(fixupName, injectid));
            if (!check.second)      // This symbol is already mapped
                throw new LowlevelError("Duplicate <callmechanism>: " + fixupName);
            while (callMechTarget.size() <= injectid)
                callMechTarget.Add("");
            callMechTarget[injectid] = fixupName;
        }

        /// \brief Map a \e p-code \e script name to a payload id
        ///
        /// \param scriptName is the formal name of the p-code script
        /// \param injectid is the integer id
        protected void registerExeScript(string scriptName,int injectid)
        {
            pair<Dictionary<string, int>::iterator, bool> check;
            check = scriptMap.insert(pair<string, int>(scriptName, injectid));
            if (!check.second)      // This symbol is already mapped
                throw new LowlevelError("Duplicate <script>: " + scriptName);
            while (scriptNames.size() <= injectid)
                scriptNames.Add("");
            scriptNames[injectid] = scriptName;
        }

        /// \brief Allocate a new InjectPayload object
        ///
        /// This acts as an InjectPayload factory. The formal name and type of the payload are given,
        /// \b this library allocates a new object that fits with its storage scheme and returns the id.
        /// \param sourceName is a string describing the source of the new payload
        /// \param name is the formal name of the payload
        /// \param type is the formal type (CALLFIXUP_TYPE, CALLOTHERFIXUP_TYPE, etc.) of the payload
        /// \return the id associated with the new InjectPayload object
        protected abstract int allocateInject(string sourceName, string name,int type);

        ///\brief Finalize a payload within the library, once the payload is initialized
        ///
        /// This provides the derived class the opportunity to add the payload name to the
        /// symbol tables or do anything else it needs to once the InjectPayload object
        /// has been fully initialized.
        /// \param injectid is the id of the InjectPayload to finalize
        protected abstract void registerInject(int injectid)=0;

        public PcodeInjectLibrary(Architecture g, uint tmpbase)
        {
            glb = g;
            tempbase = tmpbase;
        }

        ~PcodeInjectLibrary()
        {
            List<InjectPayload*>::iterator iter;
            for (iter = injection.begin(); iter != injection.end(); ++iter)
                delete* iter;
        }

        /// Get the (current) offset for building temporary registers
        public uint getUniqueBase() => tempbase;

        /// Map name and type to the payload id
        /// The given name is looked up in a symbol table depending on the given type.
        /// The integer id of the matching InjectPayload is returned.
        /// \param type is the payload type
        /// \param nm is the formal name of the payload
        /// \return the payload id or -1 if there is no matching payload
        public int getPayloadId(int type, string nm)
        {
            Dictionary<string, int>::const_iterator iter;
            if (type == InjectPayload::CALLFIXUP_TYPE)
            {
                iter = callFixupMap.find(nm);
                if (iter == callFixupMap.end())
                    return -1;
            }
            else if (type == InjectPayload::CALLOTHERFIXUP_TYPE)
            {
                iter = callOtherFixupMap.find(nm);
                if (iter == callOtherFixupMap.end())
                    return -1;
            }
            else if (type == InjectPayload::CALLMECHANISM_TYPE)
            {
                iter = callMechFixupMap.find(nm);
                if (iter == callMechFixupMap.end())
                    return -1;
            }
            else
            {
                iter = scriptMap.find(nm);
                if (iter == scriptMap.end())
                    return -1;
            }
            return (*iter).second;
        }

        /// Get the InjectPayload by id
        public InjectPayload getPayload(int id) => injection[id];

        /// Get the call-fixup name associated with an id
        /// \param injectid is an integer id of a call-fixup payload
        /// \return the name of the payload or the empty string
        public string getCallFixupName(int injectid)
        {
            if ((injectid < 0) || (injectid >= callFixupNames.size()))
                return "";
            return callFixupNames[injectid];
        }

        /// Get the callother-fixup name associated with an id
        /// \param injectid is an integer id of a callother-fixup payload
        /// \return the name of the payload or the empty string
        public string getCallOtherTarget(int injectid)
        {
            if ((injectid < 0) || (injectid >= callOtherTarget.size()))
                return "";
            return callOtherTarget[injectid];
        }

        /// Get the call mechanism name associated with an id
        /// \param injectid is an integer id of a call mechanism payload
        /// \return the name of the payload or the empty string
        public string getCallMechanismName(int injectid)
        {
            if ((injectid < 0) || (injectid >= callMechTarget.size()))
                return "";
            return callMechTarget[injectid];
        }

        /// \brief Parse and register an injection payload from a stream element
        ///
        /// The element is one of: \<pcode>, \<callfixup> \<callotherfixup>, etc.
        /// The InjectPayload is allocated and then initialized using the element.
        /// Then the InjectPayload is finalized with the library.
        /// \param src is a string describing the source of the payload being decoded
        /// \param nm is the name of the payload
        /// \param tp is the type of the payload (CALLFIXUP_TYPE, EXECUTABLEPCODE_TYPE, etc.)
        /// \param decoder is the stream decoder
        /// \return the id of the newly registered payload
        public int decodeInject(string src, string suffix, int tp, Decoder decoder)
        {
            int injectid = allocateInject(src, nm, tp);
            getPayload(injectid).decode(decoder);
            registerInject(injectid);
            return injectid;
        }

        /// \brief A method for parsing p-code generated externally for use in debugging
        ///
        /// Instantiate a special InjectPayloadDynamic object initialized with an
        /// \<injectdebug> element.  Within the library, this replaces the original InjectPayload,
        /// allowing its p-code to be \e replayed for debugging purposes.
        /// \param decoder is the stream decoder
        public virtual void decodeDebug(Decoder decoder)
        {
        }

        /// \brief Manually add a call-fixup payload given a compilable snippet of p-code \e source
        ///
        /// The snippet is compiled immediately to produce the payload.
        /// \param name is the formal name of the new payload
        /// \param snippetstring is the compilable snippet of p-code \e source
        /// \return the id of the new payload
        public abstract int manualCallFixup(string name, string snippetstring);

        /// \brief Manually add a callother-fixup payload given a compilable snippet of p-code \e source
        ///
        /// The snippet is compiled immediately to produce the payload. Symbol names for
        /// input and output parameters must be provided to the compiler.
        /// \param name is the formal name of the new payload
        /// \param outname is the name of the output symbol
        /// \param inname is the ordered list of input symbol names
        /// \param snippet is the compilable snippet of p-code \e source
        /// \return the id of the new payload
        public abstract int manualCallOtherFixup(string name, string  outname, List<string> inname,
            string snippet);

        /// \brief Retrieve a reusable context object for \b this library
        ///
        /// The object returned by this method gets passed to the payload inject() method.
        /// The clear() method must be called between uses.
        /// \return the cached context object
        public abstract InjectContext getCachedContext();

        /// \brief Get the array of op-code behaviors for initializing and emulator
        ///
        /// Behaviors are pulled from the underlying architecture in order to initialize
        /// the Emulate object which services the \e p-code \e script payloads.
        /// \return the array of OpBehavior objects indexed by op-code
        public abstract List<OpBehavior> getBehaviors();
    }
}
