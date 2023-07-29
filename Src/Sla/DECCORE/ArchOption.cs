using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Base class for options classes that affect the configuration of the Architecture object
    ///
    /// Each class instance affects configuration through its apply() method, which is handed the
    /// Architecture object to be configured along with string based parameters. The apply() methods
    /// are run once during initialization of the Architecture object.
    internal abstract class ArchOption
    {
        protected string name;      ///< Name of the option
        
        public string getName() => name; ///< Return the name of the option

        /// \brief Apply a particular configuration option to the Architecture
        ///
        /// This method is overloaded by the different Option classes to provide possible configuration
        /// of different parts of the Architecture. The user can provide up to three optional parameters
        /// to tailor a specific type of configuration. The method returns a confirmation/failure message
        /// as feedback.
        /// \param glb is the Architecture being configured
        /// \param p1 is the first optional configuration string
        /// \param p2 is the second optional configuration string
        /// \param p3 is the third optional configuration string
        /// \return a confirmation/failure message
        public abstract string apply(Architecture glb, string p1, string p2, string p3);
    
        ~ArchOption()
        {
        }

        /// Parse an "on" or "off" string
        /// If the parameter is "on" return \b true, if "off" return \b false.
        /// Any other value causes an exception.
        /// \param p is the parameter
        /// \return the parsed boolean value
        public static bool onOrOff(string p)
        {
            if (p.size() == 0)
                return true;
            if (p == "on")
                return true;
            if (p == "off")
                return false;
            throw ParseError("Must specify toggle value, on/off");
        }
    }
}
