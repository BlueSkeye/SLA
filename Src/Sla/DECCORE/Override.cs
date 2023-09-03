using Sla.CORE;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A container of commands that override the decompiler's default behavior for a single function
    ///
    /// Information about a particular function that can be overridden includes:
    ///   - sub-functions:  How they are called and where they call to
    ///   - jumptables:     Mark indirect jumps that need multistage analysis
    ///   - deadcode:       Details about how dead code is eliminated
    ///   - data-flow:      Override the interpretation of specific branch instructions
    ///
    /// Commands exist independently of the main data-flow, control-flow, and symbol structures
    /// and survive decompilation restart. A few analyses, mid transformation, insert a new command
    /// to fix a problem that was discovered too late and then force a restart via Funcdata::setRestartPending()
    ///
    /// The class accept new commands via the insert* methods. The decompiler applies them by
    /// calling the apply* or get* methods.
    internal class Override
    {
        /// \brief Enumeration of possible branch overrides
        public enum Branching
        {
            /// No override
            NONE = 0,
            /// Replace primary CALL or RETURN with suitable BRANCH operation
            BRANCH = 1,
            /// Replace primary BRANCH or RETURN with suitable CALL operation
            CALL = 2,
            /// Replace primary BRANCH or RETURN with suitable CALL/RETURN operation
            CALL_RETURN = 3,
            /// Replace primary BRANCH or CALL with a suitable RETURN operation
            RETURN = 4
        }

        /// Force goto on jump at \b targetpc to \b destpc
        private Dictionary<Address, Address> forcegoto = new Dictionary<Address, Address>();
        /// Delay count indexed by address space
        private List<int> deadcodedelay = new List<int>();
        /// Override indirect at \b call-point into direct to \b addr
        private Dictionary<Address, Address> indirectover = new Dictionary<Address, Address>();
        /// Override prototype at \b call-point
        private Dictionary<Address, FuncProto> protoover = new Dictionary<Address, FuncProto>();
        /// Addresses of indirect jumps that need multistage recovery
        private List<Address> multistagejump = new List<Address>();
        /// Override the CALL <. BRANCH
        private Dictionary<Address, Branching> flowoverride = new Dictionary<Address, Branching>();

        /// Clear the entire set of overrides
        private void clear()
        {
            //Dictionary<Address, FuncProto>::iterator iter;

            //for (iter = protoover.begin(); iter != protoover.end(); ++iter)
            //    delete(*iter).second;

            forcegoto.Clear();
            deadcodedelay.Clear();
            indirectover.Clear();
            protoover.Clear();
            multistagejump.Clear();
            flowoverride.Clear();
        }

        /// \brief Generate \e warning message related to a dead code delay
        ///
        /// This is triggered by the insertDeadcodeDelay() command on a specific address space
        /// \param index is the index of the address space
        /// \param glb is the Architecture object
        /// \return the generated message
        private static string generateDeadcodeDelayMessage(int index, Architecture glb)
        {
            AddrSpace spc = glb.getSpace(index);
            string res = $"Restarted to delay deadcode elimination for space: {spc.getName()}";
            return res;
        }

        ~Override()
        {
            clear();
        }

        /// \brief Force a specific branch instruction to be an unstructured \e goto
        ///
        /// The command is specified as the address of the branch instruction and
        /// the destination address of the branch.  The decompiler will automatically
        /// mark this as a \e unstructured, when trying to structure the control-flow
        /// \param targetpc is the address of the branch instruction
        /// \param destpc is the destination address of the branch
        public void insertForceGoto(Address targetpc, Address destpc)
        {
            forcegoto[targetpc] = destpc;
        }

        /// \brief Override the number of passes that are executed before \e dead-code elimination starts
        ///
        /// Every address space has an assigned \e delay (which may be zero) before a PcodeOp
        /// involving a Varnode in that address space can be eliminated. This command allows the
        /// delay for a specific address space to be increased so that new Varnode accesses can be discovered.
        /// \param spc is the address space to modify
        /// \param delay is the size of the delay (in passes)
        public void insertDeadcodeDelay(AddrSpace spc, int delay)
        {
            while (deadcodedelay.size() <= spc.getIndex())
                deadcodedelay.Add(-1);

            deadcodedelay[spc.getIndex()] = delay;
        }

        /// \brief Check if a delay override is already installed for an address space
        ///
        /// \param spc is the address space
        /// \return \b true if an override has already been installed
        public bool hasDeadcodeDelay(AddrSpace spc)
        {
            int index = spc.getIndex();
            if (index >= deadcodedelay.size())
                return false;
            int val = deadcodedelay[index];
            if (val == -1) return false;
            return (val != spc.getDeadcodeDelay());
        }

        /// \brief Override an indirect call turning it into a direct call
        ///
        /// The command consists of the address of the indirect call instruction and
        /// the target address of the direct address
        /// \param callpoint is the address of the indirect call
        /// \param directcall is the target address of the direct call
        public void insertIndirectOverride(Address callpoint, Address directcall)
        {
            indirectover[callpoint] = directcall;
        }

        /// \brief Override the assumed function prototype at a specific call site
        ///
        /// The exact input and output storage locations are overridden for a
        /// specific call instruction (direct or indirect).
        /// \param callpoint is the address of the call instruction
        /// \param p is the overriding function prototype
        public void insertProtoOverride(Address callpoint, FuncProto p)
        {
            //Dictionary<Address, FuncProto*>::iterator iter;

            //iter = protoover.find(callpoint);
            //if (iter != protoover.end())    // Check for pre-existing override
            //    delete(*iter).second;   // and delete it

            p.setOverride(true);       // Mark this as an override
            protoover[callpoint] = p;   // Take ownership of the object
        }

        /// \brief Flag an indirect jump for multistage analysis
        ///
        /// \param addr is the address of the indirect jump
        public void insertMultistageJump(Address addr)
        {
            multistagejump.Add(addr);
        }

        /// \brief Mark a branch instruction with a different flow type
        ///
        /// Change the interpretation of a BRANCH, CALL, or RETURN
        /// \param addr is the address of the branch instruction
        /// \param type is the type of flow that should be forced
        public void insertFlowOverride(Address addr, Branching type)
        {
            flowoverride[addr] = type;
        }

        /// \brief Look for and apply a function prototype override
        ///
        /// Given a call point, look for a prototype override and copy
        /// the call specification in
        /// \param data is the (calling) function
        /// \param fspecs is a reference to the call specification
        public void applyPrototype(Funcdata data, FuncCallSpecs fspecs)
        {
            if (0 != protoover.Count) {
                FuncProto? prototype;
                if (protoover.TryGetValue(fspecs.getOp().getAddr(), out prototype)) {
                    fspecs.copy(prototype);
                }
            }
        }

        /// \brief Look for and apply destination overrides of indirect calls
        ///
        /// Given an indirect call, look for any overrides, then copy in
        /// the overriding target address of the direct call
        /// \param data is (calling) function
        /// \param fspecs is a reference to the call specification
        public void applyIndirect(Funcdata data, FuncCallSpecs fspecs)
        {
            if (0 != indirectover.Count) {
                Address? coveredAddress;
                if (indirectover.TryGetValue(fspecs.getOp().getAddr(), out coveredAddress))
                    fspecs.setAddress(coveredAddress);
            }
        }

        /// \brief Check for a multistage marker for a specific indirect jump
        ///
        /// Given the address of an indirect jump, look for the multistate command
        /// \param addr is the address of the indirect jump
        public bool queryMultistageJumptable(Address addr)
        {
            for (int i = 0; i < multistagejump.size(); ++i) {
                if (multistagejump[i] == addr)
                    return true;
            }
            return false;
        }

        /// \brief Apply any dead-code delay overrides
        ///
        /// Look for delays of each address space and apply them to the Heritage object
        /// \param data is the function
        public void applyDeadCodeDelay(Funcdata data)
        {
            Architecture glb = data.getArch();
            for (int i = 0; i < deadcodedelay.size(); ++i) {
                int delay = deadcodedelay[i];
                if (delay < 0) continue;
                AddrSpace spc = glb.getSpace(i);
                data.setDeadCodeDelay(spc, delay);
            }
        }

        /// \brief Push all the force-goto overrides into the function
        ///
        /// \param data is the function
        public void applyForceGoto(Funcdata data)
        {
            foreach (KeyValuePair<Address, Address> pair in forcegoto)
                data.forceGoto(pair.Key, pair.Value);
        }

        /// Are there any flow overrides
        public bool hasFlowOverride() => (0 != flowoverride.Count);

        /// \brief Return the particular flow override at a given address
        ///
        /// \param addr is the address of a branch instruction
        /// \return the override type
        public Override.Branching getFlowOverride(Sla.CORE.Address addr)
        {
            Override.Branching result;
            return flowoverride.TryGetValue(addr, out result) ? result : Override.Branching.NONE;
        }

        /// \brief Dump a description of the overrides to stream
        ///
        /// Give a description of each override, one per line, that is suitable for debug
        /// \param s is the output stream
        /// \param glb is the Architecture
        public void printRaw(TextWriter s, Architecture glb)
        {
            foreach (KeyValuePair<Address, Address> pair in forcegoto)
                s.WriteLine($"force goto at {pair.Key} jumping to {pair.Value}");

            for (int i = 0; i < deadcodedelay.size(); ++i) {
                if (deadcodedelay[i] < 0) continue;
                AddrSpace spc = glb.getSpace(i);
                s.WriteLine($"dead code delay on {spc.getName()} set to {deadcodedelay[i]}");
            }

            foreach (KeyValuePair<Address, Address> pair in indirectover)
                s.WriteLine($"override indirect at {pair.Key} to call directly to {pair.Value}");

            foreach (KeyValuePair<Address, FuncProto> pair in protoover) {
                s.Write($"override prototype at {pair.Key} to ");
                pair.Value.printRaw("func", s);
                s.WriteLine();
            }
        }

        /// \brief Create warning messages that describe current overrides
        ///
        /// Message are designed to be displayed in the function header comment
        /// \param messagelist will hold the generated list of messages
        /// \param glb is the Architecture
        public void generateOverrideMessages(List<string> messagelist, Architecture glb)
        {
            // Generate deadcode delay messages
            for (int i = 0; i < deadcodedelay.size(); ++i) {
                if (deadcodedelay[i] >= 0)
                    messagelist.Add(generateDeadcodeDelayMessage(i, glb));
            }
        }

        /// \brief Encode the override commands to a stream
        ///
        /// All the commands are written as children of a root \<override> element.
        /// \param encoder is the stream encoder
        /// \param glb is the Architecture
        public void encode(Sla.CORE.Encoder encoder, Architecture glb)
        {
            if (   (0 == forcegoto.Count)
                && deadcodedelay.empty()
                && (0 == indirectover.Count)
                && (0 == protoover.Count)
                && multistagejump.empty()
                && (0 == flowoverride.Count))
                return;
            encoder.openElement(ElementId.ELEM_OVERRIDE);

            foreach (KeyValuePair<Address, Address> pair in forcegoto) {
                encoder.openElement(ElementId.ELEM_FORCEGOTO);
                pair.Key.encode(encoder);
                pair.Value.encode(encoder);
                encoder.closeElement(ElementId.ELEM_FORCEGOTO);
            }

            for (int i = 0; i < deadcodedelay.size(); ++i) {
                if (deadcodedelay[i] < 0) continue;
                AddrSpace spc = glb.getSpace(i);
                encoder.openElement(ElementId.ELEM_DEADCODEDELAY);
                encoder.writeSpace(AttributeId.ATTRIB_SPACE, spc);
                encoder.writeSignedInteger(AttributeId.ATTRIB_DELAY, deadcodedelay[i]);
                encoder.closeElement(ElementId.ELEM_DEADCODEDELAY);
            }

            foreach (KeyValuePair<Address, Address> pair in indirectover) {
                encoder.openElement(ElementId.ELEM_INDIRECTOVERRIDE);
                pair.Key.encode(encoder);
                pair.Value.encode(encoder);
                encoder.closeElement(ElementId.ELEM_INDIRECTOVERRIDE);
            }

            foreach (KeyValuePair<Address, FuncProto> pair in protoover) {
                encoder.openElement(ElementId.ELEM_PROTOOVERRIDE);
                pair.Key.encode(encoder);
                pair.Value.encode(encoder);
                encoder.closeElement(ElementId.ELEM_PROTOOVERRIDE);
            }

            for (int i = 0; i < multistagejump.size(); ++i) {
                encoder.openElement(ElementId.ELEM_MULTISTAGEJUMP);
                multistagejump[i].encode(encoder);
                encoder.closeElement(ElementId.ELEM_MULTISTAGEJUMP);
            }

            foreach (KeyValuePair<Address, Branching> pair in flowoverride) {
                encoder.openElement(ElementId.ELEM_FLOW);
                encoder.writeString(AttributeId.ATTRIB_TYPE, typeToString(pair.Value));
                pair.Key.encode(encoder);
                encoder.closeElement(ElementId.ELEM_FLOW);
            }
            encoder.closeElement(ElementId.ELEM_OVERRIDE);
        }

        /// \brief Parse and \<override> element containing override commands
        ///
        /// \param decoder is the stream decoder
        /// \param glb is the Architecture
        public void decode(Sla.CORE.Decoder decoder, Architecture glb)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_OVERRIDE);
            while(true) {
                uint subId = decoder.openElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_INDIRECTOVERRIDE) {
                    Address callpoint = Address.decode(decoder);
                    Address directcall = Address.decode(decoder);
                    insertIndirectOverride(callpoint, directcall);
                }
                else if (subId == ElementId.ELEM_PROTOOVERRIDE) {
                    Address callpoint = Address.decode(decoder);
                    FuncProto fp = new FuncProto();
                    fp.setInternal(glb.defaultfp, glb.types.getTypeVoid());
                    fp.decode(decoder, glb);
                    insertProtoOverride(callpoint, fp);
                }
                else if (subId == ElementId.ELEM_FORCEGOTO) {
                    Address targetpc = Address.decode(decoder);
                    Address destpc = Address.decode(decoder);
                    insertForceGoto(targetpc, destpc);
                }
                else if (subId == ElementId.ELEM_DEADCODEDELAY) {
                    int delay = (int)decoder.readSignedInteger(AttributeId.ATTRIB_DELAY);
                    AddrSpace spc = decoder.readSpace(AttributeId.ATTRIB_SPACE);
                    if (delay < 0)
                        throw new LowlevelError("Bad deadcodedelay tag");
                    insertDeadcodeDelay(spc, delay);
                }
                else if (subId == ElementId.ELEM_MULTISTAGEJUMP) {
                    Address callpoint = Address.decode(decoder);
                    insertMultistageJump(callpoint);
                }
                else if (subId == ElementId.ELEM_FLOW) {
                    Branching type = stringToType(decoder.readString(AttributeId.ATTRIB_TYPE));
                    Address addr = Address.decode(decoder);
                    if ((type == Branching.NONE) || (addr.isInvalid()))
                        throw new LowlevelError("Bad flowoverride tag");
                    insertFlowOverride(addr, type);
                }
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        /// Convert a flow override type to a string
        /// \param tp is the override type
        /// \return the corresponding name string
        public static string typeToString(Branching tp)
        {
            if (tp == Branching.BRANCH)
                return "branch";
            if (tp == Branching.CALL)
                return "call";
            if (tp == Branching.CALL_RETURN)
                return "callreturn";
            if (tp == Branching.RETURN)
                return "return";
            return "none";
        }

        /// Convert a string to a flow override type
        /// \param nm is the override name
        /// \return the override enumeration type
        public static Branching stringToType(string nm)
        {
            if (nm == "branch")
                return Branching.BRANCH;
            else if (nm == "call")
                return Branching.CALL;
            else if (nm == "callreturn")
                return Branching.CALL_RETURN;
            else if (nm == "return")
                return Branching.RETURN;
            return Branching.NONE;
        }
    }
}
