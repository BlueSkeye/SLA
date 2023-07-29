using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

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
        private PatternEquation pateq;     ///< Pattern to prepend to each Constructor (or null)
        private List<ContextChange> contvec; ///< Context change to associate with each constructor (or null)
        
        public WithBlock()
        {
            pateq = (PatternEquation*)0;
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
            if (pateq != (PatternEquation*)0)
                pateq.layClaim();
            if (cvec != (List<ContextChange*>*)0)
            {
                for (int4 i = 0; i < cvec.size(); ++i)
                    contvec.push_back((*cvec)[i]);  // Lay claim to -cvec-s pointers, we don't clone
                delete cvec;
            }
        }

        ~WithBlock()
        {
            if (pateq != (PatternEquation*)0)
                PatternEquation::release(pateq);
            for (int4 i = 0; i < contvec.size(); ++i)
            {
                delete contvec[i];
            }
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
            list<WithBlock>::const_iterator iter;
            for (iter = stack.begin(); iter != stack.end(); ++iter)
            {
                PatternEquation* witheq = (*iter).pateq;
                if (witheq != (PatternEquation*)0)
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
            List<ContextChange*>* res = (List<ContextChange*>*)0;
            list<WithBlock>::const_iterator iter;
            for (iter = stack.begin(); iter != stack.end(); ++iter)
            {
                List<ContextChange> changelist = (*iter).contvec;
                if (changelist.size() == 0) continue;
                if (res == (List<ContextChange*>*)0)
                    res = new List<ContextChange*>();
                for (int4 i = 0; i < changelist.size(); ++i)
                {
                    res.push_back(changelist[i].clone());
                }
            }
            if (contvec != (List<ContextChange*>*)0)
            {
                if (contvec.size() != 0)
                {
                    if (res == (List<ContextChange*>*)0)
                        res = new List<ContextChange*>();
                    for (int4 i = 0; i < contvec.size(); ++i)
                        res.push_back((*contvec)[i]);      // lay claim to contvecs pointer
                }
                delete contvec;
            }
            return res;
        }

        /// \brief Get the active subtable from the stack of currently active \b with blocks
        ///
        /// Find the subtable associated with the innermost \b with block and return it.
        /// \param stack is the stack of currently active \b with blocks
        /// \return the innermost subtable (or null)
        public static SubtableSymbol getCurrentSubtable(List<WithBlock> stack)
        {
            list<WithBlock>::const_iterator iter;
            for (iter = stack.begin(); iter != stack.end(); ++iter)
            {
                if ((*iter).ss != (SubtableSymbol*)0)
                    return (*iter).ss;
            }
            return (SubtableSymbol*)0;
        }
    }
}
