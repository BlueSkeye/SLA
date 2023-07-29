using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A circular buffer template
    ///
    /// A circular buffer implementation that can act as a stack: push(), pop().
    /// Or it can act as a queue: push(), popbottom().  The size of the buffer can be expanded
    /// on the fly using expand(). The object being buffered must support a void constructor and
    /// the assignment operator.  Objects can also be looked up via an integer reference.
    internal class circularqueue<_type>
    {
        /// An array of the template object
        private _type cache;
        /// Index within the array of the leftmost object in the queue
        private int left;
        /// Index within the array of the rightmost object in the queue
        private int right;
        /// Size of the array
        private int max;

        /// Construct queue of a given size
        public circularqueue(int sz)
        {
            max = sz;
            left = 1;           // Set queue to be empty
            right = 0;
            cache = new _type[sz];
        }

        ~circularqueue()
        {
            delete[] cache;
        }

        /// Establish a new maximum queue size
        /// This destroys the old queue and reallocates a new queue with the given maximum size
        /// \param sz the maximum size of the new queue
        public void setMax(int sz)
        {
            if (max != sz)
            {
                delete[] cache;
                max = sz;
                cache = new _type[sz];
            }
            left = 1;           // This operation empties queue
            right = 0;
        }

        /// Get the maximum queue size
        public int getMax() => max;

        /// Expand the (maximum) size of the queue
        /// Expand the maximum size of \b this queue.  Objects currently in the queue
        /// are preserved, which involves copying the objects. This routine invalidates
        /// references referring to objects currently in the queue, although the references
        /// can be systematically adjusted to be valid again.
        /// \param amount is the number of additional objects the resized queue will support
        public void expand(int amount)
        {
            _type* newcache = new _type[max + amount];

            int i = left;
            int j = 0;

            // Assume there is at least one element in queue
            while (i != right)
            {
                newcache[j++] = cache[i];
                i = (i + 1) % max;
            }
            newcache[j] = cache[i]; // Copy rightmost
            left = 0;
            right = j;

            delete[] cache;
            cache = newcache;
            max += amount;
        }

        /// Clear the queue
        public void clear()
        {
            left = 1; right = 0;
        }

        /// Is the queue empty
        public bool empty() => (left == (right+1)%max);

        /// Get a reference to the last object on the queue/stack
        public int topref() => right;

        /// Get a reference to the first object on the queue/stack
        public int bottomref() => left;

        /// Retrieve an object by its reference
        public _type @ref(int r) => cache[r];

        /// Get the last object on the queue/stack
        public _type top() => cache[right];

        /// Get the first object on the queue/stack
        public _type bottom() => cache[left];

        /// Push a new object onto the queue/stack
        public _type push()
        {
            right = (right + 1) % max;
            return cache[right];
        }

        /// Pop the (last) object on the stack
        public _type pop()
        {
            int tmp = right;
            right = (right + max - 1) % max;
            return cache[tmp];
        }

        /// Get the (next) object in the queue
        public _type popbottom()
        {
            int tmp = left;
            left = (left + 1) % max;
            return cache[tmp];
        }
    }
}
