using Sla.CORE;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A class for ordering Varnode terms in an additive expression.
    ///
    /// Given the final PcodeOp in a data-flow expression that sums 2 or more
    /// Varnode \e terms, this class collects all the terms then allows
    /// sorting of the terms to facilitate constant collapse and factoring simplifications.
    internal class TermOrder
    {
        /// The final PcodeOp in the expression
        private PcodeOp root;
        /// Collected terms
        private List<AdditiveEdge> terms = new List<AdditiveEdge>();
        /// An array of references to terms for quick sorting
        private List<AdditiveEdge> sorter = new List<AdditiveEdge>();

        /// \brief A comparison operator for ordering terms in a sum
        ///
        /// This is based on Varnode::termOrder which groups constants terms and
        /// ignores multiplicative coefficients.
        /// \param op1 is the first term to compare
        /// \param op2 is the second term
        /// \return \b true if the first term is less than the second
        private static bool additiveCompare(AdditiveEdge op1, AdditiveEdge op2)
        {
            return (-1 == op1.getVarnode().termOrder(op2.getVarnode()));
        }

        /// Construct given root PcodeOp
        public TermOrder(PcodeOp rt)
        {
            root = rt;
        }

        /// Get the number of terms in the expression
        public int getSize() => terms.size();

        /// Collect all the terms in the expression
        /// Assuming root.getOut() is the root of an expression formed with the
        /// OpCode.CPUI_INT_ADD op, collect all the Varnode \e terms of the expression.
        public void collect()
        {
            Varnode curvn;
            PcodeOp curop;
            PcodeOp subop;
            PcodeOp? multop;

            List<PcodeOp> opstack = new List<PcodeOp>();   // Depth first traversal path
            List<PcodeOp?> multstack = new List<PcodeOp?>();

            opstack.Add(root);
            multstack.Add((PcodeOp)null);

            while (!opstack.empty()) {
                curop = opstack.GetLastItem();
                multop = multstack.GetLastItem();
                opstack.RemoveLastItem();
                multstack.RemoveLastItem();
                for (int i = 0; i < curop.numInput(); ++i) {
                    // curvn is a node of the subtree IF
                    curvn = curop.getIn(i);
                    if (!curvn.isWritten()) {
                        // curvn is not defined by another operation
                        terms.Add(new AdditiveEdge(curop, i, multop));
                        continue;
                    }
                    if (curvn.loneDescend() == (PcodeOp)null) {
                        // curvn has more then one use
                        terms.Add(new AdditiveEdge(curop, i, multop));
                        continue;
                    }
                    subop = curvn.getDef();
                    if (subop.code() != OpCode.CPUI_INT_ADD) {
                        // or if curvn is defined with some other type of op
                        if ((subop.code() == OpCode.CPUI_INT_MULT) && (subop.getIn(1).isConstant())) {
                            PcodeOp? addop = subop.getIn(0).getDef();
                            if ((addop != (PcodeOp)null) && (addop.code() == OpCode.CPUI_INT_ADD)) {
                                if (addop.getOut().loneDescend() != (PcodeOp)null) {
                                    opstack.Add(addop);
                                    multstack.Add(subop);
                                    continue;
                                }
                            }
                        }
                        terms.Add(new AdditiveEdge(curop, i, multop));
                        continue;
                    }
                    opstack.Add(subop);
                    multstack.Add(multop);
                }
            }
        }

        /// Sort the terms using additiveCompare()
        public void sortTerms()
        {
            foreach (AdditiveEdge edge in terms)
                sorter.Add(edge);
            sorter.Sort(additiveCompare);
        }

        /// Get the sorted list of references
        public List<AdditiveEdge> getSort() => sorter;
    }
}
