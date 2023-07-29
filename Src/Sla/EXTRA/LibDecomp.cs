using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal static class LibDecomp
    {
        internal static void startDecompilerLibrary(string sleighhome)
        {
            AttributeId::initialize();
            ElementId::initialize();
            CapabilityPoint::initializeAll();
            ArchitectureCapability::sortCapabilities();

            if (sleighhome != null)
                SleighArchitecture::scanForSleighDirectories(sleighhome);
        }

        internal static void startDecompilerLibrary(List<string> extrapaths)
        {
            AttributeId::initialize();
            ElementId::initialize();
            CapabilityPoint::initializeAll();
            ArchitectureCapability::sortCapabilities();

            for (uint4 i = 0; i < extrapaths.size(); ++i)
                SleighArchitecture::specpaths.addDir2Path(extrapaths[i]);
        }

        internal static void startDecompilerLibrary(string sleighhome, List<string> extrapaths)
        {
            AttributeId::initialize();
            ElementId::initialize();
            CapabilityPoint::initializeAll();
            ArchitectureCapability::sortCapabilities();

            if (sleighhome != null)
                SleighArchitecture::scanForSleighDirectories(sleighhome);

            for (uint4 i = 0; i < extrapaths.size(); ++i)
                SleighArchitecture::specpaths.addDir2Path(extrapaths[i]);
        }

        internal static void shutdownDecompilerLibrary()
        {
        }
    }
}
