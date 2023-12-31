﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A class for sorting comments into and within basic blocks
    /// The decompiler endeavors to display comments within the flow of the
    /// source code statements it generates. Comments should be placed at or near
    /// the statement that encompasses the address of the original instruction
    /// to which the comment is attached. This is complicated by the fact that
    /// instructions may get removed and transformed during decompilation and even whole
    /// basic blocks may get removed.
    /// This class sorts comments into the basic block that contains
    /// it. As statements are emitted, comments can get picked up, in the correct order,
    /// even if there is no longer a specific p-code operation at the comment's address.
    /// The decompiler maintains information about basic blocks that have been entirely
    /// removed, in which case, the user can elect to not display the corresponding comments.
    /// This class also acts as state for walking comments within a specific basic block or
    /// within the header.
    internal class CommentSorter
    {
        public enum HeaderCommentFlag: uint
        {
            /// Basic header comments
            header_basic = 0,
            /// Comment that can't be placed in code flow
            header_unplaced = 1
        }

        /// \brief The sorting key for placing a Comment within a specific basic block
        private class Subsort : IComparable<Subsort>
        {
            // Either the basic block index or -1 for a function header
            internal int index;
            // The order index within the basic block
            internal HeaderCommentFlag order;
            // A final count to guarantee a unique sorting
            internal uint pos;

            /// \brief Compare comments based on basic block, then position within the block
            /// \param op2 is the other key to compare with \b this
            /// \return \b true if \b this gets ordered before the other key
            public static bool operator <(Subsort op1, Subsort op2)
            {
                if (op1.index == op2.index) {
                    return (op1.order == op2.order)
                        ? (op1.pos < op2.pos)
                        : (op1.order < op2.order);
                }
                return (op1.index < op2.index);
            }

            public static bool operator >(Subsort op1, Subsort op2)
            {
                if (op1.index == op2.index) {
                    return (op1.order == op2.order)
                        ? (op1.pos > op2.pos)
                        : (op1.order > op2.order);
                }
                return (op1.index > op2.index);
            }

            /// \brief Initialize a key for a header comment
            /// \param headerType can be either \b header_basic or \b header_unplaced
            public void setHeader(CommentSorter.HeaderCommentFlag headerType)
            {
                // -1 indicates a header comment
                index = -1;
                order = headerType;
            }

            /// \brief Initialize a key for a basic block position
            /// \param i is the index of the basic block
            /// \param ord is the position within the block
            public void setBlock(int i, HeaderCommentFlag ord)
            {
                index = i;
                order = ord;
            }

            public int CompareTo(Subsort? other)
            {
                if (other == null) throw new ArgumentNullException();
                if (this > other) {
                    return 1;
                }
                if (this < other) {
                    return -1;
                }
                return 0;
            }

            internal class Comparer : IComparer<Subsort>
            {
                internal static Comparer Instance = new Comparer();

                private Comparer() { }

                /// \brief Compare comments based on basic block, then position within the block
                /// \param op2 is the other key to compare with \b this
                /// \return \b true if \b this gets ordered before the other key
                public int Compare(Subsort? x, Subsort? y)
                {
                    if (null == x) throw new ArgumentNullException();
                    if (null == y) throw new ArgumentNullException();
                    if (x.index == y.index) {
                        return (x.order == y.order)
                            ? (x.pos.CompareTo(y.pos))
                            : (x.order.CompareTo(y.order));
                    }
                    return x.index.CompareTo(y.index);
                }
            }
        }

        /// Comments for the current function, sorted by block
        private SortedList<Subsort, Comment> commmap =
            new SortedList<Subsort, Comment>(Subsort.Comparer.Instance);

        // Iterator to current comment being walked
        private /*mutable*/ int /*Dictionary<Subsort, Comment >.Enumerator*/ start;

        // Last comment in current set being walked
        private int /*Dictionary<Subsort, Comment>.Enumerator*/ stop;

        // Statement landmark within current set of comments
        private int /*Dictionary<Subsort, Comment>.Enumerator*/ opstop;

        // True if unplaced comments should be displayed (in the header)
        private bool displayUnplacedComments;

        // Establish sorting key for a Comment
        /// Figure out position of given Comment and initialize its key.
        /// \param subsort is a reference to the key to be initialized
        /// \param comm is the given Comment
        /// \param fd is the function owning the Comment
        /// \return \b true if the Comment could be positioned at all
        private bool findPosition(Subsort subsort, Comment comm, Funcdata fd)
        {
            if (comm.getType() == 0) {
                return false;
            }
            Address fad = fd.getAddress();
            if (   (0 != (comm.getType() & (Comment.comment_type.header | Comment.comment_type.warningheader)))
                && (comm.getAddr() == fad))
            {
                // If it is a header comment at the address associated with the beginning of the function
                subsort.setHeader(CommentSorter.HeaderCommentFlag.header_basic);
                return true;
            }

            // Try to find block containing comment
            // Find op at lowest address greater or equal to comment's address
            PcodeOpTree.Enumerator opiter = fd.beginOp(comm.getAddr());
            PcodeOp? backupOp = null;
            if (opiter != fd.endOpAll()) {
                // If there is an op at or after the comment
                PcodeOp op = opiter.Current.Value;
                BlockBasic? block = op.getParent();
                if (block == null) {
                    throw new LowlevelError("Dead op reaching CommentSorter");
                }
                if (block.contains(comm.getAddr())) {
                    // If the op's block contains the address
                    // Associate comment with this op
                    subsort.setBlock(block.getIndex(), (HeaderCommentFlag)op.getSeqNum().getOrder());
                    return true;
                }
                if (comm.getAddr() == op.getAddr()) {
                    backupOp = op;
                }
            }
            if (opiter != fd.beginOpAll()) {
                // If there is a previous op
                --opiter;
                PcodeOp op = opiter.Current.Value;
                BlockBasic? block = op.getParent();
                if (block == null) {
                    throw new LowlevelError("Dead op reaching CommentSorter");
                }
                if (block.contains(comm.getAddr())) {
                    // If the op's block contains the address
                    // Treat the comment as being in this block at the very end
                    subsort.setBlock(block.getIndex(), (HeaderCommentFlag)0xffffffff);
                    return true;
                }
            }
            if (backupOp != null) {
                // Its possible the op migrated from its original basic block.
                // Since the address matches exactly, hang the comment on it
                subsort.setBlock(backupOp.getParent().getIndex(),
                    (HeaderCommentFlag)backupOp.getSeqNum().getOrder());
                return true;
            }
            if (fd.beginOpAll() == fd.endOpAll()) {
                // If there are no ops at all
                // Put comment at the beginning of the first block
                subsort.setBlock(0, 0);
                return true;
            }
            if (displayUnplacedComments) {
                subsort.setHeader(HeaderCommentFlag.header_unplaced);
                return true;
            }
            // Basic block containing comment has been excised
            return false;
        }

        /// Constructor
        public CommentSorter()
        {
            displayUnplacedComments = false;
        }

        /// \brief Collect and sort comments specific to the given function.
        /// Only keep comments matching one of a specific set of properties
        /// \param tp is the set of properties (may be zero)
        /// \param fd is the given function
        /// \param db is the container of comments to collect from
        /// \param displayUnplaced is \b true if unplaced comments should be displayed in the header
        public void setupFunctionList(Comment.comment_type tp, Funcdata fd, CommentDatabase db,
            bool displayUnplaced)
        {
            commmap.Clear();
            displayUnplacedComments = displayUnplaced;
            if (tp == 0) return;
            Address fad = fd.getAddress();
            IEnumerator<Comment> iter = db.beginComment(fad);
            IEnumerator<Comment> lastiter = db.endComment(fad);
            Subsort subsort = new Subsort() {
                pos = 0
            };
            while (iter.Current != lastiter.Current) {
                Comment comm = iter.Current;
                if (findPosition(subsort, comm, fd)) {
                    comm.setEmitted(false);
                    commmap[subsort] = comm;
                    // Advance the uniqueness counter
                    subsort.pos += 1;
                }
                if (!iter.MoveNext()) {
                    break;
                }
            }
        }

        /// Prepare to walk comments from a single basic block
        /// Find iterators that bound everything in the basic block
        /// \param bl is the basic block
        public void setupBlockList(FlowBlock bl)
        {
            Subsort subsort = new Subsort() {
                index = bl.getIndex(),
                order = 0,
                pos = 0
            };
            start = commmap.lower_bound(subsort);
            unchecked { subsort.order = (HeaderCommentFlag)0xffffffff; }
            subsort.pos = 0xffffffff;
            stop = commmap.upper_bound(subsort);
        }

        /// Establish a p-code landmark within the current set of comments
        /// This will generally get called with the root p-code op of a statement
        /// being emitted by the decompiler. This establishes a key value within the
        /// basic block, so it is known where to stop emitting comments within the
        /// block for emitting the statement.
        /// \param op is the p-code representing the root of a statement
        public void setupOpList(PcodeOp? op)
        {
            if (op == null) {
                // If NULL op
                // pick up any remaining comments in this basic block
                opstop = stop;
                return;
            }
            Subsort subsort = new Subsort() {
                index = op.getParent().getIndex(),
                order = (HeaderCommentFlag)op.getSeqNum().getOrder(),
                pos = 0xffffffff
            };
            opstop = commmap.upper_bound(subsort);
        }

        /// Prepare to walk comments in the header
        /// Header comments are grouped together. Set up iterators.
        /// \param headerType selects either \b header_basic or \b header_unplaced comments
        public void setupHeader(HeaderCommentFlag headerType)
        {
            Subsort subsort = new Subsort() {
                index = -1,
                order = headerType,
                pos = 0
            };
            start = commmap.lower_bound(subsort);
            subsort.pos = 0xffffffff;
            opstop = commmap.upper_bound(subsort);
        }

        /// Return \b true if there are more comments to emit in the current set
        public bool hasNext()
        {
            return (start < opstop);
        }

        /// Advance to the next comment
        public Comment getNext()
        {
            if (start >= commmap.Count) throw new InvalidOperationException();
            Comment res = commmap.ElementAt(start++).Value;
            return res;
        }
    }
}
