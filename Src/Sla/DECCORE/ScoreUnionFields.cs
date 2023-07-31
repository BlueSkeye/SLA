using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.FuncCallSpecs;
using static System.Formats.Asn1.AsnWriter;

namespace Sla.DECCORE
{
    /// \brief Analyze data-flow to resolve which field of a union data-type is being accessed
    ///
    /// A Varnode with a data-type that is either a union, a pointer to union, or a part of a union, can
    /// be accessed in multiple ways.  Each individual read (or write) of the Varnode may be accessing either
    /// a specific field of the union or accessing the union as a whole.  The particular access may not be
    /// explicitly known but can sometimes be inferred from data-flow near the Varnode.  This class scores
    /// all the possible fields of a data-type involving a union for a specific Varnode.
    ///
    /// Because the answer may be different for different accesses, the Varnode must be specified as an
    /// access \e edge, a PcodeOp and a \b slot.  A slot >= 0 indicates the index of a Varnode that is being read
    /// by the PcodeOp, a slot == -1 indicates the output Varnode being written by the PcodeOp.
    ///
    /// The result of scoring is returned as a ResolvedUnion record.
    internal class ScoreUnionFields
    {
        /// \brief A trial data-type fitted to a specific place in the data-flow
        private class Trial
        {
            // friend class ScoreUnionFields;
            /// \brief An enumerator to distinguish how an individual trial follows data-flow
            internal enum dir_type
            {
                /// Only push the fit down \e with the data-flow
                fit_down,
                /// Only push the fit up \e against the data-flow
                fit_up
            }
            /// The Varnode we are testing for data-type fit
            private Varnode vn;
            /// The PcodeOp reading the Varnode (or null)
            private PcodeOp op;
            /// The slot reading the Varnode (or -1)
            private int inslot;
            /// Direction to push fit.  0=down 1=up
            private dir_type direction;
            /// Field can be accessed as an array
            private bool array;
            /// The putative data-type of the Varnode
            private Datatype fitType;
            /// The original field being scored by \b this trial
            private int scoreIndex;

            /// \brief Construct a downward trial for a Varnode
            ///
            /// \param o is the PcodeOp reading the Varnode
            /// \param slot is the input slot being read
            /// \param ct is the trial data-type to fit
            /// \param index is the scoring index
            /// \param isArray is \b true if the data-type to fit is a pointer to an array
            public Trial(PcodeOp o, int slot, Datatype ct, int index, bool isArray)
            {
                op = o;
                inslot = slot;
                direction = fit_down;
                fitType = ct;
                scoreIndex = index;
                vn = o.getIn(slot);
                array = isArray;
            }

            /// \brief Construct an upward trial for a Varnode
            ///
            /// \param v is the Varnode to fit
            /// \param ct is the trial data-type to fit
            /// \param index is the scoring index
            /// \param isArray is \b true if the data-type to fit is a pointer to an array
            public Trial(Varnode v, Datatype ct, int index, bool isArray)
            {
                vn = v;
                op = (PcodeOp)null;
                inslot = -1;
                direction = fit_up;
                fitType = ct;
                scoreIndex = index;
                array = isArray;
            }
        }

        /// \brief A mark accumulated when a given Varnode is visited with a specific field index
        private class VisitMark
        {
            /// Varnode reached by trial field
            private Varnode vn;
            /// Index of the trial field
            private int index;

            public VisitMark(Varnode v, int i)
            {
                vn = v;
                index = i;
            }

            /// \brief Compare two VisitMarks for use in a set container
            ///
            /// \param op2 is the other VisitMark being compared with \b this
            /// \return \b true if \b this should be ordered before \b op2
            public static bool operator <(VisitMark op1, VisitMark op2)
            {
                if (vn != op2.vn)
                    return (vn < op2.vn);
                return (index < op2.index);
            }
        }

        /// The factory containing data-types
        private TypeFactory typegrp;
        /// Score for each field, indexed by fieldNum + 1 (whole union is index=0)
        private List<int> scores;
        /// Field corresponding to each score
        private List<Datatype> fields;
        /// Places that have already been visited
        private set<VisitMark> visited;
        /// Current trials being pushed
        private List<Trial> trialCurrent;
        /// Next set of trials
        private List<Trial> trialNext;
        /// The best result
        private ResolvedUnion result;
        /// Number of trials evaluated so far
        private int trialCount;
        /// Maximum number of levels to score through
        private static const int maxPasses = 6;
        /// Threshold of trials over which to cancel additional passes
        private const int threshold = 256;
        /// Maximum number of trials to evaluate
        private static const int maxTrials = 1024;

        /// Check if given PcodeOp is operating on array with union elements
        /// If the \b op is adding a constant size or a multiple of a constant size to the given input slot, where the
        /// size is at least as large as the union, return \b true.
        /// \param op is the given PcodeOp
        /// \param inslot is given input slot
        /// \return \b true if \b op is doing array arithmetic with elements at least as large as the union
        private bool testArrayArithmetic(PcodeOp op, int inslot)
        {
            if (op.code() == OpCode.CPUI_INT_ADD)
            {
                Varnode* vn = op.getIn(1 - inslot);
                if (vn.isConstant())
                {
                    if (vn.getOffset() >= result.baseType.getSize())
                        return true;        // Array with union elements
                }
                else if (vn.isWritten())
                {
                    PcodeOp* multOp = vn.getDef();
                    if (multOp.code() == OpCode.CPUI_INT_MULT)
                    {
                        Varnode* vn2 = multOp.getIn(1);
                        if (vn2.isConstant() && vn2.getOffset() >= result.baseType.getSize())
                            return true;// Array with union elements
                    }
                }
            }
            else if (op.code() == OpCode.CPUI_PTRADD)
            {
                Varnode* vn = op.getIn(2);
                if (vn.getOffset() >= result.baseType.getSize())
                    return true;
            }
            return false;
        }

        /// Preliminary checks before doing full scoring
        /// Identify cases where we know the union shouldn't be resolved to a field.
        /// \param op is the PcodeOp manipulating the union variable
        /// \param inslot is -1 if the union is the output, >=0 if the union is an input to the op
        /// \param parent is the parent union or pointer to union
        /// \return \b true if the union should \e not be resolved to a field
        private bool testSimpleCases(PcodeOp op, int inslot, Datatype parent)
        {
            if (op.isMarker())
                return true;        // Propagate raw union across MULTIEQUAL and INDIRECT
            if (parent.getMetatype() == type_metatype.TYPE_PTR)
            {
                if (inslot < 0)
                    return true;        // Don't resolve pointers "up", there's only 1 possibility for assignment
                if (testArrayArithmetic(op, inslot))
                    return true;
            }
            if (op.code() != OpCode.CPUI_COPY)
                return false;       // A more complicated case
            if (inslot < 0)
                return false;       // Generally we don't want to propagate union backward thru COPY
            if (op.getOut().isTypeLock())
                return false;       // Do the full scoring
            return true;            // Assume we don't have to extract a field if copying
        }

        /// Score trial data-type against a locked data-type
        /// A trial that encounters a locked data-type does not propagate through it but scores
        /// the trial data-type against the locked data-type.
        /// \param ct is the trial data-type
        /// \param lockType is the locked data-type
        /// \return the score
        private int scoreLockedType(Datatype ct, Datatype lockType)
        {
            int score = 0;

            if (lockType == ct)
                score += 5;     // Perfect match

            while (ct.getMetatype() == type_metatype.TYPE_PTR)
            {
                if (lockType.getMetatype() != type_metatype.TYPE_PTR) break;
                score += 5;
                ct = ((TypePointer*)ct).getPtrTo();
                lockType = ((TypePointer*)lockType).getPtrTo();
            }

            type_metatype ctMeta = ct.getMetatype();
            type_metatype vnMeta = lockType.getMetatype();
            if (ctMeta == vnMeta)
            {
                if (ctMeta == type_metatype.TYPE_STRUCT || ctMeta == type_metatype.TYPE_UNION || ctMeta == type_metatype.TYPE_ARRAY || ctMeta == type_metatype.TYPE_CODE)
                    score += 10;
                else
                    score += 3;
            }
            else
            {
                if ((ctMeta == type_metatype.TYPE_INT && vnMeta == type_metatype.TYPE_UINT) || (ctMeta == type_metatype.TYPE_UINT && vnMeta == type_metatype.TYPE_INT))
                    score -= 1;
                else
                    score -= 5;
                if (ct.getSize() != lockType.getSize())
                    score -= 2;
            }
            return score;
        }

        /// Score trial data-type against a parameter
        /// Look up the call-specs for the given CALL.  If the inputs are locked, find the corresponding
        /// parameter and score the trial data-type against it.
        /// \param ct is the trial data-type
        /// \param callOp is the CALL
        /// \param paramSlot is the input slot of the trial data-type
        /// \return the score
        private int scoreParameter(Datatype ct, PcodeOp callOp, int paramSlot)
        {
            Funcdata fd = callOp.getParent().getFuncdata();

            FuncCallSpecs* fc = fd.getCallSpecs(callOp);
            if (fc != (FuncCallSpecs*)0 && fc.isInputLocked() && fc.numParams() > paramSlot)
            {
                return scoreLockedType(ct, fc.getParam(paramSlot).getType());
            }
            type_metatype meta = ct.getMetatype();
            if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE)
                return -1;      // Vaguely unlikely thing to pass as a param
            return 0;
        }

        /// Score trial data-type against return data-type of function
        /// Look up the call-specs for the given CALL.  If the output is locked,
        /// score the trial data-type against it.
        /// \param ct is the trial data-type
        /// \param callOp is the CALL
        /// \return the score
        private int scoreReturnType(Datatype ct, PcodeOp callOp)
        {
            Funcdata fd = callOp.getParent().getFuncdata();

            FuncCallSpecs* fc = fd.getCallSpecs(callOp);
            if (fc != (FuncCallSpecs*)0 && fc.isOutputLocked())
            {
                return scoreLockedType(ct, fc.getOutputType());
            }
            type_metatype meta = ct.getMetatype();
            if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE)
                return -1;      // Vaguely unlikely thing to return from a function
            return 0;
        }

        /// Score trial data-type as a pointer to LOAD/STORE
        /// Test if the data-type is a pointer and if the pointed-to data-type is
        /// compatible with the size of the value being loaded or stored. A \b score is
        /// passed back for how closely the data-type fits this scenario, and if it
        /// does we return the data-type of the pointer value.
        /// \param ct is the trial data-type
        /// \param vn is the Varnode holding the value being loaded or stored
        /// \param score is used to pass back the score
        /// \return the data-type of the value or null
        private Datatype derefPointer(Datatype ct, Varnode vn, int score)
        {
            Datatype* resType = (Datatype)null;
            score = 0;
            if (ct.getMetatype() == type_metatype.TYPE_PTR)
            {
                Datatype* ptrto = ((TypePointer*)ct).getPtrTo();
                while (ptrto != (Datatype)null && ptrto.getSize() > vn.getSize())
                {
                    ulong newoff;
                    ptrto = ptrto.getSubType(0, &newoff);
                }
                if (ptrto != (Datatype)null && ptrto.getSize() == vn.getSize())
                {
                    score = 10;
                    resType = ptrto;
                }
            }
            else
                score = -10;
            return resType;
        }

        /// Create new trials based an reads of given Varnode
        /// If the Varnode has already been visited, no new trials are created
        /// \param vn is the given Varnode
        /// \param ct is the data-type to associate with the trial
        /// \param scoreIndex is the field index to score the trial against
        /// \param isArray is \b true if the data-type to fit is a pointer to an array
        private void newTrialsDown(Varnode vn, Datatype ct, int scoreIndex, bool isArray)
        {
            VisitMark mark(vn, scoreIndex);
            if (!visited.insert(mark).second)
                return;             // Already visited this Varnode
            if (vn.isTypeLock())
            {
                scores[scoreIndex] += scoreLockedType(ct, vn.getType());
                return;             // Don't propagate through locked Varnode
            }
            list<PcodeOp*>::const_iterator piter;
            for (piter = vn.beginDescend(); piter != vn.endDescend(); ++piter)
            {
                PcodeOp* op = *piter;
                trialNext.emplace_back(op, op.getSlot(vn), ct, scoreIndex, isArray);
            }
        }

        /// Create new trials based on given input slot
        /// If the input slot is a Varnode that has already been visited, no new trial is created
        /// \param op is the PcodeOp with the given slot
        /// \param slot is the index of the given input slot
        /// \param ct is the data-type to associate with the trial
        /// \param scoreIndex is the field index to score the trial against
        /// \param isArray is \b true if the data-type to fit is a pointer to an array
        private void newTrials(PcodeOp op, int slot, Datatype ct, int scoreIndex, bool isArray)
        {
            Varnode* vn = op.getIn(slot);
            VisitMark mark(vn, scoreIndex);
            if (!visited.insert(mark).second)
                return;             // Already visited this Varnode
            if (vn.isTypeLock())
            {
                scores[scoreIndex] += scoreLockedType(ct, vn.getType());
                return;             // Don't propagate through locked Varnode
            }
            trialNext.emplace_back(vn, ct, scoreIndex, isArray);    // Try to fit up
            list<PcodeOp*>::const_iterator iter;
            for (iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
            {
                PcodeOp* readOp = *iter;
                int inslot = readOp.getSlot(vn);
                if (readOp == op && inslot == slot)
                    continue;           // Don't go down PcodeOp we came from
                trialNext.emplace_back(readOp, inslot, ct, scoreIndex, isArray);
            }
        }

        /// Try to fit the given trial following data-flow down
        /// The trial's data-type is fitted to its PcodeOp as the incoming Varnode and a
        /// score is computed and added to the score for the trial's union field.  The fitting may
        /// produce a new data-type which indicates scoring for the trial recurses into the output.
        /// This method builds trials for any new data-type unless \b lastLevel is \b true
        /// Varnode of its PcodeOp.
        /// \param trial is the given trial
        /// \param lastLevel is \b true if the method should skip building new trials
        private void scoreTrialDown(Trial trial, bool lastLevel)
        {
            if (trial.direction == Trial::fit_up)
                return;             // Trial doesn't push in this direction
            Datatype* resType = (Datatype)null;   // Assume by default we don't propagate
            type_metatype meta = trial.fitType.getMetatype();
            int score = 0;
            switch (trial.op.code())
            {
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_MULTIEQUAL:
                case OpCode.CPUI_INDIRECT:
                    resType = trial.fitType;        // No score, but we can propagate
                    break;
                case OpCode.CPUI_LOAD:
                    resType = derefPointer(trial.fitType, trial.op.getOut(), score);
                    break;
                case OpCode.CPUI_STORE:
                    if (trial.inslot == 1)
                    {
                        Datatype* ptrto = derefPointer(trial.fitType, trial.op.getIn(2), score);
                        if (ptrto != (Datatype)null)
                        {
                            if (!lastLevel)
                                newTrials(trial.op, 2, ptrto, trial.scoreIndex, trial.array);   // Propagate to value being STOREd
                        }
                    }
                    else if (trial.inslot == 2)
                    {
                        if (meta == type_metatype.TYPE_CODE)
                            score = -5;
                        else
                            score = 1;
                    }
                    break;
                case OpCode.CPUI_CBRANCH:
                    if (meta == type_metatype.TYPE_BOOL)
                        score = 10;
                    else
                        score = -10;
                    break;
                case OpCode.CPUI_BRANCHIND:
                    if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION ||
                    meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else
                        score = 1;
                    break;
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLOTHER:
                    if (trial.inslot > 0)
                        score = scoreParameter(trial.fitType, trial.op, trial.inslot - 1);
                    break;
                case OpCode.CPUI_CALLIND:
                    if (trial.inslot == 0)
                    {
                        if (meta == type_metatype.TYPE_PTR)
                        {
                            Datatype* ptrto = ((TypePointer*)trial.fitType).getPtrTo();
                            if (ptrto.getMetatype() == type_metatype.TYPE_CODE)
                            {
                                score = 10;
                            }
                            else
                            {
                                score = -10;
                            }
                        }
                    }
                    else
                    {
                        score = scoreParameter(trial.fitType, trial.op, trial.inslot - 1);
                    }
                    break;
                case OpCode.CPUI_RETURN:
                    // We could check for locked return data-type
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE)
                        score = -1;
                    break;
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_INT_NOTEQUAL:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -1;
                    else
                        score = 1;
                    break;
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_SCARRY:
                case OpCode.CPUI_INT_SBORROW:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_UNKNOWN || meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_BOOL)
                        score = -1;
                    else
                        score = 5;
                    break;
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_INT_CARRY:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_UNKNOWN || meta == type_metatype.TYPE_UINT)
                        score = 5;
                    else if (meta == type_metatype.TYPE_INT)
                        score = -5;
                    break;
                case OpCode.CPUI_INT_ZEXT:
                    if (meta == type_metatype.TYPE_UINT)
                        score = 2;
                    else if (meta == type_metatype.TYPE_INT || meta == type_metatype.TYPE_BOOL)
                        score = 1;
                    else if (meta == type_metatype.TYPE_UNKNOWN)
                        score = 0;
                    else    // struct,union,ptr,array,code,float
                        score = -5;
                    break;
                case OpCode.CPUI_INT_SEXT:
                    if (meta == type_metatype.TYPE_INT)
                        score = 2;
                    else if (meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_BOOL)
                        score = 1;
                    else if (meta == type_metatype.TYPE_UNKNOWN)
                        score = 0;
                    else    // struct,union,ptr,array,code,float
                        score = -5;
                    break;
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_SUB:
                case OpCode.CPUI_PTRSUB:
                    if (meta == type_metatype.TYPE_PTR)
                    {
                        if (trial.inslot >= 0)
                        {
                            Varnode* vn = trial.op.getIn(1 - trial.inslot);
                            if (vn.isConstant())
                            {
                                TypePointer* baseType = (TypePointer*)trial.fitType;
                                ulong off = vn.getOffset();
                                ulong parOff;
                                TypePointer* par;
                                resType = baseType.downChain(off, par, parOff, trial.array, typegrp);
                                if (resType != (Datatype)null)
                                    score = 5;
                            }
                            else
                            {
                                if (trial.array)
                                {
                                    score = 1;
                                    int elSize = 1;
                                    if (vn.isWritten())
                                    {
                                        PcodeOp* multOp = vn.getDef();
                                        if (multOp.code() == OpCode.CPUI_INT_MULT)
                                        {
                                            Varnode* multVn = multOp.getIn(1);
                                            if (multVn.isConstant())
                                                elSize = (int)multVn.getOffset();
                                        }
                                    }
                                    TypePointer* baseType = (TypePointer*)trial.fitType;
                                    if (baseType.getPtrTo().getSize() == elSize)
                                    {
                                        score = 5;
                                        resType = trial.fitType;
                                    }
                                }
                                else
                                    score = 5;  // Indexing into something that is not an array
                            }
                        }
                    }
                    else if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else
                        score = 1;
                    break;
                case OpCode.CPUI_INT_2COMP:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_UNKNOWN || meta == type_metatype.TYPE_BOOL)
                        score = -1;
                    else if (meta == type_metatype.TYPE_INT)
                        score = 5;
                    break;
                case OpCode.CPUI_INT_NEGATE:
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                case OpCode.CPUI_POPCOUNT:
                case OpCode.CPUI_LZCOUNT:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -1;
                    else if (meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                        score = 2;
                    break;
                case OpCode.CPUI_INT_LEFT:
                case OpCode.CPUI_INT_RIGHT:
                    if (trial.inslot == 0)
                    {
                        if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                            score = -5;
                        else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                            score = -1;
                        else if (meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                            score = 2;
                    }
                    else
                    {
                        if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE ||
                            meta == type_metatype.TYPE_FLOAT || meta == type_metatype.TYPE_PTR)
                            score = -5;
                        else
                            score = 1;
                    }
                    break;
                case OpCode.CPUI_INT_SRIGHT:
                    if (trial.inslot == 0)
                    {
                        if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                            score = -5;
                        else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL || meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                            score = -1;
                        else
                            score = 2;
                    }
                    else
                    {
                        if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE ||
                            meta == type_metatype.TYPE_FLOAT || meta == type_metatype.TYPE_PTR)
                            score = -5;
                        else
                            score = 1;
                    }
                    break;
                case OpCode.CPUI_INT_MULT:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -10;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -2;
                    else
                        score = 5;
                    break;
                case OpCode.CPUI_INT_DIV:
                case OpCode.CPUI_INT_REM:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -10;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -2;
                    else if (meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                        score = 5;
                    break;
                case OpCode.CPUI_INT_SDIV:
                case OpCode.CPUI_INT_SREM:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -10;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -2;
                    else if (meta == type_metatype.TYPE_INT)
                        score = 5;
                    break;
                case OpCode.CPUI_BOOL_NEGATE:
                case OpCode.CPUI_BOOL_AND:
                case OpCode.CPUI_BOOL_XOR:
                case OpCode.CPUI_BOOL_OR:
                    if (meta == type_metatype.TYPE_BOOL)
                        score = 10;
                    else if (meta == type_metatype.TYPE_INT || meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                        score = -1;
                    else
                        score = -10;
                    break;
                case OpCode.CPUI_FLOAT_EQUAL:
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                case OpCode.CPUI_FLOAT_LESS:
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                case OpCode.CPUI_FLOAT_NAN:
                case OpCode.CPUI_FLOAT_ADD:
                case OpCode.CPUI_FLOAT_DIV:
                case OpCode.CPUI_FLOAT_MULT:
                case OpCode.CPUI_FLOAT_SUB:
                case OpCode.CPUI_FLOAT_NEG:
                case OpCode.CPUI_FLOAT_ABS:
                case OpCode.CPUI_FLOAT_SQRT:
                case OpCode.CPUI_FLOAT_FLOAT2FLOAT:
                case OpCode.CPUI_FLOAT_TRUNC:
                case OpCode.CPUI_FLOAT_CEIL:
                case OpCode.CPUI_FLOAT_FLOOR:
                case OpCode.CPUI_FLOAT_ROUND:
                    if (meta == type_metatype.TYPE_FLOAT)
                        score = 10;
                    else
                        score = -10;
                    break;
                case OpCode.CPUI_FLOAT_INT2FLOAT:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -10;
                    else if (meta == type_metatype.TYPE_PTR)
                        score = -5;
                    else if (meta == type_metatype.TYPE_INT)
                        score = 5;
                    break;
                case OpCode.CPUI_PIECE:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    break;
                case OpCode.CPUI_SUBPIECE:
                    {
                        int offset = TypeOpSubpiece::computeByteOffsetForComposite(trial.op);
                        resType = scoreTruncation(trial.fitType, trial.op.getOut(), offset, trial.scoreIndex);
                        break;
                    }
                case OpCode.CPUI_PTRADD:
                    if (meta == type_metatype.TYPE_PTR)
                    {
                        if (trial.inslot == 0)
                        {
                            Datatype* ptrto = ((TypePointer*)trial.fitType).getPtrTo();
                            if (ptrto.getSize() == trial.op.getIn(2).getOffset())
                            {
                                score = 10;
                                resType = trial.fitType;
                            }
                        }
                        else
                        {
                            score = -10;
                        }
                    }
                    else if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else
                        score = 1;
                    break;
                case OpCode.CPUI_SEGMENTOP:
                    if (trial.inslot == 2)
                    {
                        if (meta == type_metatype.TYPE_PTR)
                            score = 5;
                        else if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                            score = -5;
                        else
                            score = -1;
                    }
                    else
                    {
                        if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT ||
                            meta == type_metatype.TYPE_PTR)
                            score = -2;
                    }
                    break;
                default:
                    score = -10;        // Doesn't fit
                    break;
            }
            scores[trial.scoreIndex] += score;
            if (resType != (Datatype)null && !lastLevel)
                newTrialsDown(trial.op.getOut(), resType, trial.scoreIndex, trial.array);
        }

        /// Try to fit the given trial following data-flow up
        private void scoreTrialUp(Trial trial, bool lastLevel)
        {
            if (trial.direction == Trial::fit_down)
                return;             // Trial doesn't push in this direction
            int score = 0;
            if (!trial.vn.isWritten())
            {
                if (trial.vn.isConstant())
                    scoreConstantFit(trial);
                return;     // Nothing to propagate up through
            }
            Datatype* resType = (Datatype)null;   // Assume by default we don't propagate
            int newslot = 0;
            type_metatype meta = trial.fitType.getMetatype();
            PcodeOp* def = trial.vn.getDef();
            switch (def.code())
            {
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_MULTIEQUAL:
                case OpCode.CPUI_INDIRECT:
                    resType = trial.fitType;        // No score, but we can propagate
                    newslot = 0;
                    break;
                case OpCode.CPUI_LOAD:
                    resType = typegrp.getTypePointer(def.getIn(1).getSize(), trial.fitType, 1);
                    newslot = 1;    // No score, but we can propagate
                    break;
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLOTHER:
                case OpCode.CPUI_CALLIND:
                    score = scoreReturnType(trial.fitType, def);
                    break;
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_INT_NOTEQUAL:
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_SCARRY:
                case OpCode.CPUI_INT_SBORROW:
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_INT_CARRY:
                case OpCode.CPUI_BOOL_NEGATE:
                case OpCode.CPUI_BOOL_AND:
                case OpCode.CPUI_BOOL_XOR:
                case OpCode.CPUI_BOOL_OR:
                case OpCode.CPUI_FLOAT_EQUAL:
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                case OpCode.CPUI_FLOAT_LESS:
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                case OpCode.CPUI_FLOAT_NAN:
                    if (meta == type_metatype.TYPE_BOOL)
                        score = 10;
                    else if (trial.fitType.getSize() == 1)
                        score = 1;
                    else
                        score = -10;
                    break;
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_SUB:
                case OpCode.CPUI_PTRSUB:
                    if (meta == type_metatype.TYPE_PTR)
                    {
                        score = 5;  // Don't try to back up further
                    }
                    else if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else
                        score = 1;
                    break;
                case OpCode.CPUI_INT_2COMP:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_UNKNOWN || meta == type_metatype.TYPE_BOOL)
                        score = -1;
                    else if (meta == type_metatype.TYPE_INT)
                        score = 5;
                    break;
                case OpCode.CPUI_INT_NEGATE:
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                case OpCode.CPUI_POPCOUNT:
                case OpCode.CPUI_LZCOUNT:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -1;
                    else if (meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                        score = 2;
                    break;
                case OpCode.CPUI_INT_LEFT:
                case OpCode.CPUI_INT_RIGHT:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -1;
                    else if (meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                        score = 2;
                    break;
                case OpCode.CPUI_INT_SRIGHT:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL || meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                        score = -1;
                    else
                        score = 2;
                    break;
                case OpCode.CPUI_INT_MULT:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -10;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -2;
                    else
                        score = 5;
                    break;
                case OpCode.CPUI_INT_DIV:
                case OpCode.CPUI_INT_REM:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -10;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -2;
                    else if (meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_UNKNOWN)
                        score = 5;
                    break;
                case OpCode.CPUI_INT_SDIV:
                case OpCode.CPUI_INT_SREM:
                    if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -10;
                    else if (meta == type_metatype.TYPE_PTR || meta == type_metatype.TYPE_BOOL)
                        score = -2;
                    else if (meta == type_metatype.TYPE_INT)
                        score = 5;
                    break;
                case OpCode.CPUI_FLOAT_ADD:
                case OpCode.CPUI_FLOAT_DIV:
                case OpCode.CPUI_FLOAT_MULT:
                case OpCode.CPUI_FLOAT_SUB:
                case OpCode.CPUI_FLOAT_NEG:
                case OpCode.CPUI_FLOAT_ABS:
                case OpCode.CPUI_FLOAT_SQRT:
                case OpCode.CPUI_FLOAT_FLOAT2FLOAT:
                case OpCode.CPUI_FLOAT_CEIL:
                case OpCode.CPUI_FLOAT_FLOOR:
                case OpCode.CPUI_FLOAT_ROUND:
                case OpCode.CPUI_FLOAT_INT2FLOAT:
                    if (meta == type_metatype.TYPE_FLOAT)
                        score = 10;
                    else
                        score = -10;
                    break;
                case OpCode.CPUI_FLOAT_TRUNC:
                    if (meta == type_metatype.TYPE_INT || meta == type_metatype.TYPE_UINT)
                        score = 2;
                    else
                        score = -2;
                    break;
                case OpCode.CPUI_PIECE:
                    if (meta == type_metatype.TYPE_FLOAT || meta == type_metatype.TYPE_BOOL)
                        score = -5;
                    else if (meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_PTR)
                        score = -2;
                    break;
                case OpCode.CPUI_SUBPIECE:
                    if (meta == type_metatype.TYPE_INT || meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_BOOL)
                    {
                        if (def.getIn(1).getOffset() == 0)
                            score = 3;      // Likely truncation
                        else
                            score = 1;
                    }
                    else
                        score = -5;
                    break;
                case OpCode.CPUI_PTRADD:
                    if (meta == type_metatype.TYPE_PTR)
                    {
                        Datatype* ptrto = ((TypePointer*)trial.fitType).getPtrTo();
                        if (ptrto.getSize() == def.getIn(2).getOffset())
                            score = 10;
                        else
                            score = 2;
                    }
                    else if (meta == type_metatype.TYPE_ARRAY || meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_UNION || meta == type_metatype.TYPE_CODE || meta == type_metatype.TYPE_FLOAT)
                        score = -5;
                    else
                        score = 1;
                    break;
                default:
                    score = -10;    // Datatype doesn't fit
                    break;
            }
            scores[trial.scoreIndex] += score;
            if (resType != (Datatype)null && !lastLevel)
            {
                newTrials(def, newslot, resType, trial.scoreIndex, trial.array);
            }
        }

        /// Score a truncation in the data-flow
        /// The truncation may be an explicit OpCode.CPUI_SUBPIECE, or it may be implied.
        /// A score is computed for fitting a given data-type to the truncation, and a possible
        /// data-type to recurse is also computed.
        /// \param ct is the given data-type to truncate
        /// \param vn is the Varnode the truncation will fit into
        /// \param offset is the number of bytes truncated off the start of the data-type
        /// \param scoreIndex is the field being scored
        /// \return the data-type to recurse or null
        private Datatype scoreTruncation(Datatype ct, Varnode vn, int offset, int scoreIndex)
        {
            int score;
            if (ct.getMetatype() == type_metatype.TYPE_UNION)
            {
                TypeUnion* unionDt = (TypeUnion*)ct;
                ct = (Datatype)null;          // Don't recurse a data-type from truncation of a union
                score = -10;            // Negative score if the union has no field matching the size
                int num = unionDt.numDepend();
                for (int i = 0; i < num; ++i)
                {
                    TypeField field = unionDt.getField(i);
                    if (field.offset == offset && field.type.getSize() == vn.getSize())
                    {
                        score = 10;
                        if (result.getBase() == unionDt)
                            score += 5;
                        break;
                    }
                }
            }
            else
            {
                ulong off = offset;
                score = 10;     // If we can find a size match for the truncation
                while (ct != (Datatype)null && (off != 0 || ct.getSize() != vn.getSize()))
                {
                    if (ct.getMetatype() == type_metatype.TYPE_INT || ct.getMetatype() == type_metatype.TYPE_UINT)
                    {
                        if (ct.getSize() >= vn.getSize() + off)
                        {
                            score = 1;  // Size doesn't match, but still possibly a reasonable operation
                            break;
                        }
                    }
                    ct = ct.getSubType(off, &off);
                }
                if (ct == (Datatype)null)
                    score = -10;
            }
            scores[scoreIndex] += score;
            return ct;
        }

        /// Score trial data-type against a constant
        /// Assume the constant has no data-type of its own to match against.
        /// Evaluate if the constant looks like an integer or pointer etc. and score the trial data-type against that.
        /// \param trial is the trial of the constant Varnode
        private void scoreConstantFit(Trial trial)
        {
            int size = trial.vn.getSize();
            ulong val = trial.vn.getOffset();
            type_metatype meta = trial.fitType.getMetatype();
            int score = 0;
            if (meta == type_metatype.TYPE_BOOL)
            {
                score = (size == 1 && val < 2) ? 2 : -2;
            }
            else if (meta == type_metatype.TYPE_FLOAT)
            {
                score = -1;
                FloatFormat format = typegrp.getArch().translate.getFloatFormat(size);
                if (format != (FloatFormat*)0) {
                    int exp = format.extractExponentCode(val);
                    if (exp < 7 && exp > -4)        // Check for common exponent range
                        score = 2;
                }
            }
            else if (meta == type_metatype.TYPE_INT || meta == type_metatype.TYPE_UINT || meta == type_metatype.TYPE_PTR)
            {
                if (val == 0)
                {
                    score = 2;  // Zero is equally valid as pointer or integer
                }
                else
                {
                    AddrSpace* spc = typegrp.getArch().getDefaultDataSpace();
                    bool looksLikePointer = false;
                    if (val >= spc.getPointerLowerBound() && val <= spc.getPointerUpperBound())
                    {
                        if (bit_transitions(val, size) >= 3)
                        {
                            looksLikePointer = true;
                        }
                    }
                    if (meta == type_metatype.TYPE_PTR)
                    {
                        score = looksLikePointer ? 2 : -2;
                    }
                    else
                    {
                        score = looksLikePointer ? 1 : 2;
                    }
                }
            }
            else
                score = -2;
            scores[trial.scoreIndex] += score;
        }

        /// Score all the current trials
        /// Run through each trial in the current list and compute a score.  If the trial recurses and this is
        /// \e not the final pass, build new trials for the recursion.
        /// \param lastPass is \b true if this is the last pass
        private void runOneLevel(bool lastPass)
        {
            list<Trial>::const_iterator iter;
            for (iter = trialCurrent.begin(); iter != trialCurrent.end(); ++iter)
            {
                trialCount += 1;
                if (trialCount > maxTrials)
                    return;             // Absolute number of trials reached
                Trial trial = *iter;
                scoreTrialDown(trial, lastPass);
                scoreTrialUp(trial, lastPass);
            }
        }

        /// Assuming scoring is complete, compute the best index
        private void computeBestIndex()
        {
            int bestScore = scores[0];
            int bestIndex = 0;
            for (int i = 1; i < scores.size(); ++i)
            {
                if (scores[i] > bestScore)
                {
                    bestScore = scores[i];
                    bestIndex = i;
                }
            }
            result.fieldNum = bestIndex - 1;    // Renormalize score index to field index
            result.resolve = fields[bestIndex];
        }

        /// Calculate best fitting field
        /// Try to fit each possible field over multiple levels of the data-flow.
        /// Return the index of the highest scoring field or -1 if the union data-type
        /// itself is the best fit.
        private void run()
        {
            trialCount = 0;
            for (int pass = 0; pass < maxPasses; ++pass)
            {
                if (trialCurrent.empty())
                    break;
                if (trialCount > threshold)
                    break;              // Threshold reached, don't score any more trials
                if (pass + 1 == maxPasses)
                    runOneLevel(true);
                else
                {
                    runOneLevel(false);
                    trialCurrent.swap(trialNext);
                    trialNext.clear();
                }
            }
        }

        /// \brief Score a given data-type involving a union against data-flow
        ///
        /// The data-type must either be a union or a pointer to union.
        /// Set up the initial set of trials based on the given data-flow edge (PcodeOp and slot).
        /// \param tgrp is the TypeFactory owning the data-types
        /// \param parentType is the given data-type to score
        /// \param op is PcodeOp of the given data-flow edge
        /// \param slot is slot of the given data-flow edge
        public ScoreUnionFields(TypeFactory tgrp, Datatype parentType, PcodeOp op, int slot)
        {
            typegrp = tgrp;
            result = new ResolvedUnion(parentType);
            if (testSimpleCases(op, slot, parentType))
                return;
            int wordSize = (parentType.getMetatype() == type_metatype.TYPE_PTR) ? ((TypePointer*)parentType).getWordSize() : 0;
            int numFields = result.baseType.numDepend();
            scores.resize(numFields + 1, 0);
            fields.resize(numFields + 1, (Datatype)null);
            Varnode* vn;
            if (slot < 0)
            {
                vn = op.getOut();
                if (vn.getSize() != parentType.getSize())
                    scores[0] -= 10;        // Data-type does not even match size of Varnode
                else
                    trialCurrent.emplace_back(vn, parentType, 0, false);
            }
            else
            {
                vn = op.getIn(slot);
                if (vn.getSize() != parentType.getSize())
                    scores[0] -= 10;
                else
                    trialCurrent.emplace_back(op, slot, parentType, 0, false);
            }
            fields[0] = parentType;
            visited.insert(VisitMark(vn, 0));
            for (int i = 0; i < numFields; ++i)
            {
                Datatype* fieldType = result.baseType.getDepend(i);
                bool isArray = false;
                if (wordSize != 0)
                {
                    if (fieldType.getMetatype() == type_metatype.TYPE_ARRAY)
                        isArray = true;
                    fieldType = typegrp.getTypePointerStripArray(parentType.getSize(), fieldType, wordSize);
                }
                if (vn.getSize() != fieldType.getSize())
                    scores[i + 1] -= 10;    // Data-type does not even match size of Varnode, don't create trial
                else if (slot < 0)
                {
                    trialCurrent.emplace_back(vn, fieldType, i + 1, isArray);
                }
                else
                {
                    trialCurrent.emplace_back(op, slot, fieldType, i + 1, isArray);
                }
                fields[i + 1] = fieldType;
                visited.insert(VisitMark(vn, i + 1));
            }
            run();
            computeBestIndex();
        }

        /// \brief Score a union data-type against data-flow, where there is a SUBPIECE
        ///
        /// A truncation is fit to each union field before doing the fit against data-flow.
        /// Only fields that match the offset and the truncation size (of the SUBPIECE) are scored further.
        /// If there is a good fit, the scoring for that field recurses into the given data-flow edge.
        /// This is only used where there is a SUBPIECE and the base scoring indicates the whole union is
        /// the best match for the input.
        /// \param tgrp is the TypeFactory owning the data-types
        /// \param unionType is the data-type to score, which must be a TypeUnion
        /// \param offset is the given starting offset of the truncation
        /// \param op is the SUBPIECE op
        public ScoreUnionFields(TypeFactory tgrp, TypeUnion unionType, int offset, PcodeOp op)
        {
            typegrp = tgrp;
            result = new ResolvedUnion(unionType);
            Varnode* vn = op.getOut();
            int numFields = unionType.numDepend();
            scores.resize(numFields + 1, 0);
            fields.resize(numFields + 1, (Datatype)null);
            fields[0] = unionType;
            scores[0] = -10;
            for (int i = 0; i < numFields; ++i)
            {
                TypeField unionField = unionType.getField(i);
                fields[i + 1] = unionField.type;
                if (unionField.type.getSize() != vn.getSize() || unionField.offset != offset)
                {
                    scores[i + 1] = -10;
                    continue;
                }
                newTrialsDown(vn, unionField.type, i + 1, false);
            }
            trialCurrent.swap(trialNext);
            if (trialCurrent.size() > 1)
                run();
            computeBestIndex();
        }

        /// \brief Score a union data-type against data-flow, where there is an implied truncation
        ///
        /// A truncation is fit to each union field before doing the fit against data-flow, starting with
        /// the given PcodeOp and input slot.
        /// \param tgrp is the TypeFactory owning the data-types
        /// \param unionType is the data-type to score, which must be a TypeUnion
        /// \param offset is the given starting offset of the truncation
        /// \param op is the PcodeOp initially reading/writing the union
        /// \param slot is the -1 if the op is writing, >= 0 if reading
        public ScoreUnionFields(TypeFactory tgrp, TypeUnion unionType, int offset, PcodeOp op, int slot)
        {
            typegrp = tgrp;
            result = new ResolvedUnion(unionType);
            Varnode* vn = (slot < 0) ? op.getOut() : op.getIn(slot);
            int numFields = unionType.numDepend();
            scores.resize(numFields + 1, 0);
            fields.resize(numFields + 1, (Datatype)null);
            fields[0] = unionType;
            scores[0] = -10;        // Assume the untruncated entire union is not a good fit
            for (int i = 0; i < numFields; ++i)
            {
                TypeField unionField = unionType.getField(i);
                fields[i + 1] = unionField.type;
                // Score the implied truncation
                Datatype* ct = scoreTruncation(unionField.type, vn, offset - unionField.offset, i + 1);
                if (ct != (Datatype)null)
                {
                    if (slot < 0)
                        trialCurrent.emplace_back(vn, ct, i + 1, false);        // Try to flow backward
                    else
                        trialCurrent.emplace_back(op, slot, ct, i + 1, false);  // Flow downward
                    visited.insert(VisitMark(vn, i + 1));
                }
            }
            if (trialCurrent.size() > 1)
                run();
            computeBestIndex();
        }

        /// Get the resulting best field resolution
        public ResolvedUnion getResult() => result;
    }
}
