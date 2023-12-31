﻿using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief A container for disassembly context used by the SLEIGH engine
    ///
    /// This acts as a factor for the ParserContext objects which are used to disassemble
    /// a single instruction.  These all share a ContextCache which is a front end for
    /// accessing the ContextDatabase and resolving context variables from the SLEIGH spec.
    /// ParserContext objects are stored in a hash-table keyed by the address of the instruction.
    internal class DisassemblyCache
    {
        private Translate translate;       ///< The Translate object that owns this cache
        private ContextCache contextcache; ///< Cached values from the ContextDatabase
        private AddrSpace constspace;  ///< The constant address space
        private int minimumreuse;      ///< Can call getParserContext this many times, before a ParserContext is reused
        private uint mask;         ///< Size of the hashtable in form 2^n-1
        private ParserContext[] list;       ///< (circular) array of currently cached ParserContext objects
        private int nextfree;      ///< Current end/beginning of circular list
        private ParserContext[] hashtable;  ///< Hashtable for looking up ParserContext via Address

        /// Initialize the hash-table of ParserContexts
        /// \param min is the minimum number of allocations before a reuse is expected
        /// \param hashsize is the number of elements in the hash-table
        private void initialize(int min, int hashsize)
        {
            minimumreuse = min;
            mask = (uint)(hashsize - 1);
            ulong masktest = Globals.coveringmask((ulong)mask);
            if (masktest != (ulong)mask)    // -hashsize- must be a power of 2
                throw new LowlevelError("Bad windowsize for disassembly cache");
            list = new ParserContext[minimumreuse];
            nextfree = 0;
            hashtable = new ParserContext[hashsize];
            for (int i = 0; i < minimumreuse; ++i) {
                ParserContext position = new ParserContext(contextcache, translate);
                position.initialize(75, 20, constspace);
                list[i] = position;
            }
            ParserContext pos = list[0];
            for (int i = 0; i < hashsize; ++i)
                // Make sure all hashtable positions point to a real ParserContext
                hashtable[i] = pos;
        }

        /// Free the hash-table of ParserContexts
        private void free()
        {
            //for (int i = 0; i < minimumreuse; ++i)
            //    delete list[i];
            //delete[] list;
            //delete[] hashtable;
        }

        /// \param trans is the Translate object instantiating this cache (for inst_next2 callbacks)
        /// \param ccache is the ContextCache front-end shared across all the parser contexts
        /// \param cspace is the constant address space used for minting constant Varnodes
        /// \param cachesize is the number of distinct ParserContext objects in this cache
        /// \param windowsize is the size of the ParserContext hash-table
        public DisassemblyCache(Translate trans, ContextCache ccache, AddrSpace cspace, int cachesize,
            int windowsize)
        {
            translate = trans;
            contextcache = ccache;
            constspace = cspace;
            initialize(cachesize, windowsize);      // Set default settings for the cache
        }

        ~DisassemblyCache()
        {
            free();
        }

        /// Get the parser for a particular Address
        /// Return a (possibly cached) ParserContext that is associated with \e addr
        /// If n different calls to this interface are made with n different Addresses, if
        ///    - n <= minimumreuse   AND
        ///    - all the addresses are within the windowsize (=mask+1)
        ///
        /// then the cacher guarantees that you get all different ParserContext objects
        /// \param addr is the Address to disassemble at
        /// \return the ParserContext associated with the address
        public ParserContext getParserContext(Address addr)
        {
            int hashindex = (int)(((int)addr.getOffset()) & mask);
            ParserContext res = hashtable[hashindex];
            if (res.getAddr() == addr)
                return res;
            res = list[nextfree];
            nextfree += 1;      // Advance the circular index
            if (nextfree >= minimumreuse)
                nextfree = 0;
            res.setAddr(addr);
            res.setParserState(ParserContext.State.uninitialized);  // Need to start over with parsing
            hashtable[hashindex] = res; // Stick it into the hashtable
            return res;
        }
    }
}
