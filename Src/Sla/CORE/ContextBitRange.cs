using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief Description of a context variable within the disassembly context \e blob
    /// Disassembly context is stored as individual (integer) values packed into a sequence of words. This class
    /// represents the info for encoding or decoding a single value within this sequence.  A value is
    /// a contiguous range of bits within one context word. Size can range from 1 bit up to the size of a word.
    public class ContextBitRange
    {
        private int word;      ///< Index of word containing this context value
        private int startbit;  ///< Starting bit of the value within its word (0=most significant bit 1=least significant)
        private int endbit;        ///< Ending bit of the value within its word
        private int shift;     ///< Right-shift amount to apply when unpacking this value from its word
        private uint mask;     ///< Mask to apply (after shifting) when unpacking this value from its word

        ///< Construct an undefined bit range
        public ContextBitRange()
        {
        }

        ///< Construct a context value given an absolute bit range
        /// Bits within the whole context blob are labeled starting with 0 as the most significant bit
        /// in the first word in the sequence. The new context value must be contained within a single
        /// word.
        /// \param sbit is the starting (most significant) bit of the new value
        /// \param ebit is the ending (least significant) bit of the new value
        public ContextBitRange(int sbit, int ebit)
        {
            word = sbit / (8 * sizeof(uint));
            startbit = sbit - word * 8 * sizeof(uint);
            endbit = ebit - word * 8 * sizeof(uint);
            shift = 8 * sizeof(uint) - endbit - 1;
            mask = (~((uint)0)) >> (startbit + shift);
        }

        ///< Return the shift-amount for \b this value
        public int getShift()
        {
            return shift;
        }

        ///< Return the mask for \b this value
        public uint getMask()
        {
            return mask;
        }

        ///< Return the word index for \b this value
        public int getWord()
        {
            return word;
        }

        /// \brief Set \b this value within a given context blob
        /// \param vec is the given context blob to alter (as an array of uint words)
        /// \param val is the integer value to set
        public void setValue(uint[] vec, uint val)
        {
            uint newval = vec[word];
            newval &= ~(mask << shift);
            newval |= ((val & mask) << shift);
            vec[word] = newval;
        }

        /// \brief Retrieve \b this value from a given context blob
        /// \param vec is the given context blob (as an array of uint words)
        /// \return the recovered integer value
        public uint getValue(uint[] vec)
        {
            return ((vec[word] >> shift) & mask);
        }
    }
}
