/* ###
 * IP: GHIDRA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/* A Bison parser, made by GNU Bison 3.0.4.  */

/* Bison implementation for Yacc-like parsers in C

   Copyright (C) 1984, 1989-1990, 2000-2015 Free Software Foundation, Inc.

   This program is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with this program.  If not, see <http://www.gnu.org/licenses/>.  */

/* As a special exception, you may create a larger work that contains
   part or all of the Bison parser skeleton and distribute that work
   under terms of your choice, so long as that work isn't itself a
   parser generator using the skeleton or a modified version thereof
   as a parser skeleton.  Alternatively, if you modify or redistribute
   the parser skeleton itself, you may (at your option) remove this
   special exception, which will cause the skeleton and the resulting
   Bison output files to be licensed under the GNU General Public
   License without this special exception.

   This special exception was added by the Free Software Foundation in
   version 2.2 of Bison.  */

/* C LALR(1) parser skeleton written by Richard Stallman, by
   simplifying the original so-called "semantic" parser.  */

/* All symbols defined below should begin with yy or YY, to avoid
   infringing on user name space.  This should be done even for local
   variables, as they might otherwise be expanded by user macros.
   There are some unavoidable exceptions within include files to
   define necessary library symbols; they are noted "INFRINGES ON
   USER NAME SPACE" below.  */

// typedef void *Locator;		///< Placeholder for a document locator object
using System.Numerics;
using System.Security.AccessControl;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using ghidra;
using Locator = object;
using System.Runtime.InteropServices.JavaScript;
using System.Linq;
// typedef List<Element *> List;		///< A list of XML elements

///* Identify Bison output.  */
//#define YYBISON 1

///* Bison version.  */
//#define YYBISON_VERSION "3.0.4"

///* Skeleton name.  */
//#define YYSKELETON_NAME "yacc.c"

///* Pure parsers.  */
//#define YYPURE 0

///* Push parsers.  */
//#define YYPUSH 0

///* Pull parsers.  */
//#define YYPULL 1

///* Substitute the type names.  */
//#define YYSTYPE         XMLSTYPE
///* Substitute the variable and function names.  */
//#define yylex           xmllex
//#define yyerror         xmlerror
//#define yydebug         xmldebug
//#define yynerrs         xmlnerrs

//#define yylval          xmllval
//#define yychar          xmlchar

/* Copy the first part of user declarations.  */

//#include "xml.hh"
// CharData mode   look for '<' '&' or "]]>"
// Name mode       look for non-name char
// CData mode      looking for "]]>"
// Entity mode     looking for ending ';'
// AttValue mode   looking for endquote  or '&'
// Comment mode    looking for "--"

//#include <iostream>
//#include <string>


namespace Sla.CORE {
    public static class Xml
    {


        // -------------------------- Xml related -----------------------------
        /// \brief Start-up the XML parser given a stream and a handler
        /// This runs the low-level XML parser.
        /// \param i is the given stream to get character data from
        /// \param hand is the ContentHandler that stores or processes the XML content events
        /// \param dbg is non-zero if the parser should output debug information during its parse
        /// \return 0 if there is no error during parsing or a (non-zero) error condition
        internal static int xml_parse(TextReader i, ContentHandler hand, int dbg = 0)
        {
            //#if YYDEBUG
            //  yydebug = dbg;
            //#endif
            Parsing.global_scan = new XmlScan(i);
            try
            {
                Parsing.handler = hand;
                Parsing.handler.startDocument();
                int result = Parsing.xmlparse();
                if (0 == result)
                {
                    Parsing.handler.endDocument();
                }
                return result;
            }
            finally
            {
                if (null != Parsing.global_scan)
                {
                    Parsing.global_scan.Dispose();
                    Parsing.global_scan = null;
                }
            }
        }

        /// \brief Parse the given XML stream into an in-memory document
        /// The stream is parsed using the standard ContentHandler for producing an in-memory
        /// DOM representation of the XML document.
        /// \param i is the given stream
        /// \return the in-memory XML document
        internal static Document xml_tree(TextReader i)
        {
            Document doc = new Document();
            TreeHandler handle = new TreeHandler(doc);
            if (0 != xml_parse(i, handle)) {
                // delete doc;
                throw new DecoderError(handle.getError() ?? "NULL");
            }
            return doc;
        }

        /// \brief Send the given character array to a stream, escaping characters with special XML meaning
        /// This makes the following character substitutions:
        ///   - '<' =>  "&lt;"
        ///   - '>' =>  "&gt;"
        ///   - '&' =>  "&amp;"
        ///   - '"' =>  "&quot;"
        ///   - '\'' => "&apos;"
        ///
        /// \param s is the stream to write to
        /// \param str is the given character array to escape
        internal static void xml_escape(TextWriter s, string str)
        {
            int stringLength = str.Length;
            for (int index = 0; index < stringLength; index++) {
                char scannedCharacter = str[index];
                if ('\0' == scannedCharacter) {
                    break;
                }
                if ('?' <= scannedCharacter) {
                    s.Write(str);
                    continue;
                }
                switch (scannedCharacter) {
                    case '<':
                        s.Write("&lt;");
                        break;
                    case '>':
                        s.Write("&gt;");
                        break;
                    case '&':
                        s.Write("&amp;");
                        break;
                    case '"':
                        s.Write("&quot;");
                        break;
                    case '\'':
                        s.Write("&apos;");
                        break;
                    default:
                        s.Write(scannedCharacter);
                        break;
                }
            }
        }

        // Some helper functions for writing XML documents directly to a stream
        /// \brief Output an XML attribute name/value pair to stream
        /// \param s is the output stream
        /// \param attr is the name of the attribute
        /// \param val is the attribute value
        internal static void a_v(TextWriter s, string attr, string val)
        {
            s.Write(' ');
            s.Write(attr);
            s.Write("=\"");
            xml_escape(s, val);
            s.Write("\"");
        }

        /// \brief Output the given signed integer as an XML attribute value
        /// \param s is the output stream
        /// \param attr is the name of the attribute
        /// \param val is the given integer value
        internal static void a_v_i(TextWriter s, string attr, long val)
        {
            s.Write(' ');
            s.Write(attr);
            s.Write("=\"");
            s.Write(val);
            s.Write("\"");
        }

        /// \brief Output the given unsigned integer as an XML attribute value
        /// \param s is the output stream
        /// \param attr is the name of the attribute
        /// \param val is the given unsigned integer value
        internal static void a_v_u(TextWriter s, string attr, ulong val)
        {
            s.Write(' ');
            s.Write(attr);
            s.Write("=\"0x{0:X}\"", val);
        }

        /// \brief Output the given boolean value as an XML attribute
        /// \param s is the output stream
        /// \param attr is the name of the attribute
        /// \param val is the given boolean value
        internal static void a_v_b(TextWriter s, string attr, bool val)
        {
            s.Write(' ');
            s.Write(attr);
            s.Write("=\"");
            s.Write(val ? "true" : "false");
            s.Write("\"");
        }

        /// \brief Read an XML attribute value as a boolean
        /// This method is intended to recognize the strings, "true", "yes", and "1"
        /// as a \b true value.  Anything else is returned as \b false.
        /// \param attr is the given XML attribute value (as a string)
        /// \return either \b true or \b false
        internal static bool xml_readbool(string attr)
        {
            if (0 == attr.Length)
            {
                return false;
            }
            char firstc = attr[0];
            // For backward compatibility
            return (firstc == 't')
                || (firstc == '1')
                || (firstc == 'y');
        }

        /// Interface to the scanner
        //extern int xmllex(void);
        /// Interface for registering an error in parsing
        //extern int xmlerror(const char *str);
        //# ifndef YY_NULLPTR
        //#  if defined __cplusplus && 201103L <= __cplusplus
        //#   define YY_NULLPTR nullptr
        //#  else
        //#   define YY_NULLPTR 0
        //#  endif
        //# endif

        ///* Enabling verbose error messages.  */
        //#ifdef YYERROR_VERBOSE
        //# undef YYERROR_VERBOSE
        //# define YYERROR_VERBOSE 1
        //#else
        //# define YYERROR_VERBOSE 0
        //#endif

        ///* Debug traces.  */
        //#ifndef XMLDEBUG
        //# if defined YYDEBUG
        //#if YYDEBUG
        //#   define XMLDEBUG 1
        //#  else
        //#   define XMLDEBUG 0
        //#  endif
        //# else /* ! defined YYDEBUG */
        //#  define XMLDEBUG 0
        //# endif /* ! defined YYDEBUG */
        //#endif  /* ! defined XMLDEBUG */
        //#if XMLDEBUG
        //extern int xmldebug;
        //#endif

        /* Token type.  */
        //#ifndef XMLTOKENTYPE
        //#define XMLTOKENTYPE
        public enum xmltokentype
        {
            CHARDATA = 258,
            CDATA = 259,
            ATTVALUE = 260,
            COMMENT = 261,
            CHARREF = 262,
            NAME = 263,
            SNAME = 264,
            ELEMBRACE = 265,
            COMMBRACE = 266
        }
        //#endif

        /* Value type.  */
        //#if ! defined XMLSTYPE && ! defined XMLSTYPE_IS_DECLARED

        internal class /*union*/ XMLSTYPE
        {
            public int? i;
            public string? str;
            public Attributes? attr;
            public NameValue? pair;
        }

        private const int XMLSTYPE_IS_TRIVIAL = 1;
        private const int XMLSTYPE_IS_DECLARED = 1;
        //#endif

        // extern XMLSTYPE xmllval;

        /* Copy the second part of user declarations.  */
        //#ifdef short
        //# undef short
        //#endif

        //#ifndef YY_
        //# if defined YYENABLE_NLS && YYENABLE_NLS
        //#  if ENABLE_NLS
        //#   include <libintl.h> /* INFRINGES ON USER NAME SPACE */
        //#   define YY_(Msgid) dgettext ("bison-runtime", Msgid)
        //#  endif
        //# endif
        //# ifndef YY_
        //#  define YY_(Msgid) Msgid
        //# endif
        //#endif

        //#ifndef YY_ATTRIBUTE
        //# if (defined __GNUC__                                               \
        //      && (2 < __GNUC__ || (__GNUC__ == 2 && 96 <= __GNUC_MINOR__)))  \
        //     || defined __SUNPRO_C && 0x5110 <= __SUNPRO_C
        //#  define YY_ATTRIBUTE(Spec) __attribute__(Spec)
        //# else
        //#  define YY_ATTRIBUTE(Spec) /* empty */
        //# endif
        //#endif

        //#ifndef YY_ATTRIBUTE_PURE
        //# define YY_ATTRIBUTE_PURE   YY_ATTRIBUTE ((__pure__))
        //#endif

        //#ifndef YY_ATTRIBUTE_UNUSED
        //# define YY_ATTRIBUTE_UNUSED YY_ATTRIBUTE ((__unused__))
        //#endif

        //#if !defined _Noreturn \
        //     && (!defined __STDC_VERSION__ || __STDC_VERSION__ < 201112)
        //# if defined _MSC_VER && 1200 <= _MSC_VER
        //#  define _Noreturn __declspec (noreturn)
        //# else
        //#  define _Noreturn YY_ATTRIBUTE ((__noreturn__))
        //# endif
        //#endif

        ///* Suppress unused-variable warnings by "using" E.  */
        //#if ! defined lint || defined __GNUC__
        //# define YYUSE(E) ((void) (E))
        //#else
        //# define YYUSE(E) /* empty */
        //#endif

        //#if defined __GNUC__ && 407 <= __GNUC__ * 100 + __GNUC_MINOR__
        ///* Suppress an incorrect diagnostic about yylval being uninitialized.  */
        //# define YY_IGNORE_MAYBE_UNINITIALIZED_BEGIN \
        //    _Pragma ("GCC diagnostic push") \
        //    _Pragma ("GCC diagnostic ignored \"-Wuninitialized\"")\
        //    _Pragma ("GCC diagnostic ignored \"-Wmaybe-uninitialized\"")
        //# define YY_IGNORE_MAYBE_UNINITIALIZED_END \
        //    _Pragma ("GCC diagnostic pop")
        //#else
        //# define YY_INITIAL_VALUE(Value) Value
        //#endif
        //#ifndef YY_IGNORE_MAYBE_UNINITIALIZED_BEGIN
        //# define YY_IGNORE_MAYBE_UNINITIALIZED_BEGIN
        //# define YY_IGNORE_MAYBE_UNINITIALIZED_END
        //#endif
        //#ifndef YY_INITIAL_VALUE
        //# define YY_INITIAL_VALUE(Value) /* Nothing. */
        //#endif


        //#if ! defined yyoverflow || YYERROR_VERBOSE

        ///* The parser invokes alloca or malloc; define the necessary symbols.  */

        //# ifdef YYSTACK_USE_ALLOCA
        //#  if YYSTACK_USE_ALLOCA
        //#   ifdef __GNUC__
        //#    define YYSTACK_ALLOC __builtin_alloca
        //#   elif defined __BUILTIN_VA_ARG_INCR
        //#    include <alloca.h> /* INFRINGES ON USER NAME SPACE */
        //#   elif defined _AIX
        //#    define YYSTACK_ALLOC __alloca
        //#   elif defined _MSC_VER
        //#    include <malloc.h> /* INFRINGES ON USER NAME SPACE */
        //#    define alloca _alloca
        //#   else
        //#    define YYSTACK_ALLOC alloca
        //#    if ! defined _ALLOCA_H && ! defined EXIT_SUCCESS
        //#     include <stdlib.h> /* INFRINGES ON USER NAME SPACE */
        //      /* Use EXIT_SUCCESS as a witness for stdlib.h.  */
        //#     ifndef EXIT_SUCCESS
        //#      define EXIT_SUCCESS 0
        //#     endif
        //#    endif
        //#   endif
        //#  endif
        //# endif

        //# ifdef YYSTACK_ALLOC
        //   /* Pacify GCC's 'empty if-body' warning.  */
        //#  define YYSTACK_FREE(Ptr) do { /* empty */; } while (0)
        //#  ifndef YYSTACK_ALLOC_MAXIMUM
        //    /* The OS might guarantee only one guard page at the bottom of the stack,
        //       and a page size can be as small as 4096 bytes.  So we cannot safely
        //       invoke alloca (N) if N exceeds 4096.  Use a slightly smaller number
        //       to allow for a few compiler-allocated temporary stack slots.  */
        //#   define YYSTACK_ALLOC_MAXIMUM 4032 /* reasonable circa 2006 */
        //#  endif
        //# else
        //#  define YYSTACK_ALLOC YYMALLOC
        //#  define YYSTACK_FREE YYFREE
        //#  ifndef YYSTACK_ALLOC_MAXIMUM
        //#   define YYSTACK_ALLOC_MAXIMUM YYSIZE_MAXIMUM
        //#  endif
        //#  if (defined __cplusplus && ! defined EXIT_SUCCESS \
        //       && ! ((defined YYMALLOC || defined malloc) \
        //             && (defined YYFREE || defined free)))
        //#   include <stdlib.h> /* INFRINGES ON USER NAME SPACE */
        //#   ifndef EXIT_SUCCESS
        //#    define EXIT_SUCCESS 0
        //#   endif
        //#  endif
        //#  ifndef YYMALLOC
        //#   define YYMALLOC malloc
        //#   if ! defined malloc && ! defined EXIT_SUCCESS
        //void *malloc (ulong); /* INFRINGES ON USER NAME SPACE */
        //#   endif
        //#  endif
        //#  ifndef YYFREE
        //#   define YYFREE free
        //#   if ! defined free && ! defined EXIT_SUCCESS
        //void free (void *); /* INFRINGES ON USER NAME SPACE */
        //#   endif
        //#  endif
        //# endif
        //#endif /* ! defined yyoverflow || YYERROR_VERBOSE */


        //#if (! defined yyoverflow \
        //     && (! defined __cplusplus \
        //         || (defined XMLSTYPE_IS_TRIVIAL && XMLSTYPE_IS_TRIVIAL)))

        ///* A type that is properly aligned for any stack member.  */
        //union yyalloc
        //{
        //  yytype_int16 yyss_alloc;
        //  XMLSTYPE yyvs_alloc;
        //};

        ///* The size of the maximum gap between one aligned stack and the next.  */
        //# define YYSTACK_GAP_MAXIMUM (sizeof (union yyalloc) - 1)

        ///* The size of an array large to enough to hold all stacks, each with
        //   N elements.  */
        //# define YYSTACK_BYTES(N) \
        //     ((N) * (sizeof (yytype_int16) + sizeof (XMLSTYPE)) \
        //      + YYSTACK_GAP_MAXIMUM)

        //# define YYCOPY_NEEDED 1

        ///* Relocate STACK from its old location to the new one.  The
        //   local variables YYSIZE and YYSTACKSIZE give the old and new number of
        //   elements in the stack, and YYPTR gives the new location of the
        //   stack.  Advance YYPTR to a properly aligned location for the next
        //   stack.  */
        //# define YYSTACK_RELOCATE(Stack_alloc, Stack)                           \
        //    do                                                                  \
        //      {                                                                 \
        //        ulong yynewbytes;                                            \
        //        YYCOPY (&yyptr.Stack_alloc, Stack, yysize);                    \
        //        Stack = &yyptr.Stack_alloc;                                    \
        //        yynewbytes = yystacksize * sizeof (*Stack) + YYSTACK_GAP_MAXIMUM; \
        //        yyptr += yynewbytes / sizeof (*yyptr);                          \
        //      }                                                                 \
        //    while (0)

        //#endif

        //#if defined YYCOPY_NEEDED && YYCOPY_NEEDED
        ///* Copy COUNT objects from SRC to DST.  The source and destination do
        //   not overlap.  */
        //# ifndef YYCOPY
        //#  if defined __GNUC__ && 1 < __GNUC__
        //#   define YYCOPY(Dst, Src, Count) \
        //      __builtin_memcpy (Dst, Src, (Count) * sizeof (*(Src)))
        //#  else
        //#   define YYCOPY(Dst, Src, Count)              \
        //      do                                        \
        //        {                                       \
        //          ulong yyi;                         \
        //          for (yyi = 0; yyi < (Count); yyi++)   \
        //            (Dst)[yyi] = (Src)[yyi];            \
        //        }                                       \
        //      while (0)
        //#  endif
        //# endif
        //#endif /* !YYCOPY_NEEDED */

        /* YYTRANSLATE[TOKEN-NUM] -- Symbol number corresponding to TOKEN-NUM
           as returned by xmllex, without out-of-bounds checking.  */
        internal static readonly byte[] /*yytype_uint8*/ yytranslate =
        {
           0,     2,     2,     2,     2,     2,     2,     2,     2,    15,
          13,     2,     2,    14,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,    12,    18,    17,    48,     2,     2,    47,    16,
           2,     2,     2,     2,     2,    19,     2,    46,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,    49,
           2,    32,    20,    21,     2,    25,     2,    23,    24,    31,
           2,     2,     2,     2,     2,     2,     2,     2,     2,    28,
          30,     2,     2,     2,    26,     2,     2,     2,     2,    29,
           2,    22,     2,    27,     2,     2,     2,     2,     2,    40,
          41,    34,     2,    42,     2,    37,     2,     2,    45,    44,
          39,    38,     2,     2,    35,    36,     2,     2,    33,     2,
          43,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     2,     2,     2,     2,
           2,     2,     2,     2,     2,     2,     1,     2,     3,     4,
           5,     6,     7,     8,     9,    10,    11
        };

    //#if XMLDEBUG
    //  /* YYRLINE[YYN] -- Source line where rule number YYN was defined.  */
    //static const yytype_uint8 yyrline[] =
    //{
    //       0,   138,   138,   139,   140,   141,   142,   143,   144,   145,
    //     147,   148,   149,   150,   151,   152,   153,   154,   155,   156,
    //     157,   158,   159,   160,   161,   163,   164,   165,   166,   167,
    //     168,   169,   171,   172,   173,   174,   175,   176,   177,   179,
    //     180,   181,   182,   183,   184,   185,   187,   188,   190,   191,
    //     192,   193,   195,   196,   197,   198,   199,   200,   202,   203,
    //     204,   205,   206,   207,   208,   210,   211,   213,   214,   215,
    //     216
    //};
    //#endif

        //#if XMLDEBUG || YYERROR_VERBOSE || 0
        ///* YYTNAME[SYMBOL-NUM] -- String name of the symbol SYMBOL-NUM.
        //   First, the terminals, then, starting at YYNTOKENS, nonterminals.  */
        //static const char *const yytname[] =
        //{
        //  "$end", "error", "$undefined", "CHARDATA", "CDATA", "ATTVALUE",
        //  "COMMENT", "CHARREF", "NAME", "SNAME", "ELEMBRACE", "COMMBRACE", "' '",
        //  "'\\n'", "'\\r'", "'\\t'", "'\\''", "'\"'", "'!'", "'-'", "'>'", "'?'",
        //  "'['", "'C'", "'D'", "'A'", "'T'", "']'", "'O'", "'Y'", "'P'", "'E'",
        //  "'='", "'v'", "'e'", "'r'", "'s'", "'i'", "'o'", "'n'", "'c'", "'d'",
        //  "'g'", "'x'", "'m'", "'l'", "'/'", "'&'", "'#'", "';'", "$accept",
        //  "document", "whitespace", "S", "attsinglemid", "attdoublemid",
        //  "AttValue", "elemstart", "commentstart", "Comment", "PI", "CDSect",
        //  "CDStart", "CDEnd", "doctypepro", "prologpre", "prolog", "doctypedecl",
        //  "Eq", "Misc", "VersionInfo", "EncodingDecl", "xmldeclstart", "XMLDecl",
        //  "element", "STag", "EmptyElemTag", "stagstart", "SAttribute",
        //  "etagbrace", "ETag", "content", "Reference", "refstart", "charrefstart",
        //  "CharRef", "EntityRef", YY_NULLPTR
        //};
        //#endif

        //# ifdef YYPRINT
        //    /* YYTOKNUM[NUM] -- (External) token number corresponding to the
        //       (internal) symbol number NUM (which must be that of a token).  */
        //    static const yytype_uint16 yytoknum[] =
        //{
        //       0,   256,   257,   258,   259,   260,   261,   262,   263,   264,
        //     265,   266,    32,    10,    13,     9,    39,    34,    33,    45,
        //      62,    63,    91,    67,    68,    65,    84,    93,    79,    89,
        //      80,    69,    61,   118,   101,   114,   115,   105,   111,   110,
        //      99,   100,   103,   120,   109,   108,    47,    38,    35,    59
        //};
        //# endif

        //#define YYTABLE_NINF -1
    }
}
