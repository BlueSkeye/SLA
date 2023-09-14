using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Choose names for all high-level variables (HighVariables)
    internal class ActionNameVars : Action
    {
        /// This class is a record in a database used to store and lookup potential names
        internal struct OpRecommend
        {
            /// The data-type associated with a name
            internal Datatype ct;
            /// A possible name for a variable
            internal string namerec;
        };

        /// \brief Add a recommendation to the database based on a particular sub-function parameter.
        ///
        /// We know \b vn holds data-flow for parameter \b param,  try to attach its name to \b vn's symbol.
        /// We update map from \b vn to a name recommendation record.
        /// If \b vn is input to multiple functions, the one whose parameter has the most specified type
        /// will be preferred. If \b vn is passed to the function via a cast, this name will only be used
        /// if there is no other function that takes \b vn as a parameter.
        /// \param param is function prototype symbol
        /// \param vn is the Varnode associated with the parameter
        /// \param recmap is the recommendation map
        private static void makeRec(ProtoParameter param, Varnode vn,
            Dictionary<HighVariable, OpRecommend> recmap)
        {
            if (!param.isNameLocked()) return;
            if (param.isNameUndefined()) return;
            if (vn.getSize() != param.getSize()) return;
            Datatype? ct = param.getType();
            if (vn.isImplied() && vn.isWritten()) {
                // Skip any cast into the function
                PcodeOp castop = vn.getDef() ?? throw new ApplicationException();
                if (castop.code() == OpCode.CPUI_CAST) {
                    vn = castop.getIn(0) ?? throw new ApplicationException();
                    // Indicate that this is a less preferred name
                    ct = (Datatype)null;
                }
            }
            HighVariable high = vn.getHigh();
            if (high.isAddrTied())
                // Don't propagate parameter name to address tied variable
                return;
            if (!param.getName().StartsWith("param_")) return;

            OpRecommend recommended;
            if (recmap.TryGetValue(high, out recommended)) {
                // We have seen this varnode before
                if (ct == (Datatype)null)
                    // Cannot override with null (casted) type
                    return;
                Datatype? oldtype = recommended.ct;
                if (oldtype != (Datatype)null) {
                    if (oldtype.typeOrder(ct) <= 0)
                        // oldtype is more specified
                        return;
                }
                recommended.ct = ct;
                recommended.namerec = param.getName();
            }
            else {
                OpRecommend oprec;
                oprec.ct = ct;
                oprec.namerec = param.getName();
                recmap[high] = oprec;
            }
        }

        /// Mark the switch variable for bad jump-tables
        /// Name the Varnode which seems to be the putative switch variable for an
        /// unrecovered jump-table with a special name.
        /// \param data is the function being analyzed
        private static void lookForBadJumpTables(Funcdata data)
        {
            int numfunc = data.numCalls();
            ScopeLocal localmap = data.getScopeLocal();
            for (int i = 0; i < numfunc; ++i) {
                FuncCallSpecs fc = data.getCallSpecs(i);
                if (fc.isBadJumpTable()) {
                    PcodeOp op = fc.getOp();
                    Varnode vn = op.getIn(0) ?? throw new ApplicationException();
                    if (vn.isImplied() && vn.isWritten()) {
                        // Skip any cast into the function
                        PcodeOp castop = vn.getDef() ?? throw new ApplicationException();
                        if (castop.code() == OpCode.CPUI_CAST)
                            vn = castop.getIn(0);
                    }
                    if (vn.isFree()) continue;
                    Symbol sym = vn.getHigh().getSymbol();
                    if (sym == (Symbol)null)
                        continue;
                    if (sym.isNameLocked())
                        // Override any unlocked name
                        continue;
                    if (sym.getScope() != localmap)
                        // Only name this in the local scope
                        continue;
                    string newname = "UNRECOVERED_JUMPTABLE";
                    sym.getScope().renameSymbol(sym, localmap.makeNameUnique(newname));
                }
            }
        }

        /// \brief Collect potential variable names from sub-function parameters.
        ///
        /// Run through all sub-functions with a known prototype and collect potential
        /// names for current Varnodes used to pass the parameters. For these Varnodes,
        /// select from among these names.
        /// \param data is the function being analyzed
        /// \param varlist is a list of Varnodes representing HighVariables that need names
        private static void lookForFuncParamNames(Funcdata data, List<Varnode> varlist)
        {
            int numfunc = data.numCalls();
            if (numfunc == 0) return;
            Dictionary<HighVariable, OpRecommend> recmap = new Dictionary<HighVariable, OpRecommend>();

            ScopeLocal localmap = data.getScopeLocal();
            for (int i = 0; i < numfunc; ++i) {
                // Run through all calls to functions
                FuncCallSpecs fc = data.getCallSpecs(i);
                if (!fc.isInputLocked()) continue;
                PcodeOp op = fc.getOp();
                int numparam = fc.numParams();
                if (numparam >= op.numInput())
                    numparam = op.numInput() - 1;
                for (int j = 0; j < numparam; ++j) {
                    // Looking for a parameter
                    ProtoParameter param = fc.getParam(j);
                    Varnode vn = op.getIn(j + 1);
                    makeRec(param, vn, recmap);
                }
            }
            if (0 == recmap.Count) return;

            Dictionary<HighVariable, OpRecommend>.Enumerator iter;
            for (int i = 0; i < varlist.size(); ++i) {
                // Do the actual naming in the original (address based) order
                Varnode vn = varlist[i];
                if (vn.isFree())
                    continue;
                if (vn.isInput())
                    // Don't override unaffected or input naming strategy
                    continue;
                HighVariable high = vn.getHigh();
                if (high.getNumMergeClasses() > 1)
                    // Don't inherit a name if speculatively merged
                    continue;
                Symbol? sym = high.getSymbol();
                if (sym == (Symbol)null)
                    continue;
                if (!sym.isNameUndefined())
                    continue;
                OpRecommend recommended;
                if (recmap.TryGetValue(high, out recommended)) {
                    sym.getScope().renameSymbol(sym, localmap.makeNameUnique(recommended.namerec));
                }
            }
        }

        /// \brief Link symbols associated with a given \e spacebase Varnode
        ///
        /// Look for PTRSUB ops which indicate a symbol reference within the address space
        /// referred to by the \e spacebase Varnode.  Decode any symbol reference and link it
        /// to the appropriate HighVariable
        /// \param vn is the given \e spacebase Varnode
        /// \param data is the function containing the Varnode
        /// \param namerec is used to store any recovered Symbol without a name
        private static void linkSpacebaseSymbol(Varnode vn, Funcdata data, List<Varnode> namerec)
        {
            if (!vn.isConstant() && !vn.isInput()) return;
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.code() != OpCode.CPUI_PTRSUB) continue;
                Varnode offVn = op.getIn(1) ?? throw new ApplicationException();
                Symbol? sym = data.linkSymbolReference(offVn);
                if ((sym != (Symbol)null) && sym.isNameUndefined())
                    namerec.Add(offVn);
            }
        }

        /// \brief Link formal Symbols to their HighVariable representative in the given Function
        ///
        /// Run through all HighVariables for the given function and set up the explicit mapping with
        /// existing Symbol objects.  If there is no matching Symbol for a given HighVariable, a new
        /// Symbol is created. Any Symbol that does not have a name is added to a list for further
        /// name resolution.
        /// \param data is the given function
        /// \param namerec is the container for collecting Symbols with a name
        private static void linkSymbols(Funcdata data, List<Varnode> namerec)
        {
            AddrSpaceManager manage = data.getArch();
            AddrSpace constSpace = manage.getConstantSpace();
            IEnumerator<Varnode> iter = data.beginLoc(constSpace);
            IEnumerator<Varnode> enditer = data.endLoc(constSpace);
            while (iter.MoveNext()) {
                Varnode curvn = iter.Current;
                if (curvn.getSymbolEntry() != (SymbolEntry)null)
                    // Special equate symbol
                    data.linkSymbol(curvn);
                else if (curvn.isSpacebase())
                    linkSpacebaseSymbol(curvn, data, namerec);
            }

            for (int i = 0; i < manage.numSpaces(); ++i) {
                // Build a list of nameable highs
                AddrSpace? spc = manage.getSpace(i);
                if (spc == (AddrSpace)null) continue;
                if (spc == constSpace) continue;
                iter = data.beginLoc(spc);
                // enditer = data.endLoc(spc);
                while (iter.MoveNext()) {
                    Varnode curvn = iter.Current;
                    if (curvn.isFree()) {
                        continue;
                    }
                    if (curvn.isSpacebase())
                        linkSpacebaseSymbol(curvn, data, namerec);
                    Varnode vn = curvn.getHigh().getNameRepresentative();
                    if (vn != curvn) continue; // Hit each high only once
                    HighVariable high = vn.getHigh();
                    if (!high.hasName()) continue;
                    Symbol sym = data.linkSymbol(vn);
                    if (sym != (Symbol)null) {
                        // Can we associate high with a nameable symbol
                        if (sym.isNameUndefined() && high.getSymbolOffset() < 0)
                            // Add if no name, and we have a high representing the whole
                            namerec.Add(vn);
                        if (sym.isSizeTypeLocked()) {
                            if (vn.getSize() == sym.getType().getSize())
                                sym.getScope().overrideSizeLockType(sym, high.getType());
                        }
                    }
                }
            }
        }

        public ActionNameVars(string g)
            : base(ruleflags.rule_onceperfunc, "namevars", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionNameVars(getGroup());
        }

        public override int apply(Funcdata data)
        {
            List<Varnode> namerec = new List<Varnode>();

            linkSymbols(data, namerec);
            // Make sure recommended names hit before subfunc
            data.getScopeLocal().recoverNameRecommendationsForSymbols();
            lookForBadJumpTables(data);
            lookForFuncParamNames(data, namerec);

            int @base = 1;
            for (int i = 0; i < namerec.size(); ++i) {
                Varnode vn = namerec[i];
                Symbol sym = vn.getHigh().getSymbol() ?? throw new ApplicationException();
                if (sym.isNameUndefined()) {
                    Scope scope = sym.getScope();
                    string newname = scope.buildDefaultName(sym, @base, vn);
                    scope.renameSymbol(sym, newname);
                }
            }
            data.getScopeLocal().assignDefaultNames(@base);
            return 0;
        }
    }
}
