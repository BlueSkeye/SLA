using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief The base class for a detailed definition of a user-defined p-code operation
    ///
    /// Within the raw p-code framework, the CALLOTHER opcode represents a user defined
    /// operation. At this level, the operation is just a placeholder for inputs and outputs
    /// to same black-box procedure. The first input parameter (index 0) must be a constant
    /// id associated with the particular procedure. Classes derived off of this base class
    /// provide a more specialized definition of an operation/procedure. The specialized classes
    /// are managed via UserOpManage and are associated with CALLOTHER ops via the constant id.
    ///
    /// The derived classes can in principle implement any functionality, tailored to the architecture
    /// or program. At this base level, the only commonality is a formal \b name of the operator and
    /// its CALLOTHER index.  A facility for reading in implementation details is provided via decode().
    internal abstract class UserPcodeOp
    {
        /// \brief Enumeration of different boolean properties that can be assigned to a CALLOTHER
        public enum userop_flags
        {
            /// Displayed as assignment, `in1 = in2`, where the first parameter is an annotation
            annotation_assignment = 1,
            /// Don't emit special token, just emit the first input parameter as expression
            no_operator = 2
        }

        /// Low-level name of p-code operator
        protected string name;
        /// Index passed in the CALLOTHER op
        protected int useropindex;
        /// Architecture owning the user defined op
        protected Architecture glb;
        /// Boolean attributes of the CALLOTHER
        protected uint flags;

        /// Construct from name and index
        public UserPcodeOp(Architecture g, string nm,int ind)
        {
            name = nm;
            useropindex = ind;
            glb = g;
            flags = 0;
        }

        /// Get the low-level name of the p-code op
        public string getName() => name;

        /// Get the constant id of the op
        public int getIndex() => useropindex;

        /// Get display type (0=functional)
        public uint getDisplay() => (flags & (annotation_assignment | no_operator));

        ~UserPcodeOp()
        {
        }

        /// \brief Get the symbol representing this operation in decompiled code
        ///
        /// This will return the symbol formally displayed in source code, which can be
        /// tailored more than the low-level name
        /// \param op is the operation (in context) where a symbol is needed
        /// \return the symbol as a string
        public virtual string getOperatorName(PcodeOp op) => name;

        /// \brief Assign a size to an annotation input to \b this userop
        ///
        /// Assuming an annotation refers to a special symbol accessed by \b this operation, retrieve the
        /// size (in bytes) of the symbol, which isn't ordinarily stored as part of the annotation.
        /// \param vn is the annotation Varnode
        /// \param op is the specific PcodeOp instance of \b this userop
        public virtual int extractAnnotationSize(Varnode vn, PcodeOp op)
        {
            throw new LowlevelError("Unexpected annotation input for CALLOTHER " + name);
        }

        /// \brief Restore the detailed description from a stream element
        ///
        /// The details of how a user defined operation behaves are parsed from the element.
        /// \param decoder is the stream decoder
        public abstract void decode(Decoder decoder);
    }
}
