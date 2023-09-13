using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for splitting larger registers holding smaller logical lanes
    ///
    /// Given a starting Varnode in the data-flow, look for evidence of the Varnode
    /// being interpreted as disjoint logical values concatenated together (lanes).
    /// If the interpretation is consistent for data-flow involving the Varnode, split
    /// Varnode and data-flow into explicit operations on the lanes.
    internal class TransformManager
    {
        /// Function being operated on
        private Funcdata fd;
        /// Map from large Varnodes to their new pieces
        private Dictionary<int, TransformVar[]> pieceMap = new Dictionary<int, TransformVar[]>();
        /// Storage for Varnode placeholder nodes
        private List<TransformVar> newVarnodes = new List<TransformVar>();
        /// Storage for PcodeOp placeholder nodes
        private List<TransformOp> newOps = new List<TransformOp>();

        /// \brief Handle some special PcodeOp marking
        /// If a PcodeOp is an INDIRECT creation, we need to do special marking of the op and Varnodes
        /// \param rop is the placeholder op with the special requirement
        private void specialHandling(TransformOp rop)
        {
            if ((rop.special & TransformOp.Annotation.indirect_creation) != 0)
                fd.markIndirectCreation(rop.replacement, false);
            else if ((rop.special & TransformOp.Annotation.indirect_creation_possible_out) != 0)
                fd.markIndirectCreation(rop.replacement, true);
        }

        /// Create a new op for each placeholder
        /// Run through the list of TransformOp placeholders and create the actual PcodeOp object.
        /// If the op has an output Varnode, create it.  Make sure all the new ops are inserted in
        /// control flow.
        private void createOps()
        {
            foreach (TransformOp op in newOps)
                op.createReplacement(fd);

            int followCount;
            do {
                followCount = 0;
                foreach (TransformOp op in newOps) {
                    if (!op.attemptInsertion(fd))
                        followCount += 1;
                }
            } while (followCount != 0);
        }

        /// Create a Varnode for each placeholder
        /// Record any input vars in the given container
        /// \param inputList will hold any inputs
        private void createVarnodes(List<TransformVar> inputList)
        {
            foreach (TransformVar[] vArray in pieceMap.Values) {
                for (int i = 0; ; ++i) {
                    TransformVar rvn = vArray[i];
                    if (rvn.type == TransformVar.ReplaceType.piece) {
                        Varnode vn = rvn.vn;
                        if (vn.isInput()) {
                            inputList.Add(rvn);
                            if (vn.isMark())
                                rvn.flags |= TransformVar.Flags.input_duplicate;
                            else
                                vn.setMark();
                        }
                    }
                    rvn.createReplacement(fd);
                    if ((rvn.flags & TransformVar.Flags.split_terminator) != 0)
                        break;
                }
            }
            foreach (TransformVar variable in newVarnodes) {
                variable.createReplacement(fd);
            }
        }

        /// Remove old preexisting PcodeOps and Varnodes that are now obsolete
        private void removeOld()
        {
            foreach (TransformOp rop in newOps) {
                if ((rop.special & TransformOp.Annotation.op_replacement) != 0) {
                    if (!rop.op.isDead())
                        fd.opDestroy(rop.op);  // Destroy old op (and its output Varnode)
                }
            }
        }

        /// Remove old input Varnodes, mark new input Varnodes
        /// Remove all input Varnodes from the given container.
        /// Mark all the replacement Varnodes as inputs.
        /// \param inputList is the given container of input placeholders
        private void transformInputVarnodes(List<TransformVar> inputList)
        {
            for (int i = 0; i < inputList.size(); ++i) {
                TransformVar rvn = inputList[i];
                if ((rvn.flags & TransformVar.Flags.input_duplicate) == 0)
                    fd.deleteVarnode(rvn.vn);
                rvn.replacement = fd.setInputVarnode(rvn.replacement);
            }
        }

        /// Set input Varnodes for all new ops
        private void placeInputs()
        {
            foreach (TransformOp rop in newOps) {
                PcodeOp op = rop.replacement;
                for (int i = 0; i < rop.input.size(); ++i) {
                    TransformVar rvn = rop.input[i];
                    Varnode vn = rvn.replacement;
                    fd.opSetInput(op, vn, i);
                }
                specialHandling(rop);
            }
        }

        public TransformManager(Funcdata f)
        {
            fd = f;
        }

        ~TransformManager()
        {
            //Dictionary<int, TransformVar>::iterator iter;
            //for (iter = pieceMap.begin(); iter != pieceMap.end(); ++iter)
            //{
            //    delete[](*iter).second;
            //}
        }

        /// \brief Should the address of the given Varnode be preserved when constructing a piece
        ///
        /// A new Varnode will be created that represents a logical piece of the given Varnode.
        /// This routine determines whether the new Varnode should be constructed using
        /// storage which overlaps the given Varnode. It returns \b true if overlapping storage
        /// should be used, \b false if the new Varnode should be constructed as a unique temporary.
        /// \param vn is the given Varnode
        /// \param bitSize is the logical size of the Varnode piece being constructed
        /// \param lsbOffset is the least significant bit position of the logical value within the given Varnode
        /// \return \b true if overlapping storage should be used in construction
        public virtual bool preserveAddress(Varnode vn, int bitSize, int lsbOffset)
        {
            if ((lsbOffset & 7) != 0) return false; // Logical value not aligned
            if (vn.getSpace().getType() == spacetype.IPTR_INTERNAL) return false;
            return true;
        }

        /// Get function being transformed
        public Funcdata getFunction() => fd;

        /// Clear mark for all Varnodes in the map
        public void clearVarnodeMarks()
        {
            foreach (TransformVar variable in pieceMap.Values) {
                Varnode? vn = variable.vn;
                if (vn != (Varnode)null)
                    vn.clearMark();
            }
        }

        /// Make placeholder for preexisting Varnode
        /// \param vn is the preexisting Varnode to create a placeholder for
        /// \return the new placeholder node
        public TransformVar newPreexistingVarnode(Varnode vn)
        {
            TransformVar[] res = new TransformVar[1];
            pieceMap[(int)vn.getCreateIndex()] = res;   // Enter preexisting Varnode into map, so we don't make another placeholder

            // value of 0 treats this as "piece" of itself at offset 0, allows getPiece() to find it
            res.initialize(TransformVar.ReplaceType.preexisting, vn, vn.getSize() * 8, vn.getSize(), 0);
            res.flags = TransformVar.Flags.split_terminator;
            return res;
        }

        /// Make placeholder for new unique space Varnode
        /// \param size is the size in bytes of the new unique Varnode
        /// \return the new placeholder node
        public TransformVar newUnique(int size)
        {
            TransformVar res = new TransformVar();
            res.initialize(TransformVar.ReplaceType.normal_temp, (Varnode)null, size * 8, size, 0);
            newVarnodes.Add(res);
            return res;
        }

        /// Make placeholder for constant Varnode
        /// Create a new constant in the transform view.  A piece of an existing constant
        /// can be created  by giving the existing value and the least significant offset.
        /// \param size is the size in bytes of the new constant
        /// \param lsbOffset is the number of bits to strip off of the existing value
        /// \param val is the value of the constant
        /// \return the new placeholder node
        public TransformVar newConstant(int size, int lsbOffset, ulong val)
        {
            TransformVar res = new TransformVar();
            res.initialize(TransformVar.ReplaceType.constant, (Varnode)null, size * 8, size,
                (val >> lsbOffset) & Globals.calc_mask((uint)size));
            newVarnodes.Add(res);
            return res;
        }

        /// Make placeholder for special iop constant
        /// Used for creating INDIRECT placeholders.
        /// \param vn is the original iop parameter to the INDIRECT
        /// \return the new placeholder node
        public TransformVar newIop(Varnode vn)
        {
            TransformVar res = new TransformVar();
            res.initialize(TransformVar.ReplaceType.constant_iop, (Varnode)null, vn.getSize() * 8,
                vn.getSize(), vn.getOffset());
            newVarnodes.Add(res);
            return res;
        }

        /// Make placeholder for piece of a Varnode
        /// Given a single logical value within a larger Varnode, create a placeholder for
        /// that logical value.
        /// \param vn is the large Varnode
        /// \param bitSize is the size of the logical value in bits
        /// \param lsbOffset is the number of least significant bits of the Varnode dropped from the value
        /// \return the placeholder variable
        public TransformVar newPiece(Varnode vn, int bitSize, int lsbOffset)
        {
            TransformVar res = new TransformVar[1];
            pieceMap[(int)vn.getCreateIndex()] = res;
            int byteSize = (bitSize + 7) / 8;
            TransformVar.ReplaceType type = preserveAddress(vn, bitSize, lsbOffset)
                ? TransformVar.ReplaceType.piece
                : TransformVar.ReplaceType.piece_temp;
            res.initialize(type, vn, bitSize, byteSize, (ulong)lsbOffset);
            res.flags = TransformVar.Flags.split_terminator;
            return res;
        }

        /// \brief Create placeholder nodes splitting a Varnode into its lanes
        ///
        /// Given a big Varnode and a lane description, create placeholders for all the explicit pieces
        /// that the big Varnode will be split into.
        /// \param vn is the big Varnode to split
        /// \param description shows how the big Varnode will be split
        /// \return an array of the new TransformVar placeholders from least to most significant
        public TransformVar[] newSplit(Varnode vn, LaneDescription description)
        {
            int num = description.getNumLanes();
            TransformVar[] res = new TransformVar[num];
            pieceMap[(int)vn.getCreateIndex()] = res;
            for (int i = 0; i < num; ++i) {
                int bitpos = description.getPosition(i) * 8;
                TransformVar newVar = res[i];
                int byteSize = description.getSize(i);
                if (vn.isConstant())
                    newVar.initialize(TransformVar.ReplaceType.constant, vn, byteSize * 8, byteSize, (vn.getOffset() >> bitpos) & Globals.calc_mask((uint)byteSize));
                else {
                    TransformVar.ReplaceType type = preserveAddress(vn, byteSize * 8, bitpos)
                        ? TransformVar.ReplaceType.piece
                        : TransformVar.ReplaceType.piece_temp;
                    newVar.initialize(type, vn, byteSize * 8, byteSize, (ulong)bitpos);
                }
            }
            res[num - 1].flags = TransformVar.Flags.split_terminator;
            return res;
        }

        /// \brief Create placeholder nodes splitting a Varnode into a subset of lanes in the given description
        ///
        /// Given a big Varnode and specific subset of a lane description, create placeholders for all
        /// the explicit pieces that the big Varnode will be split into.
        /// \param vn is the big Varnode to split
        /// \param description gives a list of potentional lanes
        /// \param numLanes is the number of lanes in the subset
        /// \param startLane is the starting (least significant) lane in the subset
        /// \return an array of the new TransformVar placeholders from least to most significant
        public TransformVar[] newSplit(Varnode vn, LaneDescription description, int numLanes, int startLane)
        {
            TransformVar[] res = new TransformVar[numLanes];
            pieceMap[(int)vn.getCreateIndex()] = res;
            int baseBitPos = description.getPosition(startLane) * 8;
            for (int i = 0; i < numLanes; ++i) {
                int bitpos = description.getPosition(startLane + i) * 8 - baseBitPos;
                int byteSize = description.getSize(startLane + i);
                TransformVar newVar = res[i];
                if (vn.isConstant())
                    newVar.initialize(TransformVar.ReplaceType.constant, vn, byteSize * 8, byteSize, (vn.getOffset() >> bitpos) & Globals.calc_mask((uint)byteSize));
                else {
                    TransformVar.ReplaceType type = preserveAddress(vn, byteSize * 8, bitpos)
                        ? TransformVar.ReplaceType.piece
                        : TransformVar.ReplaceType.piece_temp;
                    newVar.initialize(type, vn, byteSize * 8, byteSize, (ulong)bitpos);
                }
            }
            res[numLanes - 1].flags = TransformVar.Flags.split_terminator;
            return res;
        }

        /// \brief Create a new placeholder op intended to replace an existing op
        ///
        /// An uninitialized placeholder for the new op is created.
        /// \param numParams is the number of Varnode inputs intended for the new op
        /// \param opc is the opcode of the new op
        /// \param replace is the existing op the new op will replace
        /// \return the new placeholder node
        public TransformOp newOpReplace(int numParams, OpCode opc, PcodeOp replace)
        {
            TransformOp rop = new TransformOp() {
                op = replace,
                replacement = (PcodeOp)null,
                opc = opc,
                special = TransformOp.Annotation.op_replacement,
                output = (TransformVar)null,
                follow = (TransformOp)null
            };
            rop.input.resize(numParams, (TransformVar)null);
            newOps.Add(rop);
            return rop;
        }

        /// \brief Create a new placeholder op that will not replace an existing op
        ///
        /// An uninitialized placeholder for the new op is created. When (if) the new op is created
        /// it will not replace an existing op.  The op that follows it must be given.
        /// \param numParams is the number of Varnode inputs intended for the new op
        /// \param opc is the opcode of the new op
        /// \param follow is the placeholder for the op that follow the new op when it is created
        /// \return the new placeholder node
        public TransformOp newOp(int numParams, OpCode opc, TransformOp follow)
        {
            TransformOp rop = new TransformOp() {
                op = follow.op,
                replacement = (PcodeOp)null,
                opc = opc,
                special = 0,
                output = (TransformVar)null,
                follow = follow
            };
            rop.input.resize(numParams, (TransformVar)null);
            newOps.Add(rop);
            return rop;
        }

        /// \brief Create a new placeholder op for an existing PcodeOp
        ///
        /// An uninitialized placeholder for the existing op is created. When applied, this causes
        /// the op to be transformed as described by the placeholder, changing its opcode and
        /// inputs.  The output however is unaffected.
        /// \param numParams is the number of Varnode inputs intended for the transformed op
        /// \param opc is the opcode of the transformed op
        /// \param originalOp is the preexisting PcodeOp
        /// \return the new placeholder node
        public TransformOp newPreexistingOp(int numParams, OpCode opc, PcodeOp originalOp)
        {
            TransformOp rop = new TransformOp() {
                op = originalOp,
                replacement = (PcodeOp)null,
                opc = opc,
                special = TransformOp.Annotation.op_preexisting,
                output = (TransformVar)null,
                follow = (TransformOp)null
            };
            rop.input.resize(numParams, (TransformVar)null);
            newOps.Add(rop);
            return rop;
        }

        /// Get (or create) placeholder for preexisting Varnode
        /// Check if a placeholder node was created for the preexisting Varnode for,
        /// otherwise create a new one.
        /// \param vn is the preexisting Varnode to find a placeholder for
        /// \return the placeholder node
        public TransformVar getPreexistingVarnode(Varnode vn)
        {
            if (vn.isConstant())
                return newConstant(vn.getSize(), 0, vn.getOffset());
            TransformVar result;
            if (pieceMap.TryGetValue((int)vn.getCreateIndex(), out result))
                return result;
            return newPreexistingVarnode(vn);
        }

        /// Get (or create) placeholder piece
        /// Given a big Varnode, find the placeholder corresponding to the logical value
        /// given by a size and significance offset.  If it doesn't exist, create it.
        /// \param vn is the big Varnode containing the logical value
        /// \param bitSize is the size of the logical value in bytes
        /// \param lsbOffset is the signficance offset of the logical value within the Varnode
        /// \return the found/created placeholder
        public TransformVar getPiece(Varnode vn, int bitSize, int lsbOffset)
        {
            TransformVar res;
            if (pieceMap.TryGetValue((int)vn.getCreateIndex(), out res)) {
                if (res.bitSize != bitSize || res.val != lsbOffset)
                    throw new LowlevelError("Cannot create multiple pieces for one Varnode through getPiece");
                return res;
            }
            return newPiece(vn, bitSize, lsbOffset);
        }

        /// \brief Find (or create) placeholder nodes splitting a Varnode into its lanes
        ///
        /// Given a big Varnode and a lane description, look up placeholders for all its
        /// explicit pieces. If they don't exist, create them.
        /// \param vn is the big Varnode to split
        /// \param description shows how the big Varnode will be split
        /// \return an array of the TransformVar placeholders from least to most significant
        public TransformVar getSplit(Varnode vn, LaneDescription description)
        {
            TransformVar result;
            return pieceMap.TryGetValue((int)vn.getCreateIndex(), out result)
                ? result
                : newSplit(vn, description);
        }

        /// \brief Find (or create) placeholder nodes splitting a Varnode into a subset of lanes from a description
        ///
        /// Given a big Varnode and a specific subset of a lane description, look up placeholders
        /// for all the explicit pieces. If they don't exist, create them.
        /// \param vn is the big Varnode to split
        /// \param description describes all the possible lanes
        /// \param numLanes is the number of lanes in the subset
        /// \param startLane is the starting (least significant) lane in the subset
        /// \return an array of the TransformVar placeholders from least to most significant
        public TransformVar[] getSplit(Varnode vn, LaneDescription description, int numLanes, int startLane)
        {
            TransformVar[] result;
            return pieceMap.TryGetValue((int)vn.getCreateIndex(), out result)
                ? result
                : newSplit(vn, description, numLanes, startLane);
        }

        /// Mark given variable as input to given op
        /// \param rop is the given placeholder op whose input is set
        /// \param rvn is the placeholder variable to set
        /// \param slot is the input position to set
        public void opSetInput(TransformOp rop, TransformVar rvn, int slot)
        {
            rop.input[slot] = rvn;
        }

        /// Mark given variable as output of given op
        /// Establish that the given op produces the given var as output.
        /// Mark both the \e output field of the TransformOp and the \e def field of the TransformVar.
        /// \param rop is the given op
        /// \param rvn is the given variable
        public void opSetOutput(TransformOp rop, TransformVar rvn)
        {
            rop.output = rvn;
            rvn.def = rop;
        }

        /// Should newPreexistingOp be called
        /// Varnode marking prevents duplicate TransformOp (and TransformVar) records from getting
        /// created, except in the case of a preexisting PcodeOp with 2 (or more) non-constant inputs.
        /// Because the op is preexisting the output Varnode doesn't get marked, and the op will
        /// be visited for each input.  This method determines when the TransformOp object should be
        /// created, with the goal of creating it exactly once even though the op is visited more than once.
        /// It currently assumes the PcodeOp is binary, and the slot along which the op is
        /// currently visited is passed in, along with the TransformVar for the \e other input. It returns
        /// \b true if the TransformOp should be created.
        /// \param slot is the incoming slot along which the op is visited
        /// \param rvn is the other input
        public static bool preexistingGuard(int slot, TransformVar rvn)
        {
            if (slot == 0) return true; // If we came in on the first slot, build the TransformOp
            if (rvn.type == TransformVar.ReplaceType.piece || rvn.type == TransformVar.ReplaceType.piece_temp)
                return false;       // The op was/will be visited on slot 0, don't create TransformOp now
            return true;            // The op was not (will not be) visited on slot 0, build now
        }

        /// Apply the full transform to the function
        public void apply()
        {
            List<TransformVar> inputList = new List<TransformVar>();
            createOps();
            createVarnodes(inputList);
            removeOld();
            transformInputVarnodes(inputList);
            placeInputs();
        }
    }
}
