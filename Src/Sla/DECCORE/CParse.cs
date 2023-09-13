using Sla.CORE;

namespace Sla.DECCORE
{
    internal class CParse
    {
        [Flags()]
        public enum Flags
        {
            f_typedef = 1,
            f_extern = 2,
            f_static = 4,
            f_auto = 8,
            f_register = 16,
            f_const = 32,
            f_restrict = 64,
            f_volatile = 128,
            f_inline = 256,
            f_struct = 512,
            f_union = 1024,
            f_enum = 2048
        }
        
        public enum DocType
        {
            doc_declaration,
            doc_parameter_declaration
        }

        // #define yylval grammarlval
        // #define yyparse grammarparse
        private Grammar.GRAMMARSTYPE grammarlval;

        private Architecture glb;
        private Dictionary<string, Flags> keywords = new Dictionary<string, Flags>();
        private GrammarLexer lexer;
        private int lineno;
        private int colno;
        // Location of last token
        private int filenum;
        private List<TypeDeclarator> typedec_alloc = new List<TypeDeclarator>();
        private List<TypeSpecifiers> typespec_alloc = new List<TypeSpecifiers>();
        private List<List<uint>> vecuint4_alloc = new List<List<uint>>();
        private List<List<TypeDeclarator>> vecdec_alloc = new List<List<TypeDeclarator>>();
        private List<string> string_alloc = new List<string>();
        private List<ulong> num_alloc = new List<ulong>();
        private List<Enumerator> enum_alloc = new List<Enumerator>();
        private List<List<Enumerator>> vecenum_alloc = new List<List<Enumerator>>();

        private List<TypeDeclarator> lastdecls = new List<TypeDeclarator>();
        // Message to parser indicating desired object
        private Grammar.grammartokentype firsttoken;
        private string lasterror;

        private void setError(string msg)
        {
            StringWriter s = new StringWriter();

            s.Write(msg);
            lexer.writeLocation(s, lineno, filenum);
            s.WriteLine();
            lexer.writeTokenLocation(s, lineno, colno);
            lasterror = s.ToString();
        }

        private Grammar.grammartokentype lookupIdentifier(string nm)
        {
            Flags result;
            if (keywords.TryGetValue(nm, out result)) {
                switch (result) {
                    case Flags.f_typedef:
                    case Flags.f_extern:
                    case Flags.f_static:
                    case Flags.f_auto:
                    case Flags.f_register:
                        return Grammar.grammartokentype.STORAGE_CLASS_SPECIFIER;
                    case Flags.f_const:
                    case Flags.f_restrict:
                    case Flags.f_volatile:
                        return Grammar.grammartokentype.TYPE_QUALIFIER;
                    case Flags.f_inline:
                        return Grammar.grammartokentype.FUNCTION_SPECIFIER;
                    case Flags.f_struct:
                        return Grammar.grammartokentype.STRUCT;
                    case Flags.f_union:
                        return Grammar.grammartokentype.UNION;
                    case Flags.f_enum:
                        return Grammar.grammartokentype.ENUM;
                    default:
                        break;
                }
            }
            Datatype? tp = glb.types.findByName(nm);
            if (tp != (Datatype)null) {
                grammarlval.type = tp;
                return Grammar.grammartokentype.TYPE_NAME;
            }
            if (glb.hasModel(nm))
                return Grammar.grammartokentype.FUNCTION_SPECIFIER;
            // Unknown identifier
            return Grammar.grammartokentype.IDENTIFIER;
        }

        private bool runParse(DocType doctype)
        {
            // Assuming the stream has been setup, parse it
            switch (doctype) {
                case DocType.doc_declaration:
                    firsttoken = Grammar.grammartokentype.DECLARATION_RESULT;
                    break;
                case DocType.doc_parameter_declaration:
                    firsttoken = Grammar.grammartokentype.PARAM_RESULT;
                    break;
                default:
                    throw new LowlevelError("Bad document type");
            }
            // Setup global object for yyparse
            parse = this;
            int res = grammarparse();
            if (res != 0) {
                if (lasterror.Length == 0)
                    setError("Syntax error");
                return false;
            }
            return true;
        }

        public CParse(Architecture g, int maxbuf)
        {
            lexer = new GrammarLexer(maxbuf);
            glb = g;
            firsttoken = (Grammar.grammartokentype)(-1);
            lineno = -1;
            colno = -1;
            filenum = -1;
            lastdecls = (List<TypeDeclarator>)null;
            keywords["typedef"] = Flags.f_typedef;
            keywords["extern"] = Flags.f_extern;
            keywords["static"] = Flags.f_static;
            keywords["auto"] = Flags.f_auto;
            keywords["register"] = Flags.f_register;
            keywords["const"] = Flags.f_const;
            keywords["restrict"] = Flags.f_restrict;
            keywords["volatile"] = Flags.f_volatile;
            keywords["inline"] = Flags.f_inline;
            keywords["struct"] = Flags.f_struct;
            keywords["union"] = Flags.f_union;
            keywords["enum"] = Flags.f_enum;
        }

        ~CParse()
        {
            clearAllocation();
        }

        public void clear()
        {
            clearAllocation();
            lasterror = string.Empty;
            lastdecls = (List<TypeDeclarator>)null;
            lexer.clear();
            firsttoken = (Grammar.grammartokentype)(-1);
        }

        public List<TypeDeclarator> mergeSpecDecVec(TypeSpecifiers spec)
        {
            List<TypeDeclarator> declist = new List<TypeDeclarator>();
            vecdec_alloc.Add(declist);
            TypeDeclarator dec = new TypeDeclarator();
            typedec_alloc.Add(dec);
            declist.Add(dec);
            return mergeSpecDecVec(spec, declist);
        }

        public List<TypeDeclarator> mergeSpecDecVec(TypeSpecifiers spec,
            List<TypeDeclarator> declist)
        {
            for (int i = 0; i < declist.size(); ++i)
                mergeSpecDec(spec, declist[i]);
            return declist;
        }

        public TypeDeclarator mergeSpecDec(TypeSpecifiers spec)
        {
            TypeDeclarator dec = new TypeDeclarator();
            typedec_alloc.Add(dec);
            return mergeSpecDec(spec, dec);
        }

        public TypeDeclarator mergeSpecDec(TypeSpecifiers spec, TypeDeclarator dec)
        {
            dec.basetype = spec.type_specifier;
            dec.model = spec.function_specifier;
            dec.flags |= spec.flags;
            return dec;
        }

        public TypeSpecifiers addSpecifier(TypeSpecifiers spec, string str)
        {
            Flags flag = convertFlag(str);
            spec.flags |= flag;
            return spec;
        }

        public TypeSpecifiers addTypeSpecifier(TypeSpecifiers spec, Datatype tp)
        {
            if (spec.type_specifier != (Datatype)null)
                setError("Multiple type specifiers");
            spec.type_specifier = tp;
            return spec;
        }

        public TypeSpecifiers addFuncSpecifier(TypeSpecifiers spec, string str)
        {
            Flags result;
            if (keywords.TryGetValue(str, out result))
                spec.flags |= result; // A reserved specifier
            else {
                if (spec.function_specifier.Length != 0)
                    setError("Multiple parameter models");
                spec.function_specifier = str;
            }
            return spec;
        }

        public TypeDeclarator mergePointer(List<uint> ptr, TypeDeclarator dec)
        {
            for (int i = 0; i < ptr.size(); ++i) {
                PointerModifier newmod = new PointerModifier(ptr[i]);
                dec.mods.Add(newmod);
            }
            return dec;
        }

        public TypeDeclarator newDeclarator(string str)
        {
            TypeDeclarator res = new TypeDeclarator(str);
            typedec_alloc.Add(res);
            return res;
        }

        public TypeDeclarator newDeclarator()
        {
            TypeDeclarator res = new TypeDeclarator();
            typedec_alloc.Add(res);
            return res;
        }

        public TypeSpecifiers newSpecifier()
        {
            TypeSpecifiers spec = new TypeSpecifiers();
            typespec_alloc.Add(spec);
            return spec;
        }

        public List<TypeDeclarator> newVecDeclarator()
        {
            List<TypeDeclarator> res = new List<TypeDeclarator>();
            vecdec_alloc.Add(res);
            return res;
        }

        public List<uint> newPointer()
        {
            List<uint> res = new List<uint>();
            vecuint4_alloc.Add(res);
            return res;
        }

        public TypeDeclarator newArray(TypeDeclarator dec, uint flags, ulong num)
        {
            ArrayModifier newmod = new ArrayModifier(flags, (int)num);
            dec.mods.Add(newmod);
            return dec;
        }

        public TypeDeclarator newFunc(TypeDeclarator dec, List<TypeDeclarator> declist)
        {
            bool dotdotdot = false;
            if (!declist.empty()) {
                if (declist.GetLastItem() == (TypeDeclarator)null) {
                    dotdotdot = true;
                    declist.RemoveLastItem();
                }
            }
            FunctionModifier newmod = new FunctionModifier(declist, dotdotdot);
            dec.mods.Add(newmod);
            return dec;
        }

        public Datatype? newStruct(string ident, List<TypeDeclarator> declist)
        {
            // Build a new structure
            TypeStruct res = glb.types.getTypeStruct(ident); // Create stub (for recursion)
            List<TypeField> sublist = new List<TypeField>();

            for (int i = 0; i < declist.size(); ++i) {
                TypeDeclarator decl = declist[i];
                if (!decl.isValid()) {
                    setError("Invalid structure declarator");
                    glb.types.destroyType(res);
                    return (Datatype)null;
                }
                sublist.Add(new TypeField(0, -1, decl.getIdentifier(), decl.buildType(glb)));
            }

            TypeStruct.assignFieldOffsets(sublist, glb.types.getStructAlign());
            if (!glb.types.setFields(sublist, res, -1, 0)) {
                setError("Bad structure definition");
                glb.types.destroyType(res);
                return (Datatype)null;
            }
            return res;
        }

        public Datatype? oldStruct(string ident)
        {
            Datatype? res = glb.types.findByName(ident);
            if ((res == (Datatype)null) || (res.getMetatype() != type_metatype.TYPE_STRUCT))
                setError("Identifier does not represent a struct as required");
            return res;
        }

        public Datatype newUnion(string ident, List<TypeDeclarator> declist)
        {
            TypeUnion res = glb.types.getTypeUnion(ident); // Create stub (for recursion)
            List<TypeField> sublist = new List<TypeField>();

            for (int i = 0; i < declist.size(); ++i) {
                TypeDeclarator decl = declist[i];
                if (!decl.isValid()) {
                    setError("Invalid union declarator");
                    glb.types.destroyType(res);
                    return (Datatype)null;
                }
                sublist.Add(new TypeField(i, 0, decl.getIdentifier(), decl.buildType(glb)));
            }

            if (!glb.types.setFields(sublist, res, -1, 0)) {
                setError("Bad union definition");
                glb.types.destroyType(res);
                return (Datatype)null;
            }
            return res;
        }

        public Datatype? oldUnion(string ident)
        {
            Datatype? res = glb.types.findByName(ident);
            if ((res == (Datatype)null) || (res.getMetatype() != type_metatype.TYPE_UNION))
                setError("Identifier does not represent a union as required");
            return res;
        }

        public Enumerator newEnumerator(string ident)
        {
            Enumerator res = new Enumerator(ident);
            enum_alloc.Add(res);
            return res;
        }

        public Enumerator newEnumerator(string ident,ulong val)
        {
            Enumerator res = new Enumerator(ident, val);
            enum_alloc.Add(res);
            return res;
        }

        public List<Enumerator> newVecEnumerator()
        {
            List<Enumerator> res = new List<Enumerator>();
            vecenum_alloc.Add(res);
            return res;
        }

        public Datatype newEnum(string ident, List<Enumerator> vecenum)
        {
            TypeEnum res = glb.types.getTypeEnum(ident);
            List<string> namelist = new List<string>();
            List<ulong> vallist = new List<ulong>();
            List<bool> assignlist = new List<bool>();
            for (int i = 0; i < vecenum.size(); ++i) {
                Enumerator enumer = vecenum[i];
                namelist.Add(enumer.enumconstant);
                vallist.Add(enumer.value);
                assignlist.Add(enumer.constantassigned);
            }
            if (!glb.types.setEnumValues(namelist, vallist, assignlist, res)) {
                setError("Bad enumeration values");
                glb.types.destroyType(res);
                return (Datatype)null;
            }
            return res;
        }

        public Datatype? oldEnum(string ident)
        {
            Datatype? res = glb.types.findByName(ident);
            if ((res == (Datatype)null) || (!res.isEnumType()))
                setError("Identifier does not represent an enum as required");
            return res;
        }

        public Flags convertFlag(string str)
        {
            Flags flags;

            if (keywords.TryGetValue(str, out flags))
                return flags;
            setError("Unknown qualifier");
            return 0;
        }

        public void clearAllocation()
        {
            //list<TypeDeclarator*>::iterator iter1;

            //for (iter1 = typedec_alloc.begin(); iter1 != typedec_alloc.end(); ++iter1)
            //    delete* iter1;
            typedec_alloc.Clear();

            //list<TypeSpecifiers*>::iterator iter2;
            //for (iter2 = typespec_alloc.begin(); iter2 != typespec_alloc.end(); ++iter2)
            //    delete* iter2;
            typespec_alloc.Clear();

            //list<List<uint>*>::iterator iter3;
            //for (iter3 = vecuint4_alloc.begin(); iter3 != vecuint4_alloc.end(); ++iter3)
            //    delete* iter3;
            vecuint4_alloc.Clear();

            //list<List<TypeDeclarator*>*>::iterator iter4;
            //for (iter4 = vecdec_alloc.begin(); iter4 != vecdec_alloc.end(); ++iter4)
            //    delete* iter4;
            vecdec_alloc.Clear();

            //list<string*>::iterator iter5;
            //for (iter5 = string_alloc.begin(); iter5 != string_alloc.end(); ++iter5)
            //    delete* iter5;
            string_alloc.Clear();

            //list<ulong*>::iterator iter6;
            //for (iter6 = num_alloc.begin(); iter6 != num_alloc.end(); ++iter6)
            //    delete* iter6;
            num_alloc.Clear();

            //list<Enumerator*>::iterator iter7;
            //for (iter7 = enum_alloc.begin(); iter7 != enum_alloc.end(); ++iter7)
            //    delete* iter7;
            enum_alloc.Clear();

            //list<List<Enumerator*>*>::iterator iter8;
            //for (iter8 = vecenum_alloc.begin(); iter8 != vecenum_alloc.end(); ++iter8)
            //    delete* iter8;
            vecenum_alloc.Clear();
        }

        public Grammar.grammartokentype lex()
        {
            GrammarToken tok = new GrammarToken();

            if (firsttoken != (Grammar.grammartokentype)(-1)) {
                Grammar.grammartokentype retval = firsttoken;
                firsttoken = (Grammar.grammartokentype)(-1);
                return retval;
            }
            if (lasterror.Length != 0)
                return Grammar.grammartokentype.BADTOKEN;
            lexer.getNextToken(tok);
            lineno = tok.getLineNo();
            colno = tok.getColNo();
            filenum = tok.getFileNum();
            switch (tok.getType()) {
                case GrammarToken.Token.integer:
                case GrammarToken.Token.charconstant:
                    grammarlval.i = tok.getInteger();
                    num_alloc.Add(grammarlval.i);
                    return Grammar.grammartokentype.NUMBER;
                case GrammarToken.Token.identifier:
                    grammarlval.str = tok.getString();
                    string_alloc.Add(grammarlval.str);
                    return lookupIdentifier(grammarlval.str);
                case GrammarToken.Token.stringval:
                    // delete tok.getString();
                    setError("Illegal string constant");
                    return Grammar.grammartokentype.BADTOKEN;
                case GrammarToken.Token.dotdotdot:
                    return Grammar.grammartokentype.DOTDOTDOT;
                case GrammarToken.Token.badtoken:
                    setError(lexer.getError()); // Error from lexer
                    return Grammar.grammartokentype.BADTOKEN;
                case GrammarToken.Token.endoffile:
                    // No more tokens
                    return (Grammar.grammartokentype)(-1);
                default:
                    return (Grammar.grammartokentype)tok.getType();
            }
        }

        public bool parseFile(string filename, DocType doctype)
        {
            // Run the parser on a file, return true if no parse errors
            clear();            // Clear out any old parsing

            FileStream s;

            try { s = File.OpenRead(filename); }// open file
            catch {
                throw new LowlevelError($"Unable to open file for parsing: {filename}");
            }

            // Inform lexer of filename and stream
            lexer.pushFile(filename, new StreamReader(s));
            bool res = runParse(doctype);
            s.Close();
            return res;
        }

        public bool parseStream(TextReader s, DocType doctype)
        {
            clear();

            lexer.pushFile("stream", s);
            return runParse(doctype);
        }

        public string getError() => lasterror;

        public void setResultDeclarations(List<TypeDeclarator> val)
        {
            lastdecls = val;
        }

        public List<TypeDeclarator> getResultDeclarations() => lastdecls;
    }
}
