using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for automatically registering extension points to the decompiler
    /// This uses the C++ static initializer feature to automatically \e discover
    /// and register extension point. Code for an extension should provide
    /// a class that derives from CapabilityPoint and overrides the initialize() method.
    /// Additionally there should be a singleton static instantiation of this extension class.
    /// The extensions are accumulated in a list automatically, then the decompiler engine
    /// will ensure that the initialize() method is called on each extension, allowing it
    /// to complete its integration.
    internal abstract class CapabilityPoint
    {
        // This gets allocated exactly once on first call
        private static List<CapabilityPoint> thelist = new List<CapabilityPoint>();

        /// Retrieve the list of extension point singletons
        /// Access static List of CapabilityPoint objects that are registered during static initialization
        /// The list itself is created once on the first call to this method
        /// \e after all the static initializers have run
        /// \return the list of registered extensions
        private static List<CapabilityPoint> getList()
        {
            return thelist;
        }

        /// Construct extension capability exactly once
        /// Constructing the object automatically registers it.
        /// For global instances, this happens during static initialization
        protected CapabilityPoint()
        {
            getList().Add(this);
        }

        /// Destructor
        ~CapabilityPoint()
        {
        }

        /// \brief Complete initialization of an extension point
        /// This method is implemented by each extension so it can do specialized integration
        public abstract void initialize();

        /// Finish initialization for all extension points
        /// Give all registered capabilities a chance to initialize (\e after all static initialization has happened)
        public static void initializeAll()
        {
            List<CapabilityPoint> list = getList();
            for (int i = 0; i < list.Count; ++i) {
                CapabilityPoint ptr = list[i];
                ptr.initialize();
            }
            list.Clear();
        }
    }
}
