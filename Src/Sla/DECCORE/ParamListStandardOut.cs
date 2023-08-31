using Sla.CORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Formats.Tar;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A standard model for returning output parameters from a function
    ///
    /// This has a more involved assignment strategy than its parent class.
    /// Entries in the resource list are treated as a \e group, meaning that only one can
    /// fit the desired storage size and type attributes of the return value. If no entry
    /// fits, the return value is converted to a pointer data-type, storage allocation is
    /// attempted again, and the return value is marked as a \e hidden return parameter
    /// to inform the input model.
    internal class ParamListStandardOut : ParamListRegisterOut
    {
        /// Constructor for use with decode()
        public ParamListStandardOut()
            : base()
        {
        }

        /// Copy constructor
        public ParamListStandardOut(ParamListStandardOut op2)
            : base(op2)
        {
        }
        
        public override uint getType() => p_standard_out;

        public override void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res)
        {
            List<int> status = new List<int>(numgroup);
            ParameterPieces newPiece = new ParameterPieces() {
                type = proto[0],
                flags = 0
            };
            res.Add(newPiece);
            if (proto[0].getMetatype() == type_metatype.TYPE_VOID) {
                // Leave the address as invalid
                return;
            }
            newPiece.addr = assignAddress(proto[0], status);
            if (newPiece.addr.isInvalid()) {
                // Could not assign an address (too big)
                AddrSpace? spc = spacebase;
                if (spc == (AddrSpace)null)
                    spc = typefactory.getArch().getDefaultDataSpace();
                int pointersize = (int)spc.getAddrSize();
                int wordsize = (int)spc.getWordSize();
                Datatype pointertp = typefactory.getTypePointer(pointersize, proto[0],
                    (uint)wordsize);
                newPiece.addr = assignAddress(pointertp, status);
                if (newPiece.addr.isInvalid())
                    throw new ParamUnassignedError("Cannot assign return value as a pointer");
                newPiece.type = pointertp;
                newPiece.flags = ParameterPieces.Flags.indirectstorage;

                newPiece = new ParameterPieces() {
                    // that holds a pointer to where the return value should be stored
                    // leave its address invalid, to be filled in by the input list assignMap
                    type = pointertp,
                    // Mark it as special
                    flags = ParameterPieces.Flags.hiddenretparm
                };
                // Add extra storage location in the input params
                res.Add(newPiece);
            }
        }

        public override void decode(Sla.CORE.Decoder decoder, List<EffectRecord> effectlist,
            bool normalstack)
        {
            base.decode(decoder, effectlist, normalstack);
            // Check for double precision entries
            ParamEntry? previous1 = (ParamEntry)null;
            ParamEntry? previous2 = (ParamEntry)null;
            foreach (ParamEntry curEntry in entry) {
                if (previous1 != (ParamEntry)null) {
                    ParamEntry.orderWithinGroup(previous1, curEntry);
                    if (previous2 != (ParamEntry)null)
                        ParamEntry.orderWithinGroup(previous2, curEntry);
                }
                previous2 = previous1;
                previous1 = curEntry;
            }
        }

        public override ParamList clone()
        {
            ParamList res = new ParamListStandardOut(this);
            return res;
        }
    }
}
