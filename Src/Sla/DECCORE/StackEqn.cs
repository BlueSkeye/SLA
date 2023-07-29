using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A stack equation
    internal struct StackEqn
    {
        /// Variable with 1 coefficient
        private int var1;
        /// Variable with -1 coefficient
        private int var2;
        /// Right hand side of the equation
        private int rhs;

        ///< Order two equations
        /// \param a is the first equation to compare
        /// \param b is the second
        /// \return true if the first equation comes before the second
        internal static bool compare(StackEqn a, StackEqn b)
        {
            return (a.var1 < b.var1);
        }
    }
}
