using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeDeclarator
    {
        // friend class CParse;
        private List<TypeModifier> mods;
        private Datatype basetype;
        private string ident;           // variable identifier associated with type
        private string model;           // name of model associated with function pointer
        private uint flags;            // Specifiers qualifiers
        
        public TypeDeclarator()
        {
            basetype = (Datatype)null;
            flags = 0;
        }

        private TypeDeclarator(string nm)
        {
            ident=nm;
            basetype=(Datatype)null;
            flags=0;
        }
    
        ~TypeDeclarator()
        {
            for (uint i = 0; i < mods.size(); ++i)
                delete mods[i];
        }

        private Datatype getBaseType() => basetype;

        private int numModifiers() => mods.size();

        private string getIdentifier() => ident;

        private ProtoModel getModel(Architecture glb)
        {
            // Get prototype model
            ProtoModel* protomodel = (ProtoModel)null;
            if (model.size() != 0)
                protomodel = glb.getModel(model);
            if (protomodel == (ProtoModel)null)
                protomodel = glb.defaultfp;
            return protomodel;
        }

        private bool getPrototype(PrototypePieces pieces, Architecture glb)
        {
            TypeModifier* mod = (TypeModifier*)0;
            if (mods.size() > 0)
                mod = mods[0];
            if ((mod == (TypeModifier*)0) || (mod.getType() != TypeModifier::function_mod))
                return false;
            FunctionModifier* fmod = (FunctionModifier*)mod;

            pieces.model = getModel(glb);
            pieces.name = ident;
            pieces.intypes.clear();
            fmod.getInTypes(pieces.intypes, glb);
            pieces.innames.clear();
            fmod.getInNames(pieces.innames);
            pieces.dotdotdot = fmod.isDotdotdot();

            // Construct the output type
            pieces.outtype = basetype;
            List<TypeModifier*>::const_iterator iter;
            iter = mods.end();
            --iter;         // At least one modification
            while (iter != mods.begin())
            { // Do not apply function modifier
                pieces.outtype = (*iter).modType(pieces.outtype, this, glb);
                --iter;
            }
            return true;
        }

        private bool hasProperty(uint mask) => ((flags & mask) != 0);

        private Datatype buildType(Architecture glb)
        { // Apply modifications to the basetype, (in reverse order of binding)
            Datatype* restype = basetype;
            List<TypeModifier*>::const_iterator iter;
            iter = mods.end();
            while (iter != mods.begin())
            {
                --iter;
                restype = (*iter).modType(restype, this, glb);
            }
            return restype;
        }

        private bool isValid()
        {
            if (basetype == (Datatype)null)
                return false;       // No basetype

            int count = 0;
            if ((flags & CParse::f_typedef) != 0)
                count += 1;
            if ((flags & CParse::f_extern) != 0)
                count += 1;
            if ((flags & CParse::f_static) != 0)
                count += 1;
            if ((flags & CParse::f_auto) != 0)
                count += 1;
            if ((flags & CParse::f_register) != 0)
                count += 1;
            if (count > 1)
                throw ParseError("Multiple storage specifiers");

            count = 0;
            if ((flags & CParse::f_const) != 0)
                count += 1;
            if ((flags & CParse::f_restrict) != 0)
                count += 1;
            if ((flags & CParse::f_volatile) != 0)
                count += 1;
            if (count > 1)
                throw ParseError("Multiple type qualifiers");

            for (uint i = 0; i < mods.size(); ++i)
            {
                if (!mods[i].isValid())
                    return false;
            }
            return true;
        }
    }
}
