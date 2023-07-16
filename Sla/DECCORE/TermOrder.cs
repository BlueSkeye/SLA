using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
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
            return (-1 == op1->getVarnode()->termOrder(op2->getVarnode()));
        }

    /// Construct given root PcodeOp
    public TermOrder(PcodeOp rt)
        {
            root = rt;
        }

        /// Get the number of terms in the expression
        public int getSize() => terms.size();

        /// Collect all the terms in the expression
        /// Assuming root->getOut() is the root of an expression formed with the
        /// CPUI_INT_ADD op, collect all the Varnode \e terms of the expression.
        public void collect()
        {
            Varnode* curvn;
            PcodeOp* curop;
            PcodeOp* subop,*multop;

            vector<PcodeOp*> opstack;   // Depth first traversal path
            vector<PcodeOp*> multstack;

            opstack.push_back(root);
            multstack.push_back((PcodeOp*)0);

            while (!opstack.empty())
            {
                curop = opstack.back();
                multop = multstack.back();
                opstack.pop_back();
                multstack.pop_back();
                for (int4 i = 0; i < curop->numInput(); ++i)
                {
                    curvn = curop->getIn(i);    // curvn is a node of the subtree IF
                    if (!curvn->isWritten())
                    { // curvn is not defined by another operation
                        terms.push_back(AdditiveEdge(curop, i, multop));
                        continue;
                    }
                    if (curvn->loneDescend() == (PcodeOp*)0)
                    { // curvn has more then one use
                        terms.push_back(AdditiveEdge(curop, i, multop));
                        continue;
                    }
                    subop = curvn->getDef();
                    if (subop->code() != CPUI_INT_ADD)
                    { // or if curvn is defined with some other type of op
                        if ((subop->code() == CPUI_INT_MULT) && (subop->getIn(1)->isConstant()))
                        {
                            PcodeOp* addop = subop->getIn(0)->getDef();
                            if ((addop != (PcodeOp*)0) && (addop->code() == CPUI_INT_ADD))
                            {
                                if (addop->getOut()->loneDescend() != (PcodeOp*)0)
                                {
                                    opstack.push_back(addop);
                                    multstack.push_back(subop);
                                    continue;
                                }
                            }
                        }
                        terms.push_back(AdditiveEdge(curop, i, multop));
                        continue;
                    }
                    opstack.push_back(subop);
                    multstack.push_back(multop);
                }
            }
        }

        /// Sort the terms using additiveCompare()
        public void sortTerms()
        {
            for (vector<AdditiveEdge>::iterator iter = terms.begin(); iter != terms.end(); ++iter)
                sorter.push_back(&(*iter));

            sort(sorter.begin(), sorter.end(), additiveCompare);
        }

        /// Get the sorted list of references
        public List<AdditiveEdge> getSort() => sorter;
    }
}
