using ghidra;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

using ParamEntryResolver = Sla.EXTRA.rangemap<Sla.DECCORE.ParamEntryRange>;

namespace Sla.DECCORE
{
    /// \brief A standard model for parameters as an ordered list of storage resources
    ///
    /// This is a configurable model for passing (input) parameters as a list to a function.
    /// The model allows 1 or more resource lists based on data-type, either type_metatype.TYPE_UNKNOWN for
    /// general purpose or type_metatype.TYPE_FLOAT for floating-point registers. Within a resource list,
    /// any number of parameters can be used but they must come starting at the beginning of
    /// the list with no \e holes (skipped resources). A resource list can include (at the end)
    /// \e stack parameters that are allocated based on an alignment.  Optionally, the model supports
    /// converting data-types larger than a specified size to pointers within the parameter list.
    internal class ParamListStandard : ParamList
    {
        /// Number of \e groups in this parameter convention
        protected int numgroup;
        /// Maximum heritage delay across all parameters
        protected int maxdelay;
        /// If non-zero, maximum size of a data-type before converting to a pointer
        protected int pointermax;
        /// Does a \b this parameter come before a hidden return parameter
        protected bool thisbeforeret;
        /// The starting group for each resource section
        protected List<int> resourceStart;
        /// The ordered list of parameter entries
        protected List<ParamEntry> entry;
        /// Map from space id to resolver
        protected List<ParamEntryResolver> resolverMap;
        /// Address space containing relative offset parameters
        protected AddrSpace spacebase;

        /// Given storage location find matching ParamEntry
        protected ParamEntry findEntry(Address loc,int size);

        /// Assign storage for given parameter data-type
        protected Address assignAddress(Datatype tp, List<int> status);

        /// Select entry to fill an unreferenced param
        protected ParamEntry selectUnreferenceEntry(int grp, type_metatype prefType);

        /// Build map from parameter trials to model ParamEntrys
        protected void buildTrialMap(ParamActive active);
        
        protected void separateSections(ParamActive active, List<int> trialStart);
        
        protected static void markGroupNoUse(ParamActive active, int activeTrial, int trialStart);
        
        protected static void markBestInactive(ParamActive active, int group, int groupStart,
            type_metatype prefType);
        
        protected static void forceExclusionGroup(ParamActive active);
        
        protected static void forceNoUse(ParamActive active, int start, int stop);
        
        protected static void forceInactiveChain(ParamActive active, int maxchain, int start,
            int stop, int groupstart);

        /// Calculate the maximum heritage delay for any potential parameter in this list
        protected void calcDelay();
        
        protected void addResolverRange(AddrSpace spc, ulong first, ulong last,
            ParamEntry paramEntry, int position);

        /// Build the ParamEntry resolver maps
        protected void populateResolver();
        
        protected void parsePentry(Decoder decoder, List<EffectRecord> effectlist,
                 int groupid, bool normalstack, bool autokill, bool splitFloat, bool grouped);
        
        protected void parseGroup(Decoder decoder, List<EffectRecord> effectlist,
                int groupid, bool normalstack, bool autokill, bool splitFloat);

        /// Construct for use with decode()
        public ParamListStandard()
        {
        }

        /// Copy constructor
        public ParamListStandard(ParamListStandard op2);

        ~ParamListStandard();

        /// Get the list of parameter entries
        public List<ParamEntry> getEntry() => entry;

        public virtual uint getType() => p_standard;

        public virtual void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res);

        public virtual void fillinMap(ParamActive active);

        public virtual bool checkJoin(Address hiaddr, int hisize, Address loaddr, int losize);

        public virtual bool checkSplit(Address loc, int size, int splitpoint);

        public virtual int characterizeAsParam(Address loc, int size);

        public virtual bool possibleParam(Address loc, int size);

        public virtual bool possibleParamWithSlot(Address loc, int size, int slot, int slotsize);

        public virtual bool getBiggestContainedParam(Address loc, int size, VarnodeData res);

        public virtual bool unjustifiedContainer(Address loc, int size, VarnodeData res);

        public virtual OpCode assumedExtension(Address addr, int size, VarnodeData res);

        public virtual AddrSpace getSpacebase() => spacebase;

        public virtual void getRangeList(AddrSpace spc, RangeList res);

        public virtual int getMaxDelay() => maxdelay;

        public virtual void decode(Sla.CORE.Decoder decoder, List<EffectRecord> effectlist, bool normalstack);

        public virtual ParamList clone();
    }
}
