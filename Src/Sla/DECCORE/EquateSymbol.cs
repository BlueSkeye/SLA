using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Sla.DECCORE
{
    /// \brief A Symbol that holds \b equate information for a constant
    /// This is a symbol that labels a constant. It can either replace the
    /// constant's token with the symbol name, or it can force a conversion in
    /// the emitted format of the constant.
    internal class EquateSymbol : Symbol
    {
        ///< Value of the constant being equated
        private ulong value;

        /// Constructor
        /// Create a symbol either to associate a name with a constant or to force a display conversion
        /// \param sc is the scope owning the new symbol
        /// \param nm is the name of the equate (an empty string can be used for a convert)
        /// \param format is the desired display conversion (0 for no conversion)
        /// \param val is the constant value whose display is being altered
        public EquateSymbol(Scope sc, string nm, uint format, ulong val)
            : base(sc, nm, null)
        {
            value = val;
            category = equate;
            type = sc.getArch().types.getBase(1, type_metatype.TYPE_UNKNOWN);
            dispflags |= format;
        }

        /// Constructor for use with decode
        public EquateSymbol(Scope sc)
            : base(sc)
        {
            value = 0;
            category = equate;
        }
        
        public ulong getValue() => value; ///< Get the constant value

        /// Is the given value similar to \b this equate
        /// An EquateSymbol should survive certain kinds of transforms during decompilation,
        /// such as negation, twos-complementing, adding or subtracting 1.
        /// Return \b true if the given value looks like a transform of this type relative
        /// to the underlying value of \b this equate.
        /// \param op2Value is the given value
        /// \param size is the number of bytes of precision
        /// \return \b true if it is a transformed form
        public bool isValueClose(ulong op2Value, int size)
        {
            if (value == op2Value) return true;
            ulong mask = Globals.calc_mask(size);
            ulong maskValue = value & mask;
            if (maskValue != value)
            {       // If '1' bits are getting masked off
                    // Make sure only sign-extension is getting masked off
                if (value != Globals.sign_extend(maskValue, size, sizeof(ulong)))
                    return false;
            }
            if (maskValue == (op2Value & mask)) return true;
            if (maskValue == (~op2Value & mask)) return true;
            if (maskValue == (-op2Value & mask)) return true;
            if (maskValue == ((op2Value + 1) & mask)) return true;
            if (maskValue == ((op2Value - 1) & mask)) return true;
            return false;
        }

        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_EQUATESYMBOL);
            encodeHeader(encoder);
            encoder.openElement(ELEM_VALUE);
            encoder.writeUnsignedInteger(ATTRIB_CONTENT, value);
            encoder.closeElement(ELEM_VALUE);
            encoder.closeElement(ELEM_EQUATESYMBOL);
        }

        public override void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_EQUATESYMBOL);
            decodeHeader(decoder);

            uint subId = decoder.openElement(ElementId.ELEM_VALUE);
            value = decoder.readUnsignedInteger(ATTRIB_CONTENT);
            decoder.closeElement(subId);

            TypeFactory* types = scope.getArch().types;
            type = types.getBase(1, type_metatype.TYPE_UNKNOWN);
            decoder.closeElement(elemId);
        }
    }
}
