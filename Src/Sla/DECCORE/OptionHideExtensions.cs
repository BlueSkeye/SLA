
namespace Sla.DECCORE
{
    internal class OptionHideExtensions : ArchOption
    {
        public OptionHideExtensions()
        {
            name = "hideextensions";
        }

        /// \class OptionHideExtensions
        /// \brief Toggle whether implied extensions (ZEXT or SEXT) are printed
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);
            PrintC? lng = glb.print as PrintC;
            if (lng == (PrintC)null)
                return "Can only toggle extension hiding for C language";
            lng.setHideImpliedExts(val);
            string prop;
            prop = val ? "on" : "off";
            return "Implied extension hiding turned " + prop;
        }
    }
}
