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
    internal class FunctionModifier : TypeModifier
    {
        private List<TypeDeclarator> paramlist;
        private bool dotdotdot;
        
        public FunctionModifier(List<TypeDeclarator> p,bool dtdtdt)
        {
            paramlist = *p;
            if (paramlist.size() == 1)
            {
                TypeDeclarator* decl = paramlist[0];
                if (decl->numModifiers() == 0)
                { // Check for void as an inputtype
                    Datatype* ct = decl->getBaseType();
                    if ((ct != (Datatype*)0) && (ct->getMetatype() == TYPE_VOID))
                        paramlist.clear();
                }
            }
            dotdotdot = dtdtdt;
        }

        public void getInTypes(List<Datatype> intypes, Architecture glb)
        {
            for (uint4 i = 0; i < paramlist.size(); ++i)
            {
                Datatype* ct = paramlist[i]->buildType(glb);
                intypes.push_back(ct);
            }
        }

        public void getInNames(List<string> innames)
        {
            for (uint4 i = 0; i < paramlist.size(); ++i)
                innames.push_back(paramlist[i]->getIdentifier());
        }

        public bool isDotdotdot() => dotdotdot;
    
        public override uint4 getType() => function_mod;

        public override bool isValid()
        {
            for (uint4 i = 0; i < paramlist.size(); ++i)
            {
                TypeDeclarator* decl = paramlist[i];
                if (!decl->isValid()) return false;
                if (decl->numModifiers() == 0)
                {
                    Datatype* ct = decl->getBaseType();
                    if ((ct != (Datatype*)0) && (ct->getMetatype() == TYPE_VOID))
                        return false;       // Extra void type
                }
            }
            return true;
        }

        public override Datatype modType(Datatype @base, TypeDeclarator decl, Architecture glb)
        {
            vector<Datatype*> intypes;

            // Varargs is encoded as extra null pointer in paramlist
            bool dotdotdot = false;
            if ((!paramlist.empty()) && (paramlist.back() == (TypeDeclarator*)0))
            {
                dotdotdot = true;
            }

            getInTypes(intypes, glb);

            ProtoModel* protomodel = decl->getModel(glb);
            return glb->types->getTypeCode(protomodel, base, intypes, dotdotdot);
        }
    }
}
