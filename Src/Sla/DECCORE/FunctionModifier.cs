
namespace Sla.DECCORE
{
    internal class FunctionModifier : TypeModifier
    {
        private List<TypeDeclarator> paramlist;
        private bool dotdotdot;
        
        public FunctionModifier(List<TypeDeclarator> p,bool dtdtdt)
        {
            paramlist = p;
            if (paramlist.size() == 1) {
                TypeDeclarator decl = paramlist[0];
                if (decl.numModifiers() == 0) {
                    // Check for void as an inputtype
                    Datatype? ct = decl.getBaseType();
                    if (   (ct != (Datatype)null)
                        && (ct.getMetatype() == type_metatype.TYPE_VOID))
                    {
                        paramlist.Clear();
                    }
                }
            }
            dotdotdot = dtdtdt;
        }

        public void getInTypes(List<Datatype> intypes, Architecture glb)
        {
            for (int i = 0; i < paramlist.size(); ++i) {
                Datatype ct = paramlist[i].buildType(glb) ?? throw new ApplicationException();
                intypes.Add(ct);
            }
        }

        public void getInNames(List<string> innames)
        {
            for (int i = 0; i < paramlist.size(); ++i)
                innames.Add(paramlist[i].getIdentifier());
        }

        public bool isDotdotdot() => dotdotdot;
    
        public override Modifier getType() => Modifier.function_mod;

        public override bool isValid()
        {
            for (int i = 0; i < paramlist.size(); ++i) {
                TypeDeclarator decl = paramlist[i];
                if (!decl.isValid()) return false;
                if (decl.numModifiers() == 0) {
                    Datatype? ct = decl.getBaseType();
                    if (   (ct != (Datatype)null)
                        && (ct.getMetatype() == type_metatype.TYPE_VOID))
                    {
                        // Extra void type
                        return false;
                    }
                }
            }
            return true;
        }

        public override Datatype modType(Datatype? @base, TypeDeclarator decl,
            Architecture glb)
        {
            List<Datatype> intypes = new List<Datatype>();

            // Varargs is encoded as extra null pointer in paramlist
            bool dotdotdot = false;
            if (   !paramlist.empty()
                && (paramlist.GetLastItem() == (TypeDeclarator)null))
            {
                dotdotdot = true;
            }

            getInTypes(intypes, glb);

            ProtoModel protomodel = decl.getModel(glb) ?? throw new ApplicationException();
            return glb.types.getTypeCode(protomodel, @base, intypes, dotdotdot);
        }
    }
}
