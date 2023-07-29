using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A special form of ValueSet associated with the \e read \e point of a Varnode
    ///
    /// When a Varnode is read, it may have a more restricted range at the point of the read
    /// compared to the full scope. This class officially stores the value set at the point
    /// of the read (specified by PcodeOp and slot).  It is computed as a final step after
    /// the main iteration has completed.
    internal class ValueSetRead
    {
        // friend class ValueSetSolver;
        /// 0=pure constant 1=stack relative
        private int4 typeCode;
        /// The slot being read
        private int4 slot;
        /// The PcodeOp at the point of the value set read
        private PcodeOp op;
        /// Range of values or offsets in this set
        private CircleRange range;
        /// Constraint associated with the equation
        private CircleRange equationConstraint;
        /// Type code of the associated equation
        private int4 equationTypeCode;
        /// Set to \b true if left boundary of range didn't change (last iteration)
        private bool leftIsStable;
        /// Set to \b true if right boundary of range didn't change (last iteration)
        private bool rightIsStable;

        /// Establish \e read this value set corresponds to
        /// \param o is the PcodeOp reading the value set
        /// \param slt is the input slot the values are coming in from
        private void setPcodeOp(PcodeOp o, int4 slt)
        {
            typeCode = 0;
            op = o;
            slot = slt;
            equationTypeCode = -1;
        }

        /// Insert an equation restricting \b this value set
        /// \param slt is the given slot
        /// \param type is the constraint characteristic
        /// \param constraint is the given range
        private void addEquation(int4 slt, int4 type, CircleRange constraint)
        {
            if (slot == slt)
            {
                equationTypeCode = type;
                equationConstraint = constraint;
            }
        }

        /// Return '0' for normal constant, '1' for spacebase relative
        public int4 getTypeCode() => typeCode;

        /// Get the actual range of values
        public CircleRange getRange() => range;

        /// Return \b true if the left boundary hasn't been changing
        public bool isLeftStable() => leftIsStable;

        /// Return \b true if the right boundary hasn't been changing
        public bool isRightStable() => rightIsStable;

        /// Compute \b this value set
        /// This value set will be the same as the ValueSet of the Varnode being read but may
        /// be modified due to additional control-flow constraints
        public void compute()
        {
            Varnode* vn = op.getIn(slot);
            ValueSet* valueSet = vn.getValueSet();
            typeCode = valueSet.getTypeCode();
            range = valueSet.getRange();
            leftIsStable = valueSet.isLeftStable();
            rightIsStable = valueSet.isRightStable();
            if (typeCode == equationTypeCode)
            {
                if (0 != range.intersect(equationConstraint))
                {
                    range = equationConstraint;
                }
            }
        }

        /// Write a text description of \b to the given stream
        /// \param s is the stream to print to
        public void printRaw(TextWriter s)
        {
            s << "Read: " << get_opname(op.code());
            s << '(' << op.getSeqNum() << ')';
            if (typeCode == 0)
                s << " absolute ";
            else
                s << " stackptr ";
            range.printRaw(s);
        }
    }
}
