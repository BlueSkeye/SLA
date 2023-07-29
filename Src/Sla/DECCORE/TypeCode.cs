using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
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
        /// If non-null, this describes the prototype of the underlying function
        protected FuncProto proto;
        /// Factory owning \b this
        protected TypeFactory factory;

        /// Establish a function pointer
        /// Turn on the data-type's function prototype
        /// \param tfact is the factory that owns \b this
        /// \param model is the prototype model
        /// \param outtype is the return type of the prototype
        /// \param intypes is the list of input parameters
        /// \param dotdotdot is true if the prototype takes variable arguments
        /// \param voidtype is the reference "void" data-type
        protected void setPrototype(TypeFactory tfact, ProtoModel model, Datatype outtype,
            List<Datatype> intypes, bool dotdotdot, Datatype voidtype)
        {
            factory = tfact;
            flags |= variable_length;
            if (proto != (FuncProto*)0)
                delete proto;
            proto = new FuncProto();
            proto->setInternal(model, voidtype);
            vector<Datatype*> typelist;
            vector<string> blanknames(intypes.size()+1);
            if (outtype == (Datatype*)0)
                typelist.push_back(voidtype);
            else
                typelist.push_back(outtype);
            for (int4 i = 0; i < intypes.size(); ++i)
                typelist.push_back(intypes[i]);

            proto->updateAllTypes(blanknames, typelist, dotdotdot);
            proto->setInputLock(true);
            proto->setOutputLock(true);
        }

        /// Set a particular function prototype on \b this
        /// The prototype is copied in.
        /// \param typegrp is the factory owning \b this
        /// \param fp is the prototype to set (may be null)
        protected void setPrototype(TypeFactory typegrp,FuncProto fp)
        {
            if (proto != (FuncProto*)0)
            {
                delete proto;
                proto = (FuncProto*)0;
                factory = (TypeFactory*)0;
            }
            if (fp != (FuncProto*)0) {
                factory = typegrp;
                proto = new FuncProto();
                proto->copy(*fp);
            }
        }

        /// Restore stub of data-type without the full prototype
        /// \param decoder is the stream decoder
        protected void decodeStub(Decoder decoder)
        {
            if (decoder.peekElement() != 0)
            {
                // Traditionally a <prototype> tag implies variable length, without a "varlength" attribute
                flags |= variable_length;
            }
            decodeBasic(decoder);
        }

        /// Restore any prototype description
        /// A single child element indicates a full function prototype.
        /// \param decoder is the stream decoder
        /// \param isConstructor is \b true if the prototype is a constructor
        /// \param isDestructor is \b true if the prototype is a destructor
        /// \param typegrp is the factory owning the code object
        protected void decodePrototype(Decoder decoder, bool isConstructor, bool isDestructor,
            TypeFactory typegrp)
        {
            if (decoder.peekElement() != 0)
            {
                Architecture* glb = typegrp.getArch();
                factory = &typegrp;
                proto = new FuncProto();
                proto->setInternal(glb->defaultfp, typegrp.getTypeVoid());
                proto->decode(decoder, glb);
                proto->setConstructor(isConstructor);
                proto->setDestructor(isDestructor);
            }
            markComplete();
        }

        /// Construct from another TypeCode
        public TypeCode(TypeCode op)
            : base(op)
        {
            proto = (FuncProto*)0;
            factory = op.factory;
            if (op.proto != (FuncProto*)0)
            {
                proto = new FuncProto();
                proto->copy(*op.proto);
            }
        }

        /// Construct an incomplete TypeCode
        public TypeCode()
            : base(1, TYPE_CODE)
        {
            proto = (FuncProto*)0;
            factory = (TypeFactory*)0;
            flags |= type_incomplete;
        }

        /// Compare surface characteristics of two TypeCodes
        /// Compare basic characteristics of \b this with another TypeCode, not including the prototype
        ///    -  -1 or 1 if -this- and -op- are different in surface characteristics
        ///    -   0 if they are exactly equal and have no parameters
        ///    -   2 if they are equal on the surface, but additional comparisons must be made on parameters
        /// \param op is the other data-type to compare to
        /// \return the comparison value
        public int4 compareBasic(TypeCode op)
        {
            if (proto == (FuncProto*)0)
            {
                if (op->proto == (FuncProto*)0) return 0;
                return 1;
            }
            if (op->proto == (FuncProto*)0)
                return -1;

            if (!proto->hasModel())
            {
                if (op->proto->hasModel()) return 1;
            }
            else
            {
                if (!op->proto->hasModel()) return -1;
                string model1 = proto->getModelName());
                string model2 = op->proto->getModelName());
                if (model1 != model2)
                    return (model1 < model2) ? -1 : 1;
            }
            int4 nump = proto->numParams();
            int4 opnump = op->proto->numParams();
            if (nump != opnump)
                return (opnump < nump) ? -1 : 1;
            uint4 myflags = proto->getComparableFlags();
            uint4 opflags = op->proto->getComparableFlags();
            if (myflags != opflags)
                return (myflags < opflags) ? -1 : 1;

            return 2;           // Carry on with comparison of parameters
        }

        /// Get the function prototype
        public FuncProto getPrototype() => proto;

        ~TypeCode()
        {
            if (proto != (FuncProto*)0)
                delete proto;
        }

        public override void printRaw(TextWriter s)
        {
            if (name.size() > 0)
                s << name;
            else
                s << "funcptr";
            s << "()";
        }

        public override Datatype getSubType(uintb off, uintb newoff)
        {
            if (factory == (TypeFactory*)0) return (Datatype*)0;
            *newoff = 0;
            return factory->getBase(1, TYPE_CODE);  // Return code byte unattached to function prototype
        }

        public override int4 compare(Datatype op, int4 level)
        {
            int4 res = Datatype::compare(op, level);
            if (res != 0) return res;
            TypeCode tc = (TypeCode*)&op;
            res = compareBasic(tc);
            if (res != 2) return res;

            level -= 1;
            if (level < 0)
            {
                if (id == op.getId()) return 0;
                return (id < op.getId()) ? -1 : 1;
            }
            int4 nump = proto->numParams();
            for (int4 i = 0; i < nump; ++i)
            {
                Datatype* param = proto->getParam(i)->getType();
                Datatype* opparam = tc->proto->getParam(i)->getType();
                int4 c = param->compare(*opparam, level);
                if (c != 0)
                    return c;
            }
            Datatype* otype = proto->getOutputType();
            Datatype* opotype = tc->proto->getOutputType();
            if (otype == (Datatype*)0)
            {
                if (opotype == (Datatype*)0) return 0;
                return 1;
            }
            if (opotype == (Datatype*)0) return -1;
            return otype->compare(*opotype, level);
        }

        public override int4 compareDependency(Datatype op)
        {
            int4 res = Datatype::compareDependency(op);
            if (res != 0) return res;
            TypeCode tc = (TypeCode*)&op;
            res = compareBasic(tc);
            if (res != 2) return res;

            int4 nump = proto->numParams();
            for (int4 i = 0; i < nump; ++i)
            {
                Datatype* param = proto->getParam(i)->getType();
                Datatype* opparam = tc->proto->getParam(i)->getType();
                if (param != opparam)
                    return (param < opparam) ? -1 : 1; // Compare pointers directly
            }
            Datatype* otype = proto->getOutputType();
            Datatype* opotype = tc->proto->getOutputType();
            if (otype == (Datatype*)0)
            {
                if (opotype == (Datatype*)0) return 0;
                return 1;
            }
            if (opotype == (Datatype*)0) return -1;
            if (otype != opotype)
                return (otype < opotype) ? -1 : 1;
            return 0;
        }

        public override Datatype clone() => new TypeCode(this);

        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype*)0)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ELEM_TYPE);
            encodeBasic(metatype, encoder);
            if (proto != (FuncProto*)0)
                proto->encode(encoder);
            encoder.closeElement(ELEM_TYPE);
        }
    }
}
