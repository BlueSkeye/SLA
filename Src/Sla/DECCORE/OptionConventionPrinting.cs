
namespace Sla.DECCORE
{
    internal class OptionConventionPrinting : ArchOption
    {
        public OptionConventionPrinting()
        {
            name = "conventionprinting";
        }

        /// \class OptionConventionPrinting
        /// \brief Toggle whether the \e calling \e convention is printed when emitting function prototypes
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);
            if (glb.print.getName() != "c-language")
                return "Can only set convention printing for C language";
            PrintC lng = (PrintC)glb.print;
            lng.setConvention(val);
            string prop;
            prop = val ? "on" : "off";
            return "Convention printing turned " + prop;
        }
    }
}
