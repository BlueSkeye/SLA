using Sla.SLEIGH;

namespace Sla.SLACOMP
{
    /// \brief Subtable, pattern, and context information applied across a \b with block
    ///
    /// The header of a \b with block is applied to all constructors in the block. It
    /// attaches each constructor to a specific subtable. A pattern expression and/or a
    /// a series of context changes is attached to each constructor as well.
    internal class WithBlock
    {
        private SubtableSymbol ss;         ///< Subtable containing each Constructor (or null for root table)
        private PatternEquation? pateq;     ///< Pattern to prepend to each Constructor (or null)
        private List<ContextChange> contvec; ///< Context change to associate with each constructor (or null)
        
        public WithBlock()
        {
            pateq = (PatternEquation)null;
        }

        ///< Set components of the header
        /// Establish each component of the \b with block header
        /// \param s is the subtable (or null)
        /// \param pq is the pattern to prepend (or null)
        /// \param cvec is the set of context changes (or null)
        public void set(SubtableSymbol s, PatternEquation pq, List<ContextChange> cvec)
        {
            ss = s;
            pateq = pq;
            if (pateq != (PatternEquation)null)
                pateq.layClaim();
            if (cvec != (List<ContextChange>)null) {
                for (int i = 0; i < cvec.size(); ++i)
                    contvec.Add(cvec[i]);  // Lay claim to -cvec-s pointers, we don't clone
                // delete cvec;
            }
        }

        ~WithBlock()
        {
            if (pateq != (PatternEquation)null)
                PatternEquation.release(pateq);
            //for (int i = 0; i < contvec.size(); ++i) {
            //    delete contvec[i];
            //}
        }

        /// \brief Build a complete pattern equation from any surrounding \b with blocks
        ///
        /// Given the pattern equation parsed locally from a Constructor and the stack of
        /// surrounding \b with blocks, create the final pattern equation for the Constructor.
        /// Each \b with block pattern is preprended to the local pattern.
        /// \param stack is the stack of \b with blocks currently active at the Constructor
        /// \param pateq is the pattern equation parsed from the local Constructor statement
        /// \return the final pattern equation
        public static PatternEquation collectAndPrependPattern(List<WithBlock> stack, PatternEquation pateq)
        {
            foreach (WithBlock block in stack) {
                PatternEquation? witheq = block.pateq;
                if (witheq != (PatternEquation)null)
                    pateq = new EquationAnd(witheq, pateq);
            }
            return pateq;
        }

        /// \brief Build a complete array of context changes from any surrounding \b with blocks
        ///
        /// Given a list of ContextChanges parsed locally from a Constructor and the stack of
        /// surrounding \b with blocks, make a new list of ContextChanges, prepending everything from
        /// the stack to the local List.  Return the new list and delete the old.
        /// \param stack is the current \b with block stack
        /// \param contvec is the local list of ContextChanges (or null)
        /// \return the new list of ContextChanges
        public static List<ContextChange> collectAndPrependContext(List<WithBlock> stack, List<ContextChange> contvec)
        {
            List<ContextChange>? res = (List<ContextChange>)null;
            foreach (WithBlock block in stack) {
                List<ContextChange> changelist = block.contvec;
                if (changelist.size() == 0) continue;
                if (res == (List<ContextChange>)null)
                    res = new List<ContextChange>();
                for (int i = 0; i < changelist.size(); ++i) {
                    res.Add(changelist[i].clone());
                }
            }
            if (contvec != (List<ContextChange>)null) {
                if (contvec.size() != 0) {
                    if (res == (List<ContextChange>)null)
                        res = new List<ContextChange>();
                    for (int i = 0; i < contvec.size(); ++i)
                        res.Add(contvec[i]);      // lay claim to contvecs pointer
                }
                // delete contvec;
            }
            return res;
        }

        /// \brief Get the active subtable from the stack of currently active \b with blocks
        ///
        /// Find the subtable associated with the innermost \b with block and return it.
        /// \param stack is the stack of currently active \b with blocks
        /// \return the innermost subtable (or null)
        public static SubtableSymbol? getCurrentSubtable(List<WithBlock> stack)
        {
            foreach (WithBlock block in stack) {
                if (block.ss != (SubtableSymbol)null)
                    return block.ss;
            }
            return (SubtableSymbol)null;
        }
    }
}
