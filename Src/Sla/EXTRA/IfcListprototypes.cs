using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcListprototypes : IfaceDecompCommand
    {
        /// \class IfcListprototypes
        /// \brief List known prototype models: `list prototypes`
        ///
        /// All prototype models are listed with markup indicating the
        /// \e default, the evaluation model for the active function, and
        /// the evaluation model for called functions.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null) {
                throw new IfaceExecutionError("No load image present");
            }

            IEnumerator<KeyValuePair<string, ProtoModel>> iter = dcp.conf.protoModels.GetEnumerator();
            while (iter.MoveNext()) {
                ProtoModel model = iter.Current.Value;
                status.optr.Write(model.getName());
                if (model == dcp.conf.defaultfp)
                    status.optr.Write(" default");
                else if (model == dcp.conf.evalfp_called)
                    status.optr.Write(" eval called");
                else if (model == dcp.conf.evalfp_current)
                    status.optr.Write(" eval current");
                status.optr.WriteLine();
            }
        }
    }
}
