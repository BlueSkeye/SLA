using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A collection of parameter descriptions backed by Symbol information
    ///
    /// Input parameters are determined by symbols a function Scope
    /// (category 0).  Information about the return-value is stored internally.
    /// ProtoParameter objects are constructed on the fly as requested and cached.
    internal class ProtoStoreSymbol : ProtoStore
    {
        /// Backing Scope for input parameters
        private Scope scope;
        /// A usepoint reference for storage locations (usually function entry -1)
        private Address restricted_usepoint;
        /// Cache of allocated input parameters
        private List<ProtoParameter> inparam;
        /// The return-value parameter
        private ProtoParameter outparam;

        /// Fetch or allocate the parameter for the indicated slot
        /// Retrieve the specified ProtoParameter object, making sure it is a ParameterSymbol.
        /// If it doesn't exist, or if the object in the specific slot is not a ParameterSymbol,
        /// allocate an (uninitialized) parameter.
        /// \param i is the specified input slot
        /// \return the corresponding parameter
        private ParameterSymbol getSymbolBacked(int i)
        {
            while (inparam.size() <= i)
                inparam.Add((ProtoParameter*)0);
            ParameterSymbol* res = dynamic_cast<ParameterSymbol*>(inparam[i]);
            if (res != (ParameterSymbol*)0)
                return res;
            if (inparam[i] != (ProtoParameter*)0)
                delete inparam[i];
            res = new ParameterSymbol();
            inparam[i] = res;
            return res;
        }

        /// \param sc is the function Scope that will back \b this store
        /// \param usepoint is the starting address of the function (-1)
        public ProtoStoreSymbol(Scope sc, Address usepoint)
        {
            scope = sc;
            restricted_usepoint = usepoint;
            outparam = (ProtoParameter*)0;
            ParameterPieces pieces;
            pieces.type = scope.getArch().types.getTypeVoid();
            pieces.flags = 0;
            ProtoStoreSymbol::setOutput(pieces);
        }

        ~ProtoStoreSymbol()
        {
            for (int i = 0; i < inparam.size(); ++i)
            {
                ProtoParameter* param = inparam[i];
                if (param != (ProtoParameter*)0)
                    delete param;
            }
            if (outparam != (ProtoParameter*)0)
                delete outparam;
        }

        public override ProtoParameter setInput(int i, string nm, ParameterPieces pieces)
        {
            ParameterSymbol* res = getSymbolBacked(i);
            res.sym = scope.getCategorySymbol(Symbol::function_parameter, i);
            SymbolEntry* entry;
            Address usepoint;

            bool isindirect = (pieces.flags & ParameterPieces::indirectstorage) != 0;
            bool ishidden = (pieces.flags & ParameterPieces::hiddenretparm) != 0;
            if (res.sym != (Symbol)null)
            {
                entry = res.sym.getFirstWholeMap();
                if ((entry.getAddr() != pieces.addr) || (entry.getSize() != pieces.type.getSize()))
                {
                    scope.removeSymbol(res.sym);
                    res.sym = (Symbol)null;
                }
            }
            if (res.sym == (Symbol)null)
            {
                if (scope.discoverScope(pieces.addr, pieces.type.getSize(), usepoint) == (Scope)null)
                    usepoint = restricted_usepoint;
                res.sym = scope.addSymbol(nm, pieces.type, pieces.addr, usepoint).getSymbol();
                scope.setCategory(res.sym, Symbol::function_parameter, i);
                if (isindirect || ishidden)
                {
                    uint mirror = 0;
                    if (isindirect)
                        mirror |= Varnode::indirectstorage;
                    if (ishidden)
                        mirror |= Varnode::hiddenretparm;
                    scope.setAttribute(res.sym, mirror);
                }
                return res;
            }
            if (res.sym.isIndirectStorage() != isindirect)
            {
                if (isindirect)
                    scope.setAttribute(res.sym, Varnode::indirectstorage);
                else
                    scope.clearAttribute(res.sym, Varnode::indirectstorage);
            }
            if (res.sym.isHiddenReturn() != ishidden)
            {
                if (ishidden)
                    scope.setAttribute(res.sym, Varnode::hiddenretparm);
                else
                    scope.clearAttribute(res.sym, Varnode::hiddenretparm);
            }
            if ((nm.size() != 0) && (nm != res.sym.getName()))
                scope.renameSymbol(res.sym, nm);
            if (pieces.type != res.sym.getType())
                scope.retypeSymbol(res.sym, pieces.type);
            return res;
        }

        public override void clearInput(int i)
        {
            Symbol* sym = scope.getCategorySymbol(Symbol::function_parameter, i);
            if (sym != (Symbol)null)
            {
                scope.setCategory(sym, Symbol::no_category, 0); // Remove it from category list
                scope.removeSymbol(sym);   // Remove it altogether
            }
            // Renumber any category 0 symbol with index greater than i
            int sz = scope.getCategorySize(Symbol::function_parameter);
            for (int j = i + 1; j < sz; ++j)
            {
                sym = scope.getCategorySymbol(Symbol::function_parameter, j);
                if (sym != (Symbol)null)
                    scope.setCategory(sym, Symbol::function_parameter, j - 1);
            }
        }

        public override void clearAllInputs()
        {
            scope.clearCategory(0);
        }

        public override int getNumInputs()
        {
            return scope.getCategorySize(Symbol::function_parameter);
        }

        public override ProtoParameter getInput(int i)
        {
            Symbol* sym = scope.getCategorySymbol(Symbol::function_parameter, i);
            if (sym == (Symbol)null)
                return (ProtoParameter*)0;
            ParameterSymbol* res = getSymbolBacked(i);
            res.sym = sym;
            return res;
        }

        public override ProtoParameter setOutput(ParameterPieces piece)
        {
            if (outparam != (ProtoParameter*)0)
                delete outparam;
            outparam = new ParameterBasic("", piece.addr, piece.type, piece.flags);
            return outparam;
        }

        public override void clearOutput()
        {
            ParameterPieces pieces;
            pieces.type = scope.getArch().types.getTypeVoid();
            pieces.flags = 0;
            setOutput(pieces);
        }

        public override ProtoParameter getOutput()
        {
            return outparam;
        }

        public override ProtoStore clone()
        {
            ProtoStoreSymbol* res;
            res = new ProtoStoreSymbol(scope, restricted_usepoint);
            delete res.outparam;
            if (outparam != (ProtoParameter*)0)
                res.outparam = outparam.clone();
            else
                res.outparam = (ProtoParameter*)0;
            return res;
        }

        public override void encode(Encoder encoder)
        { // Do not store anything explicitly for a symboltable backed store
          // as the symboltable will be stored separately
        }

        public override void decode(Decoder decoder, ProtoModel model)
        {
            throw new LowlevelError("Do not decode symbol-backed prototype through this interface");
        }
    }
}
