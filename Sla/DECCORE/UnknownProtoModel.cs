using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief An unrecognized prototype model
    ///
    /// This kind of model is created for function prototypes that specify a model name for which
    /// there is no matching object.  A model is created for the name by cloning behavior from a
    /// placeholder model, usually the \e default model.  This object mostly behaves like its placeholder
    /// model but can identify itself as an \e unknown model and adopts the unrecognized model name.
    internal class UnknownProtoModel : ProtoModel
    {
        /// The model whose behavior \b this adopts as a behavior placeholder
        private ProtoModel placeholderModel;
        
        public UnknownProtoModel(string nm, ProtoModel placeHold)
            : base(nm, placeHold)
        {
            placeholderModel = placeHold;
        }

        /// Retrieve the placeholder model
        public ProtoModel getPlaceholderModel() => placeholderModel;
    
        public override bool isUnknown() => true;
    }
}
