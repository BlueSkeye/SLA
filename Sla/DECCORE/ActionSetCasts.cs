using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Fill-in CPUI_CAST p-code ops as required by the casting strategy
    ///
    /// Setting the casts is complicated by type inference and
    /// implied variables.  By the time this Action is run, the
    /// type inference algorithm has labeled every Varnode with what
    /// it thinks the type should be.  This casting algorithm tries
    /// to get the code to legally match this inference result by
    /// adding casts.  Following the data flow, it tries the best it
    /// can to get each token to match the inferred type.  For
    /// implied variables, the type is completely determined by the
    /// syntax of the output language, so implied casts won't work in this case.
    /// For most of these cases, the algorithm just changes the type
    /// to that dictated by syntax and gets back on track at the
    /// next explicit variable in the flow. It tries to avoid losing
    /// pointer types however because any CPUI_PTRADD \b mst have a pointer
    /// input. In this case, it casts to the necessary pointer type
    /// immediately.
    internal class ActionSetCasts : Action
    {
        private static void checkPointerIssues(PcodeOp* op, Varnode* vn, Funcdata &data);

        private static bool testStructOffset0(Varnode* vn, PcodeOp* op, Datatype* ct, CastStrategy* castStrategy);

        private static bool tryResolutionAdjustment(PcodeOp* op, int4 slot, Funcdata &data);

        private static bool isOpIdentical(Datatype* ct1, Datatype* ct2);

        private static int4 resolveUnion(PcodeOp* op, int4 slot, Funcdata &data);

        private static int4 castOutput(PcodeOp* op, Funcdata &data, CastStrategy* castStrategy);

        private static int4 castInput(PcodeOp* op, int4 slot, Funcdata &data, CastStrategy* castStrategy);

        private static PcodeOp* insertPtrsubZero(PcodeOp* op, int4 slot, Datatype* ct, Funcdata &data);
        
        public ActionSetCasts(string g)
            : base(rule_onceperfunc,"setcasts", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionSetCasts(getGroup());
        }

        public override int apply(Funcdata data);
    }
}
