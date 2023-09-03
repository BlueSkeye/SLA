using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class TypeDeclarator
    {
        // friend class CParse;
        internal List<TypeModifier> mods;
        internal Datatype? basetype;
        // variable identifier associated with type
        private string ident;
        // name of model associated with function pointer
        internal string model;
        // Specifiers qualifiers
        internal CParse.Flags flags;
        
        public TypeDeclarator()
        {
            basetype = (Datatype)null;
            flags = 0;
        }

        internal TypeDeclarator(string nm)
        {
            ident=nm;
            basetype = (Datatype)null;
            flags=0;
        }
    
        ~TypeDeclarator()
        {
            //for (uint i = 0; i < mods.size(); ++i)
            //    delete mods[i];
        }

        internal Datatype? getBaseType() => basetype;

        internal int numModifiers() => mods.size();

        internal string getIdentifier() => ident;

        internal ProtoModel? getModel(Architecture glb)
        {
            // Get prototype model
            ProtoModel? protomodel = (ProtoModel)null;
            if (model.Length != 0)
                protomodel = glb.getModel(model);
            if (protomodel == (ProtoModel)null)
                protomodel = glb.defaultfp;
            return protomodel;
        }

        private bool getPrototype(PrototypePieces pieces, Architecture glb)
        {
            TypeModifier? mod = (TypeModifier)null;
            if (mods.size() > 0)
                mod = mods[0];
            if ((mod == (TypeModifier)null) || (mod.getType() != TypeModifier.Modifier.function_mod))
                return false;
            FunctionModifier fmod = (FunctionModifier)mod;

            pieces.model = getModel(glb);
            pieces.name = ident;
            pieces.intypes.Clear();
            fmod.getInTypes(pieces.intypes, glb);
            pieces.innames.Clear();
            fmod.getInNames(pieces.innames);
            pieces.dotdotdot = fmod.isDotdotdot();

            // Construct the output type
            pieces.outtype = basetype;
            IEnumerator<TypeModifier> iter = mods.GetReverseEnumerator();
            // At least one modification
            if (!iter.MoveNext()) throw new BugException();
            while (iter.MoveNext()) {
                // Do not apply function modifier
                pieces.outtype = iter.Current.modType(pieces.outtype, this, glb);
            }
            return true;
        }

        private bool hasProperty(CParse.Flags mask) => ((flags & mask) != 0);

        internal Datatype? buildType(Architecture glb)
        {
            // Apply modifications to the basetype, (in reverse order of binding)
            Datatype? restype = basetype;
            IEnumerator<TypeModifier> iter = mods.GetReverseEnumerator();
            while (iter.MoveNext()) {
                restype = iter.Current.modType(restype, this, glb);
            }
            return restype;
        }

        internal bool isValid()
        {
            if (basetype == (Datatype)null)
                return false;       // No basetype

            int count = 0;
            if ((flags & CParse.Flags.f_typedef) != 0)
                count += 1;
            if ((flags & CParse.Flags.f_extern) != 0)
                count += 1;
            if ((flags & CParse.Flags.f_static) != 0)
                count += 1;
            if ((flags & CParse.Flags.f_auto) != 0)
                count += 1;
            if ((flags & CParse.Flags.f_register) != 0)
                count += 1;
            if (count > 1)
                throw new ParseError("Multiple storage specifiers");

            count = 0;
            if ((flags & CParse.Flags.f_const) != 0)
                count += 1;
            if ((flags & CParse.Flags.f_restrict) != 0)
                count += 1;
            if ((flags & CParse.Flags.f_volatile) != 0)
                count += 1;
            if (count > 1)
                throw new ParseError("Multiple type qualifiers");

            for (int i = 0; i < mods.size(); ++i) {
                if (!mods[i].isValid())
                    return false;
            }
            return true;
        }
    }
}
