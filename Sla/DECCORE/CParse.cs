using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
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
        
        private Architecture glb;
        private Dictionary<string, uint4> keywords;
        private GrammarLexer lexer;
        private int4 lineno;
        private int colno;
        private int filenum;    // Location of last token
        private List<TypeDeclarator> typedec_alloc;
        private List<TypeSpecifiers> typespec_alloc;
        private List<List<uint4>> vecuint4_alloc;
        private List<List<TypeDeclarator>> vecdec_alloc;
        private List<string> string_alloc;
        private List<uintb> num_alloc;
        private List<Enumerator> enum_alloc;
        private List<List<Enumerator>> vecenum_alloc;

        private List<TypeDeclarator> lastdecls;
        private int4 firsttoken;        // Message to parser indicating desired object
        private string lasterror;

        private void setError(string msg)
        {
            ostringstream s;

            s << msg;
            lexer.writeLocation(s, lineno, filenum);
            s << '\n';
            lexer.writeTokenLocation(s, lineno, colno);
            lasterror = s.str();
        }

        private int4 lookupIdentifier(string nm)
        {
            map<string, uint4>::const_iterator iter = keywords.find(nm);
            if (iter != keywords.end())
            {
                switch ((*iter).second)
                {
                    case f_typedef:
                    case f_extern:
                    case f_static:
                    case f_auto:
                    case f_register:
                        return STORAGE_CLASS_SPECIFIER;
                    case f_const:
                    case f_restrict:
                    case f_volatile:
                        return TYPE_QUALIFIER;
                    case f_inline:
                        return FUNCTION_SPECIFIER;
                    case f_struct:
                        return STRUCT;
                    case f_union:
                        return UNION;
                    case f_enum:
                        return ENUM;
                    default:
                        break;
                }
            }
            Datatype* tp = glb->types->findByName(nm);
            if (tp != (Datatype*)0)
            {
                yylval.type = tp;
                return TYPE_NAME;
            }
            if (glb->hasModel(nm))
                return FUNCTION_SPECIFIER;
            return IDENTIFIER;      // Unknown identifier
        }

        private bool runParse(uint4 doctype)
        { // Assuming the stream has been setup, parse it
            switch (doctype)
            {
                case doc_declaration:
                    firsttoken = DECLARATION_RESULT;
                    break;
                case doc_parameter_declaration:
                    firsttoken = PARAM_RESULT;
                    break;
                default:
                    throw LowlevelError("Bad document type");
            }
            parse = this;           // Setup global object for yyparse
            int4 res = yyparse();
            if (res != 0)
            {
                if (lasterror.size() == 0)
                    setError("Syntax error");
                return false;
            }
            return true;
        }

        public CParse(Architecture g, int4 maxbuf)
        {
            lexer = new GrammarLexer(maxbuf);
            glb = g;
            firsttoken = -1;
            lineno = -1;
            colno = -1;
            filenum = -1;
            lastdecls = (vector<TypeDeclarator*>*)0;
            keywords["typedef"] = f_typedef;
            keywords["extern"] = f_extern;
            keywords["static"] = f_static;
            keywords["auto"] = f_auto;
            keywords["register"] = f_register;
            keywords["const"] = f_const;
            keywords["restrict"] = f_restrict;
            keywords["volatile"] = f_volatile;
            keywords["inline"] = f_inline;
            keywords["struct"] = f_struct;
            keywords["union"] = f_union;
            keywords["enum"] = f_enum;
        }

        ~CParse()
        {
            clearAllocation();
        }

        public void clear()
        {
            clearAllocation();
            lasterror.clear();
            lastdecls = (vector<TypeDeclarator*>*)0;
            lexer.clear();
            firsttoken = -1;
        }

        public List<TypeDeclarator> mergeSpecDecVec(TypeSpecifiers spec)
        {
            vector<TypeDeclarator*>* declist;
            declist = new vector<TypeDeclarator*>();
            vecdec_alloc.push_back(declist);
            TypeDeclarator* dec = new TypeDeclarator();
            typedec_alloc.push_back(dec);
            declist->push_back(dec);
            return mergeSpecDecVec(spec, declist);
        }

        public List<TypeDeclarator> mergeSpecDecVec(TypeSpecifiers spec,
            List<TypeDeclarator> declist)
        {
            for (uint4 i = 0; i < declist->size(); ++i)
                mergeSpecDec(spec, (*declist)[i]);
            return declist;
        }

        public TypeDeclarator mergeSpecDec(TypeSpecifiers spec)
        {
            dec->basetype = spec->type_specifier;
            dec->model = spec->function_specifier;
            dec->flags |= spec->flags;
            return dec;
        }

        public TypeDeclarator mergeSpecDec(TypeSpecifiers spec, TypeDeclarator dec)
        {
            dec->basetype = spec->type_specifier;
            dec->model = spec->function_specifier;
            dec->flags |= spec->flags;
            return dec;
        }

        public TypeSpecifiers addSpecifier(TypeSpecifiers spec, string str)
        {
            uint4 flag = convertFlag(str);
            spec->flags |= flag;
            return spec;
        }

        public TypeSpecifiers addTypeSpecifier(TypeSpecifiers spec, Datatype tp)
        {
            if (spec->type_specifier != (Datatype*)0)
                setError("Multiple type specifiers");
            spec->type_specifier = tp;
            return spec;
        }

        public TypeSpecifiers addFuncSpecifier(TypeSpecifiers spec, string str)
        {
            map<string, uint4>::const_iterator iter;

            iter = keywords.find(*str);
            if (iter != keywords.end())
                spec->flags |= (*iter).second; // A reserved specifier
            else
            {
                if (spec->function_specifier.size() != 0)
                    setError("Multiple parameter models");
                spec->function_specifier = *str;
            }
            return spec;
        }

        public TypeDeclarator mergePointer(List<uint4> ptr, TypeDeclarator dec)
        {
            for (uint4 i = 0; i < ptr->size(); ++i)
            {
                PointerModifier* newmod = new PointerModifier((*ptr)[i]);
                dec->mods.push_back(newmod);
            }
            return dec;
        }

        public TypeDeclarator newDeclarator(string str)
        {
            TypeDeclarator* res = new TypeDeclarator(*str);
            typedec_alloc.push_back(res);
            return res;
        }

        public TypeDeclarator newDeclarator()
        {
            TypeDeclarator* res = new TypeDeclarator();
            typedec_alloc.push_back(res);
            return res;
        }

        public TypeSpecifiers newSpecifier()
        {
            TypeSpecifiers* spec = new TypeSpecifiers();
            typespec_alloc.push_back(spec);
            return spec;
        }

        public List<TypeDeclarator> newVecDeclarator()
        {
            vector<TypeDeclarator*>* res = new vector<TypeDeclarator*>();
            vecdec_alloc.push_back(res);
            return res;
        }

        public List<uint4> newPointer()
        {
            vector<uint4>* res = new vector<uint4>();
            vecuint4_alloc.push_back(res);
            return res;
        }

        public TypeDeclarator newArray(TypeDeclarator dec, uint4 flags, uintb num)
        {
            ArrayModifier* newmod = new ArrayModifier(flags, (int4) * num);
            dec->mods.push_back(newmod);
            return dec;
        }

        public TypeDeclarator newFunc(TypeDeclarator dec, List<TypeDeclarator> declist)
        {
            bool dotdotdot = false;
            if (!declist->empty())
            {
                if (declist->back() == (TypeDeclarator*)0)
                {
                    dotdotdot = true;
                    declist->pop_back();
                }
            }
            FunctionModifier* newmod = new FunctionModifier(declist, dotdotdot);
            dec->mods.push_back(newmod);
            return dec;
        }

        public Datatype newStruct(string ident, List<TypeDeclarator> declist)
        { // Build a new structure
            TypeStruct* res = glb->types->getTypeStruct(ident); // Create stub (for recursion)
            vector<TypeField> sublist;

            for (uint4 i = 0; i < declist->size(); ++i)
            {
                TypeDeclarator* decl = (*declist)[i];
                if (!decl->isValid())
                {
                    setError("Invalid structure declarator");
                    glb->types->destroyType(res);
                    return (Datatype*)0;
                }
                sublist.emplace_back(0, -1, decl->getIdentifier(), decl->buildType(glb));
            }

            TypeStruct::assignFieldOffsets(sublist, glb->types->getStructAlign());
            if (!glb->types->setFields(sublist, res, -1, 0))
            {
                setError("Bad structure definition");
                glb->types->destroyType(res);
                return (Datatype*)0;
            }
            return res;
        }

        public Datatype oldStruct(string ident)
        {
            Datatype* res = glb->types->findByName(ident);
            if ((res == (Datatype*)0) || (res->getMetatype() != TYPE_STRUCT))
                setError("Identifier does not represent a struct as required");
            return res;
        }

        public Datatype newUnion(string ident, List<TypeDeclarator> declist)
        {
            TypeUnion* res = glb->types->getTypeUnion(ident); // Create stub (for recursion)
            vector<TypeField> sublist;

            for (uint4 i = 0; i < declist->size(); ++i)
            {
                TypeDeclarator* decl = (*declist)[i];
                if (!decl->isValid())
                {
                    setError("Invalid union declarator");
                    glb->types->destroyType(res);
                    return (Datatype*)0;
                }
                sublist.emplace_back(i, 0, decl->getIdentifier(), decl->buildType(glb));
            }

            if (!glb->types->setFields(sublist, res, -1, 0))
            {
                setError("Bad union definition");
                glb->types->destroyType(res);
                return (Datatype*)0;
            }
            return res;
        }

        public Datatype oldUnion(string ident)
        {
            Datatype* res = glb->types->findByName(ident);
            if ((res == (Datatype*)0) || (res->getMetatype() != TYPE_UNION))
                setError("Identifier does not represent a union as required");
            return res;
        }

        public Enumerator newEnumerator(string ident)
        {
            Enumerator* res = new Enumerator(ident);
            enum_alloc.push_back(res);
            return res;
        }

        public Enumerator newEnumerator(string ident,uintb val)
        {
            Enumerator* res = new Enumerator(ident, val);
            enum_alloc.push_back(res);
            return res;
        }

        public List<Enumerator> newVecEnumerator()
        {
            vector<Enumerator*>* res = new vector<Enumerator*>();
            vecenum_alloc.push_back(res);
            return res;
        }

        public Datatype newEnum(string ident, List<Enumerator> vecenum)
        {
            TypeEnum* res = glb->types->getTypeEnum(ident);
            vector<string> namelist;
            vector<uintb> vallist;
            vector<bool> assignlist;
            for (uint4 i = 0; i < vecenum->size(); ++i)
            {
                Enumerator* enumer = (*vecenum)[i];
                namelist.push_back(enumer->enumconstant);
                vallist.push_back(enumer->value);
                assignlist.push_back(enumer->constantassigned);
            }
            if (!glb->types->setEnumValues(namelist, vallist, assignlist, res))
            {
                setError("Bad enumeration values");
                glb->types->destroyType(res);
                return (Datatype*)0;
            }
            return res;
        }

        public Datatype oldEnum(string ident)
        {
            Datatype* res = glb->types->findByName(ident);
            if ((res == (Datatype*)0) || (!res->isEnumType()))
                setError("Identifier does not represent an enum as required");
            return res;
        }

        public uint4 convertFlag(string str)
        {
            map<string, uint4>::const_iterator iter;

            iter = keywords.find(*str);
            if (iter != keywords.end())
                return (*iter).second;
            setError("Unknown qualifier");
            return 0;
        }

        public void clearAllocation()
        {
            list<TypeDeclarator*>::iterator iter1;

            for (iter1 = typedec_alloc.begin(); iter1 != typedec_alloc.end(); ++iter1)
                delete* iter1;
            typedec_alloc.clear();

            list<TypeSpecifiers*>::iterator iter2;
            for (iter2 = typespec_alloc.begin(); iter2 != typespec_alloc.end(); ++iter2)
                delete* iter2;
            typespec_alloc.clear();

            list<vector<uint4>*>::iterator iter3;
            for (iter3 = vecuint4_alloc.begin(); iter3 != vecuint4_alloc.end(); ++iter3)
                delete* iter3;
            vecuint4_alloc.clear();

            list<vector<TypeDeclarator*>*>::iterator iter4;
            for (iter4 = vecdec_alloc.begin(); iter4 != vecdec_alloc.end(); ++iter4)
                delete* iter4;
            vecdec_alloc.clear();

            list<string*>::iterator iter5;
            for (iter5 = string_alloc.begin(); iter5 != string_alloc.end(); ++iter5)
                delete* iter5;
            string_alloc.clear();

            list<uintb*>::iterator iter6;
            for (iter6 = num_alloc.begin(); iter6 != num_alloc.end(); ++iter6)
                delete* iter6;
            num_alloc.clear();

            list<Enumerator*>::iterator iter7;
            for (iter7 = enum_alloc.begin(); iter7 != enum_alloc.end(); ++iter7)
                delete* iter7;
            enum_alloc.clear();

            list<vector<Enumerator*>*>::iterator iter8;
            for (iter8 = vecenum_alloc.begin(); iter8 != vecenum_alloc.end(); ++iter8)
                delete* iter8;
            vecenum_alloc.clear();
        }

        public int4 lex()
        {
            GrammarToken tok;

            if (firsttoken != -1)
            {
                int4 retval = firsttoken;
                firsttoken = -1;
                return retval;
            }
            if (lasterror.size() != 0)
                return BADTOKEN;
            lexer.getNextToken(tok);
            lineno = tok.getLineNo();
            colno = tok.getColNo();
            filenum = tok.getFileNum();
            switch (tok.getType())
            {
                case GrammarToken::integer:
                case GrammarToken::charconstant:
                    yylval.i = new uintb(tok.getInteger());
                    num_alloc.push_back(yylval.i);
                    return NUMBER;
                case GrammarToken::identifier:
                    yylval.str = tok.getString();
                    string_alloc.push_back(yylval.str);
                    return lookupIdentifier(*yylval.str);
                case GrammarToken::stringval:
                    delete tok.getString();
                    setError("Illegal string constant");
                    return BADTOKEN;
                case GrammarToken::dotdotdot:
                    return DOTDOTDOT;
                case GrammarToken::badtoken:
                    setError(lexer.getError()); // Error from lexer
                    return BADTOKEN;
                case GrammarToken::endoffile:
                    return -1;          // No more tokens
                default:
                    return (int4)tok.getType();
            }
        }

        public bool parseFile(string filename, uint4 doctype)
        { // Run the parser on a file, return true if no parse errors
            clear();            // Clear out any old parsing

            ifstream s(nm.c_str()); // open file
            if (!s)
                throw LowlevelError("Unable to open file for parsing: " + nm);

            lexer.pushFile(nm, &s);     // Inform lexer of filename and stream
            bool res = runParse(doctype);
            s.close();
            return res;
        }

        public bool parseStream(istream s, uint4 doctype)
        {
            clear();

            lexer.pushFile("stream", &s);
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
