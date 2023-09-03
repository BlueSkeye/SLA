using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Datatype object representing executable code.
    ///
    /// Sometimes, this holds the "function" being pointed to by a function pointer
    internal class TypeCode : Datatype
    {
        // friend class TypeFactory;
        // If non-null, this describes the prototype of the underlying function
        internal FuncProto? proto;
        // Factory owning \b this
        protected TypeFactory? factory;

        /// Establish a function pointer
        /// Turn on the data-type's function prototype
        /// \param tfact is the factory that owns \b this
        /// \param model is the prototype model
        /// \param outtype is the return type of the prototype
        /// \param intypes is the list of input parameters
        /// \param dotdotdot is true if the prototype takes variable arguments
        /// \param voidtype is the reference "void" data-type
        internal void setPrototype(TypeFactory tfact, ProtoModel model, Datatype? outtype,
            List<Datatype> intypes, bool dotdotdot, Datatype voidtype)
        {
            factory = tfact;
            flags |= Properties.variable_length;
            //if (proto != (FuncProto)null)
            //    delete proto;
            proto = new FuncProto();
            proto.setInternal(model, voidtype);
            List<Datatype> typelist = new List<Datatype>();
            List<string> blanknames = new List<string>(intypes.Count+1);
            if (outtype == (Datatype)null)
                typelist.Add(voidtype);
            else
                typelist.Add(outtype);
            for (int i = 0; i < intypes.Count; ++i)
                typelist.Add(intypes[i]);

            proto.updateAllTypes(blanknames, typelist, dotdotdot);
            proto.setInputLock(true);
            proto.setOutputLock(true);
        }

        /// Set a particular function prototype on \b this
        /// The prototype is copied @in.
        /// \param typegrp is the factory owning \b this
        /// \param fp is the prototype to set (may be null)
        internal void setPrototype(TypeFactory typegrp, FuncProto fp)
        {
            if (proto != (FuncProto)null) {
                // delete proto;
                proto = (FuncProto)null;
                factory = (TypeFactory)null;
            }
            if (fp != (FuncProto)null) {
                factory = typegrp;
                proto = new FuncProto();
                proto.copy(fp);
            }
        }

        /// Restore stub of data-type without the full prototype
        /// \param decoder is the stream decoder
        internal void decodeStub(Sla.CORE.Decoder decoder)
        {
            if (decoder.peekElement() != 0) {
                // Traditionally a <prototype> tag implies variable length, without a "varlength" attribute
                flags |= Properties.variable_length;
            }
            decodeBasic(decoder);
        }

        /// Restore any prototype description
        /// A single child element indicates a full function prototype.
        /// \param decoder is the stream decoder
        /// \param isConstructor is \b true if the prototype is a constructor
        /// \param isDestructor is \b true if the prototype is a destructor
        /// \param typegrp is the factory owning the code object
        internal void decodePrototype(Decoder decoder, bool isConstructor, bool isDestructor,
            TypeFactory typegrp)
        {
            if (decoder.peekElement() != 0) {
                Architecture glb = typegrp.getArch();
                factory = typegrp;
                proto = new FuncProto();
                proto.setInternal(glb.defaultfp, typegrp.getTypeVoid());
                proto.decode(decoder, glb);
                proto.setConstructor(isConstructor);
                proto.setDestructor(isDestructor);
            }
            markComplete();
        }

        /// Construct from another TypeCode
        public TypeCode(TypeCode op)
            : base(op)
        {
            proto = (FuncProto)null;
            factory = op.factory;
            if (op.proto != (FuncProto)null) {
                proto = new FuncProto();
                proto.copy(op.proto);
            }
        }

        /// Construct an incomplete TypeCode
        public TypeCode()
            : base(1, type_metatype.TYPE_CODE)
        {
            proto = (FuncProto)null;
            factory = (TypeFactory)null;
            flags |= Properties.type_incomplete;
        }

        /// Compare surface characteristics of two TypeCodes
        /// Compare basic characteristics of \b this with another TypeCode, not including the prototype
        ///    -  -1 or 1 if -this- and -op- are different in surface characteristics
        ///    -   0 if they are exactly equal and have no parameters
        ///    -   2 if they are equal on the surface, but additional comparisons must be made on parameters
        /// \param op is the other data-type to compare to
        /// \return the comparison value
        public int compareBasic(TypeCode op)
        {
            if (proto == (FuncProto)null) {
                if (op.proto == (FuncProto)null) return 0;
                return 1;
            }
            if (op.proto == (FuncProto)null)
                return -1;

            if (!proto.hasModel()) {
                if (op.proto.hasModel()) return 1;
            }
            else {
                if (!op.proto.hasModel()) return -1;
                string model1 = proto.getModelName();
                string model2 = op.proto.getModelName();
                if (model1 != model2)
                    return string.Compare(model1, model2);
            }
            int nump = proto.numParams();
            int opnump = op.proto.numParams();
            if (nump != opnump)
                return (opnump < nump) ? -1 : 1;
            uint myflags = proto.getComparableFlags();
            uint opflags = op.proto.getComparableFlags();
            if (myflags != opflags)
                return (myflags < opflags) ? -1 : 1;

            return 2;           // Carry on with comparison of parameters
        }

        /// Get the function prototype
        public FuncProto getPrototype() => proto;

        ~TypeCode()
        {
            //if (proto != (FuncProto)null)
            //    delete proto;
        }

        public override void printRaw(TextWriter s)
        {
            if (name.Length > 0)
                s.Write(name);
            else
                s.Write("funcptr");
            s.Write("()");
        }

        public override Datatype getSubType(ulong off, out ulong newoff)
        {
            newoff = 0;
            if (factory == (TypeFactory)null)
                return (Datatype)null;
            // Return code byte unattached to function prototype
            return factory.getBase(1, type_metatype.TYPE_CODE);
        }

        public override int compare(Datatype op, int level)
        {
            int res = base.compare(op, level);
            if (res != 0) return res;
            TypeCode tc = (TypeCode)op;
            res = compareBasic(tc);
            if (res != 2) return res;

            level -= 1;
            if (level < 0) {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            int nump = proto.numParams();
            for (int i = 0; i < nump; ++i) {
                Datatype param = proto.getParam(i).getType();
                Datatype opparam = tc.proto.getParam(i).getType();
                int c = param.compare(opparam, level);
                if (c != 0)
                    return c;
            }
            Datatype? otype = proto.getOutputType();
            Datatype opotype = tc.proto.getOutputType();
            if (otype == (Datatype)null) {
                if (opotype == (Datatype)null) return 0;
                return 1;
            }
            if (opotype == (Datatype)null) return -1;
            return otype.compare(opotype, level);
        }

        public override int compareDependency(Datatype op)
        {
            int res = base.compareDependency(op);
            if (res != 0) return res;
            TypeCode tc = (TypeCode)op;
            res = compareBasic(tc);
            if (res != 2) return res;

            int nump = proto.numParams();
            for (int i = 0; i < nump; ++i) {
                Datatype param = proto.getParam(i).getType();
                Datatype opparam = tc.proto.getParam(i).getType();
                if (param != opparam)
                    // Compare pointers directly
                    return param.CompareTo(opparam);
            }
            Datatype otype = proto.getOutputType();
            Datatype opotype = tc.proto.getOutputType();
            if (otype == (Datatype)null) {
                if (opotype == (Datatype)null) return 0;
                return 1;
            }
            if (opotype == (Datatype)null) return -1;
            return object.ReferenceEquals(otype, opotype) ? 0 : otype.CompareTo(opotype);
        }

        internal override Datatype clone() => new TypeCode(this);

        public override void encode(Sla.CORE.Encoder encoder)
        {
            if (typedefImm != (Datatype)null) {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_TYPE);
            encodeBasic(metatype, encoder);
            if (proto != (FuncProto)null)
                proto.encode(encoder);
            encoder.closeElement(ElementId.ELEM_TYPE);
        }
    }
}
