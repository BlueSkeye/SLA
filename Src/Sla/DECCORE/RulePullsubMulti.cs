using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RulePullsubMulti : Rule
    {
        public RulePullsubMulti(string g)
            : base(g, 0, "pullsub_multi")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RulePullsubMulti(getGroup());
        }

        /// \class RulePullsubMulti
        /// \brief Pull SUBPIECE back through MULTIEQUAL
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int maxByte, minByte, newSize;

            Varnode vn = op.getIn(0) ?? throw new ApplicationException();
            if (!vn.isWritten()) return 0;
            PcodeOp mult = vn.getDef() ?? throw new ApplicationException();
            if (mult.code() != OpCode.CPUI_MULTIEQUAL) return 0;
            // We only pull up, do not pull "down" to bottom of loop
            if (mult.getParent().hasLoopIn()) return 0;
            // Figure out what part of -vn- is used
            minMaxUse(vn, out maxByte, out minByte);
            newSize = maxByte - minByte + 1;
            if (maxByte < minByte || (newSize >= vn.getSize()))
                // If all or none is getting used, nothing to do
                return 0;
            if (!acceptableSize(newSize)) return 0;
            Varnode outvn = op.getOut() ?? throw new ApplicationException();
            if (outvn.isPrecisLo() || outvn.isPrecisHi())
                // Don't pull apart a double precision object
                return 0;

            // Make sure we don't new add SUBPIECE ops that aren't going to cancel in
            // some way
            int branches = mult.numInput();
            ulong consume = Globals.calc_mask((uint)newSize) << 8 * minByte;
            // Check for use of bits outside of what gets truncated later
            consume = ~consume;
            for (int i = 0; i < branches; ++i) {
                Varnode inVn = mult.getIn(i) ?? throw new ApplicationException();
                if ((consume & inVn.getConsume()) != 0) {
                    // Check if bits not truncated are still used
                    // Check if there's an extension that matches the truncation
                    if (minByte == 0 && inVn.isWritten()) {
                        PcodeOp defOp = inVn.getDef() ?? throw new ApplicationException();
                        OpCode opc = defOp.code();
                        if (opc == OpCode.CPUI_INT_ZEXT || opc == OpCode.CPUI_INT_SEXT) {
                            if (newSize == defOp.getIn(0).getSize())
                                // We have matching extension, so new SUBPIECE will
                                // cancel anyway
                                continue;
                        }
                    }
                    return 0;
                }
            }

            Address smalladdr2 = vn.getSpace().isBigEndian()
                ? vn.getAddr() + (vn.getSize() - maxByte - 1)
                :  vn.getAddr() + minByte;
            List<Varnode> @params = new List<Varnode>();
            for (int i = 0; i < branches; ++i) {
                Varnode vn_piece = mult.getIn(i);
                // We have to be wary of exponential splittings here, do not pull the SUBPIECE
                // up the MULTIEQUAL if another related SUBPIECE has already been pulled
                // Search for a previous SUBPIECE
                Varnode? vn_sub = findSubpiece(vn_piece, (uint)newSize, (uint)minByte);
                if (vn_sub == (Varnode)null)
                    // Couldn't find previous subpieceing
                    vn_sub = buildSubpiece(vn_piece, (uint)newSize, (uint)minByte, data);
                @params.Add(vn_sub);
            }
            // Build new multiequal near original multiequal
            PcodeOp new_multi = data.newOp (@params.size(), mult.getAddr());
            smalladdr2.renormalize(newSize);
            Varnode new_vn = data.newVarnodeOut(newSize, smalladdr2, new_multi);
            data.opSetOpcode(new_multi, OpCode.CPUI_MULTIEQUAL);
            data.opSetAllInput(new_multi,@params);
            data.opInsertBegin(new_multi, mult.getParent());

            replaceDescendants(vn, new_vn, maxByte, minByte, data);
            return 1;
        }

        /// \brief Compute minimum and maximum bytes being used
        ///
        /// For bytes in given Varnode pass back the largest and smallest index (lsb=0)
        /// consumed by an immediate descendant.
        /// \param vn is the given Varnode
        /// \param maxByte will hold the index of the maximum byte
        /// \param minByte will hold the index of the minimum byte
        public static void minMaxUse(Varnode vn, out int maxByte, out int minByte)
        {
            int inSize = vn.getSize();
            maxByte = -1;
            minByte = inSize;
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                OpCode opc = op.code();
                if (opc == OpCode.CPUI_SUBPIECE) {
                    int min = (int)op.getIn(1).getOffset();
                    int max = min + op.getOut().getSize() - 1;
                    if (min < minByte)
                        minByte = min;
                    if (max > maxByte)
                        maxByte = max;
                }
                else {
                    // By default assume all bytes are used
                    maxByte = inSize - 1;
                    minByte = 0;
                    return;
                }
            }
        }

        /// Replace given Varnode with (smaller) \b newVn in all descendants
        ///
        /// If minMaxUse() indicates not all bytes are used, this should always succeed
        /// \param origVn is the given Varnode
        /// \param newVn is the new Varnode to replace with
        /// \param maxByte is the maximum byte immediately used in \b origVn
        /// \param minByte is the minimum byte immediately used in \b origVn
        /// \param data is the function being analyzed
        public static void replaceDescendants(Varnode origVn, Varnode newVn, int maxByte, int minByte,
            Funcdata data)
        {
            IEnumerator<PcodeOp> iter = origVn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.code() == OpCode.CPUI_SUBPIECE) {
                    int truncAmount = (int)op.getIn(1).getOffset();
                    int outSize = op.getOut().getSize();
                    data.opSetInput(op, newVn, 0);
                    if (newVn.getSize() == outSize) {
                        if (truncAmount != minByte)
                            throw new LowlevelError("Could not perform -replaceDescendants-");
                        data.opSetOpcode(op, OpCode.CPUI_COPY);
                        data.opRemoveInput(op, 1);
                    }
                    else if (newVn.getSize() > outSize) {
                        int newTrunc = truncAmount - minByte;
                        if (newTrunc < 0)
                            throw new LowlevelError("Could not perform -replaceDescendants-");
                        if (newTrunc != truncAmount)
                        {
                            data.opSetInput(op, data.newConstant(4, (ulong)newTrunc), 1);
                        }
                    }
                    else
                        throw new LowlevelError("Could not perform -replaceDescendants-");
                }
                else
                    throw new LowlevelError("Could not perform -replaceDescendants-");
            }
        }

        /// \brief Return \b true if given size is a suitable truncated size
        ///
        /// \param size is the given size
        /// \return \b true if it is acceptable
        public static bool acceptableSize(int size)
        {
            if (size == 0) return false;
            if (size >= 8) return true;
            if (size == 1 || size == 2 || size == 4 || size == 8)
                return true;
            return false;
        }

        /// \brief  Build a SUBPIECE of given base Varnode
        ///
        /// The PcodeOp is constructed and inserted near the definition of the base Varnode.
        /// \param basevn is the given base Varnode
        /// \param outsize is the required truncated size in bytes
        /// \param shift is the number of least significant bytes to truncate
        /// \param data is the function being analyzed
        /// \return the output Varnode of the new SUBPIECE
        public static Varnode buildSubpiece(Varnode basevn, uint outsize, uint shift,
            Funcdata data)
        {
            Address newaddr;
            PcodeOp new_op;
            Varnode outvn;

            if (basevn.isInput()) {
                BlockBasic bb = (BlockBasic)data.getBasicBlocks().getBlock(0);
                newaddr = bb.getStart();
            }
            else {
                if (!basevn.isWritten()) throw new LowlevelError("Undefined pullsub");
                newaddr = basevn.getDef().getAddr();
            }
            Address smalladdr1 = new Address();
            bool usetmp = false;
            if (basevn.getAddr().isJoin()) {
                usetmp = true;
                JoinRecord joinrec = data.getArch().findJoin(basevn.getOffset());
                if (joinrec.numPieces() > 1) {
                    // If only 1 piece (float extension) automatically use unique
                    uint skipleft = shift;
                    for (int i = joinrec.numPieces() - 1; i >= 0; --i) {
                        // Move from least significant to most
                        VarnodeData vdata = joinrec.getPiece((uint)i);
                        if (skipleft >= vdata.size) {
                            skipleft -= vdata.size;
                        }
                        else {
                            if (skipleft + outsize > vdata.size)
                                break;
                            smalladdr1 = vdata.space.isBigEndian()
                                ? vdata.getAddr() + (vdata.size - (outsize + skipleft))
                                : vdata.getAddr() + skipleft;
                            usetmp = false;
                            break;
                        }
                    }
                }
            }
            else {
                smalladdr1 = basevn.getSpace().isBigEndian()
                    ? basevn.getAddr() + (basevn.getSize() - (shift + outsize))
                    : basevn.getAddr() + shift;
            }
            // Build new subpiece near definition of basevn
            new_op = data.newOp(2, newaddr);
            data.opSetOpcode(new_op, OpCode.CPUI_SUBPIECE);
            if (usetmp)
                outvn = data.newUniqueOut((int)outsize, new_op);
            else {
                smalladdr1.renormalize((int)outsize);
                outvn = data.newVarnodeOut((int)outsize, smalladdr1, new_op);
            }
            data.opSetInput(new_op, basevn, 0);
            data.opSetInput(new_op, data.newConstant(4, shift), 1);

            if (basevn.isInput())
                data.opInsertBegin(new_op, (BlockBasic)data.getBasicBlocks().getBlock(0));
            else
                data.opInsertAfter(new_op, basevn.getDef());
            return outvn;
        }

        /// \brief Find a predefined SUBPIECE of a base Varnode
        ///
        /// Given a Varnode and desired dimensions (size and shift), search for a preexisting
        /// truncation defined in the same block as the original Varnode or return NULL
        /// \param basevn is the base Varnode
        /// \param outsize is the desired truncation size
        /// \param shift if the desired truncation shift
        /// \return the truncated Varnode or NULL
        public static Varnode? findSubpiece(Varnode basevn, uint outsize, uint shift)
        {
            IEnumerator<PcodeOp> iter = basevn.beginDescend();

            while (iter.MoveNext()) {
                PcodeOp prevop = iter.Current;
                if (prevop.code() != OpCode.CPUI_SUBPIECE)
                    // Find previous SUBPIECE
                    continue;
                // Make sure output is defined in same block as vn_piece
                if (basevn.isInput() && (prevop.getParent().getIndex() != 0)) continue;
                if (!basevn.isWritten()) continue;
                if (basevn.getDef().getParent() != prevop.getParent()) continue;
                // Make sure subpiece matches form
                if (   (prevop.getIn(0) == basevn)
                    && (prevop.getOut().getSize() == outsize)
                    && (prevop.getIn(1).getOffset() == shift))
                {
                    return prevop.getOut();
                }
            }
            return (Varnode)null;
        }
    }
}
