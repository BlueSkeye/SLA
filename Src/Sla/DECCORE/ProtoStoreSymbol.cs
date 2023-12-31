﻿using Sla.CORE;
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
                inparam.Add((ProtoParameter)null);
            ParameterSymbol? res = inparam[i] as ParameterSymbol;
            if (res != (ParameterSymbol)null)
                return res;
            //if (inparam[i] != (ProtoParameter)null)
            //    delete inparam[i];
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
            outparam = (ProtoParameter)null;
            ParameterPieces pieces = new ParameterPieces();
            pieces.type = scope.getArch().types.getTypeVoid();
            pieces.flags = 0;
            setOutput(pieces);
        }

        ~ProtoStoreSymbol()
        {
            for (int i = 0; i < inparam.size(); ++i) {
                ProtoParameter param = inparam[i];
                //if (param != (ProtoParameter)null)
                //    delete param;
            }
            //if (outparam != (ProtoParameter)null)
            //    delete outparam;
        }

        public override ProtoParameter setInput(int i, string nm, ParameterPieces pieces)
        {
            ParameterSymbol res = getSymbolBacked(i);
            res.sym = scope.getCategorySymbol(Symbol.SymbolCategory.function_parameter, i);
            SymbolEntry entry;
            Address usepoint = new Address();

            bool isindirect = (pieces.flags & ParameterPieces.Flags.indirectstorage) != 0;
            bool ishidden = (pieces.flags & ParameterPieces.Flags.hiddenretparm) != 0;
            if (res.sym != (Symbol)null) {
                entry = res.sym.getFirstWholeMap();
                if ((entry.getAddr() != pieces.addr) || (entry.getSize() != pieces.type.getSize())) {
                    scope.removeSymbol(res.sym);
                    res.sym = (Symbol)null;
                }
            }
            if (res.sym == (Symbol)null) {
                if (scope.discoverScope(pieces.addr, pieces.type.getSize(), usepoint) == (Scope)null)
                    usepoint = restricted_usepoint;
                res.sym = scope.addSymbol(nm, pieces.type, pieces.addr, usepoint).getSymbol();
                scope.setCategory(res.sym, Symbol.SymbolCategory.function_parameter, i);
                if (isindirect || ishidden) {
                    Varnode.varnode_flags mirror = 0;
                    if (isindirect)
                        mirror |= Varnode.varnode_flags.indirectstorage;
                    if (ishidden)
                        mirror |= Varnode.varnode_flags.hiddenretparm;
                    scope.setAttribute(res.sym, mirror);
                }
                return res;
            }
            if (res.sym.isIndirectStorage() != isindirect) {
                if (isindirect)
                    scope.setAttribute(res.sym, Varnode.varnode_flags.indirectstorage);
                else
                    scope.clearAttribute(res.sym, Varnode.varnode_flags.indirectstorage);
            }
            if (res.sym.isHiddenReturn() != ishidden) {
                if (ishidden)
                    scope.setAttribute(res.sym, Varnode.varnode_flags.hiddenretparm);
                else
                    scope.clearAttribute(res.sym, Varnode.varnode_flags.hiddenretparm);
            }
            if ((nm.Length != 0) && (nm != res.sym.getName()))
                scope.renameSymbol(res.sym, nm);
            if (pieces.type != res.sym.getType())
                scope.retypeSymbol(res.sym, pieces.type);
            return res;
        }

        public override void clearInput(int i)
        {
            Symbol? sym = scope.getCategorySymbol(Symbol.SymbolCategory.function_parameter, i);
            if (sym != (Symbol)null) {
                scope.setCategory(sym, Symbol.SymbolCategory.no_category, 0); // Remove it from category list
                scope.removeSymbol(sym);   // Remove it altogether
            }
            // Renumber any category 0 symbol with index greater than i
            int sz = scope.getCategorySize(Symbol.SymbolCategory.function_parameter);
            for (int j = i + 1; j < sz; ++j) {
                sym = scope.getCategorySymbol(Symbol.SymbolCategory.function_parameter, j);
                if (sym != (Symbol)null)
                    scope.setCategory(sym, Symbol.SymbolCategory.function_parameter, j - 1);
            }
        }

        public override void clearAllInputs()
        {
            scope.clearCategory(0);
        }

        public override int getNumInputs()
        {
            return scope.getCategorySize(Symbol.SymbolCategory.function_parameter);
        }

        public override ProtoParameter getInput(int i)
        {
            Symbol? sym = scope.getCategorySymbol(Symbol.SymbolCategory.function_parameter, i);
            if (sym == (Symbol)null)
                return (ProtoParameter)null;
            ParameterSymbol res = getSymbolBacked(i);
            res.sym = sym;
            return res;
        }

        public override ProtoParameter setOutput(ParameterPieces piece)
        {
            //if (outparam != (ProtoParameter)null)
            //    delete outparam;
            outparam = new ParameterBasic("", piece.addr, piece.type, piece.flags);
            return outparam;
        }

        public override void clearOutput()
        {
            setOutput(new ParameterPieces() {
                type = scope.getArch().types.getTypeVoid(),
                flags = 0
            });
        }

        public override ProtoParameter getOutput()
        {
            return outparam;
        }

        public override ProtoStore clone()
        {
            ProtoStoreSymbol res = new ProtoStoreSymbol(scope, restricted_usepoint);
            // delete res.outparam;
            if (outparam != (ProtoParameter)null)
                res.outparam = outparam.clone();
            else
                res.outparam = (ProtoParameter)null;
            return res;
        }

        public override void encode(Sla.CORE.Encoder encoder)
        {
            // Do not store anything explicitly for a symboltable backed store
            // as the symboltable will be stored separately
        }

        public override void decode(Sla.CORE.Decoder decoder, ProtoModel model)
        {
            throw new LowlevelError("Do not decode symbol-backed prototype through this interface");
        }
    }
}
