
namespace Sla.EXTRA
{
    /// \brief A root class for a basic set of commands
    /// Commands derived from this class are in the "base" module.
    /// They are useful as part of any interface
    internal abstract class IfaceBaseCommand : IfaceCommand
    {
        // The interface owning this command instance
        protected IfaceStatus status;
        
        public override void setData(IfaceStatus root, IfaceData data)
        {
            status = root;
        }
        
        public override string getModule() => "base";

        public override IfaceData? createData()
        {
            return (IfaceData)null;
        }
    }
}
