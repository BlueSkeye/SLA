using Sla.CORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A logical value whose storage is split between two Varnodes
    ///
    /// This is usually a pair of Varnodes \b lo and \b hi holding the least and
    /// most significant part of the logical value respectively.  Its possible for
    /// the logical value to be a constant, in which case \b lo and \b hi are set to
    /// null and \b val holds the actual constant.
    /// Its also possible for \b hi to be null by itself, indicating that most signficant
    /// part of the variable is zero, and the logical variable is the zero extension of \b lo.
    internal class SplitVarnode
    {
        /// Least significant piece of the double precision object
        private Varnode lo;
        /// Most significant piece of the double precision object
        private Varnode hi;
        /// A representative of the whole object
        private Varnode? whole;
        /// Operation at which both \b lo and \b hi are defined
        private PcodeOp defpoint;
        /// Block in which both \b lo and \b hi are defined
        private BlockBasic defblock;
        /// Value of a double precision constant
        private ulong val;
        /// Size in bytes of the (virtual) whole
        private int wholesize;

        /// Find whole out of which \b hi and \b lo are split
        /// Look for OpCode.CPUI_SUBPIECE operations off of a common Varnode.
        /// The \b whole field is set to this Varnode if found; the definition point and block are
        /// filled in and \b true is returned.  Otherwise \b false is returned.
        /// \return \b true if the \b whole Varnode is found
        private bool findWholeSplitToPieces()
        {
            if (whole == (Varnode)null)
            {
                if (hi == (Varnode)null) return false;
                if (lo == (Varnode)null) return false;
                if (!hi.isWritten()) return false;
                PcodeOp* subhi = hi.getDef();
                if (subhi.code() == OpCode.CPUI_COPY)
                { // Go thru one level of copy, if the piece is addrtied
                    Varnode* otherhi = subhi.getIn(0);
                    if (!otherhi.isWritten()) return false;
                    subhi = otherhi.getDef();
                }
                if (subhi.code() != OpCode.CPUI_SUBPIECE) return false;
                if (subhi.getIn(1).getOffset() != wholesize - hi.getSize()) return false;
                whole = subhi.getIn(0);
                if (!lo.isWritten()) return false;
                PcodeOp* sublo = lo.getDef();
                if (sublo.code() == OpCode.CPUI_COPY)
                { // Go thru one level of copy, if the piece is addrtied
                    Varnode* otherlo = sublo.getIn(0);
                    if (!otherlo.isWritten()) return false;
                    sublo = otherlo.getDef();
                }
                if (sublo.code() != OpCode.CPUI_SUBPIECE) return false;
                Varnode* res = sublo.getIn(0);
                if (whole == (Varnode)null)
                    whole = res;
                else if (whole != res)
                    return false;       // Doesn't match between pieces
                if (sublo.getIn(1).getOffset() != 0)
                    return false;
                if (whole == (Varnode)null) return false;
            }

            if (whole.isWritten())
            {
                defpoint = whole.getDef();
                defblock = defpoint.getParent();
            }
            else if (whole.isInput())
            {
                defpoint = (PcodeOp)null;
                defblock = (BlockBasic)null;
            }
            return true;
        }

        /// Find the earliest PcodeOp where both \b lo and \b hi are defined
        /// Set the basic block, \b defblock, and PcodeOp, \b defpoint, where they are defined.
        /// Its possible that \b lo and \b hi are \e input Varnodes with no natural defining PcodeOp,
        /// in which case \b defpoint is set to null and \b defblock is set to the function entry block.
        /// The method returns \b true, if the definition point is found, which amounts to returning
        /// \b false if the SplitVarnode is only half constant or half input.
        /// \return \b true if the definition point is located
        private bool findDefinitionPoint()
        {
            PcodeOp* lastop;
            if (hi != (Varnode)null && hi.isConstant()) return false; // If one but not both is constant
            if (lo.isConstant()) return false;
            if (hi == (Varnode)null)
            {   // Implied zero extension
                if (lo.isInput())
                {
                    defblock = (BlockBasic)null;
                    defpoint = (PcodeOp)null;
                }
                else if (lo.isWritten())
                {
                    defpoint = lo.getDef();
                    defblock = defpoint.getParent();
                }
                else
                    return false;
            }
            else if (hi.isWritten())
            {
                if (!lo.isWritten()) return false;     // Do not allow mixed input/non-input pairs
                lastop = hi.getDef();
                defblock = lastop.getParent();
                PcodeOp* lastop2 = lo.getDef();
                BlockBasic* otherblock = lastop2.getParent();
                if (defblock != otherblock)
                {
                    defpoint = lastop;
                    FlowBlock* curbl = defblock;
                    while (curbl != (FlowBlock*)0)
                    { // Make sure defblock dominated by otherblock
                        curbl = curbl.getImmedDom();
                        if (curbl == otherblock) return true;
                    }
                    defblock = otherblock;      // Try lo as final defining location
                    otherblock = lastop.getParent();
                    defpoint = lastop2;
                    curbl = defblock;
                    while (curbl != (FlowBlock*)0)
                    {
                        curbl = curbl.getImmedDom();
                        if (curbl == otherblock) return true;
                    }
                    defblock = (BlockBasic)null;
                    return false;       // Not defined in same basic block
                }
                if (lastop2.getSeqNum().getOrder() > lastop.getSeqNum().getOrder())
                    lastop = lastop2;
                defpoint = lastop;
            }
            else if (hi.isInput())
            {
                if (!lo.isInput())
                    return false;       // Do not allow mixed input/non-input pairs
                defblock = (BlockBasic)null;
                defpoint = (PcodeOp)null;
            }
            return true;
        }

        /// Find whole Varnode formed as a OpCode.CPUI_PIECE of \b hi and \b lo
        /// We scan for concatenations formed out of \b hi and \b lo, in the correct significance order.
        /// We assume \b hi and \b lo are defined in the same basic block (or  are both inputs) and that
        /// the concatenation is also in this block. If such a concatenation is found, \b whole is set to the
        /// concatenated Varnode, the defining block and PcodeOp is filled in, and \b true is returned.
        /// \return \b true if a \b whole concatenated from \b hi and \b lo is found
        private bool findWholeBuiltFromPieces()
        {
            if (hi == (Varnode)null) return false;
            if (lo == (Varnode)null) return false;
            IEnumerator<PcodeOp> iter = lo.beginDescend();
            PcodeOp res = (PcodeOp)null;
            BlockBasic? bb;
            if (lo.isWritten())
                bb = lo.getDef().getParent();
            else if (lo.isInput())
                bb = (BlockBasic)null;
            else
                throw new LowlevelError("Trying to find whole on free varnode");
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.code() != OpCode.CPUI_PIECE) continue;
                if (op.getIn(0) != hi) continue;
                if (bb != (BlockBasic)null) {
                    if (op.getParent() != bb) continue; // Not defined in earliest block
                }
                else if (!op.getParent().isEntryPoint())
                    continue;
                if (res == (PcodeOp)null)
                    res = op;
                else {
                    if (op.getSeqNum().getOrder() < res.getSeqNum().getOrder()) // Find "earliest" whole
                        res = op;
                }
            }

            if (res == (PcodeOp)null)
                whole = (Varnode)null;
            else {
                defpoint = res;
                defblock = defpoint.getParent();
                whole = res.getOut();
            }
            return (whole != (Varnode)null);
        }

        /// Construct an uninitialized SplitVarnode
        public SplitVarnode()
        {
        }

        /// Construct a double precision constant
        /// Internally, the \b lo and \b hi Varnodes are set to null, and the \b val field
        /// holds the constant value.
        /// \param sz is the size in bytes of the constant
        /// \param v is the constant value
        public SplitVarnode(int sz, ulong v)
        {
            val = v;
            wholesize = sz;
            lo = (Varnode)null;
            hi = (Varnode)null;
            whole = (Varnode)null;
            defpoint = (PcodeOp)null;
            defblock = (BlockBasic)null;
        }

        public SplitVarnode(Varnode l, Varnode h)
        {
            initPartial(l.getSize() + h.getSize(), l, h);
        }    ///< Construct from \b lo and \b hi piece

        /// Construct given Varnode pieces and a known \b whole Varnode
        /// The \b lo, \b hi, and \b whole fields are filled @in.  The definition point remains uninitialized.
        /// \param w is the given whole Varnode
        /// \param l is the given (least significant) Varnode piece
        /// \param h is the given (most significant) Varnode piece
        public void initAll(Varnode w, Varnode l, Varnode h)
        {
            wholesize = w.getSize();
            lo = l;
            hi = h;
            whole = w;
            defpoint = (PcodeOp)null;
            defblock = (BlockBasic)null;
        }

        /// (Re)initialize \b this SplitVarnode as a constant
        /// \param sz is the size of the constant in bytes
        /// \param v is the constant value
        public void initPartial(int sz, ulong v)
        {
            val = v;
            wholesize = sz;
            lo = (Varnode)null;
            hi = (Varnode)null;
            whole = (Varnode)null;
            defpoint = (PcodeOp)null;
            defblock = (BlockBasic)null;
        }

        /// (Re)initialize \b this SplitVarnode given Varnode pieces
        /// The Varnode pieces can be constant, in which case a constant SplitVarnode is initialized and
        /// a constant value is built from the pieces.  The given most significant piece can be null, indicating
        /// that the most significant piece of the whole is an implied zero.
        /// \param sz is the size of the logical whole in bytes
        /// \param l is the given (least significant) Varnode piece
        /// \param h is the given (most significant) Varnode piece
        public void initPartial(int sz, Varnode l, Varnode h)
        {
            if (h == (Varnode)null)
            {   // hi is an implied zero
                hi = (Varnode)null;
                if (l.isConstant())
                {
                    val = l.getOffset();   // Assume l is a constant
                    lo = (Varnode)null;
                }
                else
                    lo = l;
            }
            else
            {
                if (l.isConstant() && h.isConstant())
                {
                    val = h.getOffset();
                    val <<= (l.getSize() * 8);
                    val |= l.getOffset();
                    lo = (Varnode)null;
                    hi = (Varnode)null;
                }
                else
                {
                    lo = l;
                    hi = h;
                }
            }
            wholesize = sz;
            whole = (Varnode)null;
            defpoint = (PcodeOp)null;
            defblock = (BlockBasic)null;
        }

        /// Try to initialize given just the most significant piece split from whole
        /// Verify that the given most significant piece is formed via OpCode.CPUI_SUBPIECE and search
        /// for the least significant piece being formed as a OpCode.CPUI_SUBPIECE of the same whole.
        /// \param h is the given (most significant) Varnode piece
        /// \return \b true if the matching \b whole and least significant piece is found
        public bool inHandHi(Varnode h)
        {
            if (!h.isPrecisHi()) return false; // Check for mark, in order to have quick -false- in most cases
                                                // Search for the companion
            if (h.isWritten())
            {
                PcodeOp* op = h.getDef();
                // We could check for double loads here
                if (op.code() == OpCode.CPUI_SUBPIECE)
                {
                    Varnode* w = op.getIn(0);
                    if (op.getIn(1).getOffset() != (ulong)(w.getSize() - h.getSize())) return false;
                    list<PcodeOp*>::const_iterator iter, enditer;
                    iter = w.beginDescend();
                    enditer = w.endDescend();
                    while (iter != enditer)
                    {
                        PcodeOp* tmpop = *iter;
                        ++iter;
                        if (tmpop.code() != OpCode.CPUI_SUBPIECE) continue;
                        Varnode* tmplo = tmpop.getOut();
                        if (!tmplo.isPrecisLo()) continue;
                        if (tmplo.getSize() + h.getSize() != w.getSize()) continue;
                        if (tmpop.getIn(1).getOffset() != 0) continue;
                        // There could conceivably be more than one, but this shouldn't happen with CSE
                        initAll(w, tmplo, h);
                        return true;
                    }
                }
            }
            return false;
        }

        /// Try to initialize given just the least significant piece split from whole
        /// Verify that the given least significant piece is formed via OpCode.CPUI_SUBPIECE and search
        /// for the most significant piece being formed as a OpCode.CPUI_SUBPIECE of the same whole.
        /// \param l is the given (least significant) Varnode piece
        /// \return \b true if the matching \b whole and most significant piece is found
        public bool inHandLo(Varnode l)
        {
            if (!l.isPrecisLo()) return false; // Check for mark, in order to have quick -false- in most cases
                                                // Search for the companion
            if (l.isWritten())
            {
                PcodeOp* op = l.getDef();
                // We could check for double loads here
                if (op.code() == OpCode.CPUI_SUBPIECE)
                {
                    Varnode* w = op.getIn(0);
                    if (op.getIn(1).getOffset() != 0) return false;
                    list<PcodeOp*>::const_iterator iter, enditer;
                    iter = w.beginDescend();
                    enditer = w.endDescend();
                    while (iter != enditer)
                    {
                        PcodeOp* tmpop = *iter;
                        ++iter;
                        if (tmpop.code() != OpCode.CPUI_SUBPIECE) continue;
                        Varnode* tmphi = tmpop.getOut();
                        if (!tmphi.isPrecisHi()) continue;
                        if (tmphi.getSize() + l.getSize() != w.getSize()) continue;
                        if (tmpop.getIn(1).getOffset() != (ulong)l.getSize()) continue;
                        // There could conceivably be more than one, but this shouldn't happen with CSE
                        initAll(w, l, tmphi);
                        return true;
                    }
                }
            }
            return false;
        }

        /// Try to initialize given just the least significant piece (other piece may be zero)
        /// The given least significant Varnode must already be marked as a piece.
        /// Initialize the SplitVarnode with the given piece and the \b whole that it came from.
        /// If a matching most significant piece can be found, as another OpCode.CPUI_SUBPIECE off of the same
        /// \b whole, initialize that as well.  Otherwise leave the most significant piece as null.
        /// \param l is the given (least significant) Varnode piece
        /// \return \b true if the SplitVarnode is successfully initialized
        public bool inHandLoNoHi(Varnode l)
        {
            if (!l.isPrecisLo()) return false;
            if (!l.isWritten()) return false;
            PcodeOp* op = l.getDef();
            if (op.code() != OpCode.CPUI_SUBPIECE) return false;
            if (op.getIn(1).getOffset() != 0) return false;
            Varnode* w = op.getIn(0);

            list<PcodeOp*>::const_iterator iter, enditer;
            iter = w.beginDescend();
            enditer = w.endDescend();
            while (iter != enditer)
            {
                PcodeOp* tmpop = *iter;
                ++iter;
                if (tmpop.code() != OpCode.CPUI_SUBPIECE) continue;
                Varnode* tmphi = tmpop.getOut();
                if (!tmphi.isPrecisHi()) continue;
                if (tmphi.getSize() + l.getSize() != w.getSize()) continue;
                if (tmpop.getIn(1).getOffset() != (ulong)l.getSize()) continue;
                // There could conceivably be more than one, but this shouldn't happen with CSE
                initAll(w, l, tmphi);
                return true;
            }
            initAll(w, l, (Varnode)null);
            return true;
        }

        /// Try to initialize given just the most significant piece concatenated into whole
        /// Initialize the SplitVarnode given the most significant piece, if it is concatenated together
        /// immediately with is least significant piece.  The OpCode.CPUI_PIECE and the matching least significant
        /// piece must be unique.  If these are found, \b hi, \b lo, and \b whole are all filled @in.
        /// \param h is the given (most significant) piece
        /// \return \b true if initialization was successful and the least significant piece was found
        public bool inHandHiOut(Varnode h)
        {
            IEnumerator<PcodeOp> iter = h.beginDescend();
            Varnode? loTmp = (Varnode)null;
            Varnode? outvn = (Varnode)null;
            while (iter.MoveNext()) {
                PcodeOp pieceop = iter.Current;
                if (pieceop.code() != OpCode.CPUI_PIECE) continue;
                if (pieceop.getIn(0) != h) continue;
                Varnode l = pieceop.getIn(1);
                if (!l.isPrecisLo()) continue;
                if (loTmp != (Varnode)null) return false; // Whole is not unique
                loTmp = l;
                outvn = pieceop.getOut();
            }
            if (loTmp != (Varnode)null) {
                initAll(outvn, loTmp, h);
                return true;
            }
            return false;
        }

        /// Try to initialize given just the least significant piece concatenated into whole
        /// Initialize the SplitVarnode given the least significant piece, if it is concatenated together
        /// immediately with is nost significant piece.  The OpCode.CPUI_PIECE and the matching most significant
        /// piece must be unique.  If these are found, \b hi, \b lo, and \b whole are all filled @in.
        /// \param l is the given (least significant) piece
        /// \return \b true if initialization was successful and the most significant piece was found
        public bool inHandLoOut(Varnode l)
        {
            IEnumerator<PcodeOp> iter = l.beginDescend();
            Varnode? hiTmp = (Varnode)null;
            Varnode? outvn = (Varnode)null;
            while (iter.MoveNext()) {
                PcodeOp pieceop = iter.Current;
                if (pieceop.code() != OpCode.CPUI_PIECE) continue;
                if (pieceop.getIn(1) != l) continue;
                Varnode h = pieceop.getIn(0);
                if (!h.isPrecisHi()) continue;
                if (hiTmp != (Varnode)null) return false; // Whole is not unique
                hiTmp = h;
                outvn = pieceop.getOut();
            }
            if (hiTmp != (Varnode)null) {
                initAll(outvn, l, hiTmp);
                return true;
            }
            return false;
        }

        /// Return \b true if \b this is a constant
        public bool isConstant() => (lo == null);

        /// Return \b true if both pieces are initialized
        public bool hasBothPieces() => ((hi!=null)&&(lo!=null));

        /// Get the size of \b this SplitVarnode as a whole in bytes
        public int getSize() => wholesize;

        /// Get the least significant Varnode piece
        public Varnode getLo() => lo;

        /// Get the most significant Varnode piece
        public Varnode getHi() => hi;

        /// Get the Varnode representing \b this as a whole
        public Varnode getWhole() => whole;

        /// Get the(final) defining PcodeOp of \b this
        public PcodeOp getDefPoint() => defpoint;

        /// Get the defining basic block of \b this
        public BlockBasic getDefBlock() => defblock;

        /// Get the value of \b this, assuming it is a constant
        public ulong getValue() => val;

        /// Does a whole Varnode already exist or can it be created before the given PcodeOp
        /// The whole Varnode must be defined or definable \e before the given PcodeOp.
        /// This is checked by comparing the given PcodeOp to the defining PcodeOp and block for \b this,
        /// which are filled in if they weren't before.
        /// \param existop is the given PcodeOp
        /// \return \b true if a whole Varnode exists or can be defined before the given PcodeOp
        public bool isWholeFeasible(PcodeOp existop)
        {
            if (isConstant()) return true;
            if ((lo != (Varnode)null) && (hi != (Varnode)null))
                if (lo.isConstant() != hi.isConstant()) return false; // Mixed constant/non-constant
            if (!findWholeSplitToPieces())
            {
                if (!findWholeBuiltFromPieces())
                {
                    if (!findDefinitionPoint())
                        return false;
                }
            }
            if (defblock == (BlockBasic)null) return true;
            FlowBlock* curbl = existop.getParent();
            if (curbl == defblock)  // If defined in same block as -existop- check PcodeOp ordering
                return (defpoint.getSeqNum().getOrder() <= existop.getSeqNum().getOrder());
            while (curbl != (FlowBlock*)0)
            { // Make sure defbock dominates block containing -existop-
                curbl = curbl.getImmedDom();
                if (curbl == defblock) return true;
            }
            return false;
        }

        /// Does a whole Varnode already exist or can it be created before the given basic block
        /// This is similar to isWholeFeasible(), but the \b whole must be defined before the end of the given
        /// basic block.
        /// \param bl is the given basic block
        /// \return \b true if a whole Varnode exists or can be defined before the end of the given basic block
        public bool isWholePhiFeasible(FlowBlock bl)
        {
            if (isConstant()) return false;
            if (!findWholeSplitToPieces())
            {
                if (!findWholeBuiltFromPieces())
                {
                    if (!findDefinitionPoint())
                        return false;
                }
            }
            if (defblock == (BlockBasic)null) return true;
            if (bl == defblock) // If defined in same block
                return true;
            while (bl != (FlowBlock*)0)
            { // Make sure defblock dominates block containing -existop-
                bl = bl.getImmedDom();
                if (bl == defblock) return true;
            }
            return false;
        }

        /// Create a \b whole Varnode for \b this, if it doesn't already exist
        /// This method assumes isWholeFeasible has been called and returned \b true.
        /// If the \b whole didn't already exist, it is created as the concatenation of its two pieces.
        /// If the pieces were constant, a constant whole Varnode is created.
        /// If the \b hi piece was null, the whole is created as a CPUI_ZEXT of the \b lo.
        /// \param data is the function owning the Varnode pieces
        public void findCreateWhole(Funcdata data)
        {
            if (isConstant())
            {
                whole = data.newConstant(wholesize, val);
                return;
            }
            else
            {
                if (lo != (Varnode)null)
                    lo.setPrecisLo();      // Mark the pieces
                if (hi != (Varnode)null)
                    hi.setPrecisHi();
            }

            if (whole != (Varnode)null) return; // Already found the whole
            PcodeOp* concatop;
            Address addr;
            BlockBasic* topblock = (BlockBasic)null;

            if (defblock != (BlockBasic)null)
                addr = defpoint.getAddr();
            else
            {
                topblock = (BlockBasic*)data.getBasicBlocks().getStartBlock();
                addr = topblock.getStart();
            }

            if (hi != (Varnode)null)
            {
                concatop = data.newOp(2, addr);
                // Do we need to pick something other than a unique????
                whole = data.newUniqueOut(wholesize, concatop);
                data.opSetOpcode(concatop, OpCode.CPUI_PIECE);
                data.opSetOutput(concatop, whole);
                data.opSetInput(concatop, hi, 0);
                data.opSetInput(concatop, lo, 1);
            }
            else
            {
                concatop = data.newOp(1, addr);
                whole = data.newUniqueOut(wholesize, concatop);
                data.opSetOpcode(concatop, OpCode.CPUI_INT_ZEXT);
                data.opSetOutput(concatop, whole);
                data.opSetInput(concatop, lo, 0);
            }

            if (defblock != (BlockBasic)null)
                data.opInsertAfter(concatop, defpoint);
            else
                data.opInsertBegin(concatop, topblock);

            defpoint = concatop;
            defblock = concatop.getParent();
        }

        /// Create a \b whole Varnode that will be a PcodeOp output
        /// If the \b whole does not already exist, it is created as a \e unique register.
        /// The new Varnode must later be set explicitly as the output of some PcodeOp.
        /// \param data is the function owning the Varnode pieces
        public void findCreateOutputWhole(Funcdata data)
        { // Create the actual -whole- varnode
            lo.setPrecisLo();      // Mark the pieces
            hi.setPrecisHi();
            if (whole != (Varnode)null) return;
            whole = data.newUnique(wholesize);
        }

        /// Create a \b whole Varnode from pieces, respecting piece storage
        /// If the pieces can be treated as a contiguous whole, use the same storage location to construct the \b whole,
        /// otherwise use a \b join address for storage.
        /// \param data is the function owning the pieces
        public void createJoinedWhole(Funcdata data)
        {
            lo.setPrecisLo();
            hi.setPrecisHi();
            if (whole != (Varnode)null) return;
            Address newaddr;
            if (!isAddrTiedContiguous(lo, hi, newaddr))
            {
                newaddr = data.getArch().constructJoinAddress(data.getArch().translate, hi.getAddr(), hi.getSize(),
                                                    lo.getAddr(), lo.getSize());
            }
            whole = data.newVarnode(wholesize, newaddr);
            whole.setWriteMask();
        }

        /// Rebuild the least significant piece as a OpCode.CPUI_SUBPIECE of the \b whole
        /// Assume \b lo was initially defined in some other way but now needs to be defined as a split from
        /// a new \b whole Varnode.  The original PcodeOp defining \b lo is transformed into a OpCode.CPUI_SUBPIECE.
        /// The method findCreateOutputWhole() must already have been called on \b this.
        public void buildLoFromWhole(Funcdata data)
        {
            PcodeOp* loop = lo.getDef();
            if (loop == (PcodeOp)null)
                throw new LowlevelError("Building low piece that was originally undefined");

            List<Varnode*> inlist;
            inlist.Add(whole);
            inlist.Add(data.newConstant(4, 0));
            if (loop.code() == OpCode.CPUI_MULTIEQUAL)
            {
                // When converting the MULTIEQUAL to a SUBPIECE, we need to reinsert the op so that we don't
                // get a break in the sequence of MULTIEQUALs at the beginning of the block
                BlockBasic* bl = loop.getParent();
                data.opUninsert(loop);
                data.opSetOpcode(loop, OpCode.CPUI_SUBPIECE);
                data.opSetAllInput(loop, inlist);
                data.opInsertBegin(loop, bl);
            }
            else if (loop.code() == OpCode.CPUI_INDIRECT)
            {
                // When converting an INDIRECT to a SUBPIECE, we need to reinsert the op AFTER the affector
                PcodeOp* affector = PcodeOp::getOpFromConst(loop.getIn(1).getAddr());
                if (!affector.isDead())
                    data.opUninsert(loop);
                data.opSetOpcode(loop, OpCode.CPUI_SUBPIECE);
                data.opSetAllInput(loop, inlist);
                if (!affector.isDead())
                    data.opInsertAfter(loop, affector);
            }
            else
            {
                data.opSetOpcode(loop, OpCode.CPUI_SUBPIECE);
                data.opSetAllInput(loop, inlist);
            }
        }

        /// Rebuild the most significant piece as a OpCode.CPUI_SUBPIECE of the \b whole
        /// Assume \b hi was initially defined in some other way but now needs to be defined as a split from
        /// a new \b whole Varnode.  The original PcodeOp defining \b hi is transformed into a OpCode.CPUI_SUBPIECE.
        /// The method findCreateOutputWhole() must already have been called on \b this.
        public void buildHiFromWhole(Funcdata data)
        {
            PcodeOp* hiop = hi.getDef();
            if (hiop == (PcodeOp)null)
                throw new LowlevelError("Building low piece that was originally undefined");

            List<Varnode*> inlist;
            inlist.Add(whole);
            inlist.Add(data.newConstant(4, lo.getSize()));
            if (hiop.code() == OpCode.CPUI_MULTIEQUAL)
            {
                // When converting the MULTIEQUAL to a SUBPIECE, we need to reinsert the op so that we don't
                // get a break in the sequence of MULTIEQUALs at the beginning of the block
                BlockBasic* bl = hiop.getParent();
                data.opUninsert(hiop);
                data.opSetOpcode(hiop, OpCode.CPUI_SUBPIECE);
                data.opSetAllInput(hiop, inlist);
                data.opInsertBegin(hiop, bl);
            }
            else if (hiop.code() == OpCode.CPUI_INDIRECT)
            {
                // When converting the INDIRECT to a SUBPIECE, we need to reinsert AFTER the affector
                PcodeOp* affector = PcodeOp::getOpFromConst(hiop.getIn(1).getAddr());
                if (!affector.isDead())
                    data.opUninsert(hiop);
                data.opSetOpcode(hiop, OpCode.CPUI_SUBPIECE);
                data.opSetAllInput(hiop, inlist);
                if (!affector.isDead())
                    data.opInsertAfter(hiop, affector);
            }
            else
            {
                data.opSetOpcode(hiop, OpCode.CPUI_SUBPIECE);
                data.opSetAllInput(hiop, inlist);
            }
        }

        /// Find the earliest definition point of the \b lo and \b hi pieces
        /// If both \b lo and \b hi pieces are written, the earlier of the two defining PcodeOps
        /// is returned.  Otherwise null is returned.
        /// \return the earlier of the two defining PcodeOps or null
        public PcodeOp findEarliestSplitPoint()
        {
            if (!hi.isWritten()) return (PcodeOp)null;
            if (!lo.isWritten()) return (PcodeOp)null;
            PcodeOp* hiop = hi.getDef();
            PcodeOp* loop = lo.getDef();
            if (loop.getParent() != hiop.getParent())
                return (PcodeOp)null;
            return (loop.getSeqNum().getOrder() < hiop.getSeqNum().getOrder()) ? loop : hiop;
        }

        /// Find the point at which the output \b whole must exist
        /// Its assumed that \b this is the output of the double precision operation being performed.
        /// The \b whole Varnode may not yet exist.  This method returns the first PcodeOp where the \b whole
        /// needs to exist.  If no such PcodeOp exists, null is returned.
        /// \return the first PcodeOp where the \b whole needs to exist or null
        public PcodeOp findOutExist()
        {
            if (findWholeBuiltFromPieces())
            {
                return defpoint;
            }
            return findEarliestSplitPoint();
        }

        /// \brief Check if the values in the given Varnodes differ by the given size
        ///
        /// Return \b true, if the (possibly dynamic) value represented by the given \b vn1 plus \b size1
        /// produces the value in the given \b vn2. For constants, the values can be computed directly, but
        /// otherwise \b vn1 and \b vn2 must be defined by INT_ADD operations from a common ancestor.
        /// \param vn1 is the first given Varnode
        /// \param vn2 is the second given Varnode
        /// \param size1 is the given size to add to \b vn1
        /// \return \b true if the values in \b vn1 and \b vn2 are related by the given size
        public static bool adjacentOffsets(Varnode vn1, Varnode vn2, ulong size1)
        {
            if (vn1.isConstant())
            {
                if (!vn2.isConstant()) return false;
                return ((vn1.getOffset() + size1) == vn2.getOffset());
            }

            if (!vn2.isWritten()) return false;
            PcodeOp* op2 = vn2.getDef();
            if (op2.code() != OpCode.CPUI_INT_ADD) return false;
            if (!op2.getIn(1).isConstant()) return false;
            ulong c2 = op2.getIn(1).getOffset();

            if (op2.getIn(0) == vn1)
                return (size1 == c2);

            if (!vn1.isWritten()) return false;
            PcodeOp* op1 = vn1.getDef();
            if (op1.code() != OpCode.CPUI_INT_ADD) return false;
            if (!op1.getIn(1).isConstant()) return false;
            ulong c1 = op1.getIn(1).getOffset();

            if (op1.getIn(0) != op2.getIn(0)) return false;
            return ((c1 + size1) == c2);
        }

        /// \brief Verify that the pointers into the given LOAD/STORE PcodeOps address contiguous memory
        ///
        /// The two given PcodeOps must either both be LOADs or both be STOREs. The pointer for the
        /// first PcodeOp is labeled as the most significant piece of the contiguous whole, the
        /// second PcodeOp is labeled as the least significant piece. The p-code defining the pointers is examined
        /// to determine if the two memory regions being pointed at really form one contiguous region.
        /// If the regions are contiguous and the pointer labeling is valid, \b true is returned, the PcodeOps are sorted
        /// into \b first and \b second based on Address, and the address space of the memory region is passed back.
        /// \param most is the given LOAD/STORE PcodeOp referring to the most significant region
        /// \param least is the given LOAD/STORE PcodeOp referring to the least significant region
        /// \param first is used to pass back the earliest of the address sorted PcodeOps
        /// \param second is used to pass back the latest of the address sorted PcodeOps
        /// \param spc is used to pass back the LOAD address space
        /// \param sizeres is used to pass back the combined LOAD size
        /// \return true if the given PcodeOps are contiguous LOADs
        public static bool testContiguousPointers(PcodeOp most, PcodeOp least, out PcodeOp first,
            out PcodeOp second, out AddrSpace spc)
        {
            spc = least.getIn(0).getSpaceFromConst();
            if (most.getIn(0).getSpaceFromConst() != spc) return false;

            if (spc.isBigEndian())
            {   // Convert significance order to address order
                first = most;
                second = least;
            }
            else
            {
                first = least;
                second = most;
            }
            Varnode* firstptr = first.getIn(1);
            if (firstptr.isFree()) return false;
            int sizeres;
            if (first.code() == OpCode.CPUI_LOAD)
                sizeres = first.getOut().getSize(); // # of bytes read by lowest address load
            else        // OpCode.CPUI_STORE
                sizeres = first.getIn(2).getSize();

            // Check if the loads are adjacent to each other
            return adjacentOffsets(first.getIn(1), second.getIn(1), (ulong)sizeres);
        }

        /// \brief Return \b true if the given pieces can be melded into a contiguous storage location
        ///
        /// The given Varnodes must be \e address \e tied, and their storage must line up, respecting their
        /// significance as pieces.
        /// \param lo is the given least significant piece
        /// \param hi is the given most significant piece
        /// \param res is used to pass back the starting address of the contigous range
        /// \return \b true if the pieces are address tied and form a contiguous range
        public static bool isAddrTiedContiguous(Varnode lo, Varnode hi, Address res)
        {
            if (!lo.isAddrTied()) return false;
            if (!hi.isAddrTied()) return false;

            // Make sure there is no explicit symbol that would prevent the pieces from being joined
            SymbolEntry* entryLo = lo.getSymbolEntry();
            SymbolEntry* entryHi = hi.getSymbolEntry();
            if (entryLo != (SymbolEntry)null || entryHi != (SymbolEntry)null)
            {
                if (entryLo == (SymbolEntry)null || entryHi == (SymbolEntry)null)
                    return false;       // One is marked with a symbol, the other is not
                if (entryLo.getSymbol() != entryHi.getSymbol())
                    return false;       // They are part of different symbols
            }
            AddrSpace* spc = lo.getSpace();
            if (spc != hi.getSpace()) return false;
            ulong looffset = lo.getOffset();
            ulong hioffset = hi.getOffset();
            if (spc.isBigEndian())
            {
                if (hioffset >= looffset) return false;
                if (hioffset + hi.getSize() != looffset) return false;
                res = hi.getAddr();
            }
            else
            {
                if (looffset >= hioffset) return false;
                if (looffset + lo.getSize() != hioffset) return false;
                res = lo.getAddr();
            }
            return true;
        }

        /// \brief Create a list of all the possible pairs that contain the same logical value as the given Varnode
        ///
        /// The given Varnode is assumed to be the logical whole that is being used in a double precision calculation.
        /// At least one of the most or least significant pieces must be extracted from the whole and must be
        /// marked as a double precision piece.
        /// \param w is the given Varnode whole
        /// \param splitvec is the container for holding any discovered SplitVarnodes
        public static void wholeList(Varnode w, List<SplitVarnode> splitvec)
        {
            SplitVarnode basic = new SplitVarnode() {
                whole = w,
                hi = (Varnode)null,
                lo = (Varnode)null,
                wholesize = w.getSize()
            };
            IEnumerator<PcodeOp> iter = basic.whole.beginDescend();
            int res = 0;
            while (iter.MoveNext()) {
                PcodeOp subop = iter.Current;
                if (subop.code() != OpCode.CPUI_SUBPIECE) continue;
                Varnode vn = subop.getOut();
                if (vn.isPrecisHi()) {
                    if (subop.getIn(1).getOffset() != basic.wholesize - vn.getSize()) continue;
                    basic.hi = vn;
                    res |= 2;
                }
                else if (vn.isPrecisLo()) {
                    if (subop.getIn(1).getOffset() != 0) continue;
                    basic.lo = vn;
                    res |= 1;
                }
            }
            if (res == 0) return;
            if (res == 3 && (basic.lo.getSize() + basic.hi.getSize() != basic.wholesize))
                return;
            splitvec.Add(basic);
            findCopies(basic, splitvec);
        }

        /// \brief Find copies from (the pieces of) the given SplitVarnode
        ///
        /// Scan for each piece being used as input to COPY operations.  If the each piece is
        /// copied within the same basic block to contiguous storage locations, create a new
        /// SplitVarnode from COPY outputs and add it to the list.
        /// \param in is the given SplitVarnode
        /// \param splitvec is the container for holding SplitVarnode copies
        public static void findCopies(SplitVarnode @in, List<SplitVarnode> splitvec)
        {
            if (!@in.hasBothPieces()) return;
            IEnumerator<PcodeOp> iter = @in.getLo().beginDescend();
            while (iter.MoveNext()) {
                PcodeOp loop = iter.Current;
                if (loop.code() != OpCode.CPUI_COPY) continue;
                Varnode locpy = loop.getOut();
                Address addr = locpy.getAddr(); // Calculate address of hi part
                if (addr.isBigEndian())
                    addr = addr - (@in.getHi().getSize());
                else
                    addr = addr + locpy.getSize();
                IEnumerator<PcodeOp> iter2 = @in.getHi().beginDescend();
                while (iter2.MoveNext()) {
                    PcodeOp hiop = iter2.Current;
                    if (hiop.code() != OpCode.CPUI_COPY) continue;
                    Varnode hicpy = hiop.getOut();
                    if (hicpy.getAddr() != addr) continue;
                    if (hiop.getParent() != loop.getParent()) continue;
                    SplitVarnode newsplit = new SplitVarnode();
                    newsplit.initAll(@in.getWhole(), locpy, hicpy);
                    splitvec.Add(newsplit);
                }
            }
        }

        /// \brief For the given CBRANCH PcodeOp, pass back the \b true and \b false basic blocks
        ///
        /// The result depends on the \e boolean \e flip property of the CBRANCH, and the user can
        /// also flip the meaning of the branches.
        /// \param boolop is the given CBRANCH PcodeOp
        /// \param flip is \b true if the caller wants to flip the meaning of the blocks
        /// \param trueout is used to pass back the true fall-through block
        /// \param falseout is used to pass back the false fall-through block
        public static void getTrueFalse(PcodeOp boolop, bool flip, out BlockBasic trueout,
            out BlockBasic falseout)
        {
            BlockBasic* parent = boolop.getParent();
            BlockBasic* trueblock = (BlockBasic*)parent.getTrueOut();
            BlockBasic* falseblock = (BlockBasic*)parent.getFalseOut();
            if (boolop.isBooleanFlip() != flip)
            {
                trueout = falseblock;
                falseout = trueblock;
            }
            else
            {
                trueout = trueblock;
                falseout = falseblock;
            }
        }

        /// \brief Return \b true if the basic block containing the given CBRANCH PcodeOp performs no other operation.
        ///
        /// The basic block can contain the CBRANCH and the one PcodeOp producing the boolean value.
        /// Otherwise \b false is returned.
        /// \param branchop is the given CBRANCH
        /// \return \b true if the parent basic block performs only the branch operation
        public static bool otherwiseEmpty(PcodeOp branchop)
        {
            BlockBasic* bl = branchop.getParent();
            if (bl.sizeIn() != 1) return false;
            PcodeOp* otherop = (PcodeOp)null;
            Varnode* vn = branchop.getIn(1);
            if (vn.isWritten())
                otherop = vn.getDef();
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = bl.beginOp();
            enditer = bl.endOp();
            while (iter != enditer)
            {
                PcodeOp* op = *iter;
                ++iter;
                if (op == otherop) continue;
                if (op == branchop) continue;
                return false;
            }
            return true;
        }

        /// \brief Verify that the given PcodeOp is a OpCode.CPUI_INT_MULT by -1
        ///
        /// The PcodeOp must be a OpCode.CPUI_INT_MULT and the second operand must be a constant -1.
        /// \param op is the given PcodeOp
        /// \return \b true if the PcodeOp is a multiple by -1
        public static bool verifyMultNegOne(PcodeOp op)
        {
            if (op.code() != OpCode.CPUI_INT_MULT) return false;
            Varnode* in1 = op.getIn(1);
            if (!in1.isConstant()) return false;
            if (in1.getOffset() != Globals.calc_mask(in1.getSize())) return false;
            return true;
        }

        /// \brief Check that the logical version of a (generic) binary double-precision operation can be created
        ///
        /// This checks only the most generic aspects of the calculation.  The input and output whole Varnodes
        /// must already exist or be creatable.  The point where the output Varnode must exist is identified
        /// and returned.  If the binary operation cannot be created, null is returned.
        /// \param out is the output of the binary operation
        /// \param in1 is the first input of the binary operation
        /// \param in2 is the second input of the binary operation
        /// \return the first PcodeOp where the output whole must exist
        public static PcodeOp prepareBinaryOp(SplitVarnode @out, SplitVarnode in1,
            SplitVarnode in2)
        {
            PcodeOp* existop = @out.findOutExist(); // Find point where output whole needs to exist
            if (existop == (PcodeOp)null) return existop; // If we can find no such point return false;
            if (!in1.isWholeFeasible(existop)) return (PcodeOp)null;
            if (!in2.isWholeFeasible(existop)) return (PcodeOp)null;
            return existop;
        }

        /// \brief Rewrite a double precision binary operation by replacing the pieces with unified Varnodes
        ///
        /// This assumes that we have checked that the transformation is possible via the various
        /// verify and prepare methods.  After this method is called, the logical inputs and output of
        /// the calculation will exist as real Varnodes.
        /// \param data is the function owning the operation
        /// \param out is the output of the binary operation
        /// \param in1 is the first input to the binary operation
        /// \param in2 is the second input to the binary operation
        /// \param existop is the precalculated PcodeOp where the output whole Varnode must exist
        /// \param opc is the opcode of the operation
        public static void createBinaryOp(Funcdata data, SplitVarnode @out, SplitVarnode in1,
            SplitVarnode in2, PcodeOp existop, OpCode opc)
        {
            @out.findCreateOutputWhole(data);
            in1.findCreateWhole(data);
            in2.findCreateWhole(data);
            if (existop.code() != OpCode.CPUI_PIECE)
            { // If the output whole didn't previously exist
                PcodeOp* newop = data.newOp(2, existop.getAddr()); // new op which creates the output whole
                data.opSetOpcode(newop, opc);
                data.opSetOutput(newop, @out.getWhole());
                data.opSetInput(newop, in1.getWhole(), 0);
                data.opSetInput(newop, in2.getWhole(), 1);
                data.opInsertBefore(newop, existop);
                @out.buildLoFromWhole(data);
                @out.buildHiFromWhole(data);
            }
            else
            {           // The whole previously existed
                data.opSetOpcode(existop, opc); // our new op replaces the op previously defining the output whole
                data.opSetInput(existop, in1.getWhole(), 0);
                data.opSetInput(existop, in2.getWhole(), 1);
            }
        }

        /// \brief Make sure input and output operands of a double precision shift operation are compatible
        ///
        /// Do generic testing that the input and output whole Varnodes can be created.  Calculate the
        /// PcodeOp where the output whole must exist and return it.  If logical operation cannot be created,
        /// return null.
        /// \param out is the output of the double precision shift operation
        /// \param in is the (first) input operand of the double precision shift operation
        /// \return the PcodeOp where output whole must exist or null
        public static PcodeOp prepareShiftOp(SplitVarnode @out, SplitVarnode @in)
        {
            PcodeOp existop = @out.findOutExist(); // Find point where output whole needs to exist
            if (existop == (PcodeOp)null) return existop;
            if (!@@in.isWholeFeasible(existop)) return (PcodeOp)null;
            return existop;
        }

        /// \brief Rewrite a double precision shift by replacing hi/lo pieces with unified Varnodes
        ///
        /// This assumes that we have checked that the transformation is possible by calling the appropriate
        /// verify and prepare methods. After this method is called, the logical inputs and output of
        /// the calculation will exist as real Varnodes.  The \e shift \e amount is not treated as a double
        /// precision variable.
        /// \param data is the function owning the operation
        /// \param out is the output of the double precision operation
        /// \param in is the first input of the operation
        /// \param sa is the Varnode indicating the \e shift \e amount for the operation
        /// \param existop is the first PcodeOp where the output whole needs to exist
        /// \param opc is the opcode of the particular shift operation
        public static void createShiftOp(Funcdata data, SplitVarnode @out, SplitVarnode @in,
            Varnode sa, PcodeOp existop, OpCode opc)
        {
            @out.findCreateOutputWhole(data);
            @@in.findCreateWhole(data);
            if (sa.isConstant())
                sa = data.newConstant(sa.getSize(), sa.getOffset());
            if (existop.code() != OpCode.CPUI_PIECE)
            { // If the output whole didn't previously exist
                PcodeOp* newop = data.newOp(2, existop.getAddr());
                data.opSetOpcode(newop, opc);
                data.opSetOutput(newop, @out.getWhole());
                data.opSetInput(newop, @@in.getWhole(), 0);
                data.opSetInput(newop, sa, 1);
                data.opInsertBefore(newop, existop);
                @out.buildLoFromWhole(data);
                @out.buildHiFromWhole(data);
            }
            else
            {           // The whole previously existed, we remake the defining op
                data.opSetOpcode(existop, opc);
                data.opSetInput(existop, @@in.getWhole(), 0);
                data.opSetInput(existop, sa, 1);
            }
        }

        /// \brief Rewrite a double precision boolean operation by replacing the input pieces with unified Varnodes
        ///
        /// This assumes we checked that the transformation is possible by calling the various verify and prepare
        /// methods. The inputs to the given PcodeOp producing the final boolean value are replaced with new
        /// logical Varnodes, and the opcode is updated.  The output Varnode is not affected.
        /// \param data is the function owning the operation
        /// \param boolop is the given PcodeOp producing the final boolean value
        /// \param in1 is the first input to the operation
        /// \param in2 is the second input to the operation
        /// \param opc is the opcode of the operation
        public static void replaceBoolOp(Funcdata data, PcodeOp boolop, SplitVarnode in1,
            SplitVarnode in2, OpCode opc)
        {
            in1.findCreateWhole(data);
            in2.findCreateWhole(data);
            data.opSetOpcode(boolop, opc);
            data.opSetInput(boolop, in1.getWhole(), 0);
            data.opSetInput(boolop, in2.getWhole(), 1);
        }

        /// \brief Make sure input operands of a double precision compare operation are compatible
        ///
        /// Do generic testing that the input whole Varnodes can be created.  If they can be created, return \b true.
        /// \param in1 is the first input operand of the double precision compare operation
        /// \param in2 is the second input operand of the double precision compare operation
        /// \return \b true if the logical transformation can be performed
        public static bool prepareBoolOp(SplitVarnode in1, SplitVarnode in2, PcodeOp testop)
        {
            if (!in1.isWholeFeasible(testop)) return false;
            if (!in2.isWholeFeasible(testop)) return false;
            return true;
        }

        /// \brief Create a new compare PcodeOp, replacing the boolean Varnode taken as input by the given CBRANCH
        ///
        /// The inputs to the new compare operation are Varnodes representing the logical whole of the double precision
        /// pieces.
        /// \param data is the function owning the operation
        /// \param cbranch is the given CBRANCH PcodeOp
        /// \param in1 is the first input to the compare operation
        /// \param in2 is the second input to the compare operation
        /// \param opc is the opcode of the compare operation
        public static void createBoolOp(Funcdata data, PcodeOp cbranch, SplitVarnode in1,
            SplitVarnode in2, OpCode opc)
        {
            PcodeOp addrop = cbranch;
            Varnode boolvn = cbranch.getIn(1);
            if (boolvn.isWritten())
                addrop = boolvn.getDef();  // Use the address of the comparison operator
            in1.findCreateWhole(data);
            in2.findCreateWhole(data);
            PcodeOp newop = data.newOp(2, addrop.getAddr());
            data.opSetOpcode(newop, opc);
            Varnode newbool = data.newUniqueOut(1, newop);
            data.opSetInput(newop, in1.getWhole(), 0);
            data.opSetInput(newop, in2.getWhole(), 1);
            data.opInsertBefore(newop, cbranch);
            data.opSetInput(cbranch, newbool, 1); // CBRANCH now determined by new compare
        }

        /// \brief Check that the logical version of a OpCode.CPUI_MULTIEQUAL operation can be created
        ///
        /// This checks only the most generic aspects of the calculation.  The input and output whole Varnodes
        /// must already exist or be creatable.  The point where the output Varnode must exist is identified
        /// and returned.  If the MULTIEQUAL operation cannot be created, null is returned.
        /// \param out is the output of the MULTIEQUAL operation
        /// \param inlist is a List of the input operands to the MULTIEQUAL
        /// \return the first PcodeOp where the output whole must exist
        public static PcodeOp preparePhiOp(SplitVarnode @out, List<SplitVarnode> inlist)
        {
            PcodeOp* existop = @out.findEarliestSplitPoint(); // Point where output whole needs to exist
            if (existop == (PcodeOp)null) return existop;
            // existop should always be a MULTIEQUAL defining one of the pieces
            if (existop.code() != OpCode.CPUI_MULTIEQUAL)
                throw new LowlevelError("Trying to create phi-node double precision op with phi-node pieces");
            BlockBasic* bl = existop.getParent();
            int numin = inlist.size();
            for (int i = 0; i < numin; ++i)
                if (!inlist[i].isWholePhiFeasible(bl.getIn(i)))
                    return (PcodeOp)null;
            return existop;
        }

        /// \brief Rewrite a double precision MULTIEQUAL operation by replacing the pieces with unified Varnodes
        ///
        /// This assumes that we have checked that the transformation is possible via the various
        /// verify and prepare methods.  After this method is called, the logical inputs and output of
        /// the calculation will exist as real Varnodes.
        /// \param data is the function owning the operation
        /// \param out is the output of the MULTIEQUAL operation
        /// \param inlist is the list of input operands to the MULTIEQUAL
        /// \param existop is the precalculated PcodeOp where the output whole Varnode must exist
        public static void createPhiOp(Funcdata data, out SplitVarnode @out,
            List<SplitVarnode> inlist, PcodeOp existop)
        {
            // Unlike replaceBoolOp, we MUST create a newop even if the output whole already exists
            // because the MULTIEQUAL has a lot of placement constraints on it
            @out.findCreateOutputWhole(data);
            int numin = inlist.size();
            for (int i = 0; i < numin; ++i)
                inlist[i].findCreateWhole(data);

            PcodeOp* newop = data.newOp(numin, existop.getAddr());
            data.opSetOpcode(newop, OpCode.CPUI_MULTIEQUAL);
            data.opSetOutput(newop, @out.getWhole());
            for (int i = 0; i < numin; ++i)
                data.opSetInput(newop, inlist[i].getWhole(), i);
            data.opInsertBefore(newop, existop);
            @out.buildLoFromWhole(data);
            @out.buildHiFromWhole(data);
        }

        /// \brief Check that the logical version of a OpCode.CPUI_INDIRECT operation can be created
        ///
        /// This checks only the most generic aspects of the calculation.  The input whole Varnode
        /// must already exist or be creatable.  If the INDIRECT operation cannot be created, \b false is returned.
        /// \param in is the (first) input operand of the INDIRECT
        /// \return \b true if the logical version of the OpCode.CPUI_INDIRECT can be created
        public static bool prepareIndirectOp(SplitVarnode @in, PcodeOp affector)
        {
            // We already have the exist point, -indop-
            return @@in.isWholeFeasible(affector);
        }

        /// \brief Rewrite a double precision INDIRECT operation by replacing the pieces with unified Varnodes
        ///
        /// This assumes that we have checked that the transformation is possible via the various
        /// verify and prepare methods.  After this method is called, the logical input and output of
        /// the calculation will exist as real Varnodes.
        /// \param data is the function owning the operation
        /// \param out is the output of the INDIRECT operation
        /// \param in is the (first) operand of the INDIRECT
        /// \param affector is the second operand to the indirect, the PcodeOp producing the indirect affect
        public static void replaceIndirectOp(Funcdata data, SplitVarnode @out, SplitVarnode @in,
            PcodeOp affector)
        {
            @out.createJoinedWhole(data);

            @@in.findCreateWhole(data);
            PcodeOp* newop = data.newOp(2, affector.getAddr());
            data.opSetOpcode(newop, OpCode.CPUI_INDIRECT);
            data.opSetOutput(newop, @out.getWhole());
            data.opSetInput(newop, @@in.getWhole(), 0);
            data.opSetInput(newop, data.newVarnodeIop(affector), 1);
            data.opInsertBefore(newop, affector);
            @out.buildLoFromWhole(data);
            @out.buildHiFromWhole(data);
        }

        /// \brief Try to perform one transform on a logical double precision operation given a specific input
        ///
        /// All the various double precision forms are lined up against the input.  The first one that matches
        /// has its associated transform performed and then 1 is returned.  If no form matches, 0 is returned.
        /// \param in is the given double precision input
        /// \param data is the function owning the Varnodes
        /// \return a count of the number of transforms applied, 0 or 1
        public static int applyRuleIn(SplitVarnode @in, Funcdata data)
        {
            for (int i = 0; i < 2; ++i)
            {
                Varnode* vn;
                vn = (i == 0) ? @in.getHi() : @in.getLo();
                if (vn == (Varnode)null) continue;
                bool workishi = (i == 0);
                list<PcodeOp*>::const_iterator iter, enditer;
                iter = vn.beginDescend();
                enditer = vn.endDescend();
                while (iter != enditer)
                {
                    PcodeOp* workop = *iter;
                    ++iter;
                    switch (workop.code())
                    {
                        case OpCode.CPUI_INT_ADD:
                            {
                                AddForm addform;
                                if (addform.applyRule(in, workop, workishi, data))
                                    return 1;
                                SubForm subform;
                                if (subform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_AND:
                            {
                                Equal3Form equal3form;
                                if (equal3form.applyRule(in, workop, workishi, data))
                                    return 1;
                                LogicalForm logicalform;
                                if (logicalform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_OR:
                            {
                                Equal2Form equal2form;
                                if (equal2form.applyRule(in, workop, workishi, data))
                                    return 1;
                                LogicalForm logicalform;
                                if (logicalform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_XOR:
                            {
                                Equal2Form equal2form;
                                if (equal2form.applyRule(in, workop, workishi, data))
                                    return 1;
                                LogicalForm logicalform;
                                if (logicalform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_EQUAL:
                        case OpCode.CPUI_INT_NOTEQUAL:
                            {
                                LessThreeWay lessthreeway;
                                if (lessthreeway.applyRule(in, workop, workishi, data))
                                    return 1;
                                Equal1Form equal1form;
                                if (equal1form.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_LESS:
                        case OpCode.CPUI_INT_LESSEQUAL:
                            {
                                LessThreeWay lessthreeway;
                                if (lessthreeway.applyRule(in, workop, workishi, data))
                                    return 1;
                                LessConstForm lessconstform;
                                if (lessconstform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_SLESS:
                            {
                                LessConstForm lessconstform;
                                if (lessconstform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_SLESSEQUAL:
                            {
                                LessConstForm lessconstform;
                                if (lessconstform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_LEFT:
                            {
                                ShiftForm shiftform;
                                if (shiftform.applyRuleLeft(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_RIGHT:
                            {
                                ShiftForm shiftform;
                                if (shiftform.applyRuleRight(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_SRIGHT:
                            {
                                ShiftForm shiftform;
                                if (shiftform.applyRuleRight(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INT_MULT:
                            {
                                MultForm multform;
                                if (multform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_MULTIEQUAL:
                            {
                                PhiForm phiform;
                                if (phiform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        case OpCode.CPUI_INDIRECT:
                            {
                                IndirectForm indform;
                                if (indform.applyRule(in, workop, workishi, data))
                                    return 1;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            return 0;
        }
    }
}
