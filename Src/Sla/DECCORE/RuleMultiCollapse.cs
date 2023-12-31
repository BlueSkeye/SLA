﻿using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleMultiCollapse : Rule
    {
        public RuleMultiCollapse(string g)
            : base(g, 0, "multicollapse")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleMultiCollapse(getGroup());
        }

        /// \class RuleMultiCollapse
        /// \brief Collapse MULTIEQUAL whose inputs all trace to the same value
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_MULTIEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            List<Varnode> skiplist = new List<Varnode>();
            List<Varnode> matchlist = new List<Varnode>();
            Varnode copyr;
            bool func_eq, nofunc;
            PcodeOp newop;
            int j;

            for (int i = 0; i < op.numInput(); ++i)   // Everything must be heritaged before collapse
                if (!op.getIn(i).isHeritageKnown()) return 0;

            func_eq = false;        // Start assuming absolute equality of branches
            nofunc = false;     // Functional equalities are initially allowed
            Varnode? defcopyr = (Varnode)null;
            j = 0;
            for (int i = 0; i < op.numInput(); ++i)
                matchlist.Add(op.getIn(i));
            for (int i = 0; i < op.numInput(); ++i) {
                // Find base branch to match
                copyr = matchlist[i];
                if ((!copyr.isWritten()) || (copyr.getDef().code() != OpCode.CPUI_MULTIEQUAL)) {
                    defcopyr = copyr;
                    break;
                }
            }

            bool success = true;
            op.getOut().setMark();
            skiplist.Add(op.getOut());
            while (j < matchlist.size()) {
                copyr = matchlist[j++];
                if (copyr.isMark()) continue; // A varnode we have seen before
                                               // indicates a loop construct, where the
                                               // value is recurring in the loop without change
                                               // so we treat this as equal to all other branches
                                               // I.e. skip this varnode
                if (defcopyr == (Varnode)null) {
                    // This is now the defining branch
                    defcopyr = copyr;       // all other branches must match
                    if (defcopyr.isWritten()) {
                        if (defcopyr.getDef().code() == OpCode.CPUI_MULTIEQUAL)
                            nofunc = true;  // MULTIEQUAL cannot match by functional equal
                    }
                    else
                        nofunc = true;      // Unwritten cannot match by functional equal
                }
                else if (defcopyr == copyr) continue; // A matching branch
                else if (   (defcopyr != copyr)
                         && !nofunc
                         && PcodeOpBank.functionalEquality(defcopyr, copyr)) {
                    // Cannot match MULTIEQUAL by functional equality
                    //      if (nofunc) return 0;	// Not allowed to match by func equal
                    func_eq = true;     // Now matching by functional equality
                    continue;
                }
                else if ((copyr.isWritten()) && (copyr.getDef().code() == OpCode.CPUI_MULTIEQUAL)) {
                    // If the non-matching branch is a MULTIEQUAL
                    newop = copyr.getDef() ?? throw new BugException();
                    skiplist.Add(copyr); // We give the branch one last chance and
                    copyr.setMark();
                    for (int i = 0; i < newop.numInput(); ++i) // add its inputs to list of things to match
                        matchlist.Add(newop.getIn(i));
                }
                else {
                    // A non-matching branch
                    success = false;
                    break;
                }
            }
            if (success) {
                for (j = 0; j < skiplist.size(); ++j) {
                    // Collapse everything in the skiplist
                    copyr = skiplist[j];
                    copyr.clearMark();
                    op = copyr.getDef();
                    if (func_eq) {
                        // We have only functional equality
                        PcodeOp earliest = Funcdata.earliestUseInBlock(op.getOut(), op.getParent())
                            ?? throw new ApplicationException();
                        // We must copy newop (defcopyr)
                        newop = defcopyr.getDef();
                        PcodeOp substitute = (PcodeOp)null;
                        for (int i = 0; i < newop.numInput(); ++i) {
                            Varnode invn = newop.getIn(i);
                            if (!invn.isConstant()) {
                                substitute = Funcdata.cseFindInBlock(newop, invn, op.getParent(), earliest); // Has newop already been copied in this block
                                break;
                            }
                        }
                        if (substitute != (PcodeOp)null) {
                            // If it has already been copied,
                            data.totalReplace(copyr, substitute.getOut()); // just use copy's output as substitute for op
                            data.opDestroy(op);
                        }
                        else {
                            // Otherwise, create a copy
                            bool needsreinsert = (op.code() == OpCode.CPUI_MULTIEQUAL);
                            List<Varnode> parms = new List<Varnode>();
                            for (int i = 0; i < newop.numInput(); ++i)
                                parms.Add(newop.getIn(i)); // Copy parameters
                            data.opSetAllInput(op, parms);
                            data.opSetOpcode(op, newop.code()); // Copy opcode
                            if (needsreinsert) {
                                // If the op is no longer a MULTIEQUAL
                                BlockBasic bl = op.getParent();
                                data.opUninsert(op);
                                data.opInsertBegin(op, bl); // Insert AFTER any other MULTIEQUAL
                            }
                        }
                    }
                    else {
                        // We have absolute equality
                        data.totalReplace(copyr, defcopyr); // Replace all refs to copyr with defcopyr
                        data.opDestroy(op); // Get rid of the MULTIEQUAL
                    }
                }
                return 1;
            }
            for (j = 0; j < skiplist.size(); ++j)
                skiplist[j].clearMark();
            return 0;
        }
    }
}
