using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Find \b explicit Varnodes: Varnodes that have an explicit token representing them in the output
    ///
    /// In the final output of the syntax tree as source code, all variables are characterized as either
    ///    - \b explicit, having a specific identifier in the source code, or
    ///    - \b implied, an intermediate result of an expression with no specific identifier
    ///
    /// This Action does preliminary scanning of Varnodes to determine which should be explicit
    /// in the final output.  Basically, if there is symbol information associated, the possibility
    /// of aliasing, or if there are too many reads of a Varnode, it should be considered explicit.
    internal class ActionMarkExplicit : Action
    {
        /// This class holds a single entry in a stack used to traverse Varnode expressions
        internal struct OpStackElement
        {
            /// The Varnode at this particular point in the path
            internal Varnode vn;
            /// The slot of the first input Varnode to traverse in this subexpression
            internal int slot;
            /// The slot(+1) of the last input Varnode to traverse in this subexpression
            internal int slotback;

            /// Record the Varnode just encountered and set-up the next (backward) edges to traverse.
            /// \param v is the Varnode just encountered
            internal OpStackElement(Varnode v)
            {
                vn = v;
                slot = 0;
                slotback = 0;
                if (v.isWritten())
                {
                    OpCode opc = v.getDef().code();
                    if (opc == CPUI_LOAD)
                    {
                        slot = 1;
                        slotback = 2;
                    }
                    else if (opc == CPUI_PTRADD)
                        slotback = 1;           // Don't traverse the multiplier slot
                    else
                        slotback = v.getDef().numInput();
                }
            }
        }

        ///< Make initial determination if a Varnode should be \e explicit
        /// If the given Varnode is defined by CPUI_NEW, return -2 indicating it should be explicit
        /// and that it needs special printing.
        /// \param vn is the given Varnode
        /// \param maxref is the maximum number of references to consider before forcing explicitness
        /// \return -1 or -2 if given Varnode should be marked explicit, the number of descendants otherwise
        private static int baseExplicit(Varnode vn, int maxref)
        {
            list<PcodeOp*>::const_iterator iter;

            PcodeOp* def = vn.getDef();
            if (def == (PcodeOp)null) return -1;
            if (def.isMarker()) return -1;
            if (def.isCall())
            {
                if ((def.code() == CPUI_NEW) && (def.numInput() == 1))
                    return -2;      // Explicit, but may need special printing
                return -1;
            }
            HighVariable* high = vn.getHigh();
            if ((high != (HighVariable)null) && (high.numInstances() > 1)) return -1; // Must not be merged at all
            if (vn.isAddrTied())
            {       // We need to see addrtied as explicit because pointers may reference it
                if (def.code() == CPUI_SUBPIECE)
                {
                    Varnode* vin = def.getIn(0);
                    if (vin.isAddrTied())
                    {
                        if (vn.overlapJoin(*vin) == def.getIn(1).getOffset())
                            return -1;      // Should be explicit, will be a copymarker and not printed
                    }
                }
                PcodeOp* useOp = vn.loneDescend();
                if (useOp == (PcodeOp)null) return -1;
                if (useOp.code() == CPUI_INT_ZEXT)
                {
                    Varnode* vnout = useOp.getOut();
                    if ((!vnout.isAddrTied()) || (0 != vnout.contains(*vn)))
                        return -1;
                }
                else if (useOp.code() == CPUI_PIECE)
                {
                    Varnode* rootVn = PieceNode::findRoot(vn);
                    if (vn == rootVn) return -1;
                    if (rootVn.getDef().isPartialRoot())
                    {
                        // Getting PIECEd into a structured thing.  Unless vn is a leaf, it should be implicit
                        if (def.code() != CPUI_PIECE) return -1;
                        if (vn.loneDescend() == (PcodeOp)null) return -1;
                        Varnode* vn0 = def.getIn(0);
                        Varnode* vn1 = def.getIn(1);
                        Address addr = vn.getAddr();
                        if (!addr.getSpace().isBigEndian())
                            addr = addr + vn1.getSize();
                        if (addr != vn0.getAddr()) return -1;
                        addr = vn.getAddr();
                        if (addr.getSpace().isBigEndian())
                            addr = addr + vn0.getSize();
                        if (addr != vn1.getAddr()) return -1;
                        // If we reach here vn is a non-leaf in a CONCAT tree and should be implicit
                    }
                }
                else
                {
                    return -1;
                }
            }
            else if (vn.isMapped())
            {
                // If NOT addrtied but is still mapped, there must be either a first use (register) mapping
                // or a dynamic mapping causing the bit to be set. In either case, it should probably be explicit
                return -1;
            }
            else if (vn.isProtoPartial() && def.code() != CPUI_PIECE)
            {
                // Varnode is part of structure. Write to structure should be an explicit statement
                return -1;
            }
            else if (def.code() == CPUI_PIECE && def.getIn(0).isProtoPartial() && !vn.isProtoPartial())
            {
                // The base of PIECE operations building a structure
                return -1;
            }
            if (vn.hasNoDescend()) return -1;  // Must have at least one descendant

            if (def.code() == CPUI_PTRSUB)
            { // A dereference
                Varnode* basevn = def.getIn(0);
                if (basevn.isSpacebase())
                { // of a spacebase
                    if (basevn.isConstant() || basevn.isInput())
                        maxref = 1000000;   // Should always be implicit, so remove limit on max references
                }
            }
            int desccount = 0;
            for (iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
            {
                PcodeOp* op = *iter;
                if (op.isMarker()) return -1;
                desccount += 1;
                if (desccount > maxref) return -1; // Must not exceed max descendants
            }

            return desccount;
        }

        /// Find multiple descendant chains
        /// Look for certain situations where one Varnode with multiple descendants has one descendant who also has
        /// multiple descendants.  This routine is handed the list of Varnodes with multiple descendants;
        /// These all must already have their mark set.
        /// For the situations we can find with one flowing into another, mark the top Varnode
        /// as \e explicit.
        /// \param multlist is the list Varnodes with multiple descendants
        /// \return the number Varnodes that were marked as explicit
        private static int multipleInteraction(List<Varnode> multlist)
        {
            List<Varnode*> purgelist;

            for (int i = 0; i < multlist.size(); ++i)
            {
                Varnode* vn = multlist[i];  // All elements in this list should have a defining op
                PcodeOp* op = vn.getDef();
                OpCode opc = op.code();
                if (op.isBoolOutput() || (opc == CPUI_INT_ZEXT) || (opc == CPUI_INT_SEXT) || (opc == CPUI_PTRADD))
                {
                    int maxparam = 2;
                    if (op.numInput() < maxparam)
                        maxparam = op.numInput();
                    Varnode* topvn = (Varnode)null;
                    for (int j = 0; j < maxparam; ++j)
                    {
                        topvn = op.getIn(j);
                        if (topvn.isMark())
                        {   // We have a "multiple" interaction between -topvn- and -vn-
                            OpCode topopc = CPUI_COPY;
                            if (topvn.isWritten())
                            {
                                if (topvn.getDef().isBoolOutput())
                                    continue;       // Try not to make boolean outputs explicit
                                topopc = topvn.getDef().code();
                            }
                            if (opc == CPUI_PTRADD)
                            {
                                if (topopc == CPUI_PTRADD)
                                    purgelist.Add(topvn);
                            }
                            else
                                purgelist.Add(topvn);
                        }
                    }
                }
            }

            for (int i = 0; i < purgelist.size(); ++i)
            {
                Varnode* vn = purgelist[i];
                vn.setExplicit();
                vn.clearImplied();
                vn.clearMark();
            }
            return purgelist.size();
        }

        /// For a given multi-descendant Varnode, decide if it should be explicit
        /// Count the number of terms in the expression making up \b vn. If
        /// there are more than \b max terms, mark \b vn as \e explicit.
        /// The given Varnode is already assumed to have multiple descendants.
        /// We do a depth first traversal along op inputs, to recursively
        /// calculate the number of explicit terms in an expression.
        /// \param vn is the given Varnode
        /// \param max is the maximum number of terms to allow
        private static void processMultiplier(Varnode vn, int max)
        {
            List<OpStackElement> opstack;
            Varnode* vncur;
            int finalcount = 0;

            opstack.Add(vn);
            do
            {
                vncur = opstack.GetLastItem().vn;
                bool isaterm = vncur.isExplicit() || (!vncur.isWritten());
                if (isaterm || (opstack.GetLastItem().slotback <= opstack.GetLastItem().slot))
                { // Trimming condition
                    if (isaterm)
                    {
                        if (!vncur.isSpacebase()) // Don't count space base
                            finalcount += 1;
                    }
                    if (finalcount > max)
                    {
                        vn.setExplicit();  // Make this variable explicit
                        vn.clearImplied();
                        return;
                    }
                    opstack.pop_back();
                }
                else
                {
                    PcodeOp* op = vncur.getDef();
                    Varnode* newvn = op.getIn(opstack.GetLastItem().slot++);
                    if (newvn.isMark())
                    {   // If an ancestor is marked(also possible implied with multiple descendants)
                        vn.setExplicit();  // then automatically consider this to be explicit
                        vn.clearImplied();
                    }
                    opstack.Add(newvn);
                }
            } while (!opstack.empty());
        }

        /// Set special properties on output of CPUI_NEW
        /// Assume \b vn is produced via a CPUI_NEW operation. If it is immediately fed to a constructor,
        /// set special printing flags on the Varnode.
        /// \param data is the function being analyzed
        /// \param vn is the given Varnode
        private static void checkNewToConstructor(Funcdata data, Varnode vn)
        {
            PcodeOp* op = vn.getDef();
            BlockBasic* bb = op.getParent();
            PcodeOp* firstuse = (PcodeOp)null;
            list<PcodeOp*>::const_iterator iter;
            for (iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
            {
                PcodeOp* curop = *iter;
                if (curop.getParent() != bb) continue;
                if (firstuse == (PcodeOp)null)
                    firstuse = curop;
                else if (curop.getSeqNum().getOrder() < firstuse.getSeqNum().getOrder())
                    firstuse = curop;
                else if (curop.code() == CPUI_CALLIND)
                {
                    Varnode* ptr = curop.getIn(0);
                    if (ptr.isWritten())
                    {
                        if (ptr.getDef() == firstuse)
                            firstuse = curop;
                    }
                }
            }
            if (firstuse == (PcodeOp)null) return;

            if (!firstuse.isCall()) return;
            if (firstuse.getOut() != (Varnode)null) return;
            if (firstuse.numInput() < 2) return;       // Must have at least 1 parameter (plus destination varnode)
            if (firstuse.getIn(1) != vn) return;       // First parameter must result of new
                                                        //  if (!fc.isConstructor()) return;		// Function must be a constructor
            data.opMarkSpecialPrint(firstuse);      // Mark call to print the new operator as well
            data.opMarkNonPrinting(op);         // Don't print the new operator as stand-alone operation
        }

        public ActionMarkExplicit(string g)
            : base(rule_onceperfunc,"markexplicit", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMarkExplicit(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            VarnodeDefSet::const_iterator viter, enditer;
            List<Varnode*> multlist;      // implied varnodes with >1 descendants
            int maxref;

            maxref = data.getArch().max_implied_ref;
            enditer = data.beginDef(0); // Cut out free varnodes
            for (viter = data.beginDef(); viter != enditer; ++viter)
            {
                Varnode* vn = *viter;

                int desccount = baseExplicit(vn, maxref);
                if (desccount < 0)
                {
                    vn.setExplicit();
                    count += 1;
                    if (desccount < -1)
                        checkNewToConstructor(data, vn);
                }
                else if (desccount > 1)
                {   // Keep track of possible implieds with more than one descendant
                    vn.setMark();
                    multlist.Add(vn);
                }
            }

            count += multipleInteraction(multlist);
            int maxdup = data.getArch().max_term_duplication;
            for (int i = 0; i < multlist.size(); ++i)
            {
                Varnode* vn = multlist[i];
                if (vn.isMark())       // Mark may have been cleared by multipleInteraction
                    processMultiplier(vn, maxdup);
            }
            for (int i = 0; i < multlist.size(); ++i)
                multlist[i].clearMark();
            return 0;
        }
    }
}
