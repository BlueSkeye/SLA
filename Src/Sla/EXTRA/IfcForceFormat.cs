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
            Varnode* vn = dcp.readVarnode(s);
            if (!vn.isConstant())
                throw IfaceExecutionError("Can only force format on a constant");
            type_metatype mt = vn.getType().getMetatype();
            if ((mt != TYPE_INT) && (mt != TYPE_UINT) && (mt != TYPE_UNKNOWN))
                throw IfaceExecutionError("Can only force format on integer type constant");
            dcp.fd.buildDynamicSymbol(vn);
            Symbol* sym = vn.getHigh().getSymbol();
            if (sym == (Symbol*)0)
                throw IfaceExecutionError("Unable to create symbol");
            string formatString;
            s >> ws >> formatString;
            uint4 format = Datatype::encodeIntegerFormat(formatString);
            sym.getScope().setDisplayFormat(sym, format);
            sym.getScope().setAttribute(sym, Varnode::typelock);
            *status.optr << "Successfully forced format display" << endl;
        }
    }
}
