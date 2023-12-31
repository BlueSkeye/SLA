﻿using Sla.CORE;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A range of values attached to a Varnode within a data-flow subsystem
    ///
    /// This class acts as both the set of values for the Varnode and as a node in a
    /// sub-graph overlaying the full data-flow of the function containing the Varnode.
    /// The values are stored in the CircleRange field and can be interpreted either as
    /// absolute values (if \b typeCode is 0) or as values relative to a stack pointer
    /// or some other register (if \b typeCode is non-zero).
    internal class ValueSet
    {
        ///< Maximum step inferred for a value set
        public const int MAX_STEP = 32;

        /// \brief An external that can be applied to a ValueSet
        /// An Equation is attached to a particular ValueSet and its underlying Varnode
        /// providing additional restriction on the ValueSet of an input parameter of the
        /// operation producing the Varnode.
        internal class Equation
        {
            // friend class ValueSet;
            /// The input parameter slot to which the constraint is attached
            internal int slot;
            /// The constraint characteristic 0=absolute 1=relative to a spacebase register
            internal int typeCode;
            /// The range constraint
            internal CircleRange range;

            public Equation(int s, int tc, CircleRange rng)
            {
                slot = s;
                typeCode = tc;
                range = rng;
            }
        }

        // friend class ValueSetSolver;
        /// 0=pure constant 1=stack relative
        internal int typeCode;
        /// Number of input parameters to defining operation
        private int numParams;
        /// Depth first numbering / widening count
        internal int count;
        /// Op-code defining Varnode
        internal OpCode opCode;
        /// Set to \b true if left boundary of range didn't change (last iteration)
        private bool leftIsStable;
        /// Set to \b true if right boundary of range didn't change (last iteration)
        private bool rightIsStable;
        /// Varnode whose set this represents
        internal Varnode vn;
        /// Range of values or offsets in this set
        private CircleRange range;
        /// Any equations associated with this value set
        private List<Equation> equations;
        /// If Varnode is a component head, pointer to corresponding Partition
        internal Partition partHead;
        /// Next ValueSet to iterate
        internal ValueSet next;

        /// Does the indicated equation apply for the given input slot
        /// Perform basic checks that the selected Equation exists and applies
        /// to the indicated input slot.
        /// \param num is the index selecting an Equation
        /// \param slot is the indicated slot
        /// \return \b true if the Equation exists and applies
        private bool doesEquationApply(int num, int slot)
        {
            if (num < equations.size()) {
                if (equations[num].slot == slot) {
                    if (equations[num].typeCode == typeCode)
                        return true;
                }
            }
            return false;
        }

        /// Mark value set as possibly containing any value
        internal void setFull()
        {
            range.setFull(vn.getSize());
            typeCode = 0;
        }

        /// Attach \b this to given Varnode and set initial values
        /// The initial values in \b this are set based on the type of Varnode:
        ///   - Constant gets the single value
        ///   - Input gets all possible values
        ///   - Other Varnodes that are written start with an empty set
        ///
        /// \param v is the given Varnode to attach to
        /// \param tCode indicates whether to treat values as constants are relative offsets
        internal void setVarnode(Varnode v, int tCode)
        {
            typeCode = tCode;
            vn = v;
            vn.setValueSet(this);
            if (typeCode != 0) {
                opCode = OpCode.CPUI_MAX;
                numParams = 0;
                range.setRange(0, vn.getSize());   // Treat as offset of 0 relative to special value
                leftIsStable = true;
                rightIsStable = true;
            }
            else if (vn.isWritten()) {
                PcodeOp op = vn.getDef();
                opCode = op.code();
                if (opCode == OpCode.CPUI_INDIRECT) {
                    // Treat OpCode.CPUI_INDIRECT as OpCode.CPUI_COPY
                    numParams = 1;
                    opCode = OpCode.CPUI_COPY;
                }
                else
                    numParams = op.numInput();
                leftIsStable = false;
                rightIsStable = false;
            }
            else if (vn.isConstant()) {
                opCode = OpCode.CPUI_MAX;
                numParams = 0;
                range.setRange(vn.getOffset(), vn.getSize());
                leftIsStable = true;
                rightIsStable = true;
            }
            else {
                // Some other form of input
                opCode = OpCode.CPUI_MAX;
                numParams = 0;
                typeCode = 0;
                range.setFull(vn.getSize());
                leftIsStable = false;
                rightIsStable = false;
            }
        }

        /// Insert an equation restricting \b this value set
        /// Equations are stored as an array of (slot,range) pairs, ordered on slot.
        /// \param slot is the given slot
        /// \param type is the constraint characteristic
        /// \param constraint is the given range
        internal void addEquation(int slot, int type, CircleRange constraint)
        {
            int equationsCount = equations.Count;
            int insertionIndex;
            for(insertionIndex = 0; insertionIndex < equationsCount; insertionIndex++) {
                if (equations[insertionIndex].slot > slot)
                    break;
            }
            Equation newEquation = new Equation(slot, type, constraint);
            if (insertionIndex >= equationsCount)
                equations.Add(newEquation);
            else
                equations.Insert(insertionIndex, newEquation);
        }

        /// Add a widening landmark
        internal void addLandmark(int type, CircleRange constraint)
        {
            addEquation(numParams, type, constraint);
        }

        /// Figure out if \b this value set is absolute or relative
        /// Examine the input value sets that determine \b this set and decide if it
        /// is relative. In general, \b this will be relative if any of its inputs are.
        /// Certain combinations are indeterminate, which this method flags by
        /// returning \b true. The Varnode attached to \b this must have a defining op.
        /// \return \b true if there is an indeterminate combination
        private bool computeTypeCode()
        {
            int relCount = 0;
            int lastTypeCode = 0;
            PcodeOp op = vn.getDef() ?? throw new BugException();
            for (int i = 0; i < numParams; ++i) {
                ValueSet valueSet = op.getIn(i).getValueSet();
                if (valueSet.typeCode != 0) {
                    relCount += 1;
                    lastTypeCode = valueSet.typeCode;
                }
            }
            if (relCount == 0) {
                typeCode = 0;
                return false;
            }
            // Only certain operations can propagate a relative value set
            switch (opCode) {
                case OpCode.CPUI_PTRSUB:
                case OpCode.CPUI_PTRADD:
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_SUB:
                    if (relCount == 1)
                        typeCode = lastTypeCode;
                    else
                        return true;
                    break;
                case OpCode.CPUI_CAST:
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_INDIRECT:
                case OpCode.CPUI_MULTIEQUAL:
                    typeCode = lastTypeCode;
                    break;
                default:
                    return true;
            }
            return false;
        }

        /// Regenerate \b this value set from operator inputs
        /// Recalculate \b this value set by grabbing the value sets of the inputs to the
        /// operator defining the Varnode attached to \b this value set and pushing them
        /// forward through the operator.
        /// \return \b true if there was a change to \b this value set
        internal bool iterate(Widener widener)
        {
            if (!vn.isWritten()) return false;
            if (widener.checkFreeze(this)) return false;
            if (count == 0) {
                if (computeTypeCode()) {
                    setFull();
                    return true;
                }
            }
            count += 1;     // Count this iteration
            CircleRange res = new CircleRange();
            PcodeOp op = vn.getDef();
            int eqPos = 0;
            if (opCode == OpCode.CPUI_MULTIEQUAL) {
                int pieces = 0;
                for (int i = 0; i < numParams; ++i) {
                    ValueSet inSet = op.getIn(i).getValueSet();
                    if (doesEquationApply(eqPos, i)) {
                        CircleRange rangeCopy = inSet.range;
                        if (0 != rangeCopy.intersect(equations[eqPos].range)) {
                            rangeCopy = equations[eqPos].range;
                        }
                        pieces = res.circleUnion(rangeCopy);
                        eqPos += 1; // Equation was used
                    }
                    else {
                        pieces = res.circleUnion(inSet.range);
                    }
                    if (pieces == 2) {
                        if (res.minimalContainer(inSet.range, MAX_STEP))
                            // Could not get clean union, force it
                            break;
                    }
                }
                if (0 != res.circleUnion(range)) {
                    // Union with the previous iteration's set
                    res.minimalContainer(range, MAX_STEP);
                }
                if (!range.isEmpty() && !res.isEmpty()) {
                    leftIsStable = range.getMin() == res.getMin();
                    rightIsStable = range.getEnd() == res.getEnd();
                }
            }
            else if (numParams == 1) {
                ValueSet inSet1 = op.getIn(0).getValueSet();
                if (doesEquationApply(eqPos, 0)) {
                    CircleRange rangeCopy = inSet1.range;
                    if (0 != rangeCopy.intersect(equations[eqPos].range)) {
                        rangeCopy = equations[eqPos].range;
                    }
                    if (!res.pushForwardUnary(opCode, rangeCopy, inSet1.vn.getSize(), (uint)vn.getSize())) {
                        setFull();
                        return true;
                    }
                    eqPos += 1;
                }
                else if (!res.pushForwardUnary(opCode, inSet1.range, inSet1.vn.getSize(), (uint)vn.getSize())) {
                    setFull();
                    return true;
                }
                leftIsStable = inSet1.leftIsStable;
                rightIsStable = inSet1.rightIsStable;
            }
            else if (numParams == 2) {
                ValueSet inSet1 = op.getIn(0).getValueSet();
                ValueSet inSet2 = op.getIn(1).getValueSet();
                if (equations.size() == 0) {
                    if (!res.pushForwardBinary(opCode, inSet1.range, inSet2.range, inSet1.vn.getSize(),
                        vn.getSize(), MAX_STEP))
                    {
                        setFull();
                        return true;
                    }
                }
                else {
                    CircleRange range1 = inSet1.range;
                    CircleRange range2 = inSet2.range;
                    if (doesEquationApply(eqPos, 0)) {
                        if (0 != range1.intersect(equations[eqPos].range))
                            range1 = equations[eqPos].range;
                        eqPos += 1;
                    }
                    if (doesEquationApply(eqPos, 1)) {
                        if (0 != range2.intersect(equations[eqPos].range))
                            range2 = equations[eqPos].range;
                    }
                    if (!res.pushForwardBinary(opCode, range1, range2, inSet1.vn.getSize(), vn.getSize(),
                        MAX_STEP))
                    {
                        setFull();
                        return true;
                    }
                }
                leftIsStable = inSet1.leftIsStable && inSet2.leftIsStable;
                rightIsStable = inSet1.rightIsStable && inSet2.rightIsStable;
            }
            else if (numParams == 3) {
                ValueSet inSet1 = op.getIn(0).getValueSet();
                ValueSet inSet2 = op.getIn(1).getValueSet();
                ValueSet inSet3 = op.getIn(2).getValueSet();
                CircleRange range1 = inSet1.range;
                CircleRange range2 = inSet2.range;
                if (doesEquationApply(eqPos, 0)) {
                    if (0 != range1.intersect(equations[eqPos].range))
                        range1 = equations[eqPos].range;
                    eqPos += 1;
                }
                if (doesEquationApply(eqPos, 1)) {
                    if (0 != range2.intersect(equations[eqPos].range))
                        range2 = equations[eqPos].range;
                }
                if (!res.pushForwardTrinary(opCode, range1, range2, inSet3.range, inSet1.vn.getSize(),
                    vn.getSize(), MAX_STEP))
                {
                    setFull();
                    return true;
                }
                leftIsStable = inSet1.leftIsStable && inSet2.leftIsStable;
                rightIsStable = inSet1.rightIsStable && inSet2.rightIsStable;
            }
            else
                return false;       // No way to change this value set

            if (res == range)
                return false;
            if (partHead != (Partition)null) {
                if (!widener.doWidening(this, range, res))
                    setFull();
            }
            else
                range = res;
            return true;
        }

        /// Get the current iteration count
        public int getCount() => count;

        /// Get any \e landmark range
        /// If a landmark was associated with \b this value set, return its range,
        /// otherwise return null.
        /// \return the landmark range or null
        public CircleRange? getLandMark()
        {
            // Any equation can serve as a landmark.  We prefer the one restricting the
            // value of an input branch, as these usually give a tighter approximation
            // of the stable point.
            for (int i = 0; i < equations.size(); ++i) {
                if (equations[i].typeCode == typeCode)
                    return equations[i].range;
            }
            return (CircleRange)null;
        }

        /// Return '0' for normal constant, '1' for spacebase relative
        public int getTypeCode() => typeCode;

        /// Get the Varnode attached to \b this ValueSet
        public Varnode getVarnode() => vn;

        /// Get the actual range of values
        public CircleRange getRange() => range;

        /// Return \b true if the left boundary hasn't been changing
        public bool isLeftStable() => leftIsStable;

        /// Return \b true if the right boundary hasn't been changing
        public bool isRightStable() => rightIsStable;

        /// Write a text description of \b to the given stream
        /// \param s is the stream to print to
        public void printRaw(TextWriter s)
        {
            if (vn == (Varnode)null)
                s.Write("root");
            else
                vn.printRaw(s);
            if (typeCode == 0)
                s.Write(" absolute");
            else
                s.Write(" stackptr");
            if (opCode == OpCode.CPUI_MAX) {
                if (vn.isConstant())
                    s.Write(" const");
                else
                    s.Write(" input");
            }
            else
                s.Write($" {Globals.get_opname(opCode)}");
            s.Write(' ');
            range.printRaw(s);
        }
    }
}
