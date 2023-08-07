using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief SLEIGH specification compiling
    ///
    /// Class for parsing SLEIGH specifications (.slaspec files) and producing the
    /// \e compiled form (.sla file), which can then be loaded by a SLEIGH disassembly
    /// and p-code generation engine.  This full parser contains the p-code parser SleighPcode
    /// within it.  The main entry point is run_compilation(), which takes the input and output
    /// file paths as parameters.  Various options and preprocessor macros can be set using the
    /// various set*() methods prior to calling run_compilation.
    internal class SleighCompile : SleighBase
    {
        // friend class SleighPcode;
        public SleighPcode pcode;            ///< The p-code parsing (sub)engine

        private Dictionary<string, string> preproc_defines;  ///< Defines for the preprocessor
        private List<FieldContext> contexttable;  ///< Context field definitions (prior to defining ContextField and ContextSymbol)
        private List<ConstructTpl> macrotable;   ///< SLEIGH macro definitions
        private List<Token> tokentable;      ///< SLEIGH token definitions
        private List<SubtableSymbol> tables; ///< SLEIGH subtables
        private List<SectionSymbol> sections;    ///< Symbols defining Constructor sections
        private List<WithBlock> withstack;      ///< Current stack of \b with blocks
        private Constructor curct;         ///< Current Constructor being defined
        private MacroSymbol curmacro;      ///< Current macro being defined
        private bool contextlock;           ///< If the context layout has been established yet
        private List<string> relpath;     ///< Relative path (to cwd) for each filename
        private List<string> filename;        ///< Stack of current files being parsed
        private List<int> lineno;            ///< Current line number for each file in stack
        private Dictionary<Constructor, Location> ctorLocationMap;        ///< Map each Constructor to its defining parse location
        private Dictionary<SleighSymbol, Location> symbolLocationMap; ///< Map each symbol to its defining parse location
        private int userop_count;          ///< Number of userops defined
        private bool warnunnecessarypcode;      ///< \b true if we warn of unnecessary ZEXT or SEXT
        private bool warndeadtemps;         ///< \b true if we warn of temporaries that are written but not read
        private bool lenientconflicterrors;     ///< \b true if we ignore most pattern conflict errors
        private bool largetemporarywarning;     ///< \b true if we warn about temporaries larger than SleighBase::MAX_UNIQUE_SIZE
        private bool warnalllocalcollisions;        ///< \b true if local export collisions generate individual warnings
        private bool warnallnops;           ///< \b true if pcode NOPs generate individual warnings
        private bool failinsensitivedups;       ///< \b true if case insensitive register duplicates cause error
        private List<string> noplist;     ///< List of individual NOP warnings
        private /*mutable*/ Location currentLocCache;   ///< Location for (last) request of current location
        private int errors;              ///< Number of fatal errors encountered

        ///< Get the current file and line number being parsed
        /// The current filename and line number are placed into a Location object
        /// which is then returned.
        /// \return the current Location
        private Location getCurrentLocation()
        {
            // Update the location cache field
            currentLocCache = Location(filename.GetLastItem(), lineno.GetLastItem());
            return &currentLocCache;
        }

        ///< Get SLEIGHs predefined address spaces and symbols
        /// Create the address spaces: \b const, \b unique, and \b other.
        /// Define the special symbols: \b inst_start, \b inst_next, \b inst_next2, \b epsilon.
        /// Define the root subtable symbol: \b instruction
        private void predefinedSymbols()
        {
            symtab.addScope();      // Create global scope

            // Some predefined symbols
            root = new SubtableSymbol("instruction"); // Base constructors
            symtab.addSymbol(root);
            insertSpace(new ConstantSpace(this, this));
            SpaceSymbol* spacesym = new SpaceSymbol(getConstantSpace()); // Constant space
            symtab.addSymbol(spacesym);
            OtherSpace* otherSpace = new OtherSpace(this, this, OtherSpace::INDEX);
            insertSpace(otherSpace);
            spacesym = new SpaceSymbol(otherSpace);
            symtab.addSymbol(spacesym);
            insertSpace(new UniqueSpace(this, this, numSpaces(), 0));
            spacesym = new SpaceSymbol(getUniqueSpace()); // Temporary register space
            symtab.addSymbol(spacesym);
            StartSymbol* startsym = new StartSymbol("inst_start", getConstantSpace());
            symtab.addSymbol(startsym);
            EndSymbol* endsym = new EndSymbol("inst_next", getConstantSpace());
            symtab.addSymbol(endsym);
            Next2Symbol* next2sym = new Next2Symbol("inst_next2", getConstantSpace());
            symtab.addSymbol(next2sym);
            EpsilonSymbol* epsilon = new EpsilonSymbol("epsilon", getConstantSpace());
            symtab.addSymbol(epsilon);
            pcode.setConstantSpace(getConstantSpace());
            pcode.setUniqueSpace(getUniqueSpace());
        }

        /// \brief Calculate the complete context layout for all definitions sharing the same backing storage Varnode
        ///
        /// Internally context is stored in an array of (32-bit) words.  The bit-range for each field definition is
        /// adjusted to pack the fields within this array, but overlapping bit-ranges between definitions are preserved.
        /// Due to the internal storage word size, the covering range across a set of overlapping definitions cannot
        /// exceed the word size (of 32-bits).
        /// Within the sorted list of all context definitions, the subset sharing the same backing storage is
        /// provided to this method as a starting index and a size (of the subset), along with the total number
        /// of context bits already allocated.
        /// \param start is the provided starting index of the definition subset
        /// \param sz is the provided number of definitions in the subset
        /// \param numbits is the number of previously allocated context bits
        /// \return the total number of allocated bits (after the new allocations)
        private int calcContextVarLayout(int start, int sz, int numbits)
        {
            VarnodeSymbol* sym = contexttable[start].sym;
            FieldQuality* qual;
            int i, j;
            int maxbits;

            if ((sym.getSize()) % 4 != 0)
                reportError(getCurrentLocation(), "Invalid size of context register '" + sym.getName() + "': must be a multiple of 4 bytes");
            maxbits = sym.getSize() * 8 - 1;
            i = 0;
            while (i < sz)
            {

                qual = contexttable[i + start].qual;
                int min = qual.low;
                int max = qual.high;
                if ((max - min) > (8 * sizeof(uint)))
                    reportError(getCurrentLocation(), "Size of bitfield '" + qual.name + "' larger than 32 bits");
                if (max > maxbits)
                    reportError(getCurrentLocation(), "Scope of bitfield '" + qual.name + "' extends beyond the size of context register");
                j = i + 1;
                // Find union of fields overlapping with first field
                while (j < sz)
                {
                    qual = contexttable[j + start].qual;
                    if (qual.low <= max)
                    {   // We have overlap of context variables
                        if (qual.high > max)
                            max = qual.high;
                        // reportWarning("Local context variables overlap in "+sym.getName(),false);
                    }
                    else
                        break;
                    j = j + 1;
                }

                int alloc = max - min + 1;
                int startword = numbits / (8 * sizeof(uint));
                int endword = (numbits + alloc - 1) / (8 * sizeof(uint));
                if (startword != endword)
                    numbits = endword * (8 * sizeof(uint)); // Bump up to next word

                uint low = numbits;
                numbits += alloc;

                for (; i < j; ++i)
                {
                    qual = contexttable[i + start].qual;
                    uint l = qual.low - min + low;
                    uint h = numbits - 1 - (max - qual.high);
                    ContextField* field = new ContextField(qual.signext, l, h);
                    addSymbol(new ContextSymbol(qual.name, field, sym, qual.low, qual.high, qual.flow));
                }

            }
            sym.markAsContext();
            return numbits;
        }

        ///< Build decision trees for all subtables
        /// A separate decision tree is calculated for each subtable, and information about
        /// conflicting patterns is accumulated.  Identical pattern pairs are reported
        /// as errors, and indistinguishable pattern pairs are reported as errors depending
        /// on the \b lenientconflicterrors setting.
        private void buildDecisionTrees()
        {
            DecisionProperties props;
            root.buildDecisionTree(props);

            for (int i = 0; i < tables.size(); ++i)
                tables[i].buildDecisionTree(props);

            List<pair<Constructor, Constructor>> ierrors = props.getIdentErrors();
            if (ierrors.size() != 0)
            {
                string identMsg = "Constructor has identical pattern to constructor at ";
                for (int i = 0; i < ierrors.size(); ++i)
                {
                    errors += 1;
                    Location locA = getLocation(ierrors[i].first);
                    Location locB = getLocation(ierrors[i].second);
                    reportError(locA, identMsg + locB.format());
                    reportError(locB, identMsg + locA.format());
                }
            }

            List<pair<Constructor, Constructor>> cerrors = props.getConflictErrors();
            if (!lenientconflicterrors && cerrors.size() != 0)
            {
                string conflictMsg = "Constructor pattern cannot be distinguished from constructor at ";
                for (int i = 0; i < cerrors.size(); ++i)
                {
                    errors += 1;
                    Location locA = getLocation(cerrors[i].first);
                    Location locB = getLocation(cerrors[i].second);
                    reportError(locA, conflictMsg + locB.format());
                    reportError(locB, conflictMsg + locA.format());
                }
            }
        }

        ///< Generate final match patterns based on parse constraint equations
        /// For each Constructor, generate the final pattern (TokenPattern) used to match it from
        /// the parsed constraints (PatternEquation).  Accumulated error messages are reported.
        private void buildPatterns()
        {
            if (root == 0)
            {
                reportError((Location*)0, "No patterns to match.");
                return;
            }
            ostringstream msg;
            root.buildPattern(msg);    // This should recursively hit everything
            if (root.isError())
            {
                reportError(getLocation(root), msg.str());
                errors += 1;
            }
            for (int i = 0; i < tables.size(); ++i)
            {
                if (tables[i].isError())
                {
                    reportError(getLocation(tables[i]), "Problem in table '" + tables[i].getName() + "':" + msg.str());
                    errors += 1;
                }
                if (tables[i].getPattern() == (TokenPattern)null)
                {
                    reportWarning(getLocation(tables[i]), "Unreferenced table '" + tables[i].getName() + "'");
                }
            }
        }

        ///< Perform final consistency checks on the SLEIGH definitions
        /// Optimization is performed across all p-code sections.  Size restriction and other consistency
        /// checks are performed.  Errors and warnings are reported as appropriate.
        private void checkConsistency()
        {
            ConsistencyChecker checker(this, root, warnunnecessarypcode, warndeadtemps, largetemporarywarning);

            if (!checker.testSizeRestrictions())
            {
                errors += 1;
                return;
            }
            if (!checker.testTruncations())
            {
                errors += 1;
                return;
            }
            if ((!warnunnecessarypcode) && (checker.getNumUnnecessaryPcode() > 0))
            {
                ostringstream msg;
                msg << dec << checker.getNumUnnecessaryPcode();
                msg << " unnecessary extensions/truncations were converted to copies";
                reportWarning(msg.str());
                reportWarning("Use -u switch to list each individually");
            }
            checker.optimizeAll();
            if (checker.getNumReadNoWrite() > 0)
            {
                errors += 1;
                return;
            }
            if ((!warndeadtemps) && (checker.getNumWriteNoRead() > 0))
            {
                ostringstream msg;
                msg << dec << checker.getNumWriteNoRead();
                msg << " operations wrote to temporaries that were not read";
                reportWarning(msg.str());
                reportWarning("Use -t switch to list each individually");
            }
            checker.testLargeTemporary();
            if ((!largetemporarywarning) && (checker.getNumLargeTemporaries() > 0))
            {
                ostringstream msg;
                msg << dec << checker.getNumLargeTemporaries();
                msg << " constructors contain temporaries larger than ";
                msg << SleighBase::MAX_UNIQUE_SIZE << " bytes";
                reportWarning(msg.str());
                reportWarning("Use -o switch to list each individually.");
            }
        }

        /// \brief Search for offset matches between a previous set and the given current set
        ///
        /// This method is given a collection of offsets, each mapped to a particular set index.
        /// A new set of offsets and set index is given.  The new set is added to the collection.
        /// If any offset in the new set matches an offset in one of the old sets, the old matching
        /// set index is returned. Otherwise -1 is returned.
        /// \param local2Operand is the collection of previous offsets
        /// \param locals is the new given set of offsets
        /// \param operand is the new given set index
        /// \return the set index of an old matching offset or -1
        private static int findCollision(Dictionary<ulong, int> local2Operand, List<ulong> locals, int operand)
        {
            for (int i = 0; i < locals.size(); ++i)
            {
                pair<Dictionary<ulong, int>::iterator, bool> res;
                res = local2Operand.insert(pair<ulong, int>(locals[i], operand));
                if (!res.second)
                {
                    int oldIndex = (*res.first).second;
                    if (oldIndex != operand)
                        return oldIndex;
                }
            }
            return -1;
        }

        ///< Check for operands that \e might export the same local variable
        /// Because local variables can be exported and subtable symbols can be reused as operands across
        /// multiple Constructors, its possible for different operands in the same Constructor to be assigned
        /// the same exported local variable. As this is a potential spec design problem, this method searches
        /// for these collisions and potentially reports a warning.
        /// For each operand of the given Constructor, the potential local variable exports are collected and
        /// compared with the other operands.  Any potential collision may generate a warning and causes
        /// \b false to be returned.
        /// \param ct is the given Constructor
        /// \return \b true if there are no potential collisions between operands
        private bool checkLocalExports(Constructor ct)
        {
            if (ct.getTempl() == (ConstructTpl)null)
                return true;        // No template, collisions impossible
            if (ct.getTempl().buildOnly())
                return true;        // Operand exports aren't manipulated, so no collision is possible
            if (ct.getNumOperands() < 2)
                return true;        // Collision can only happen with multiple operands
            bool noCollisions = true;
            Dictionary<ulong, int> collect;
            for (int i = 0; i < ct.getNumOperands(); ++i)
            {
                List<ulong> newCollect;
                ct.getOperand(i).collectLocalValues(newCollect);
                if (newCollect.empty()) continue;
                int collideOperand = findCollision(collect, newCollect, i);
                if (collideOperand >= 0)
                {
                    noCollisions = false;
                    if (warnalllocalcollisions)
                    {
                        reportWarning(getLocation(ct), "Possible operand collision between symbols '"
                                  + ct.getOperand(collideOperand).getName()
                                  + "' and '"
                                  + ct.getOperand(i).getName() + "'");
                    }
                    break;  // Don't continue
                }
            }
            return noCollisions;
        }

        ///< Check all Constructors for local export collisions between operands
        /// Check each Constructor for collisions in turn.  If there are any collisions
        /// report a warning indicating the number of Construtors with collisions. Optionally
        /// generate a warning for each colliding Constructor.
        private void checkLocalCollisions()
        {
            int collisionCount = 0;
            SubtableSymbol* sym = root; // Start with the instruction table
            int i = -1;
            for (; ; )
            {
                int numconst = sym.getNumConstructors();
                for (int j = 0; j < numconst; ++j)
                {
                    if (!checkLocalExports(sym.getConstructor(j)))
                        collisionCount += 1;
                }
                i += 1;
                if (i >= tables.size()) break;
                sym = tables[i];
            }
            if (collisionCount > 0)
            {
                ostringstream msg;
                msg << dec << collisionCount << " constructors with local collisions between operands";
                reportWarning(msg.str());
                if (!warnalllocalcollisions)
                    reportWarning("Use -c switch to list each individually");
            }
        }

        ///< Report on all Constructors with empty semantic sections
        /// The number of \e empty Constructors, with no p-code and no export, is always reported.
        /// Optionally, empty Constructors are reported individually.
        private void checkNops()
        {
            if (noplist.size() > 0)
            {
                if (warnallnops)
                {
                    for (int i = 0; i < noplist.size(); ++i)
                        reportWarning(noplist[i]);
                }
                ostringstream msg;
                msg << dec << noplist.size() << " NOP constructors found";
                reportWarning(msg.str());
                if (!warnallnops)
                    reportWarning("Use -n switch to list each individually");
            }
        }

        ///< Check that register names can be treated as case insensitive
        /// Treating names as case insensitive, look for duplicate register names and
        /// report as errors.  For this method, \e register means any global Varnode defined
        /// using SLEIGH's `define <address space>` directive, in an address space of
        /// type \e spacetype.IPTR_PROCESSOR  (either RAM or REGISTER)
        private void checkCaseSensitivity()
        {
            if (!failinsensitivedups) return;       // Case insensitive duplicates don't cause error
            Dictionary<string, SleighSymbol*> registerMap;
            SymbolScope* scope = symtab.getGlobalScope();
            SymbolTree::const_iterator iter;
            for (iter = scope.begin(); iter != scope.end(); ++iter)
            {
                SleighSymbol* sym = *iter;
                if (sym.getType() != SleighSymbol::varnode_symbol) continue;
                VarnodeSymbol* vsym = (VarnodeSymbol*)sym;
                AddrSpace* space = vsym.getFixedVarnode().space;
                if (space.getType() != spacetype.IPTR_PROCESSOR) continue;
                string nm = sym.getName();
                transform(nm.begin(), nm.end(), nm.begin(), ::toupper);
                pair<Dictionary<string, SleighSymbol*>::iterator, bool> check;
                check = registerMap.insert(pair<string, SleighSymbol*>(nm, sym));
                if (!check.second)
                {   // Name already existed
                    SleighSymbol* oldsym = (*check.first).second;
                    ostringstream s;
                    s << "Name collision: " << sym.getName() << " --- ";
                    s << "Duplicate symbol " << oldsym.getName();
                    Location oldLocation = getLocation(oldsym);
                    if (oldLocation != (Location*)0x0)
                    {
                        s << " defined at " << oldLocation.format();
                    }
                    Location location = getLocation(sym);
                    reportError(location, s.str());
                }
            }
        }

        ///< Make sure label symbols are both defined and used
        /// Each label symbol define which operator is being labeled and must also be
        /// used as a jump destination by at least 1 operator. A description of each
        /// symbol violating this is accumulated in a string returned by this method.
        /// \param scope is the scope across which to look for label symbols
        /// \return the accumulated error messages
        private string checkSymbols(SymbolScope scope)
        {
            ostringstream msg;
            SymbolTree::const_iterator iter;
            for (iter = scope.begin(); iter != scope.end(); ++iter)
            {
                LabelSymbol* sym = (LabelSymbol*)*iter;
                if (sym.getType() != SleighSymbol::label_symbol) continue;
                if (sym.getRefCount() == 0)
                    msg << "   Label <" << sym.getName() << "> was placed but not used" << endl;
                else if (!sym.isPlaced())
                    msg << "   Label <" << sym.getName() << "> was referenced but never placed" << endl;
            }
            return msg.str();
        }

        ///< Add a new symbol to the current scope
        /// The symbol definition is assumed to have just been parsed.  It is added to the
        /// table within the current scope as determined by the parse state and is cross
        /// referenced with the current parse location.
        /// Duplicate symbol exceptions are caught and reported as a parse error.
        /// \param sym is the new symbol
        private void addSymbol(SleighSymbol sym)
        {
            try
            {
                symtab.addSymbol(sym);
                symbolLocationMap[sym] = *getCurrentLocation();
            }
            catch (SleighError err) {
                reportError(err.ToString());
            }
        }

        ///< Deduplicate the given list of symbols
        /// Find duplicates in the list and null out any entry but the first.
        /// Return an example of a symbol with duplicates or null if there are
        /// no duplicates.
        /// \param symlist is the given list of symbols (which may contain nulls)
        /// \return an example symbol with a duplicate are null
        private SleighSymbol dedupSymbolList(List<SleighSymbol> symlist)
        {
            SleighSymbol* res = (SleighSymbol)null;
            for (int i = 0; i < symlist.size(); ++i)
            {
                SleighSymbol* sym = (*symlist)[i];
                if (sym == (SleighSymbol)null) continue;
                for (int j = i + 1; j < symlist.size(); ++j)
                {
                    if ((*symlist)[j] == sym)
                    { // Found a duplicate
                        res = sym;      // Return example duplicate for error reporting
                        (*symlist)[j] = (SleighSymbol)null; // Null out the duplicate
                    }
                }
            }
            return res;
        }

        ///< Expand any formal SLEIGH macros in the given section of p-code
        /// Run through the section looking for MACRO directives.  The directive includes an
        /// id for a specific macro in the table.  Using the MacroBuilder class each directive
        /// is replaced with new sequence of OpTpls that tailors the macro with parameters
        /// in its invocation. Any errors encountered during expansion are reported.
        /// Other OpTpls in the section are unchanged.
        /// \param ctpl is the given section of p-code to expand
        /// \return \b true if there were no errors expanding a macro
        private bool expandMacros(ConstructTpl ctpl)
        {
            List<OpTpl*> newvec;
            List<OpTpl*>::const_iterator iter;
            OpTpl* op;

            for (iter = ctpl.getOpvec().begin(); iter != ctpl.getOpvec().end(); ++iter)
            {
                op = *iter;
                if (op.getOpcode() == MACROBUILD)
                {
                    MacroBuilder builder(this, newvec, ctpl.numLabels());
                    int index = op.getIn(0).getOffset().getReal();
                    if (index >= macrotable.size())
                        return false;
                    builder.setMacroOp(op);
                    ConstructTpl* macro_tpl = macrotable[index];
                    builder.build(macro_tpl, -1);
                    ctpl.setNumLabels(ctpl.numLabels() + macro_tpl.numLabels());
                    delete op;      // Throw away the place holder op
                    if (builder.hasError())
                        return false;
                }
                else
                    newvec.Add(op);
            }
            ctpl.setOpvec(newvec);
            return true;
        }

        ///< Do final checks, expansions, and linking for p-code sections
        /// For each p-code section of the given Constructor:
        ///   - Expand macros
        ///   - Check that labels are both defined and referenced
        ///   - Generate BUILD directives for subtable operands
        ///   - Propagate Varnode sizes throughout the section
        ///
        /// Each action may generate errors or warnings.
        /// \param big is the given Constructor
        /// \param vec is the list of p-code sections
        /// \return \b true if there were no fatal errors
        private bool finalizeSections(Constructor big, SectionVector vec)
        {
            List<string> errors;

            RtlPair cur = vec.getMainPair();
            int i = -1;
            string sectionstring = "   Main section: ";
            int max = vec.getMaxId();
            for (; ; )
            {
                string errstring;

                errstring = checkSymbols(cur.scope); // Check labels in the section's scope
                if (errstring.size() != 0)
                {
                    errors.Add(sectionstring + errstring);
                }
                else
                {
                    if (!expandMacros(cur.section))
                        errors.Add(sectionstring + "Could not expand macros");
                    List<int> check;
                    big.markSubtableOperands(check);
                    int res = cur.section.fillinBuild(check, getConstantSpace());
                    if (res == 1)
                        errors.Add(sectionstring + "Duplicate BUILD statements");
                    if (res == 2)
                        errors.Add(sectionstring + "Unnecessary BUILD statements");

                    if (!PcodeCompile::propagateSize(cur.section))
                        errors.Add(sectionstring + "Could not resolve at least 1 variable size");
                }
                if (i < 0)
                {       // These potential errors only apply to main section
                    if (cur.section.getResult() != (HandleTpl)null)
                    {   // If there is an export statement
                        if (big.getParent() == root)
                            errors.Add("   Cannot have export statement in root constructor");
                        else if (!forceExportSize(cur.section))
                            errors.Add("   Size of export is unknown");
                    }
                }
                if (cur.section.delaySlot() != 0)
                { // Delay slot is present in this constructor
                    if (root != big.getParent())
                    { // it is not in a root constructor
                        ostringstream msg;
                        msg << "Delay slot used in non-root constructor ";
                        big.printInfo(msg);
                        msg << endl;
                        reportWarning(getLocation(big), msg.str());
                    }
                    if (cur.section.delaySlot() > maxdelayslotbytes)   // Keep track of maximum delayslot parameter
                        maxdelayslotbytes = cur.section.delaySlot();
                }
                do
                {
                    i += 1;
                    if (i >= max) break;
                    cur = vec.getNamedPair(i);
                } while (cur.section == (ConstructTpl)null);

                if (i >= max) break;
                SectionSymbol* sym = sections[i];
                sectionstring = "   " + sym.getName() + " section: ";
            }
            if (!errors.empty())
            {
                ostringstream s;
                s << "in ";
                big.printInfo(s);
                reportError(getLocation(big), s.str());
                for (int j = 0; j < errors.size(); ++j)
                    reportError(getLocation(big), errors[j]);
                return false;
            }
            return true;
        }

        /// \brief Find a defining instance of the local variable with the given offset
        ///
        /// \param offset is the given offset
        /// \param ct is the Constructor to search
        /// \return the matchine local variable or null
        private static VarnodeTpl findSize(ConstTpl offset, ConstructTpl ct)
        {
            List<OpTpl> ops = ct.getOpvec();
            VarnodeTpl* vn;
            OpTpl* op;

            for (int i = 0; i < ops.size(); ++i)
            {
                op = ops[i];
                vn = op.getOut();
                if ((vn != (VarnodeTpl*)0) && (vn.isLocalTemp()))
                {
                    if (vn.getOffset() == offset)
                        return vn;
                }
                for (int j = 0; j < op.numInput(); ++j)
                {
                    vn = op.getIn(j);
                    if (vn.isLocalTemp() && (vn.getOffset() == offset))
                        return vn;
                }
            }
            return (VarnodeTpl*)0;
        }

        /// \brief Propagate local variable sizes into an \b export statement
        ///
        /// Look for zero size temporary Varnodes in \b export statements, search for
        /// the matching local Varnode symbol and force its size on the \b export.
        /// \param ct is the Constructor whose \b export is to be modified
        /// \return \b false if a local zero size can't be updated
        private static bool forceExportSize(ConstructTpl ct)
        {
            HandleTpl* result = ct.getResult();
            if (result == (HandleTpl)null) return true;

            VarnodeTpl* vt;

            if (result.getPtrSpace().isUniqueSpace() && result.getPtrSize().isZero())
            {
                vt = findSize(result.getPtrOffset(), ct);
                if (vt == (VarnodeTpl*)0) return false;
                result.setPtrSize(vt.getSize());
            }
            else if (result.getSpace().isUniqueSpace() && result.getSize().isZero())
            {
                vt = findSize(result.getPtrOffset(), ct);
                if (vt == (VarnodeTpl*)0) return false;
                result.setSize(vt.getSize());
            }
            return true;
        }

        /// \brief If the given Varnode is in the \e unique space, shift its offset up by \b sa bits
        ///
        /// \param vn is the given Varnode
        /// \param sa is the number of bits to shift by
        private static void shiftUniqueVn(VarnodeTpl vn, int sa)
        {
            if (vn.getSpace().isUniqueSpace() && (vn.getOffset().getType() == ConstTpl.const_type.real))
            {
                ulong val = vn.getOffset().getReal();
                val <<= sa;
                vn.setOffset(val);
            }
        }

        /// \brief Shift the offset up by \b sa bits for any Varnode used by the given op in the \e unique space
        ///
        /// \param op is the given op
        /// \param sa is the number of bits to shift by
        private static void shiftUniqueOp(OpTpl op, int sa)
        {
            VarnodeTpl* outvn = op.getOut();
            if (outvn != (VarnodeTpl*)0)
                shiftUniqueVn(outvn, sa);
            for (int i = 0; i < op.numInput(); ++i)
                shiftUniqueVn(op.getIn(i), sa);
        }

        /// \brief Shift the offset up for both \e dynamic or \e static Varnode aspects in the \e unique space
        ///
        /// \param hand is a handle template whose aspects should be modified
        /// \param sa is the number of bits to shift by
        private static void shiftUniqueHandle(HandleTpl hand, int sa)
        {
            if (hand.getSpace().isUniqueSpace() && (hand.getPtrSpace().getType() == ConstTpl.const_type.real)
                && (hand.getPtrOffset().getType() == ConstTpl.const_type.real))
            {
                ulong val = hand.getPtrOffset().getReal();
                val <<= sa;
                hand.setPtrOffset(val);
            }
            else if (hand.getPtrSpace().isUniqueSpace() && (hand.getPtrOffset().getType() == ConstTpl.const_type.real))
            {
                ulong val = hand.getPtrOffset().getReal();
                val <<= sa;
                hand.setPtrOffset(val);
            }

            if (hand.getTempSpace().isUniqueSpace() && (hand.getTempOffset().getType() == ConstTpl.const_type.real))
            {
                ulong val = hand.getTempOffset().getReal();
                val <<= sa;
                hand.setTempOffset(val);
            }
        }

        /// \brief Shift the offset up for any Varnode in the \e unique space for all p-code in the given section
        ///
        /// \param tpl is the given p-code section
        /// \param sa is the number of bits to shift by
        private static void shiftUniqueConstruct(ConstructTpl tpl, int sa)
        {
            HandleTpl* result = tpl.getResult();
            if (result != (HandleTpl)null)
                shiftUniqueHandle(result, sa);
            List<OpTpl> vec = tpl.getOpvec();
            for (int i = 0; i < vec.size(); ++i)
                shiftUniqueOp(vec[i], sa);
        }

        /// \brief Format an error or warning message given an optional source location
        ///
        /// \param loc is the given source location (or null)
        /// \param msg is the message
        /// \return the formatted message
        private static string formatStatusMessage(Location loc, string msg)
        {
            ostringstream s;
            if (loc != (Location*)0)
            {
                s << loc.format();
                s << ": ";
            }
            s << msg;
            return s.str();
        }

        ///< Modify temporary Varnode offsets to support \b crossbuilds
        /// With \b crossbuilds, temporaries may need to survive across instructions in a packet, so here we
        /// provide space in the offset of the temporary (within the \e unique space) so that the run-time SLEIGH
        /// engine can alter the value to prevent collisions with other nearby instructions
        private void checkUniqueAllocation()
        {
            if (unique_allocatemask == 0) return;   // We don't have any crossbuild directives

            unique_allocatemask = 0xff; // Provide 8 bits of free space
            int sa = 8;
            int secsize = sections.size(); // This is the upper bound for section numbers
            SubtableSymbol* sym = root; // Start with the instruction table
            int i = -1;
            for (; ; )
            {
                int numconst = sym.getNumConstructors();
                for (int j = 0; j < numconst; ++j)
                {
                    Constructor* ct = sym.getConstructor(j);
                    ConstructTpl* tpl = ct.getTempl();
                    if (tpl != (ConstructTpl)null)
                        shiftUniqueConstruct(tpl, sa);
                    for (int k = 0; k < secsize; ++k)
                    {
                        ConstructTpl* namedtpl = ct.getNamedTempl(k);
                        if (namedtpl != (ConstructTpl)null)
                            shiftUniqueConstruct(namedtpl, sa);
                    }
                }
                i += 1;
                if (i >= tables.size()) break;
                sym = tables[i];
            }
            uint ubase = getUniqueBase(); // We have to adjust the unique base
            ubase <<= sa;
            setUniqueBase(ubase);
        }

        ///< Do all post processing on the parsed data structures
        /// This method is called after parsing is complete.  It builds the final Constructor patterns,
        /// builds decision trees, does p-code optimization, and builds cross referencing structures.
        /// A number of checks are also performed, which may generate errors or warnings, including
        /// size restriction checks, pattern conflict checks, NOP constructor checks, and
        /// local collision checks.  Once this method is run, \b this SleighCompile is ready for the
        /// saveXml method.
        private void process()
        {
            checkNops();
            checkCaseSensitivity();
            if (getDefaultCodeSpace() == (AddrSpace)null)
                reportError("No default space specified");
            if (errors > 0) return;
            checkConsistency();
            if (errors > 0) return;
            checkLocalCollisions();
            if (errors > 0) return;
            buildPatterns();
            if (errors > 0) return;
            buildDecisionTrees();
            if (errors > 0) return;
            List<string> errorPairs;
            buildXrefs(errorPairs);     // Make sure we can build crossrefs properly
            if (!errorPairs.empty())
            {
                for (int i = 0; i < errorPairs.size(); i += 2)
                {
                    ostringstream s;
                    s << "Duplicate (offset,size) pair for registers: ";
                    s << errorPairs[i] << " and " << errorPairs[i + 1] << endl;
                    reportError(s.str());
                }
                return;
            }
            checkUniqueAllocation();
            symtab.purge();     // Get rid of any symbols we don't plan to save
        }

        public SleighCompile()
            : base()
        {
            pcode.setCompiler(this);
            contextlock = false;        // Context layout is not locked
            userop_count = 0;
            errors = 0;
            warnunnecessarypcode = false;
            warndeadtemps = false;
            lenientconflicterrors = true;
            largetemporarywarning = false;
            warnalllocalcollisions = false;
            warnallnops = false;
            failinsensitivedups = true;
            root = (SubtableSymbol)null;
            curmacro = (MacroSymbol*)0;
            curct = (Constructor)null;
        }

        ///< Get the source location of the given Constructor's definition
        /// \param ctor is the given Constructor
        /// \return the filename and line number
        public Location getLocation(Constructor ctor) => ctorLocationMap.at(ctor);

        ///< Get the source location of the given symbol's definition
        /// \param sym is the given symbol
        /// \return the filename and line number or null if location not found
        public Location getLocation(SleighSymbol sym)
        {
            try
            {
                return &symbolLocationMap.at(sym);
            }
            catch (out_of_range e) {
                return nullptr;
            }
        }

        ///< Issue a fatal error message
        /// The message is formatted and displayed for the user and a count is incremented.
        /// If there are too many fatal errors, the entire compilation process is terminated.
        /// Otherwise, parsing can continue, but the compiler will not produce an output file.
        /// \param msg is the error message
        public void reportError(string msg)
        {
            cerr << filename.GetLastItem() << ":" << lineno.GetLastItem() << " - ERROR " << msg << endl;
            errors += 1;
            if (errors > 1000000)
            {
                cerr << "Too many errors: Aborting" << endl;
                exit(2);
            }
        }

        ///< Issue a fatal error message with a source location
        /// The error message is formatted indicating the location of the error in source.
        /// The message is displayed for the user and a count is incremented.
        /// Otherwise, parsing can continue, but the compiler will not produce an output file.
        /// \param loc is the location of the error
        /// \param msg is the error message
        public void reportError(Location loc, string msg)
        {
            reportError(formatStatusMessage(loc, msg));
        }

        ///< Issue a warning message
        /// The message indicates a potential problem with the SLEIGH specification but does not
        /// prevent compilation from producing output.
        /// \param msg is the warning message
        public void reportWarning(string msg)
        {
            cerr << "WARN  " << msg << endl;
        }

        ///< Issue a warning message with a source location
        /// The message indicates a potential problem with the SLEIGH specification but does not
        /// prevent compilation from producing output.
        /// \param loc is the location of the problem in source
        /// \param msg is the warning message
        public void reportWarning(Location loc, string msg)
        {
            reportWarning(formatStatusMessage(loc, msg));
        }

        ///< Return the current number of fatal errors
        public int numErrors() => errors;

        ///< Get the next available temporary register offset
        /// The \e unique space acts as a pool of temporary registers that are drawn as needed.
        /// As Varnode sizes are frequently inferred and not immediately available during the parse,
        /// this method does not make an assumption about the size of the requested temporary Varnode.
        /// It reserves a fixed amount of space and returns its starting offset.
        /// \return the starting offset of the new temporary register
        public uint getUniqueAddr()
        {
            uint base = getUniqueBase();
            setUniqueBase(base + SleighBase::MAX_UNIQUE_SIZE);
            return base;
        }

        /// \brief Set whether unnecessary truncation and extension operators generate warnings individually
        ///
        /// \param val is \b true if warnings are generated individually.  The default is \b false.
        public void setUnnecessaryPcodeWarning(bool val)
        {
            warnunnecessarypcode = val;
        }

        /// \brief Set whether dead temporary registers generate warnings individually
        ///
        /// \param val is \b true if warnings are generated individually.  The default is \b false.
        public void setDeadTempWarning(bool val)
        {
            warndeadtemps = val;
        }

        /// \brief Set whether named temporary registers must be defined using the \b local keyword.
        ///
        /// \param val is \b true if the \b local keyword must always be used. The default is \b false.
        public void setEnforceLocalKeyWord(bool val)
        {
            pcode.setEnforceLocalKey(val);
        }

        /// \brief Set whether too large temporary registers generate warnings individually
        ///
        /// \param val is \b true if warnings are generated individually.  The default is \b false.
        public void setLargeTemporaryWarning(bool val)
        {
            largetemporarywarning = val;
        }

        /// \brief Set whether indistinguishable Constructor patterns generate fatal errors
        ///
        /// \param val is \b true if no error is generated.  The default is \b true.
        public void setLenientConflict(bool val) { lenientconflicterrors = val; }

        /// \brief Set whether collisions in exported locals generate warnings individually
        ///
        /// \param val is \b true if warnings are generated individually.  The default is \b false.
        public void setLocalCollisionWarning(bool val)
        {
            warnalllocalcollisions = val;
        }

        /// \brief Set whether NOP Constructors generate warnings individually
        ///
        /// \param val is \b true if warnings are generated individually.  The default is \b false.
        public void setAllNopWarning(bool val)
        {
            warnallnops = val;
        }

        /// \brief Set whether case insensitive duplicates of register names cause an error
        ///
        /// \param val is \b true is duplicates cause an error.
        public void setInsensitiveDuplicateError(bool val)
        {
            failinsensitivedups = val;
        }

        // Lexer functions
        ///< Calculate the internal context field layout
        /// All current context field definitions are analyzed, the internal packing of
        /// the fields is determined, and the final symbols (ContextSymbol) are created and
        /// added to the symbol table. No new context fields can be defined once this method is called.
        public void calcContextLayout()
        {
            if (contextlock) return;    // Already locked
            contextlock = true;

            int context_offset = 0;
            int begin, sz;
            stable_sort(contexttable.begin(), contexttable.end());
            begin = 0;
            while (begin < contexttable.size())
            { // Define the context variables
                sz = 1;
                while ((begin + sz < contexttable.size()) && (contexttable[begin + sz].sym == contexttable[begin].sym))
                    sz += 1;
                context_offset = calcContextVarLayout(begin, sz, context_offset);
                begin += sz;
            }

            //  context_size = (context_offset+8*sizeof(uint)-1)/(8*sizeof(uint));

            // Delete the quals
            for (int i = 0; i < contexttable.size(); ++i)
            {
                FieldQuality* qual = contexttable[i].qual;
                delete qual;
            }

            contexttable.clear();
        }

        ///< Get the path to the current source file
        /// Get the path of the current file being parsed as either an absolute path, or relative to cwd
        /// \return the path string
        public string grabCurrentFilePath()
        {
            if (relpath.empty()) return "";
            return (relpath.GetLastItem() + filename.GetLastItem());
        }

        ///< Push a new source file to the current parse stack
        /// The given filename can be absolute are relative to the current working directory.
        /// The directory containing the file is established as the new current working directory.
        /// The file is added to the current stack of \e included source files, and parsing
        /// is set to continue from the first line.
        /// \param fname is the absolute or relative pathname of the new source file
        public void parseFromNewFile(string fname)
        {
            string @base;
            string path;
            FileManage::splitPath(fname, path, @base);
            filename.Add(@base);
            if (relpath.empty() || FileManage::isAbsolutePath(path))
                relpath.Add(path);
            else
            {           // Relative paths from successive includes, combine
                string totalpath = relpath.GetLastItem();
                totalpath += path;
                relpath.Add(totalpath);
            }
            lineno.Add(1);
        }

        ///< Mark start of parsing for an expanded preprocessor macro
        /// Indicate to the location finder that parsing is currently in an expanded preprocessor macro
        public void parsePreprocMacro()
        {
            filename.Add(filename.GetLastItem() + ":macro");
            relpath.Add(relpath.GetLastItem());
            lineno.Add(lineno.GetLastItem());
        }

        ///< Mark end of parsing for the current file or macro
        /// Pop the current file off the \e included source file stack, indicating that parsing continues
        /// in the parent source file.
        public void parseFileFinished()
        {
            filename.RemoveLastItem();
            relpath.RemoveLastItem();
            lineno.RemoveLastItem();
        }

        ///< Indicate parsing proceeded to the next line of the current file
        public void nextLine()
        {
            lineno.GetLastItem() += 1;
        }

        ///< Retrieve a given preprocessor variable
        /// Pass back the string associated with the variable name.
        /// \param nm is the name of the given preprocessor variable
        /// \param res will hold string value passed back
        /// \return \b true if the variable was defined
        public bool getPreprocValue(string nm, string res)
        {
            Dictionary<string, string>::const_iterator iter = preproc_defines.find(nm);
            if (iter == preproc_defines.end()) return false;
            res = (*iter).second;
            return true;
        }

        ///< Set a given preprocessor variable
        /// The string value is associated with the variable name.
        /// \param nm is the name of the given preprocessor variable
        /// \param value is the string value to associate
        public void setPreprocValue(string nm, string value)
        {
            preproc_defines[nm] = value;
        }

        ///< Remove the value associated with the given preprocessor variable
        /// Any existing string value associated with the variable is removed.
        /// \param nm is the name of the given preprocessor variable
        /// \return \b true if the variable had a value (was defined) initially
        public bool undefinePreprocValue(string nm)
        {
            Dictionary<string, string>::iterator iter = preproc_defines.find(nm);
            if (iter == preproc_defines.end()) return false;
            preproc_defines.erase(iter);
            return true;
        }

        // Parser functions
        /// \brief Define a new SLEIGH token
        ///
        /// In addition to the name and size, an endianness code is provided, with the possible values:
        ///   - -1 indicates a \e little endian interpretation is forced on the token
        ///   -  0 indicates the global endianness setting is used for the token
        ///   -  1 indicates a \e big endian interpretation is forced on the token
        ///
        /// \param name is the name of the token
        /// \param sz is the number of bits in the token
        /// \param endian is the endianness code
        /// \return the new token symbol
        public TokenSymbol defineToken(string name, ulong sz, int endian)
        {
            uint size = *sz;
            delete sz;
            if ((size & 7) != 0)
            {
                reportError(getCurrentLocation(), "'" + *name + "': token size must be multiple of 8");
                size = (size / 8) + 1;
            }
            else
                size = size / 8;
            bool isBig;
            if (endian == 0)
                isBig = isBigEndian();
            else
                isBig = (endian > 0);
            Token* newtoken = new Token(*name, size, isBig, tokentable.size());
            tokentable.Add(newtoken);
            delete name;
            TokenSymbol* res = new TokenSymbol(newtoken);
            addSymbol(res);
            return res;
        }

        /// \brief Add a new field definition to the given token
        ///
        /// \param sym is the given token
        /// \param qual is the set of parsed qualities to associate with the new field
        public void addTokenField(TokenSymbol sym, FieldQuality qual)
        {
            if (qual.high < qual.low)
            {
                ostringstream s;
                s << "Field '" << qual.name << "' starts at " << qual.low << " and ends at " << qual.high;
                reportError(getCurrentLocation(), s.str());
            }
            if (sym.getToken().getSize() * 8 <= qual.high)
            {
                ostringstream s;
                s << "Field '" << qual.name << "' high must be less than token size";
                reportError(getCurrentLocation(), s.str());
            }
            TokenField* field = new TokenField(sym.getToken(), qual.signext, qual.low, qual.high);
            addSymbol(new ValueSymbol(qual.name, field));
            delete qual;
        }

        /// \brief Add a new context field definition to the given backing Varnode
        ///
        /// \param sym is the given Varnode providing backing storage for the context field
        /// \param qual is the set of parsed qualities to associate with the new field
        public bool addContextField(VarnodeSymbol sym, FieldQuality qual)
        {
            if (qual.high < qual.low)
            {
                ostringstream s;
                s << "Context field '" << qual.name << "' starts at " << qual.low << " and ends at " << qual.high;
                reportError(getCurrentLocation(), s.str());
            }
            if (sym.getSize() * 8 <= qual.high)
            {
                ostringstream s;
                s << "Context field '" << qual.name << "' high must be less than context size";
                reportError(getCurrentLocation(), s.str());
            }
            if (contextlock)
                return false;       // Context layout has already been satisfied

            contexttable.Add(FieldContext(sym, qual));
            return true;
        }

        /// \brief Define a new addresds space
        ///
        /// \param qual is the set of parsed qualities to associate with the new space
        public void newSpace(SpaceQuality qual)
        {
            if (qual.size == 0)
            {
                reportError(getCurrentLocation(), "Space definition '" + qual.name + "' missing size attribute");
                delete qual;
                return;
            }

            int delay = (qual.type == SpaceQuality::registertype) ? 0 : 1;
            AddrSpace* spc = new AddrSpace(this, this, spacetype.IPTR_PROCESSOR, qual.name, qual.size, qual.wordsize, numSpaces(), AddrSpace::hasphysical, delay);
            insertSpace(spc);
            if (qual.isdefault)
            {
                if (getDefaultCodeSpace() != (AddrSpace)null)
                    reportError(getCurrentLocation(), "Multiple default spaces -- '" + getDefaultCodeSpace().getName() + "', '" + qual.name + "'");
                else
                {
                    setDefaultCodeSpace(spc.getIndex());   // Make the flagged space the default
                    pcode.setDefaultSpace(spc);
                }
            }
            delete qual;
            addSymbol(new SpaceSymbol(spc));
        }

        /// \brief Start a new named p-code section and define the associated section symbol
        ///
        /// \param nm is the name of the section
        /// \return the new section symbol
        public SectionSymbol newSectionSymbol(string nm)
        {
            SectionSymbol* sym = new SectionSymbol(nm, sections.size());
            try
            {
                symtab.addGlobalSymbol(sym);
            }
            catch (SleighError err) {
                reportError(getCurrentLocation(), err.ToString());
            }
            sections.Add(sym);
            numSections = sections.size();
            return sym;
        }

        /// \brief Set the global endianness of the SLEIGH specification
        ///
        /// This \b must be called at the very beginning of the parse.
        /// This method additionally establishes predefined symbols for the specification.
        /// \param end is the endianness value (0=little 1=big)
        public void setEndian(int end)
        {
            setBigEndian((end == 1));
            predefinedSymbols();        // Set up symbols now that we know endianess
        }

        /// \brief Set instruction alignment for the SLEIGH specification
        ///
        /// \param val is the alignment value in bytes. 1 is the default indicating no alignment
        public void setAlignment(int val)
        {
            alignment = val;
        }

        /// \brief Definition a set of Varnodes
        ///
        /// Storage for each Varnode is allocated in sequence from the given address space,
        /// starting from the specified offset.
        /// \param spacesym is the given address space
        /// \param off is the starting offset
        /// \param size is the size (in bytes) to allocate for each Varnode
        /// \param names is the list of Varnode names to define
        public void defineVarnodes(SpaceSymbol spacesym, ulong off, ulong size, List<string> names)
        {
            AddrSpace* spc = spacesym.getSpace();
            ulong myoff = *off;
            for (int i = 0; i < names.size(); ++i)
            {
                if ((*names)[i] != "_")
                    addSymbol(new VarnodeSymbol((*names)[i], spc, myoff, *size));
                myoff += *size;
            }
            delete names;
            delete off;
            delete size;
        }

        /// \brief Define a new Varnode symbol as a subrange of bits within another symbol
        ///
        /// If the ends of the range fall on byte boundaries, we
        /// simply define a normal VarnodeSymbol, otherwise we create
        /// a special symbol which is a place holder for the bitrange operator
        /// \param name is the name of the new Varnode
        /// \param sym is the parent Varnode
        /// \param bitoffset is the (least significant) starting bit of the new Varnode within the parent
        /// \param numb is the number of bits in the new Varnode
        public void defineBitrange(string name, VarnodeSymbol sym, uint bitoffset, uint numb)
        {
            string namecopy = *name;
            delete name;
            uint size = 8 * sym.getSize(); // Number of bits
            if (numb == 0)
            {
                reportError(getCurrentLocation(), "'" + namecopy + "': size of bitrange is zero");
                return;
            }
            if ((bitoffset >= size) || ((bitoffset + numb) > size))
            {
                reportError(getCurrentLocation(), "'" + namecopy + "': bad bitrange");
                return;
            }
            if ((bitoffset % 8 == 0) && (numb % 8 == 0))
            {
                // This can be reduced to an ordinary varnode definition
                AddrSpace* newspace = sym.getFixedVarnode().space;
                ulong newoffset = sym.getFixedVarnode().offset;
                int newsize = numb / 8;
                if (isBigEndian())
                    newoffset += (size - bitoffset - numb) / 8;
                else
                    newoffset += bitoffset / 8;
                addSymbol(new VarnodeSymbol(namecopy, newspace, newoffset, newsize));
            }
            else                // Otherwise define the special symbol
                addSymbol(new BitrangeSymbol(namecopy, sym, bitoffset, numb));
        }

        /// \brief Define a list of new user-defined operators
        ///
        /// A new symbol is created for each name.
        /// \param names is the list of names
        public void addUserOp(List<string> names)
        {
            for (int i = 0; i < names.size(); ++i)
            {
                UserOpSymbol* sym = new UserOpSymbol((*names)[i]);
                sym.setIndex(userop_count++);
                addSymbol(sym);
            }
            delete names;
        }

        /// \brief Attach a list integer values, to each value symbol in the given list
        ///
        /// Each symbol's original bit representation is no longer used as the absolute integer
        /// value associated with the symbol. Instead it is used to map into this integer list.
        /// \param symlist is the given list of value symbols
        /// \param numlist is the list of integer values to attach
        public void attachValues(List<SleighSymbol> symlist, List<long> numlist)
        {
            SleighSymbol* dupsym = dedupSymbolList(symlist);
            if (dupsym != (SleighSymbol)null)
                reportWarning(getCurrentLocation(), "'attach values' list contains duplicate entries: " + dupsym.getName());
            for (int i = 0; i < symlist.size(); ++i)
            {
                ValueSymbol* sym = (ValueSymbol*)(*symlist)[i];
                if (sym == (ValueSymbol*)0) continue;
                PatternValue* patval = sym.getPatternValue();
                if (patval.maxValue() + 1 != numlist.size())
                {
                    ostringstream msg;
                    msg << "Attach value '" + sym.getName();
                    msg << "' (range 0.." << patval.maxValue() << ") is wrong size for list (of " << numlist.size() << " entries)";
                    reportError(getCurrentLocation(), msg.str());
                }
                symtab.replaceSymbol(sym, new ValueMapSymbol(sym.getName(), patval, *numlist));
            }
            delete numlist;
            delete symlist;
        }

        /// \brief Attach a list of display names to the given list of value symbols
        ///
        /// Each symbol's original bit representation is no longer used as the display name
        /// for the symbol. Instead it is used to map into this list of display names.
        /// \param symlist is the given list of value symbols
        /// \param names is the list of display names to attach
        public void attachNames(List<SleighSymbol> symlist, List<string> names)
        {
            SleighSymbol* dupsym = dedupSymbolList(symlist);
            if (dupsym != (SleighSymbol)null)
                reportWarning(getCurrentLocation(), "'attach names' list contains duplicate entries: " + dupsym.getName());
            for (int i = 0; i < symlist.size(); ++i)
            {
                ValueSymbol* sym = (ValueSymbol*)(*symlist)[i];
                if (sym == (ValueSymbol*)0) continue;
                PatternValue* patval = sym.getPatternValue();
                if (patval.maxValue() + 1 != names.size())
                {
                    ostringstream msg;
                    msg << "Attach name '" + sym.getName();
                    msg << "' (range 0.." << patval.maxValue() << ") is wrong size for list (of " << names.size() << " entries)";
                    reportError(getCurrentLocation(), msg.str());
                }
                symtab.replaceSymbol(sym, new NameSymbol(sym.getName(), patval, *names));
            }
            delete names;
            delete symlist;
        }

        /// \brief Attach a list of Varnodes to the given list of value symbols
        ///
        /// Each symbol's original bit representation is no longer used as the display name and
        /// semantic value of the symbol.  Instead it is used to map into this list of Varnodes.
        /// \param symlist is the given list of value symbols
        /// \param varlist is the list of Varnodes to attach
        public void attachVarnodes(List<SleighSymbol> symlist, List<SleighSymbol> varlist)
        {
            SleighSymbol* dupsym = dedupSymbolList(symlist);
            if (dupsym != (SleighSymbol)null)
                reportWarning(getCurrentLocation(), "'attach variables' list contains duplicate entries: " + dupsym.getName());
            for (int i = 0; i < symlist.size(); ++i)
            {
                ValueSymbol* sym = (ValueSymbol*)(*symlist)[i];
                if (sym == (ValueSymbol*)0) continue;
                PatternValue* patval = sym.getPatternValue();
                if (patval.maxValue() + 1 != varlist.size())
                {
                    ostringstream msg;
                    msg << "Attach varnode '" + sym.getName();
                    msg << "' (range 0.." << patval.maxValue() << ") is wrong size for list (of " << varlist.size() << " entries)";
                    reportError(getCurrentLocation(), msg.str());
                }
                int sz = 0;
                for (int j = 0; j < varlist.size(); ++j)
                {
                    VarnodeSymbol* vsym = (VarnodeSymbol*)(*varlist)[j];
                    if (vsym != (VarnodeSymbol*)0)
                    {
                        if (sz == 0)
                            sz = vsym.getFixedVarnode().size;
                        else if (sz != vsym.getFixedVarnode().size)
                        {
                            ostringstream msg;
                            msg << "Attach statement contains varnodes of different sizes -- " << dec << sz << " != " << dec << vsym.getFixedVarnode().size;
                            reportError(getCurrentLocation(), msg.str());
                            break;
                        }
                    }
                }
                symtab.replaceSymbol(sym, new VarnodeListSymbol(sym.getName(), patval, *varlist));
            }
            delete varlist;
            delete symlist;
        }

        /// \brief Define a new SLEIGH subtable
        ///
        /// A symbol and table entry are created.
        /// \param nm is the name of the new subtable
        public SubtableSymbol newTable(string nm)
        {
            SubtableSymbol* sym = new SubtableSymbol(*nm);
            addSymbol(sym);
            tables.Add(sym);
            delete nm;
            return sym;
        }

        /// \brief Define a new operand for the given Constructor
        ///
        /// A symbol local to the Constructor is defined, which initially is unmapped.
        /// Operands are defined in order.
        /// \param ct is the given Constructor
        /// \param nm is the name of the new operand
        public void newOperand(Constructor ct, string nm)
        {
            int index = ct.getNumOperands();
            OperandSymbol* sym = new OperandSymbol(*nm, index, ct);
            addSymbol(sym);
            ct.addOperand(sym);
            delete nm;
        }

        /// \brief Create a new constraint equation based on the given operand
        ///
        /// The constraint forces the operand to \e match the specified expression
        /// \param sym is the given operand
        /// \param patexp is the specified expression
        /// \return the new constraint equation
        public PatternEquation constrainOperand(OperandSymbol sym, PatternExpression patexp)
        {
            PatternEquation* res;
            FamilySymbol* famsym = dynamic_cast<FamilySymbol*>(sym.getDefiningSymbol());
            if (famsym != (FamilySymbol*)0)
            { // Operand already defined as family symbol
              // This equation must be a constraint
                res = new EqualEquation(famsym.getPatternValue(), patexp);
            }
            else
            {           // Operand is currently undefined, so we can't constrain
                PatternExpression::release(patexp);
                res = (PatternEquation)null;
            }
            return res;
        }

        /// \brief Map the local operand symbol to a PatternExpression
        ///
        /// The operand symbol's display string and semantic value are calculated at
        /// disassembly time based on the specified expression.
        /// \param sym is the local operand
        /// \param patexp is the expression to map to the operand
        public void defineOperand(OperandSymbol sym, PatternExpression patexp)
        {
            try
            {
                sym.defineOperand(patexp);
                sym.setOffsetIrrelevant(); // If not a self-definition, the operand has no
                                            // pattern directly associated with it, so
                                            // the operand's offset is irrelevant
            }
            catch (SleighError err) {
                reportError(getCurrentLocation(), err.ToString());
                PatternExpression::release(patexp);
            }
        }

        /// \brief Define a new \e invisible operand based on an existing symbol
        ///
        /// A new symbol is defined that is considered an operand of the current Constructor,
        /// but its display does not contribute to the display of the Constructor.
        /// The new symbol may still contribute matching patterns and p-code
        /// \param sym is the existing symbol that the new operand maps to
        /// \return an (unconstrained) operand pattern
        public PatternEquation defineInvisibleOperand(TripleSymbol sym)
        {
            int index = curct.getNumOperands();
            OperandSymbol* opsym = new OperandSymbol(sym.getName(), index, curct);
            addSymbol(opsym);
            curct.addInvisibleOperand(opsym);
            PatternEquation* res = new OperandEquation(opsym.getIndex());
            SleighSymbol::symbol_type tp = sym.getType();
            try
            {
                if ((tp == SleighSymbol::value_symbol) || (tp == SleighSymbol::context_symbol))
                {
                    opsym.defineOperand(sym.getPatternExpression());
                }
                else
                {
                    opsym.defineOperand(sym);
                }
            }
            catch (SleighError err) {
                reportError(getCurrentLocation(), err.ToString());
            }
            return res;
        }

        /// \brief Map given operand to a global symbol of same name
        ///
        /// The operand symbol still acts as a local symbol but gets its display,
        /// pattern, and semantics from the global symbol.
        /// \param sym is the given operand
        public void selfDefine(OperandSymbol sym)
        {
            TripleSymbol* glob = dynamic_cast<TripleSymbol*>(symtab.findSymbol(sym.getName(), 1));
            if (glob == (TripleSymbol)null)
            {
                reportError(getCurrentLocation(), "No matching global symbol '" + sym.getName() + "'");
                return;
            }
            SleighSymbol::symbol_type tp = glob.getType();
            try
            {
                if ((tp == SleighSymbol::value_symbol) || (tp == SleighSymbol::context_symbol))
                {
                    sym.defineOperand(glob.getPatternExpression());
                }
                else
                    sym.defineOperand(glob);
            }
            catch (SleighError err) {
                reportError(getCurrentLocation(), err.ToString());
            }
        }

        /// \brief Set \e export of a Constructor to the given Varnode
        ///
        /// SLEIGH symbols matching the Constructor use this Varnode as their semantic storage/value.
        /// \param ct is the Constructor p-code section
        /// \param vn is the given Varnode
        /// \return the p-code section
        public ConstructTpl setResultVarnode(ConstructTpl ct, VarnodeTpl vn)
        {
            HandleTpl* res = new HandleTpl(vn);
            delete vn;
            ct.setResult(res);
            return ct;
        }

        /// \brief Set a Constructor export to be the location pointed to by the given Varnode
        ///
        /// SLEIGH symbols matching the Constructor use this dynamic location as their semantic storage/value.
        /// \param ct is the Constructor p-code section
        /// \param star describes the pointer details
        /// \param vn is the given Varnode pointer
        /// \return the p-code section
        public ConstructTpl setResultStarVarnode(ConstructTpl ct, StarQuality star, VarnodeTpl vn)
        {
            HandleTpl* res = new HandleTpl(star.id, ConstTpl(ConstTpl.const_type.real, star.size), vn,
                             getUniqueSpace(), getUniqueAddr());
            delete star;
            delete vn;
            ct.setResult(res);
            return ct;
        }

        /// \brief Create a change operation that makes a temporary change to a context variable
        ///
        /// The new change operation is added to the current list.
        /// When executed, the change operation will assign a new value to the given context variable
        /// using the specified expression.  The change only applies within the parsing of a single instruction.
        /// Because we are in the middle of parsing, the \b inst_next and \b inst_next2 values have not 
        /// been computed yet.  So we check to make sure the value expression doesn't use this symbol.
        /// \param vec is the current list of change operations
        /// \param sym is the given context variable affected by the operation
        /// \param pe is the specified expression
        /// \return \b true if the expression does not use the \b inst_next or \b inst_next2 symbol
        public bool contextMod(List<ContextChange> vec, ContextSymbol sym, PatternExpression pe)
        {
            List<PatternValue> vallist =new List<PatternValue>();
            pe.listValues(vallist);
            for (uint i = 0; i < vallist.size(); ++i)
            {
                if (dynamic_cast<EndInstructionValue*>(vallist[i]) != (EndInstructionValue*)0)
                    return false;
                if (dynamic_cast<Next2InstructionValue*>(vallist[i]) != (Next2InstructionValue*)0)
                    return false;
            }
            // Otherwise we generate a "temporary" change to context instruction  (ContextOp)
            ContextField* field = (ContextField*)sym.getPatternValue();
            ContextOp* op = new ContextOp(field.getStartBit(), field.getEndBit(), pe);
            vec.Add(op);
            return true;
        }

        /// \brief Create a change operation that makes a context variable change permanent
        ///
        /// The new change operation is added to the current list.
        /// When executed, the operation makes the final value of the given context variable permanent,
        /// starting at the specified address symbol. This value is set for contexts starting at the
        /// specified symbol address and may flow to following addresses depending on the variable settings.
        /// \param vec is the current list of change operations
        /// \param sym is the specified address symbol
        /// \param cvar is the given context variable
        public void contextSet(List<ContextChange> vec, TripleSymbol sym, ContextSymbol cvar)
        {
            ContextField* field = (ContextField*)cvar.getPatternValue();
            ContextCommit* op = new ContextCommit(sym, field.getStartBit(), field.getEndBit(), cvar.getFlow());
            vec.Add(op);
        }

        /// \brief Create a macro symbol (with parameter names)
        ///
        /// An uninitialized symbol is defined and a macro table entry assigned.
        /// The body of the macro must be provided later with the buildMacro method.
        /// \param name is the name of the macro
        /// \param params is the list of parameter names for the macro
        /// \return the new macro symbol
        public MacroSymbol createMacro(string name, List<string> param)
        {
            curct = (Constructor)null;    // Not currently defining a Constructor
            curmacro = new MacroSymbol(*name, macrotable.size());
            delete name;
            addSymbol(curmacro);
            symtab.addScope();      // New scope for the body of the macro definition
            pcode.resetLabelCount();    // Macros have their own labels
            for (int i = 0; i <@params.size(); ++i) {
                OperandSymbol* oper = new OperandSymbol((*@params)[i], i,(Constructor *)0);
                addSymbol(oper);
                curmacro.addOperand(oper);
            }
            delete @params;
            return curmacro;
        }

        /// \brief Pass through operand properties of an invoked macro to the parent operands
        ///
        /// Match up any qualities of the macro's OperandSymbols with any OperandSymbol passed
        /// into the macro.
        /// \param sym is the macro being invoked
        /// \param param is the list of expressions passed to the macro
        public void compareMacroParams(MacroSymbol sym, List<ExprTree> param)
        {
            for (uint i = 0; i < param.size(); ++i)
            {
                VarnodeTpl* outvn = param[i].getOut();
                if (outvn == (VarnodeTpl*)0) continue;
                // Check if an OperandSymbol was passed into this macro
                if (outvn.getOffset().getType() != ConstTpl.const_type.handle) continue;
                int hand = outvn.getOffset().getHandleIndex();

                // The matching operands
                OperandSymbol* macroop = sym.getOperand(i);
                OperandSymbol* parentop;
                if (curct == (Constructor)null)
                    parentop = curmacro.getOperand(hand);
                else
                    parentop = curct.getOperand(hand);

                // This is the only property we check right now
                if (macroop.isCodeAddress())
                    parentop.setCodeAddress();
            }
        }

        /// \brief Create a p-code sequence that invokes a macro
        ///
        /// The given parameter expressions are expanded first into the p-code sequence,
        /// followed by a final macro build directive.
        /// \param sym is the macro being invoked
        /// \param param is the sequence of parameter expressions passed to the macro
        /// \return the p-code sequence
        public List<OpTpl> createMacroUse(MacroSymbol sym, List<ExprTree> param)
        {
            if (sym.getNumOperands() != param.size())
            {
                bool tooManyParams = param.size() > sym.getNumOperands();
                string errmsg = "Invocation of macro '" + sym.getName() + "' passes too " + (tooManyParams ? "many" : "few") + " parameters";
                reportError(getCurrentLocation(), errmsg);
                return new List<OpTpl*>;
            }
            compareMacroParams(sym, *param);
            OpTpl* op = new OpTpl(MACROBUILD);
            VarnodeTpl* idvn = new VarnodeTpl(ConstTpl(getConstantSpace()),
                                ConstTpl(ConstTpl.const_type.real, sym.getIndex()),
                                ConstTpl(ConstTpl.const_type.real, 4));
            op.addInput(idvn);
            return ExprTree::appendParams(op, param);
        }

        /// \brief Create a SectionVector containing just the \e main p-code section with no named sections
        ///
        /// \param main is the main p-code section
        /// \return the new SectionVector
        public SectionVector standaloneSection(ConstructTpl main)
        {
            SectionVector* res = new SectionVector(main, symtab.getCurrentScope());
            return res;
        }

        /// \brief Start a new named p-code section after the given \e main p-code section
        ///
        /// The \b main p-code section must already be constructed, and the new named section
        /// symbol defined.  A SectionVector is initialized with the \e main section, and a
        /// symbol scope is created for the new p-code section.
        /// \param main is the existing \e main p-code section
        /// \param sym is the existing symbol for the new named p-code section
        /// \return the new SectionVector
        public SectionVector firstNamedSection(ConstructTpl main, SectionSymbol sym)
        {
            sym.incrementDefineCount();
            SymbolScope* curscope = symtab.getCurrentScope(); // This should be a Constructor scope
            SymbolScope* parscope = curscope.getParent();
            if (parscope != symtab.getGlobalScope())
                throw new LowlevelError("firstNamedSection called when not in Constructor scope"); // Unrecoverable error
            symtab.addScope();      // Add new scope under the Constructor scope
            SectionVector* res = new SectionVector(main, curscope);
            res.setNextIndex(sym.getTemplateId());
            return res;
        }

        /// \brief Complete a named p-code section and prepare for a new named section
        ///
        /// The actual p-code templates are assigned to a previously registered p-code section symbol
        /// and is added to the existing Section Vector. The old symbol scope is popped and another
        /// scope is created for the new named section.
        /// \param vec is the existing SectionVector
        /// \param section contains the p-code templates to assign to the previous section
        /// \param sym is the symbol describing the new named section being parsed
        /// \return the updated SectionVector
        public SectionVector nextNamedSection(SectionVector vec, ConstructTpl section, SectionSymbol sym)
        {
            sym.incrementDefineCount();
            SymbolScope* curscope = symtab.getCurrentScope();
            symtab.popScope();      // Pop the scope of the last named section
            SymbolScope* parscope = symtab.getCurrentScope().getParent();
            if (parscope != symtab.getGlobalScope())
                throw new LowlevelError("nextNamedSection called when not in section scope"); // Unrecoverable
            symtab.addScope();      // Add new scope under the Constructor scope (not the last section scope)
            vec.append(section, curscope); // Associate finished section
            vec.setNextIndex(sym.getTemplateId()); // Set index for the NEXT section (not been fully parsed yet)
            return vec;
        }

        /// \brief Fill-in final named section to match the previous SectionSymbol
        ///
        /// The provided p-code templates are assigned to the previously registered p-code section symbol,
        /// and the completed section is added to the SectionVector.
        /// \param vec is the existing SectionVector
        /// \param section contains the p-code templates to assign to the last section
        /// \return the updated SectionVector
        public SectionVector finalNamedSection(SectionVector vec, ConstructTpl section)
        {
            vec.append(section, symtab.getCurrentScope());
            symtab.popScope();      // Pop the section scope
            return vec;
        }

        /// \brief Create the \b crossbuild directive as a p-code template
        ///
        /// \param addr is the address symbol indicating the instruction to \b crossbuild
        /// \param sym is the symbol indicating the p-code to be build
        /// \return the p-code template
        public List<OpTpl> createCrossBuild(VarnodeTpl addr, SectionSymbol sym)
        {
            unique_allocatemask = 1;
            List<OpTpl*>* res = new List<OpTpl*>();
            VarnodeTpl* sectionid = new VarnodeTpl(ConstTpl(getConstantSpace()),
                                                   ConstTpl(ConstTpl.const_type.real, sym.getTemplateId()),
                                                   ConstTpl(ConstTpl.const_type.real, 4));
            // This is simply a single pcodeop (template), where the opcode indicates the crossbuild directive
            OpTpl* op = new OpTpl(OpCode.CROSSBUILD);
            op.addInput(addr);     // The first input is the VarnodeTpl representing the address
            op.addInput(sectionid);    // The second input is the indexed representing the named pcode section to build
            res.Add(op);
            sym.incrementRefCount();   // Keep track of the references to the section symbol
            return res;
        }

        /// \brief Create a new Constructor under the given subtable
        ///
        /// Create the object and initialize parsing for the new definition
        /// \param sym is the given subtable or null for the root table
        /// \return the new Constructor
        public Constructor createConstructor(SubtableSymbol sym)
        {
            if (sym == (SubtableSymbol)null)
                sym = WithBlock::getCurrentSubtable(withstack);
            if (sym == (SubtableSymbol)null)
                sym = root;
            curmacro = (MacroSymbol*)0; // Not currently defining a macro
            curct = new Constructor(sym);
            curct.setLineno(lineno.GetLastItem());
            ctorLocationMap[curct] = *getCurrentLocation();
            sym.addConstructor(curct);
            symtab.addScope();      // Make a new symbol scope for our constructor
            pcode.resetLabelCount();
            int index = indexer.index(ctorLocationMap[curct].getFilename());
            curct.setSrcIndex(index);
            return curct;
        }

        ///< Is the Constructor in the root table?
        public bool isInRoot(Constructor ct) => (root == ct.getParent());

        /// \brief Reset state after a parsing error in the previous Constructor
        public void resetConstructors()
        {
            symtab.setCurrentScope(symtab.getGlobalScope()); // Purge any dangling local scopes
        }

        /// \brief Add a new \b with block to the current stack
        ///
        /// All subsequent Constructors adopt properties declared in the \b with header.
        /// \param ss the subtable to assign to each Constructor, or null
        /// \param pateq is an pattern equation constraining each Constructor, or null
        /// \param contvec is a context change applied to each Constructor, or null
        public void pushWith(SubtableSymbol ss, PatternEquation pateq, List<ContextChange> contvec)
        {
            withstack.emplace_back();
            withstack.GetLastItem().set(ss, pateq, contvec);
        }

        /// \brief Pop the current \b with block from the stack
        public void popWith()
        {
            withstack.RemoveLastItem();
        }

        /// \brief Finish building a given Constructor after all its pieces have been parsed
        ///
        /// The constraint pattern and context changes are modified by the current \b with block.
        /// The result along with any p-code sections are registered with the Constructor object.
        /// \param big is the given Constructor
        /// \param pateq is the parsed pattern equation
        /// \param contvec is the list of context changes or null
        /// \param vec is the collection of p-code sections, or null
        public void buildConstructor(Constructor big, PatternEquation pateq, List<ContextChange> contvec,
            SectionVector vec)
        {
            bool noerrors = true;
            if (vec != (SectionVector)null) {
                // If the sections were implemented
                noerrors = finalizeSections(big, vec);
                if (noerrors) {
                    // Attach the sections to the Constructor
                    big.setMainSection(vec.getMainSection());
                    int max = vec.getMaxId();
                    for (int i = 0; i < max; ++i) {
                        ConstructTpl section = vec.getNamedSection(i);
                        if (section != (ConstructTpl)null)
                            big.setNamedSection(section, i);
                    }
                }
                // delete vec;
            }
            if (noerrors) {
                pateq = WithBlock.collectAndPrependPattern(withstack, pateq);
                contvec = WithBlock.collectAndPrependContext(withstack, contvec);
                big.addEquation(pateq);
                big.removeTrailingSpace();
                if (contvec != (List<ContextChange*>*)0) {
                    big.addContext(*contvec);
                    // delete contvec;
                }
            }
            symtab.popScope();      // In all cases pop scope
        }

        /// \brief Finish defining a macro given a set of p-code templates for its body
        ///
        /// Try to propagate sizes through the templates, expand any (sub)macros and make
        /// sure any label symbols are defined and used.
        /// \param sym is the macro being defined
        /// \param rtl is the set of p-code templates
        public void buildMacro(MacroSymbol sym, ConstructTpl rtl)
        {
            string errstring = checkSymbols(symtab.getCurrentScope());
            if (errstring.size() != 0)
            {
                reportError(getCurrentLocation(), "In definition of macro '" + sym.getName() + "': " + errstring);
                return;
            }
            if (!expandMacros(rtl))
            {
                reportError(getCurrentLocation(), "Could not expand submacro in definition of macro '" + sym.getName() + "'");
                return;
            }
            PcodeCompile::propagateSize(rtl); // Propagate size information (as much as possible)
            sym.setConstruct(rtl);
            symtab.popScope();      // Pop local variables used to define macro
            macrotable.Add(rtl);
        }

        /// \brief Record a NOP constructor at the current location
        ///
        /// The location is recorded and may be reported on after parsing.
        public void recordNop()
        {
            string msg = formatStatusMessage(getCurrentLocation(), "NOP detected");

            noplist.Add(msg);
        }

        // Virtual functions (not used by the compiler)
        public virtual void initialize(DocumentStorage store)
        {
        }

        public virtual int instructionLength(Address baseaddr)
        {
            return 0;
        }

        public virtual int oneInstruction(PcodeEmit emit, Address baseaddr)
        {
            return 0;
        }

        public virtual int printAssembly(AssemblyEmit emit, Address baseaddr)
        {
            return 0;
        }

        /// \brief Set all compiler options at the same time
        ///
        /// \param defines is map of \e variable to \e value that is passed to the preprocessor
        /// \param unnecessaryPcodeWarning is \b true for individual warnings about unnecessary p-code ops
        /// \param lenientConflict is \b false to report indistinguishable patterns as errors
        /// \param allCollisionWarning is \b true for individual warnings about constructors with colliding operands
        /// \param allNopWarning is \b true for individual warnings about NOP constructors
        /// \param deadTempWarning is \b true for individual warnings about dead temporary varnodes
        /// \param enforceLocalKeyWord is \b true to force all local variable definitions to use the \b local keyword
        /// \param largeTemporaryWarning is \b true for individual warnings about temporary varnodes that are too large
        /// \param caseSensitiveRegisterNames is \b true if register names are allowed to be case sensitive
        public void setAllOptions(Dictionary<string, string> defines, bool unnecessaryPcodeWarning,
             bool lenientConflict, bool allCollisionWarning, bool allNopWarning, bool deadTempWarning,
             bool enforceLocalKeyWord, bool largeTemporaryWarning, bool caseSensitiveRegisterNames)
        {
            Dictionary<string, string>::const_iterator iter = defines.begin();
            for (iter = defines.begin(); iter != defines.end(); iter++)
            {
                setPreprocValue((*iter).first, (*iter).second);
            }
            setUnnecessaryPcodeWarning(unnecessaryPcodeWarning);
            setLenientConflict(lenientConflict);
            setLocalCollisionWarning(allCollisionWarning);
            setAllNopWarning(allNopWarning);
            setDeadTempWarning(deadTempWarning);
            setEnforceLocalKeyWord(enforceLocalKeyWord);
            setLargeTemporaryWarning(largeTemporaryWarning);
            setInsensitiveDuplicateError(!caseSensitiveRegisterNames);
        }

        /// \brief Run the full compilation process, given a path to the specification file
        ///
        /// The specification file is opened and a parse is started.  Errors and warnings
        /// are printed to standard out, and if no fatal errors are encountered, the compiled
        /// form of the specification is written out.
        /// \param filein is the given path to the specification file to compile
        /// \param fileout is the path to output file
        /// \return an error code, where 0 indicates that a compiled file was successfully produced
        public int run_compilation(string filein, string fileout)
        {
            parseFromNewFile(filein);
            slgh = this;        // Set global pointer up for parser
            sleighin = fopen(filein.c_str(), "r");  // Open the file for the lexer
            if (sleighin == (FILE*)0)
            {
                cerr << "Unable to open specfile: " << filein << endl;
                return 2;
            }

            try
            {
                int parseres = sleighparse();  // Try to parse
                fclose(sleighin);
                if (parseres == 0)
                    process();  // Do all the post-processing
                if ((parseres == 0) && (numErrors() == 0))
                { // If no errors
                    ofstream s(fileout);
                    if (!s)
                    {
                        ostringstream errs;
                        errs << "Unable to open output file: " << fileout;
                        throw new SleighError(errs.str());
                    }
                    saveXml(s); // Dump output xml
                    s.close();
                }
                else
                {
                    cerr << "No output produced" << endl;
                    return 2;
                }
                sleighlex_destroy(); // Make sure lexer is reset so we can parse multiple files
            }
            catch (LowlevelError err) {
                cerr << "Unrecoverable error: " << err.ToString() << endl;
                return 2;
            }
            return 0;
        }
    }
}
