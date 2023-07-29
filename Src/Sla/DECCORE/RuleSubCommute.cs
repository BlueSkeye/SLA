using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSubCommute : Rule
    {
        public RuleSubCommute(string g)
            : base(g, 0, "subcommute")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSubCommute(getGroup());
        }

        /// \class RuleSubCommute
        /// \brief Commute SUBPIECE operations with earlier operations where possible
        ///
        /// A SUBPIECE conmmutes with long and short forms of many operations.
        /// We try to push SUBPIECE earlier in the expression trees (preferring short versions
        /// of ops over long) in the hopes that the SUBPIECE will run into a
        /// constant, a INT_SEXT, or a INT_ZEXT, canceling out
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode @base;
            Varnode vn;
            Varnode newvn;
            Varnode outvn;
            PcodeOp longform;
            PcodeOp newsub;
            PcodeOp prevop;
            int i, j, offset, insize;

            @base = op.getIn(0);
            if (!@@base.isWritten()) return 0;
            offset = op.getIn(1).getOffset();
            outvn = op.getOut();
            if (outvn.isPrecisLo() || outvn.isPrecisHi()) return 0;
            insize = @@base.getSize();
            longform = @@base.getDef();
            j = -1;
            switch (longform.code())
            {   // Determine if this op commutes with SUBPIECE
                //  case CPUI_COPY:
                case CPUI_INT_LEFT:
                    j = 1;          // Special processing for shift amount param
                    if (offset != 0) return 0;
                    if (!longform.getIn(0).isWritten()) return 0;
                    prevop = longform.getIn(0).getDef();
                    if (prevop.code() == CPUI_INT_ZEXT)
                    {
                    }
                    else if (prevop.code() == CPUI_PIECE)
                    {
                    }
                    else
                        return 0;
                    break;
                case CPUI_INT_REM:
                case CPUI_INT_DIV:
                    {
                        // Only commutes if inputs are zero extended
                        if (offset != 0) return 0;
                        if (!longform.getIn(0).isWritten()) return 0;
                        PcodeOp* zext0 = longform.getIn(0).getDef();
                        if (zext0.code() != CPUI_INT_ZEXT) return 0;
                        Varnode* zext0In = zext0.getIn(0);
                        if (longform.getIn(1).isWritten())
                        {
                            PcodeOp* zext1 = longform.getIn(1).getDef();
                            if (zext1.code() != CPUI_INT_ZEXT) return 0;
                            Varnode* zext1In = zext1.getIn(0);
                            if (zext1In.getSize() > outvn.getSize() || zext0In.getSize() > outvn.getSize())
                            {
                                // Special case where we need a PARTIAL commute of the SUBPIECE
                                // SUBPIECE cancels the ZEXTs, but there is still some SUBPIECE left
                                if (cancelExtensions(longform, op, zext0In, zext1In, data)) // Cancel ZEXT operations
                                    return 1;                       // Leave SUBPIECE intact
                                return 0;
                            }
                            // If ZEXT sizes are both not bigger, go ahead and commute SUBPIECE (fallthru)
                        }
                        else if (longform.getIn(1).isConstant() && (zext0In.getSize() <= outvn.getSize()))
                        {
                            ulong val = longform.getIn(1).getOffset();
                            ulong smallval = val & Globals.calc_mask(outvn.getSize());
                            if (val != smallval)
                                return 0;
                        }
                        else
                            return 0;
                        break;
                    }
                case CPUI_INT_SREM:
                case CPUI_INT_SDIV:
                    {
                        // Only commutes if inputs are sign extended
                        if (offset != 0) return 0;
                        if (!longform.getIn(0).isWritten()) return 0;
                        PcodeOp* sext0 = longform.getIn(0).getDef();
                        if (sext0.code() != CPUI_INT_SEXT) return 0;
                        Varnode* sext0In = sext0.getIn(0);
                        if (longform.getIn(1).isWritten())
                        {
                            PcodeOp* sext1 = longform.getIn(1).getDef();
                            if (sext1.code() != CPUI_INT_SEXT) return 0;
                            Varnode* sext1In = sext1.getIn(0);
                            if (sext1In.getSize() > outvn.getSize() || sext0In.getSize() > outvn.getSize())
                            {
                                // Special case where we need a PARTIAL commute of the SUBPIECE
                                // SUBPIECE cancels the SEXTs, but there is still some SUBPIECE left
                                if (cancelExtensions(longform, op, sext0In, sext1In, data)) // Cancel SEXT operations
                                    return 1;                       // Leave SUBPIECE intact
                                return 0;
                            }
                            // If SEXT sizes are both not bigger, go ahead and commute SUBPIECE (fallthru)
                        }
                        else if (longform.getIn(1).isConstant() && (sext0In.getSize() <= outvn.getSize()))
                        {
                            ulong val = longform.getIn(1).getOffset();
                            ulong smallval = val & Globals.calc_mask(outvn.getSize());
                            smallval = sign_extend(smallval, outvn.getSize(), insize);
                            if (val != smallval)
                                return 0;
                        }
                        else
                            return 0;
                        break;
                    }
                case CPUI_INT_ADD:
                    if (offset != 0) return 0;  // Only commutes with least significant SUBPIECE
                    if (longform.getIn(0).isSpacebase()) return 0;    // Deconflict with RulePtrArith
                    break;
                case CPUI_INT_MULT:
                    if (offset != 0) return 0;  // Only commutes with least significant SUBPIECE
                    break;
                // Bitwise ops, type of subpiece doesnt matter
                case CPUI_INT_NEGATE:
                case CPUI_INT_XOR:
                case CPUI_INT_AND:
                case CPUI_INT_OR:
                    break;
                default:            // Most ops don't commute
                    return 0;
            }

            // Make sure no other piece of base is getting used
            if (@base.loneDescend() != op) return 0;

            if (offset == 0)
            {       // Look for overlap with RuleSubZext
                PcodeOp* nextop = outvn.loneDescend();
                if ((nextop != (PcodeOp)null) && (nextop.code() == CPUI_INT_ZEXT))
                {
                    if (nextop.getOut().getSize() == insize)
                        return 0;
                }
            }

            for (i = 0; i < longform.numInput(); ++i)
            {
                vn = longform.getIn(i);
                if (i != j)
                {
                    newsub = data.newOp(2, op.getAddr()); // Commuted SUBPIECE op
                    data.opSetOpcode(newsub, CPUI_SUBPIECE);
                    newvn = data.newUniqueOut(outvn.getSize(), newsub);  // New varnode is subpiece
                    data.opSetInput(longform, newvn, i);
                    data.opSetInput(newsub, vn, 0); // of old varnode
                    data.opSetInput(newsub, data.newConstant(4, offset), 1);
                    data.opInsertBefore(newsub, longform);
                }
            }
            data.opSetOutput(longform, outvn);
            data.opDestroy(op);     // Get rid of old SUBPIECE
            return 1;
        }

        /// \brief Eliminate input extensions on given binary PcodeOp
        ///
        /// Make some basic checks.  Replace the input and output Varnodes with smaller sizes.
        /// \param longform is the given binary PcodeOp to modify
        /// \param subOp is the PcodeOp truncating the output of \b longform
        /// \param ext0In is the first input Varnode before the extension
        /// \param ext1In is the second input Varnode before the extension
        /// \param data is the function being analyzed
        /// \return true is the PcodeOp is successfully modified
        public static bool cancelExtensions(PcodeOp longform, PcodeOp subOp, Varnode ext0In, Varnode ext1In,
        Funcdata data)
        {
            if (ext0In.getSize() != ext1In.getSize()) return false;   // Sizes must match
            if (ext0In.isFree()) return false;     // Must be able to propagate inputs
            if (ext1In.isFree()) return false;
            Varnode* outvn = longform.getOut();
            if (outvn.loneDescend() != subOp) return false;    // Must be exactly one output to SUBPIECE
            data.opUnsetOutput(longform);
            outvn = data.newUniqueOut(ext0In.getSize(), longform); // Create truncated form of longform output
            data.opSetInput(longform, ext0In, 0);
            data.opSetInput(longform, ext1In, 1);
            data.opSetInput(subOp, outvn, 0);
            return true;
        }
    }
}
