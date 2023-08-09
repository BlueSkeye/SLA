using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief The \e segmented \e address operator
    ///
    /// This op is a placeholder for address mappings involving \b segments.
    ///The map goes between a \b high-level view of a pointer, consisting of multiple pieces,
    /// and a \b low-level view, where there is only a single absolute pointer.
    /// The mapping could be
    ///    - a virtual to physical mapping for instance  or
    ///    - a segment + near pointer to a full address
    ///
    /// The output of the operator is always a full low-level pointer.
    /// The operator takes two inputs:
    ///    - the \b base or \b segment and
    ///    - the high-level \b near pointer
    ///
    /// High-level analysis can ignore the base/segment and any
    /// normalization on the near pointer.
    /// Emitted expressions involving \b this segment op prints only the \b near portion.
    /// Data-type information propagates only through this high-level side.
    ///
    /// The decompiler looks for the term-tree defined in SegmentOp
    /// and replaces it with the SEGMENTOP operator in any p-code it analyzes.
    /// The core routine that looks for the term-tree is unify().
    internal class SegmentOp : TermPatternOp
    {
        /// The physical address space into which a segmented pointer points
        private AddrSpace spc;
        /// Id of InjectPayload that emulates \b this operation
        private int injectId;
        /// The size in bytes of the \e base or \e segment value
        private int baseinsize;
        /// The size in bytes of the \e near pointer value
        private int innerinsize;
        /// Is \b true if the joined pair base:near acts as a \b far pointer
        private bool supportsfarpointer;
        /// How to resolve constant near pointers
        private VarnodeData constresolve = new VarnodeData();

        /// \param g is the owning Architecture for this instance of the segment operation
        /// \param nm is the low-level name of the segment operation
        /// \param ind is the constant id identifying the specific CALLOTHER variant
        public SegmentOp(Architecture g, string nm,int ind)
            : base(g, nm, ind)
        {
            constresolve.space = (AddrSpace)null;
        }

        /// Get the address space being pointed to
        public AddrSpace getSpace() => spc;

        /// Return \b true, if \b this op supports far pointers
        public bool hasFarPointerSupport() => supportsfarpointer;

        /// Get size in bytes of the base/segment value
        public int getBaseSize() => baseinsize;

        /// Get size in bytes of the near value
        public int getInnerSize() => innerinsize;

        /// Get the default register for resolving indirect segments
        public VarnodeData getResolve() => constresolve;

        public override int getNumVariableTerms()
        {
            if (baseinsize!=0) return 2; return 1;
        }

        public override bool unify(Funcdata data, PcodeOp op, List<Varnode> bindlist)
        {
            Varnode basevn, innervn;

            // Segmenting is done by a user defined p-code op, so this is what we look for
            // The op must have innervn and basevn (if base is present) as inputs
            // so there isn't much to follow. The OpFollow arrays are no
            // longer needed for unification but are needed to provide
            // a definition for the userop
            if (op.code() != OpCode.CPUI_CALLOTHER) return false;
            if (op.getIn(0).getOffset() != (ulong)useropindex) return false;
            if (op.numInput() != 3) return false;
            innervn = op.getIn(1);
            if (baseinsize != 0) {
                basevn = op.getIn(1);
                innervn = op.getIn(2);
                if (basevn.isConstant())
                    basevn = data.newConstant(baseinsize, basevn.getOffset());
                bindlist[0] = basevn;
            }
            else
                bindlist[0] = (Varnode)null;
            if (innervn.isConstant())
                innervn = data.newConstant(innerinsize, innervn.getOffset());
            bindlist[1] = innervn;
            return true;
        }

        public override ulong execute(List<ulong> input)
        {
            ExecutablePcode pcodeScript = (ExecutablePcode)glb.pcodeinjectlib.getPayload(injectId);
            return pcodeScript.evaluate(input);
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_SEGMENTOP);
            spc = (AddrSpace)null;
            injectId = -1;
            baseinsize = 0;
            innerinsize = 0;
            supportsfarpointer = false;
            name = "segment";       // Default name, might be overridden by userop attribute
            while(true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_SPACE)
                    spc = decoder.readSpace();
                else if (attribId == AttributeId.ATTRIB_FARPOINTER)
                    supportsfarpointer = true;
                else if (attribId == AttributeId.ATTRIB_USEROP)
                {   // Based on existing sleigh op
                    name = decoder.readString();
                }
            }
            if (spc == (AddrSpace)null)
                throw new LowlevelError("<segmentop> expecting space attribute");
            UserPcodeOp? otherop = glb.userops.getOp(name);
            if (otherop == (UserPcodeOp)null)
                throw new LowlevelError("<segmentop> unknown userop " + name);
            useropindex = otherop.getIndex();
            if ((otherop as UnspecializedPcodeOp) == (UnspecializedPcodeOp)null)
                throw new LowlevelError("Redefining userop " + name);

            while(true) {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_CONSTRESOLVE) {
                    int sz;
                    decoder.openElement();
                    if (decoder.peekElement() != 0) {
                        Address addr = Address.decode(decoder, out sz);
                        constresolve.space = addr.getSpace();
                        constresolve.offset = addr.getOffset();
                        constresolve.size = (uint)sz;
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ElementId.ELEM_PCODE) {
                    string nm = name + "_pcode";
                    string source = "cspec";
                    injectId = glb.pcodeinjectlib.decodeInject(source, nm, InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE, decoder);
                }
            }
            decoder.closeElement(elemId);
            if (injectId < 0)
                throw new LowlevelError("Missing <pcode> child in <segmentop> tag");
            InjectPayload payload = glb.pcodeinjectlib.getPayload(injectId);
            if (payload.sizeOutput() != 1)
                throw new LowlevelError("<pcode> child of <segmentop> tag must declare one <output>");
            if (payload.sizeInput() == 1) {
                innerinsize = (int)payload.getInput(0).getSize();
            }
            else if (payload.sizeInput() == 2) {
                baseinsize = (int)payload.getInput(0).getSize();
                innerinsize = (int)payload.getInput(1).getSize();
            }
            else
                throw new LowlevelError("<pcode> child of <segmentop> tag must declare one or two <input> tags");
        }
    }
}
