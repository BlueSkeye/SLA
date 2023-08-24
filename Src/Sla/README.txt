Types
C++ types
Exact size
int8_t
int16_t
int32_t
int64_t
uint8_t
uint16_t
uint32_t
uint64_t

At least size
int_least8_t
int_least16_t
int_least32_t
int_least64_t

Max sized
intmax_t

intptr_t . signed integer type capable of holding a pointer to void

TYPEDEFED
typedef uint32_t uint;
typedef int32_t intm;
typedef uint64_t ulong;
typedef ulong ulong; /* This is an unsigned big integer */
typedef int64_t long;
typedef long long; /* This is a signed big integer */
typedef uint32_t uint;
typedef int32_t int;
typedef uint16_t ushort;
typedef int16_t short;
typedef uint8_t byte;
typedef int8_t int1;
typedef uintptr_t ulong;
#define YYSTYPE XMLSTYPE
typedef unsigned char yytype_uint8;
typedef signed char yytype_int8;
typedef unsigned short int yytype_uint16;
typedef short int yytype_int16;

CONVERSION RESULT
uintp --> ulong
uintm --> uint
uintb --> ulong
intb --> long
uint8 --> ulong
int8 --> long
uint4 --> uint
int4 --> int
uint1 --> byte
ostream --> StreamWriter
YYSTYPE --> XMLSTYPE
yytype_uint8 --> byte
yytype_int8 --> sbyte
yytype_uint16 --> ushort
yytype_int16 --> short

Other aliases :

using EntryMap = Sla.EXTRA.rangemap<Sla.DECCORE.SymbolEntry>;
using ParamEntryResolver = Sla.EXTRA.rangemap<Sla.DECCORE.ParamEntryRange>;
using PcodeOpTree = System.Collections.Generic.Dictionary<Sla.CORE.SeqNum, Sla.DECCORE.PcodeOp>;
using ScopeMap = System.Collections.Generic.Dictionary<ulong, Sla.DECCORE.Scope>;
using ScopeResolve = Sla.EXTRA.rangemap<Sla.DECCORE.ScopeMapper>;
using SymbolTree = System.Collections.Generic.HashSet<Sla.SLEIGH.SleighSymbol>; // SymbolCompare
using TrackedSet = System.Collections.Generic.List<Sla.CORE.TrackedContext>;
using VariableStack = System.Collections.Generic.Dictionary<Sla.CORE.Address, System.Collections.Generic.List<Sla.DECCORE.Varnode>>;
using VarnodeLocSet = System.Collections.Generic.SortedSet<Sla.DECCORE.Varnode>; // VarnodeCompareLocDef : A set of Varnodes sorted by location (then by definition)
using VarnodeDefSet = System.Collections.Generic.SortedSet<Sla.DECCORE.Varnode>; // VarnodeDefSet : A set of Varnodes sorted by definition (then location)

Returning references from functions and references to array element
https://www.danielcrabtree.com/blog/128/c-sharp-7-ref-returns-ref-locals-and-how-to-use-them#:~:text=To%20return%20by%20reference%2C%20add,to%20be%20returned%20by%20reference.