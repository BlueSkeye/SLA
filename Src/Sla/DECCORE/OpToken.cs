using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A token representing an operator in the high-level language
    ///
    /// The token knows how to print itself and other syntax information like
    /// precedence level and associativity within the language, desired spacing,
    /// and how operator groups its input expressions. Note that an operator has
    /// a broader meaning than just p-code operators in this context.
    internal class OpToken
    {
        /// \brief The possible types of operator token
        public enum tokentype
        {
            binary,         ///< Binary operator form (printed between its inputs)
            unary_prefix,       ///< Unary operator form (printed before its input)
            postsurround,       ///< Function or array operator form
            presurround,        ///< Modifier form (like a cast operation)
            space,          ///< No explicitly printed token
            hiddenfunction      ///< Operation that isn't explicitly printed
        }

        internal string print1;      ///< Printing characters for the token
        internal string print2;      ///< (terminating) characters for the token
        internal int stage;         ///< Additional elements consumed from the RPN stack when emitting this token
        internal int precedence;        ///< Precedence level of this token (higher binds more tightly)
        internal bool associative;       ///< True if the operator is associative
        internal tokentype type;     ///< The basic token type
        internal int spacing;           ///< Spaces to print around operator
        internal int bump;          ///< Spaces to indent if we break here
        internal OpToken? negate;		///< The token representing the negation of this token

        internal OpToken(string print1, string print2, int stage, int precedence, bool associative,
            tokentype type, int spacing, int bump)
        {
            this.print1 = print1;
            this.print2 = print2;
            this.stage = stage;
            this.precedence = precedence;
            this.associative = associative;
            this.type = type;
            this.spacing = spacing;
            this.bump = bump;
        }
    }
}
