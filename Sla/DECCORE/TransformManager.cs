using System;
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
        private Dictionary<int4, TransformVar> pieceMap;
        /// Storage for Varnode placeholder nodes
        private List<TransformVar> newVarnodes;
        /// Storage for PcodeOp placeholder nodes
        private List<TransformOp> newOps;

        /// \brief Handle some special PcodeOp marking
        /// If a PcodeOp is an INDIRECT creation, we need to do special marking of the op and Varnodes
        /// \param rop is the placeholder op with the special requirement
        private void specialHandling(TransformOp rop)
        {
            if ((rop.special & TransformOp::indirect_creation) != 0)
                fd->markIndirectCreation(rop.replacement, false);
            else if ((rop.special & TransformOp::indirect_creation_possible_out) != 0)
                fd->markIndirectCreation(rop.replacement, true);
        }

        /// Create a new op for each placeholder
        /// Run through the list of TransformOp placeholders and create the actual PcodeOp object.
        /// If the op has an output Varnode, create it.  Make sure all the new ops are inserted in
        /// control flow.
        private void createOps()
        {
            list<TransformOp>::iterator iter;
            for (iter = newOps.begin(); iter != newOps.end(); ++iter)
                (*iter).createReplacement(fd);

            int4 followCount;
            do
            {
                followCount = 0;
                for (iter = newOps.begin(); iter != newOps.end(); ++iter)
                {
                    if (!(*iter).attemptInsertion(fd))
                        followCount += 1;
                }
            } while (followCount != 0);
        }

        /// Create a Varnode for each placeholder
        /// Record any input vars in the given container
        /// \param inputList will hold any inputs
        private void createVarnodes(List<TransformVar> inputList)
        {
            map<int4, TransformVar*>::iterator piter;
            for (piter = pieceMap.begin(); piter != pieceMap.end(); ++piter)
            {
                TransformVar* vArray = (*piter).second;
                for (int4 i = 0; ; ++i)
                {
                    TransformVar* rvn = vArray + i;
                    if (rvn->type == TransformVar::piece)
                    {
                        Varnode* vn = rvn->vn;
                        if (vn->isInput())
                        {
                            inputList.push_back(rvn);
                            if (vn->isMark())
                                rvn->flags |= TransformVar::input_duplicate;
                            else
                                vn->setMark();
                        }
                    }
                    rvn->createReplacement(fd);
                    if ((rvn->flags & TransformVar::split_terminator) != 0)
                        break;
                }
            }
            list<TransformVar>::iterator iter;
            for (iter = newVarnodes.begin(); iter != newVarnodes.end(); ++iter)
            {
                (*iter).createReplacement(fd);
            }
        }

        /// Remove old preexisting PcodeOps and Varnodes that are now obsolete
        private void removeOld()
        {
            list<TransformOp>::iterator iter;
            for (iter = newOps.begin(); iter != newOps.end(); ++iter)
            {
                TransformOp & rop(*iter);
                if ((rop.special & TransformOp::op_replacement) != 0)
                {
                    if (!rop.op->isDead())
                        fd->opDestroy(rop.op);  // Destroy old op (and its output Varnode)
                }
            }
        }

        /// Remove old input Varnodes, mark new input Varnodes
        /// Remove all input Varnodes from the given container.
        /// Mark all the replacement Varnodes as inputs.
        /// \param inputList is the given container of input placeholders
        private void transformInputVarnodes(List<TransformVar> inputList)
        {
            for (int4 i = 0; i < inputList.size(); ++i)
            {
                TransformVar* rvn = inputList[i];
                if ((rvn->flags & TransformVar::input_duplicate) == 0)
                    fd->deleteVarnode(rvn->vn);
                rvn->replacement = fd->setInputVarnode(rvn->replacement);
            }
        }

        /// Set input Varnodes for all new ops
        private void placeInputs()
        {
            list<TransformOp>::iterator iter;
            for (iter = newOps.begin(); iter != newOps.end(); ++iter)
            {
                TransformOp & rop(*iter);
                PcodeOp* op = rop.replacement;
                for (int4 i = 0; i < rop.input.size(); ++i)
                {
                    TransformVar* rvn = rop.input[i];
                    Varnode* vn = rvn->replacement;
                    fd->opSetInput(op, vn, i);
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
            map<int4, TransformVar*>::iterator iter;
            for (iter = pieceMap.begin(); iter != pieceMap.end(); ++iter)
            {
                delete[](*iter).second;
            }
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
        public virtual bool preserveAddress(Varnode vn, int4 bitSize, int4 lsbOffset)
        {
            if ((lsbOffset & 7) != 0) return false; // Logical value not aligned
            if (vn->getSpace()->getType() == IPTR_INTERNAL) return false;
            return true;
        }

        /// Get function being transformed
        public Funcdata getFunction() => fd;

        /// Clear mark for all Varnodes in the map
        public void clearVarnodeMarks()
        {
            map<int4, TransformVar*>::const_iterator iter;
            for (iter = pieceMap.begin(); iter != pieceMap.end(); ++iter)
            {
                Varnode* vn = (*iter).second->vn;
                if (vn == (Varnode*)0)
                    continue;
                vn->clearMark();
            }
        }

        /// Make placeholder for preexisting Varnode
        /// \param vn is the preexisting Varnode to create a placeholder for
        /// \return the new placeholder node
        public TransformVar newPreexistingVarnode(Varnode vn)
        {
            TransformVar* res = new TransformVar[1];
            pieceMap[vn->getCreateIndex()] = res;   // Enter preexisting Varnode into map, so we don't make another placeholder

            // value of 0 treats this as "piece" of itself at offset 0, allows getPiece() to find it
            res->initialize(TransformVar::preexisting, vn, vn->getSize() * 8, vn->getSize(), 0);
            res->flags = TransformVar::split_terminator;
            return res;
        }

        /// Make placeholder for new unique space Varnode
        /// \param size is the size in bytes of the new unique Varnode
        /// \return the new placeholder node
        public TransformVar newUnique(int4 size)
        {
            newVarnodes.emplace_back();
            TransformVar* res = &newVarnodes.back();
            res->initialize(TransformVar::normal_temp, (Varnode*)0, size * 8, size, 0);
            return res;
        }

        /// Make placeholder for constant Varnode
        /// Create a new constant in the transform view.  A piece of an existing constant
        /// can be created  by giving the existing value and the least significant offset.
        /// \param size is the size in bytes of the new constant
        /// \param lsbOffset is the number of bits to strip off of the existing value
        /// \param val is the value of the constant
        /// \return the new placeholder node
        public TransformVar newConstant(int4 size, int4 lsbOffset, uintb val)
        {
            newVarnodes.emplace_back();
            TransformVar* res = &newVarnodes.back();
            res->initialize(TransformVar::constant, (Varnode*)0, size * 8, size, (val >> lsbOffset) & calc_mask(size));
            return res;
        }

        /// Make placeholder for special iop constant
        /// Used for creating INDIRECT placeholders.
        /// \param vn is the original iop parameter to the INDIRECT
        /// \return the new placeholder node
        public TransformVar newIop(Varnode vn)
        {
            newVarnodes.emplace_back();
            TransformVar* res = &newVarnodes.back();
            res->initialize(TransformVar::constant_iop, (Varnode*)0, vn->getSize() * 8, vn->getSize(), vn->getOffset());
            return res;
        }

        /// Make placeholder for piece of a Varnode
        /// Given a single logical value within a larger Varnode, create a placeholder for
        /// that logical value.
        /// \param vn is the large Varnode
        /// \param bitSize is the size of the logical value in bits
        /// \param lsbOffset is the number of least significant bits of the Varnode dropped from the value
        /// \return the placeholder variable
        public TransformVar newPiece(Varnode vn, int4 bitSize, int4 lsbOffset)
        {
            TransformVar* res = new TransformVar[1];
            pieceMap[vn->getCreateIndex()] = res;
            int4 byteSize = (bitSize + 7) / 8;
            uint4 type = preserveAddress(vn, bitSize, lsbOffset) ? TransformVar::piece : TransformVar::piece_temp;
            res->initialize(type, vn, bitSize, byteSize, lsbOffset);
            res->flags = TransformVar::split_terminator;
            return res;
        }

        /// \brief Create placeholder nodes splitting a Varnode into its lanes
        ///
        /// Given a big Varnode and a lane description, create placeholders for all the explicit pieces
        /// that the big Varnode will be split into.
        /// \param vn is the big Varnode to split
        /// \param description shows how the big Varnode will be split
        /// \return an array of the new TransformVar placeholders from least to most significant
        public TransformVar newSplit(Varnode vn, LaneDescription description)
        {
            int4 num = description.getNumLanes();
            TransformVar* res = new TransformVar[num];
            pieceMap[vn->getCreateIndex()] = res;
            for (int4 i = 0; i < num; ++i)
            {
                int4 bitpos = description.getPosition(i) * 8;
                TransformVar* newVar = &res[i];
                int4 byteSize = description.getSize(i);
                if (vn->isConstant())
                    newVar->initialize(TransformVar::constant, vn, byteSize * 8, byteSize, (vn->getOffset() >> bitpos) & calc_mask(byteSize));
                else
                {
                    uint4 type = preserveAddress(vn, byteSize * 8, bitpos) ? TransformVar::piece : TransformVar::piece_temp;
                    newVar->initialize(type, vn, byteSize * 8, byteSize, bitpos);
                }
            }
            res[num - 1].flags = TransformVar::split_terminator;
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
        public TransformVar newSplit(Varnode vn, LaneDescription description, int4 numLanes, int4 startLane)
        {
            TransformVar* res = new TransformVar[numLanes];
            pieceMap[vn->getCreateIndex()] = res;
            int4 baseBitPos = description.getPosition(startLane) * 8;
            for (int4 i = 0; i < numLanes; ++i)
            {
                int4 bitpos = description.getPosition(startLane + i) * 8 - baseBitPos;
                int4 byteSize = description.getSize(startLane + i);
                TransformVar* newVar = &res[i];
                if (vn->isConstant())
                    newVar->initialize(TransformVar::constant, vn, byteSize * 8, byteSize, (vn->getOffset() >> bitpos) & calc_mask(byteSize));
                else
                {
                    uint4 type = preserveAddress(vn, byteSize * 8, bitpos) ? TransformVar::piece : TransformVar::piece_temp;
                    newVar->initialize(type, vn, byteSize * 8, byteSize, bitpos);
                }
            }
            res[numLanes - 1].flags = TransformVar::split_terminator;
            return res;
        }

        /// \brief Create a new placeholder op intended to replace an existing op
        ///
        /// An uninitialized placeholder for the new op is created.
        /// \param numParams is the number of Varnode inputs intended for the new op
        /// \param opc is the opcode of the new op
        /// \param replace is the existing op the new op will replace
        /// \return the new placeholder node
        public TransformOp newOpReplace(int4 numParams, OpCode opc, PcodeOp replace)
        {
            newOps.emplace_back();
            TransformOp & rop(newOps.back());
            rop.op = replace;
            rop.replacement = (PcodeOp*)0;
            rop.opc = opc;
            rop.special = TransformOp::op_replacement;
            rop.output = (TransformVar*)0;
            rop.follow = (TransformOp*)0;
            rop.input.resize(numParams, (TransformVar*)0);
            return &rop;
        }

        /// \brief Create a new placeholder op that will not replace an existing op
        ///
        /// An uninitialized placeholder for the new op is created. When (if) the new op is created
        /// it will not replace an existing op.  The op that follows it must be given.
        /// \param numParams is the number of Varnode inputs intended for the new op
        /// \param opc is the opcode of the new op
        /// \param follow is the placeholder for the op that follow the new op when it is created
        /// \return the new placeholder node
        public TransformOp newOp(int4 numParams, OpCode opc, TransformOp follow)
        {
            newOps.emplace_back();
            TransformOp & rop(newOps.back());
            rop.op = follow->op;
            rop.replacement = (PcodeOp*)0;
            rop.opc = opc;
            rop.special = 0;
            rop.output = (TransformVar*)0;
            rop.follow = follow;
            rop.input.resize(numParams, (TransformVar*)0);
            return &rop;
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
        public TransformOp newPreexistingOp(int4 numParams, OpCode opc, PcodeOp originalOp)
        {
            newOps.emplace_back();
            TransformOp & rop(newOps.back());
            rop.op = originalOp;
            rop.replacement = (PcodeOp*)0;
            rop.opc = opc;
            rop.special = TransformOp::op_preexisting;
            rop.output = (TransformVar*)0;
            rop.follow = (TransformOp*)0;
            rop.input.resize(numParams, (TransformVar*)0);
            return &rop;
        }

        /// Get (or create) placeholder for preexisting Varnode
        /// Check if a placeholder node was created for the preexisting Varnode for,
        /// otherwise create a new one.
        /// \param vn is the preexisting Varnode to find a placeholder for
        /// \return the placeholder node
        public TransformVar getPreexistingVarnode(Varnode vn)
        {
            if (vn->isConstant())
                return newConstant(vn->getSize(), 0, vn->getOffset());
            map<int4, TransformVar*>::const_iterator iter;
            iter = pieceMap.find(vn->getCreateIndex());
            if (iter != pieceMap.end())
                return (*iter).second;
            return newPreexistingVarnode(vn);
        }

        /// Get (or create) placeholder piece
        /// Given a big Varnode, find the placeholder corresponding to the logical value
        /// given by a size and significance offset.  If it doesn't exist, create it.
        /// \param vn is the big Varnode containing the logical value
        /// \param bitSize is the size of the logical value in bytes
        /// \param lsbOffset is the signficance offset of the logical value within the Varnode
        /// \return the found/created placeholder
        public TransformVar getPiece(Varnode vn, int4 bitSize, int4 lsbOffset)
        {
            map<int4, TransformVar*>::const_iterator iter;
            iter = pieceMap.find(vn->getCreateIndex());
            if (iter != pieceMap.end())
            {
                TransformVar* res = (*iter).second;
                if (res->bitSize != bitSize || res->val != lsbOffset)
                    throw LowlevelError("Cannot create multiple pieces for one Varnode through getPiece");
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
            map<int4, TransformVar*>::const_iterator iter;
            iter = pieceMap.find(vn->getCreateIndex());
            if (iter != pieceMap.end())
            {
                return (*iter).second;
            }
            return newSplit(vn, description);
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
        public TransformVar getSplit(Varnode vn, LaneDescription description, int4 numLanes, int4 startLane)
        {
            map<int4, TransformVar*>::const_iterator iter;
            iter = pieceMap.find(vn->getCreateIndex());
            if (iter != pieceMap.end())
            {
                return (*iter).second;
            }
            return newSplit(vn, description, numLanes, startLane);
        }

        /// Mark given variable as input to given op
        /// \param rop is the given placeholder op whose input is set
        /// \param rvn is the placeholder variable to set
        /// \param slot is the input position to set
        public void opSetInput(TransformOp rop, TransformVar rvn, int4 slot)
        {
            rop->input[slot] = rvn;
        }

        /// Mark given variable as output of given op
        /// Establish that the given op produces the given var as output.
        /// Mark both the \e output field of the TransformOp and the \e def field of the TransformVar.
        /// \param rop is the given op
        /// \param rvn is the given variable
        public void opSetOutput(TransformOp rop, TransformVar rvn)
        {
            rop->output = rvn;
            rvn->def = rop;
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
        public static bool preexistingGuard(int4 slot, TransformVar rvn)
        {
            if (slot == 0) return true; // If we came in on the first slot, build the TransformOp
            if (rvn->type == TransformVar::piece || rvn->type == TransformVar::piece_temp)
                return false;       // The op was/will be visited on slot 0, don't create TransformOp now
            return true;            // The op was not (will not be) visited on slot 0, build now
        }

        /// Apply the full transform to the function
        public void apply()
        {
            vector<TransformVar*> inputList;
            createOps();
            createVarnodes(inputList);
            removeOld();
            transformInputVarnodes(inputList);
            placeInputs();
        }
    }
}
