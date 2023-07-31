using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Placeholder node for PcodeOp that will exist after a transform is applied to a function
    internal class TransformOp
    {
        /// Special annotations on new pcode ops
        [Flags()]
        public enum Annotation
        {
            /// Op replaces an existing op
            op_replacement = 1,
            /// Op already exists (but will be transformed)
            op_preexisting = 2,
            /// Mark op as indirect creation
            indirect_creation = 4,
            /// Mark op as indirect creation and possible call output
            indirect_creation_possible_out = 8
        }

        /// Original op which \b this is splitting (or null)
        private PcodeOp op;
        /// The new replacement op
        private PcodeOp replacement;
        /// Opcode of the new op
        private OpCode opc;
        /// Special handling code when creating
        private uint special;
        /// Varnode output
        private TransformVar output;
        /// Varnode inputs
        private List<TransformVar> input;
        /// The following op after \b this (if not null)
        private TransformOp follow;

        /// Create a new PcodeOp or modify an existing one so that it matches this placeholder description.
        /// Go ahead an insert the new PcodeOp into the basic block if possible
        /// \param fd is the function in which to make the modifications
        private void createReplacement(Funcdata fd)
        {
            if ((special & TransformOp::op_preexisting) != 0)
            {
                replacement = op;
                fd.opSetOpcode(op, opc);
                while (input.size() < op.numInput())
                    fd.opRemoveInput(op, op.numInput() - 1);
                for (int i = 0; i < op.numInput(); ++i)
                    fd.opUnsetInput(op, i);            // Clear any remaining inputs
                while (op.numInput() < input.size())
                    fd.opInsertInput(op, (Varnode)null, op.numInput() - 1);
            }
            else
            {
                replacement = fd.newOp(input.size(), op.getAddr());
                fd.opSetOpcode(replacement, opc);
                if (output != (TransformVar*)0)
                    output.createReplacement(fd);
                if (follow == (TransformOp*)0)
                {       // Can be inserted immediately
                    if (opc == OpCode.CPUI_MULTIEQUAL)
                        fd.opInsertBegin(replacement, op.getParent());
                    else
                        fd.opInsertBefore(replacement, op);
                }
            }
        }

        /// Try to put the new PcodeOp into its basic block
        /// \param fd is the function into which the PcodeOp will be inserted
        /// \return \b true if the op is successfully inserted or already inserted
        private bool attemptInsertion(Funcdata fd)
        {
            if (follow != (TransformOp*)0)
            {
                if (follow.follow == (TransformOp*)0)
                {   // Check if the follow is inserted
                    if (opc == OpCode.CPUI_MULTIEQUAL)
                        fd.opInsertBegin(replacement, follow.replacement.getParent());
                    else
                        fd.opInsertBefore(replacement, follow.replacement);
                    follow = (TransformOp*)0;   // Mark that this has been inserted
                    return true;
                }
                return false;
            }
            return true;        // Already inserted
        }

        /// Get the output placeholder variable for \b this operator
        public TransformVar getOut() => output;

        /// Get the i-th input placeholder variable for \b this
        public TransformVar getIn(int i) => input[i];
    }
}
