
namespace Sla.DECCORE
{
    internal class PointerModifier : TypeModifier
    {
        private uint flags;
        
        public PointerModifier(uint fl)
        {
            flags = fl;
        }
        
        public override Modifier getType() => Modifier.pointer_mod;

        public override bool isValid() => true;

        public override Datatype modType(Datatype? @base, TypeDeclarator decl, Architecture glb)
        {
            int addrsize = (int)glb.getDefaultDataSpace().getAddrSize();
            Datatype restype = glb.types.getTypePointer(addrsize, @base,
                glb.getDefaultDataSpace().getWordSize());
            return restype;
        }
    }
}
