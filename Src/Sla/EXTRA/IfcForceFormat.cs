using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcForceFormat : IfaceDecompCommand
    {
        /// \class IfcForceFormat
        /// \brief Mark a constant to be printed in a specific format: `force varnode <varnode> [hex|dec|oct|bin|char]`
        ///
        /// A constant Varnode in the \e current function is marked so that is forced
        /// to print in one of the formats: \b hex, \b dec, \b oct, \b bin, \b char.
        public override void execute(TextReader s)
        {
            Varnode vn = dcp.readVarnode(s);
            if (!vn.isConstant())
                throw new IfaceExecutionError("Can only force format on a constant");
            type_metatype mt = vn.getType().getMetatype();
            if ((mt != type_metatype.TYPE_INT) && (mt != type_metatype.TYPE_UINT) && (mt != type_metatype.TYPE_UNKNOWN))
                throw new IfaceExecutionError("Can only force format on integer type constant");
            dcp.fd.buildDynamicSymbol(vn);
            Symbol* sym = vn.getHigh().getSymbol();
            if (sym == (Symbol)null)
                throw new IfaceExecutionError("Unable to create symbol");
            string formatString;
            s.ReadSpaces() >> formatString;
            uint format = Datatype.encodeIntegerFormat(formatString);
            sym.getScope().setDisplayFormat(sym, format);
            sym.getScope().setAttribute(sym, Varnode.varnode_flags.typelock);
            *status.optr << "Successfully forced format display" << endl;
        }
    }
}
