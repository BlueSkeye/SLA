using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Placeholder node for Varnode that will exist after a transform is applied to a function
    internal class TransformVar
    {
        /// \brief Types of replacement Varnodes
        public enum ReplaceType
        {
            /// New Varnode is a piece of an original Varnode
            piece = 1,
            /// Varnode preexisted in the original data-flow
            preexisting = 2,
            /// A new temporary (unique space) Varnode
            normal_temp = 3,
            /// A temporary representing a piece of an original Varnode
            piece_temp = 4,
            /// A new constant Varnode
            constant = 5,
            /// Special iop constant encoding a PcodeOp reference
            constant_iop = 6,
        }
        /// \brief Flags for a TransformVar
        [Flags()]
        public enum Flags
        {
            /// The last (most significant piece) of a split array
            split_terminator = 1,
            /// This is a piece of an input that has already been visited
            input_duplicate = 2
        }

        /// Original \b big Varnode of which \b this is a component
        internal Varnode vn;
        /// The new explicit lane Varnode
        internal Varnode replacement;
        /// Type of new Varnode
        internal ReplaceType type;
        /// Boolean properties of the placeholder
        internal Flags flags;
        /// Size of the lane Varnode in bytes
        private int byteSize;
        /// Size of the logical value in bits
        internal int bitSize;
        /// Value of constant or (bit) position within the original big Varnode
        internal ulong val;
        /// Defining op for new Varnode
        internal TransformOp def;

        /// Create the new/modified variable this placeholder represents
        /// Create the new/modified op this placeholder represents
        /// Create the Varnode object (constant, unique, List piece) described by the
        /// given placeholder. If the Varnode is an output, assume the op already exists
        /// and create the Varnode as an output. Set the \b replacement field with the
        /// new Varnode.
        /// \param fd is the function in which to create the replacement
        internal void createReplacement(Funcdata fd)
        {
            if (replacement != (Varnode)null)
                return;         // Replacement already created
            switch (type) {
                case ReplaceType.preexisting:
                    replacement = vn;
                    break;
                case ReplaceType.constant:
                    replacement = fd.newConstant(byteSize, val);
                    break;
                case ReplaceType.normal_temp:
                case ReplaceType.piece_temp:
                    if (def == (TransformOp)null)
                        replacement = fd.newUnique(byteSize);
                    else
                        replacement = fd.newUniqueOut(byteSize, def.replacement);
                    break;
                case ReplaceType.piece: {
                        int bytePos = (int)val;
                        if ((bytePos & 7) != 0)
                            throw new LowlevelError("Varnode piece is not byte aligned");
                        bytePos >>= 3;
                        if (vn.getSpace().isBigEndian())
                            bytePos = vn.getSize() - bytePos - byteSize;
                        Address addr = vn.getAddr() + bytePos;
                        addr.renormalize(byteSize);
                        if (def == (TransformOp)null)
                            replacement = fd.newVarnode(byteSize, addr);
                        else
                            replacement = fd.newVarnodeOut(byteSize, addr, def.replacement);
                        fd.transferVarnodeProperties(vn, replacement, bytePos);
                        break;
                    }
                case ReplaceType.constant_iop: {
                        PcodeOp indeffect = PcodeOp.getOpFromConst(new Address(fd.getArch().getIopSpace(), val));
                        replacement = fd.newVarnodeIop(indeffect);
                        break;
                    }
                default:
                    throw new LowlevelError("Bad TransformVar type");
            }
        }

        /// \brief Initialize \b this variable from raw data
        ///
        /// \param tp is the type of variable to create
        /// \param v is the underlying Varnode of which this is a piece (may be null)
        /// \param bits is the number of bits in the variable
        /// \param bytes is the number of bytes in the variable
        /// \param value is the associated value
        internal void initialize(ReplaceType tp, Varnode v, int bits, int bytes, ulong value)
        {
            type = tp;
            vn = v;
            val = value;
            bitSize = bits;
            byteSize = bytes;
            flags = 0;
            def = (TransformOp)null;
            replacement = (Varnode)null;
        }

        /// Get the original Varnode \b this placeholder models
        public Varnode getOriginal() => vn;

        /// Get the operator that defines this placeholder variable
        public TransformOp getDef() => def; 
    }
}
