using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A jump-table execution model
    ///
    /// This class holds details of the model and recovers these details in various stages.
    /// The model concepts include:
    ///   - Address Table, the set of destination addresses the jump-table can produce.
    ///   - Normalized Switch Variable, the Varnode with the most restricted set of values used
    ///       by the model to produce the destination addresses.
    ///   - Unnormalized Switch Variable, the Varnode being switched on, as seen in the decompiler output.
    ///   - Case labels, switch variable values associated with specific destination addresses.
    ///   - Guards, CBRANCH ops that enforce the normalized switch variable's value range.
    internal abstract class JumpModel
    {
        /// The jump-table that is building \b this model
        protected JumpTable jumptable;

        /// Construct given a parent jump-table
        public JumpModel(JumpTable jt)
        {
            jumptable = jt;
        }
        
        ~JumpModel()
        {
        }

        /// Return \b true if \b this model was manually overridden
        public abstract bool isOverride();

        /// Return the number of entries in the address table
        public abstract int getTableSize();

        /// \brief Attempt to recover details of the model, given a specific BRANCHIND
        /// This generally recovers the normalized switch variable and any guards.
        /// \param fd is the function containing the switch
        /// \param indop is the given BRANCHIND
        /// \param matchsize is the expected number of address table entries to recover, or 0 for no expectation
        /// \param maxtablesize is maximum number of address table entries to allow in the model
        /// \return \b true if details of the model were successfully recovered
        public abstract bool recoverModel(Funcdata fd, PcodeOp indop, uint matchsize,
            uint maxtablesize);

        /// \brief Construct the explicit list of target addresses (the Address Table) from \b this model
        /// The addresses produced all come from the BRANCHIND and may not be deduped. Alternate guard
        /// destinations are not yet included.
        /// \param fd is the function containing the switch
        /// \param indop is the root BRANCHIND of the switch
        /// \param addresstable will hold the list of Addresses
        /// \param loadpoints if non-null will hold LOAD table information used by the model
        public abstract void buildAddresses(Funcdata fd, PcodeOp indop, List<Address> addresstable,
            List<LoadTable> loadpoints);

        /// \brief Recover the unnormalized switch variable
        /// The normalized switch variable must already be recovered. The amount of normalization between
        /// the two switch variables can be restricted.
        /// \param maxaddsub is a restriction on arithmetic operations
        /// \param maxleftright is a restriction on shift operations
        /// \param maxext is a restriction on extension operations
        public abstract void findUnnormalized(uint maxaddsub, uint maxleftright, uint maxext);

        /// \brief Recover \e case labels associated with the Address table
        /// The unnormalized switch variable must already be recovered.  Values that the normalized
        /// switch value can hold or walked back to obtain the value that the unnormalized switch
        /// variable would hold. Labels are returned in the order provided by normalized switch
        /// variable iterator JumpValues.
        /// \param fd is the function containing the switch
        /// \param addresstable is the address table (used to label code blocks with bad or missing labels)
        /// \param label will hold recovered labels in JumpValues order
        /// \param orig is the JumpModel to use for the JumpValues iterator
        public abstract void buildLabels(Funcdata fd, List<Address> addresstable, List<ulong> label,
            JumpModel orig);

        /// \brief Do normalization of the given switch specific to \b this model.
        /// The PcodeOp machinery is removed so it looks like the OpCode.CPUI_BRANCHIND simply takes the
        /// switch variable as an input Varnode and automatically interprets its values to reach
        /// the correct destination.
        /// \param fd is the function containing the switch
        /// \param indop is the given switch as a OpCode.CPUI_BRANCHIND
        /// \return the Varnode holding the final unnormalized switch variable
        public abstract Varnode foldInNormalization(Funcdata fd, PcodeOp indop);

        /// \brief Eliminate any \e guard code involved in computing the switch destination
        /// We now think of the BRANCHIND as encompassing any guard function.
        /// \param fd is the function containing the switch
        /// \param jump is the JumpTable owning \b this model.
        public abstract bool foldInGuards(Funcdata fd, JumpTable jump);

        /// \brief Perform a sanity check on recovered addresses
        /// Individual addresses are checked against the function or its program to determine
        /// if they are reasonable. This method can optionally remove addresses from the table.
        /// If it does so, the underlying model is changed to reflect the removal.
        /// \param fd is the function containing the switch
        /// \param indop is the root BRANCHIND of the switch
        /// \param addresstable is the list of recovered Addresses, which may be modified
        /// \return \b true if there are (at least some) reasonable addresses in the table
        public abstract bool sanityCheck(Funcdata fd, PcodeOp indop, List<Address> addresstable);

        /// Clone \b this model
        public abstract JumpModel clone(JumpTable jt);

        /// Clear any non-permanent aspects of the model
        public virtual void clear()
        {
        }

        /// Encode this model to a stream
        public virtual void encode(Sla.CORE.Encoder encoder)
        {
        }

        /// Decode \b this model from a stream
        public virtual void decode(Sla.CORE.Decoder decoder)
        {
        }
    }
}
