
namespace Sla.EXTRA
{
    internal class IfcClosefile : IfaceBaseCommand
    {
        /// \class IfcClosefile
        /// \brief Close command, closing the current bulk output file.
        ///
        /// Subsequent bulk output is redirected to the basic interface output stream
        public override void execute(TextReader s)
        {
            if (status.optr == status.fileoptr)
                throw new IfaceExecutionError("No file open");
            ((StreamWriter)status.fileoptr).Close();
            // delete status.fileoptr;
            status.fileoptr = status.optr;
        }
    }
}
