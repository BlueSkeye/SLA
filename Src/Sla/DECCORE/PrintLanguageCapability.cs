using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Base class for high-level language capabilities
    ///
    /// This class is overridden to introduce a new high-level language back-end
    /// to the system. A static singleton is instantiated to automatically
    /// register the new capability with the system. A static array keeps track of
    /// all the registered capabilities.
    ///
    /// The singleton is registered with a name, which the user can use to select the language, and
    /// it acts as a factory for the main language printing class for the capability,
    /// which must be derived from PrintLanguage.  The factory method for the capability to override
    /// is buildLanguage().
    internal abstract class PrintLanguageCapability
    {
        /// The static array of registered high-level languages
        private static List<PrintLanguageCapability> thelist = new List<PrintLanguageCapability>();

        /// Unique identifier for language capability
        protected string name;
        /// Set to \b true to treat \b this as the default language
        protected bool isdefault;

        ///< Get the high-level language name
        public string getName() => name; 
    
        public virtual void initialize()
        {
            if (isdefault)
                // Default goes at beginning
                thelist.Insert(0, this);
            else
                thelist.Add(this);
        }

        /// \brief Build the main PrintLanguage object corresponding to \b this capability
        ///
        /// An Architecture will call this once. All decompiling from this Architecture will use this same emitter.
        /// \param glb is the Architecture that will own the new emitter
        /// \return the instantiated PrintLanguage emittter
        public abstract PrintLanguage buildLanguage(Architecture glb);

        /// Retrieve the default language capability
        /// This retrieves the capability with its \b isdefault field set or
        /// the first capability registered.
        /// \return the default language capability
        public static PrintLanguageCapability getDefault()
        {
            if (thelist.size() == 0)
                throw new LowlevelError("No print languages registered");
            return thelist[0];
        }

        /// Find a language capability by name
        /// \param name is the language name to search for
        /// \return the matching language capability or NULL
        public static PrintLanguageCapability? findCapability(string name)
        {
            for (int i = 0; i < thelist.size(); ++i) {
                PrintLanguageCapability plc = thelist[i];
                if (plc.getName() == name)
                    return plc;
            }
            return (PrintLanguageCapability)null;
        }
    }
}
