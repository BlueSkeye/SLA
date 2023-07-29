using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ghidra.Globals;

namespace ghidra
{
    // A tailor made class used to group yyparse and yylex related methods.
    internal static class Parsing
    {
        /* Error token number */
        private const int YYTERROR = 1;
        private const int YYERRCODE = 256;
#if !YYMAXDEPTH
        ///* YYMAXDEPTH -- maximum size the stacks can grow to (effective only
        //   if the built-in stack extension method is used).

        //   Do not make this value too large; the results are undefined if
        //   YYSTACK_ALLOC_MAXIMUM < YYSTACK_BYTES (YYMAXDEPTH)
        //   evaluated with infinite-precision integer arithmetic.  */
        private const int YYMAXDEPTH = 10000;
#endif
        private const int YYEMPTY = -2;
        private const int YYEOF = 0;
        /// Global reference to the scanner
        public static XmlScan global_scan;
        /// Global reference to the content handler
        public static ContentHandler handler;
        private static int yystate;
        /* Number of tokens to shift before error messages enabled.  */
        private static int yyerrstatus;
        /* Nonzero means print parse trace. It is left uninitialized so that
           multiple parsers can coexist.  */
        private static bool yydebug;
        private static readonly ulong YYSIZE_MAXIMUM = ulong.MaxValue - 1;

        /* The stacks and their tools:
           'yyss': related to states.
           'yyvs': related to semantic values.
           Refer to the stacks through separate pointers, to allow yyoverflow
           to reallocate them elsewhere.  */

        /* The state stack.  */
        private static short[] yyssa = new short[YYINITDEPTH];
        private static unsafe short* yyss;
        private static unsafe short* yyssp;

        /* The semantic value stack.  */
        private static XMLSTYPE[] yyvsa = new XMLSTYPE[YYINITDEPTH];
        private static unsafe XMLSTYPE* yyvs;
        private static unsafe XMLSTYPE* yyvsp;

        private static ulong yystacksize;

        private static int yyn;
        private static int yyresult;
        /* Lookahead token as an internal (translated) token number.  */
        private static int yytoken = 0;
        /* The variables used to return semantic value and location from the
           action routines.  */
        private static XMLSTYPE yyval;
        /* YYINITDEPTH -- initial size of the parser's stacks.  */
#if !YYINITDEPTH
        private const int YYINITDEPTH = 200;
#endif

        /* The semantic value of the lookahead symbol.  */
        private static XMLSTYPE yylval;
        /* Number of syntax errors so far.  */
        private static int yynerrs;
        /* The lookahead symbol.  */
        private static int yychar;
        ///* YYTRANSLATE[YYX] -- Symbol number corresponding to YYX as returned
        //   by xmllex, with out-of-bounds checking.  */
        private const int YYUNDEFTOK = 2;
        private const int YYMAXUTOK = 266;
        /* YYNTOKENS -- Number of terminals.  */
        private const int YYNTOKENS = 50;
        /* YYNNTS -- Number of nonterminals.  */
        private const int YYNNTS = 37;
        /* YYNRULES -- Number of rules.  */
        private const int YYNRULES = 70;
        /* YYNSTATES -- Number of states.  */
        private const int YYNSTATES = 151;

        private static int YYTRANSLATE(int candidate)
        {
            return (candidate <= YYMAXUTOK ? yytranslate[candidate] : YYUNDEFTOK);
        }

        /* YYPACT[STATE-NUM] -- Index in YYTABLE of the portion describing STATE-NUM. */
        internal static readonly short /*yytype_int16*/[] yypact =
        {
             132,  -136,    42,  -136,  -136,  -136,  -136,    22,  -136,   125,
               9,    20,  -136,  -136,   143,    28,  -136,    79,  -136,   148,
            -136,  -136,    16,    18,     6,  -136,  -136,  -136,    32,    65,
             148,  -136,  -136,   148,    38,    40,    93,    91,  -136,    -1,
              63,  -136,    39,    27,  -136,    45,    26,    52,   -12,  -136,
            -136,  -136,  -136,    69,    57,    77,   104,  -136,    -3,  -136,
            -136,  -136,  -136,    94,  -136,    95,  -136,  -136,    -4,   103,
            -136,  -136,  -136,    67,   136,  -136,  -136,   106,  -136,    68,
             109,    87,  -136,    90,  -136,   144,     2,  -136,   138,   108,
             117,  -136,   118,  -136,  -136,  -136,   125,    -2,     3,  -136,
            -136,   125,  -136,   145,   131,  -136,   147,   146,  -136,  -136,
             121,  -136,  -136,  -136,  -136,  -136,  -136,  -136,  -136,    54,
            -136,   149,   130,   150,   152,  -136,   142,   151,   140,   153,
            -136,   154,   155,   156,   157,   158,   159,   137,   161,   160,
            -136,    63,   162,   163,   136,  -136,   164,  -136,    63,   136,
            -136
        };

        /* YYDEFACT[STATE-NUM] -- Default reduction number in state STATE-NUM.
           Performed when YYTABLE does not specify something else to do.  Zero
           means the default is an error.  */
        internal static readonly byte[] /*yytype_uint8*/ yydefact =
        {
               0,    18,     0,     4,     5,     6,     7,     0,     8,    38,
               0,     0,    36,    37,    31,     0,    28,     0,    27,     0,
              58,    46,     0,     0,    21,     1,     9,    52,     0,     0,
              30,    25,    29,     0,     0,     0,     0,     0,     2,     0,
               0,    48,     0,     0,    53,     0,     0,     0,     0,    21,
              26,     3,    42,     0,     0,     0,     0,    59,     0,    67,
              64,    63,    62,     0,    60,     0,    47,    61,     0,     0,
              66,    65,    33,     0,     0,    50,    49,     0,    19,     0,
               0,     0,    43,     0,    44,     0,     0,    55,     0,     0,
               0,    68,     0,    34,    10,    13,    35,     0,     0,    54,
              51,     0,    20,     0,     0,    45,     0,     0,    22,    56,
               0,    70,    69,    11,    16,    12,    14,    17,    15,     0,
              41,     0,     0,     0,     0,    57,     0,     0,     0,     0,
              24,     0,     0,     0,     0,     0,     0,     0,     0,     0,
              32,     0,     0,     0,     0,    23,     0,    40,     0,     0,
              39
        };

        /* YYPGOTO[NTERM-NUM].  */
        internal static readonly short[] /*yytype_int16*/ yypgoto =
        {
            -136,  -136,    -8,   -17,  -136,  -136,  -133,  -136,  -136,   165,
             166,  -136,  -136,  -136,  -136,  -136,  -136,  -136,  -135,    71,
            -136,  -136,  -136,  -136,    17,  -136,  -136,  -136,  -136,  -136,
            -136,  -136,   -64,  -136,  -136,  -136,  -136
        };

        /* YYDEFGOTO[NTERM-NUM].  */
        internal static readonly byte[] /*yytype_int8*/ yydefgoto =
        {
              byte.MaxValue,     7,     8,     9,    97,    98,    99,    10,    11,    12,
              13,    62,    63,   108,    30,    14,    15,    31,    74,    16,
             120,    36,    17,    18,    19,    20,    21,    22,    44,    65,
              66,    39,    67,    68,    69,    70,    71
        };

        /* YYTABLE[YYPACT[STATE-NUM]] -- What to do in state STATE-NUM.  If
           positive, shift that token.  If negative, reduce the rule whose
           number is the opposite.  If YYTABLE_NINF, syntax error.  */
        internal static readonly byte /*yytype_uint8*/[] yytable =
        {
              35,    26,    57,   113,    90,    43,   144,    45,   116,     1,
              58,   147,    81,   149,   114,    86,   150,    27,    49,    56,
             117,    45,    25,    73,   106,    40,    28,    26,     3,     4,
               5,     6,    33,   115,   118,    26,    41,    45,     1,     3,
               4,     5,     6,    87,    91,    59,    59,    76,    26,    46,
              59,    47,     3,     4,     5,     6,    64,    96,    52,    75,
              23,    53,    42,    24,    78,    26,     3,     4,     5,     6,
              79,    80,   110,    77,    54,     3,     4,     5,     6,     3,
               4,     5,     6,    48,   119,    32,    49,   126,    26,    82,
              38,     3,     4,     5,     6,    72,    83,    84,    88,    93,
              34,    50,    26,    89,    51,     3,     4,     5,     6,    23,
              92,    26,    49,   101,    55,   103,     3,     4,     5,     6,
               3,     4,     5,     6,    73,    85,   100,    96,   109,   102,
             104,    73,    96,     3,     4,     5,     6,     3,     4,     5,
               6,   125,     1,     2,     3,     4,     5,     6,     3,     4,
               5,     6,    94,    95,    29,     3,     4,     5,     6,    37,
               3,     4,     5,     6,   105,   107,   111,   112,   121,   122,
             123,   128,   130,   124,   129,   127,   131,   133,   134,   141,
             132,     0,     0,   138,   145,   136,   142,     0,     0,   135,
             140,     0,     0,     0,   139,   137,     0,   143,     0,     0,
               0,   146,     0,   148,    60,    61
        };

        internal static readonly short[] /*yytype_int16*/ yycheck =
        {
              17,     9,     3,     5,     8,    22,   141,    19,     5,    10,
              11,   144,    24,   148,    16,    18,   149,     8,    21,    36,
              17,    19,     0,    40,    22,     9,     6,    35,    12,    13,
              14,    15,    15,    97,    98,    43,    20,    19,    10,    12,
              13,    14,    15,    46,    48,    47,    47,    20,    56,    43,
              47,    19,    12,    13,    14,    15,    39,    74,    20,    20,
              18,    21,    46,    21,    19,    73,    12,    13,    14,    15,
              44,    19,    89,    46,    34,    12,    13,    14,    15,    12,
              13,    14,    15,    18,   101,    14,    21,    33,    96,    20,
              19,    12,    13,    14,    15,    32,    39,    20,     4,    32,
              21,    30,   110,     8,    33,    12,    13,    14,    15,    18,
               7,   119,    21,    45,    21,    28,    12,    13,    14,    15,
              12,    13,    14,    15,   141,    21,    20,   144,    20,    20,
              40,   148,   149,    12,    13,    14,    15,    12,    13,    14,
              15,    20,    10,    11,    12,    13,    14,    15,    12,    13,
              14,    15,    16,    17,    11,    12,    13,    14,    15,    11,
              12,    13,    14,    15,    20,    27,    49,    49,    23,    38,
              23,    41,    20,    27,    24,    26,    34,    37,    25,    42,
              29,    -1,    -1,    26,    22,    30,    25,    -1,    -1,    35,
              31,    -1,    -1,    -1,    36,    39,    -1,    37,    -1,    -1,
              -1,    38,    -1,    39,    39,    39
        };

        /* YYSTOS[STATE-NUM] -- The (internal number of the) accessing
           symbol of state STATE-NUM.  */
        internal static readonly byte[] /*yytype_uint8*/ yystos =
        {
               0,    10,    11,    12,    13,    14,    15,    51,    52,    53,
              57,    58,    59,    60,    65,    66,    69,    72,    73,    74,
              75,    76,    77,    18,    21,     0,    52,     8,     6,    11,
              64,    67,    69,    74,    21,    53,    71,    11,    69,    81,
               9,    20,    46,    53,    78,    19,    43,    19,    18,    21,
              69,    69,    20,    21,    34,    21,    53,     3,    11,    47,
              59,    60,    61,    62,    74,    79,    80,    82,    83,    84,
              85,    86,    32,    53,    68,    20,    20,    46,    19,    44,
              19,    24,    20,    39,    20,    21,    18,    46,     4,     8,
               8,    48,     7,    32,    16,    17,    53,    54,    55,    56,
              20,    45,    20,    28,    40,    20,    22,    27,    63,    20,
              53,    49,    49,     5,    16,    82,     5,    17,    82,    53,
              70,    23,    38,    23,    27,    20,    33,    26,    41,    24,
              20,    34,    29,    37,    25,    35,    30,    39,    26,    36,
              31,    42,    25,    37,    68,    22,    38,    56,    39,    68,
              56
        };

        /* YYR1[YYN] -- Symbol number of symbol that rule YYN derives.  */
        internal static readonly byte[] /*yytype_uint8*/ yyr1 =
        {
               0,    50,    51,    51,    52,    52,    52,    52,    53,    53,
              54,    54,    54,    55,    55,    55,    56,    56,    57,    58,
              59,    60,    61,    62,    63,    64,    64,    65,    65,    65,
              66,    66,    67,    68,    68,    68,    69,    69,    69,    70,
              71,    72,    73,    73,    73,    73,    74,    74,    75,    75,
              76,    76,    77,    77,    78,    79,    80,    80,    81,    81,
              81,    81,    81,    81,    81,    82,    82,    83,    84,    85,
              86
        };

        /* YYR2[YYN] -- Number of symbols on the right hand side of rule YYN.  */
        internal static readonly byte[] /*yytype_uint8*/ yyr2 =
        {
               0,     2,     2,     3,     1,     1,     1,     1,     1,     2,
               1,     2,     2,     1,     2,     2,     2,     2,     1,     4,
               5,     2,     3,     9,     3,     1,     2,     1,     1,     2,
               2,     1,     9,     1,     2,     2,     1,     1,     1,    10,
              11,     6,     3,     4,     4,     5,     1,     3,     2,     3,
               3,     4,     2,     2,     3,     2,     3,     4,     0,     2,
               2,     2,     2,     2,     2,     1,     1,     1,     2,     3,
               3
        };

        private const int YYPACT_NINF = -136;
        /* YYFINAL -- State number of the termination state.  */
        private const int YYFINAL = 25;
        /* YYLAST -- Last index in YYTABLE.  */
        private static readonly int YYLAST = yytable.Length - 1; /* 205 */

        private static bool yypact_value_is_default(int Yystate)
        {
            return !!(Yystate == YYPACT_NINF);
        }

        private static unsafe void YY_SYMBOL_PRINT(string Title, int Type,
            XMLSTYPE* Value)
        {
            if (!yydebug) {
                return;
            }
            YYFPRINTF(Console.Error, $"{Title} ");
            yy_symbol_print(Console.Error, Type, Value);
            YYFPRINTF(Console.Error, "\n");
        }

        private static unsafe void YYPOPSTACK(int N)
        {
            yyvsp -= N;
            yyssp -= N;
        }

        private static bool yytable_value_is_error(int Yytable_value)
        {
            return false;
        }

        /*----------.
        | yyparse.  |
        `----------*/
        public static unsafe int xmlparse()
        {
#if YYERROR_VERBOSE
            /* Buffer for error messages, and its allocated size.  */
            char yymsgbuf[128];
            char *yymsg = yymsgbuf;
            ulong yymsg_alloc = sizeof yymsgbuf;
#endif
            /* The number of symbols on the RHS of the reduced rule.
                Keep to zero when no symbol should be popped.  */
            int yylen = 0;

            fixed (short* pyyssa = yyssa)
            fixed (XMLSTYPE* pyyvsa = yyvsa)
            fixed (XMLSTYPE* pyylval =&yylval)
            fixed (XMLSTYPE* pyyval = &yyval)
            {
                yyssp = yyss = pyyssa;
                yyvsp = yyvs = pyyvsa;
                yystacksize = YYINITDEPTH;

                YYDPRINTF(Console.Error, "Starting parse\n");

                yystate = 0;
                yyerrstatus = 0;
                yynerrs = 0;
                yychar = YYEMPTY; /* Cause a token to be read.  */
                goto yysetstate;

            /*------------------------------------------------------------.
            | yynewstate -- Push a new state, which is found in yystate.  |
            `------------------------------------------------------------*/
            yynewstate:
                /* In all cases, when you get here, the value and location stacks
                    have just been pushed.  So pushing a state here evens the stacks.  */
                yyssp++;

            yysetstate:
                *yyssp = (short)yystate;

                if (yyss + yystacksize - 1 <= yyssp) {
                    /* Get the current used size of the three stacks, in elements.  */
                    ulong yysize = (ulong)(yyssp - yyss + 1);

#if yyoverflow
                    {
                        /* Give user a chance to reallocate the stack.  Use copies of
                            these so that the &'s don't force the real ones into memory.  */
                        XMLSTYPE* yyvs1 = yyvs;
                        short* yyss1 = yyss;

                        /* Each stack pointer address is followed by the size of the
                            data in use in that stack, in bytes.  This used to be a
                            conditional around just the two extra args, but that might
                            be undefined if yyoverflow is a macro.  */
                        yyoverflow("memory exhausted",
                            &yyss1, yysize * sizeof(*yyssp), &yyvs1, yysize * sizeof(*yyvsp),
                            &yystacksize);
                        yyss = yyss1;
                        yyvs = yyvs1;
                    }
#else
                    /* no yyoverflow */
#if !YYSTACK_RELOCATE
                    goto yyexhaustedlab;
#else
                    /* Extend the stack our own way.  */
                    if (YYMAXDEPTH <= yystacksize)
                        goto yyexhaustedlab;
                    yystacksize *= 2;
                    if (YYMAXDEPTH < yystacksize)
                        yystacksize = YYMAXDEPTH;

                    {
                        short* yyss1 = yyss;
                        union yyalloc *yyptr =
                            (union yyalloc *) YYSTACK_ALLOC(YYSTACK_BYTES(yystacksize));
                        if (!yyptr)
                            goto yyexhaustedlab;
                        YYSTACK_RELOCATE(yyss_alloc, yyss);
                        YYSTACK_RELOCATE(yyvs_alloc, yyvs);
#undef YYSTACK_RELOCATE
                        if (yyss1 != yyssa)
                            YYSTACK_FREE(yyss1);
                    }
#endif
                    /* no yyoverflow */
#endif
                    yyssp = yyss + yysize - 1;
                    yyvsp = yyvs + yysize - 1;

                    YYDPRINTF(Console.Error, $"Stack size increased to {yystacksize}\n");

                    if (yyss + yystacksize - 1 <= yyssp) {
                        goto yyabortlab;
                    }
                }

                YYDPRINTF(Console.Error, $"Entering state {yystate}\n");

                if (yystate == YYFINAL) {
                    goto yyacceptlab;
                }

                goto yybackup;

            /*-----------.
            | yybackup.  |
            `-----------*/
            yybackup:
                /* Do appropriate processing given the current state.  Read a
                    lookahead token if we need one and don't already have one.  */

                /* First try to decide what to do without reference to lookahead token.  */
                yyn = yypact[yystate];
                if (yypact_value_is_default(yyn)) {
                    goto yydefault;
                }

                /* Not known => get a lookahead token if don't already have one.  */

                /* YYCHAR is either YYEMPTY or YYEOF or a valid lookahead symbol.  */
                if (yychar == YYEMPTY) {
                    YYDPRINTF(Console.Error, "Reading a token: ");
                    yychar = xmllex();
                }

                if (yychar <= YYEOF) {
                    yychar = yytoken = YYEOF;
                    YYDPRINTF(Console.Error, "Now at end of input.\n");
                }
                else {
                    yytoken = YYTRANSLATE(yychar);
                    YY_SYMBOL_PRINT("Next token is", yytoken, pyylval);
                }

                /* If the proper action on seeing token YYTOKEN is to reduce or to
                    detect an error, take that action.  */
                yyn += yytoken;
                if (yyn < 0 || YYLAST < yyn || yycheck[yyn] != yytoken) {
                    goto yydefault;
                }
                yyn = yytable[yyn];
                if (yyn <= 0) {
                    if (yytable_value_is_error(yyn)) {
                        goto yyerrlab;
                    }
                    yyn = -yyn;
                    goto yyreduce;
                }

                /* Count tokens shifted since error; after three, turn off error status.  */
                if (0 != yyerrstatus) {
                    yyerrstatus--;
                }

                /* Shift the lookahead token.  */
                YY_SYMBOL_PRINT("Shifting", yytoken, pyylval);

                /* Discard the shifted token.  */
                yychar = YYEMPTY;

                yystate = yyn;
                // YY_IGNORE_MAYBE_UNINITIALIZED_BEGIN
                * ++yyvsp = yylval;
                // YY_IGNORE_MAYBE_UNINITIALIZED_END
                goto yynewstate;

            /*-----------------------------------------------------------.
            | yydefault -- do the default action for the current state.  |
            `-----------------------------------------------------------*/
            yydefault:
                yyn = yydefact[yystate];
                if (yyn == 0) {
                    goto yyerrlab;
                }
                goto yyreduce;

            /*-----------------------------.
            | yyreduce -- Do a reduction.  |
            `-----------------------------*/
            yyreduce:
                /* yyn is the number of a rule to reduce with.  */
                yylen = yyr2[yyn];

                /* If YYLEN is nonzero, implement the default value of the action:
                    '$$ = $1'.

                    Otherwise, the following line sets YYVAL to garbage.
                    This behavior is undocumented and Bison
                    users should not rely upon it.  Assigning to YYVAL
                    unconditionally makes the parser a bit smaller, and it avoids a
                    GCC warning that YYVAL may be used uninitialized.  */
                yyval = yyvsp[1 - yylen];

                YY_REDUCE_PRINT(yyn);
                switch (yyn) {
                    case 10:
                        (yyval.str) = string.Empty;
                        global_scan.setmode(XmlScan.mode.AttValueSingleMode);
                        break;
                    case 11:
                        yyval.str = yyvsp[-1].str;
                        yyval.str += yyvsp[0].str;
                        yyvsp[0].str = null;
                        global_scan.setmode(XmlScan.mode.AttValueSingleMode);
                        break;
                    case 12:
                        yyval.str = yyvsp[-1].str;
                        yyval.str += yyvsp[0].i;
                        global_scan.setmode(XmlScan.mode.AttValueSingleMode);
                        break;
                    case 13:
                        yyval.str = string.Empty;
                        global_scan.setmode(XmlScan.mode.AttValueDoubleMode);
                        break;
                    case 14:
                        yyval.str = yyvsp[-1].str + yyvsp[0].str;
                        yyvsp[0].str = null;
                        global_scan.setmode(XmlScan.mode.AttValueDoubleMode);
                        break;
                    case 15:
                        yyval.str = yyvsp[-1].str + yyvsp[0].i;
                        global_scan.setmode(XmlScan.mode.AttValueDoubleMode);
                        break;
                    case 16:
                        yyval.str = yyvsp[-1].str;
                        break;
                    case 17:
                        yyval.str = yyvsp[-1].str;
                        break;
                    case 18:
                        global_scan.setmode(XmlScan.mode.NameMode);
                        yyvsp[0].str = null;
                        break;
                    case 19:
                        global_scan.setmode(XmlScan.mode.CommentMode);
                        yyvsp[-3].str = null;
                        break;
                    case 20:
                        yyvsp[-3].str = null;
                        break;
                    case 21:
                        yyvsp[-1].str = null;
                        xmlerror("Processing instructions are not supported");
                        goto yyerrorlab;
                    case 22:
                        yyval.str = yyvsp[-1].str;
                        break;
                    case 23:
                        global_scan.setmode(XmlScan.mode.CDataMode);
                        yyvsp[-8].str = null;
                        break;
                    case 32:
                        yyvsp[-8].str = null;
                        xmlerror("DTD's not supported");
                        goto yyerrorlab;
                    case 39:
                        handler.setVersion(yyvsp[0].str ?? throw new BugException());
                        yyvsp[0].str = null;
                        break;
                    case 40:
                        handler.setEncoding(yyvsp[0].str ?? throw new BugException());
                        yyvsp[0].str = null;
                        break;
                    case 46: {
                            Attributes attributes = yyvsp[0].attr ?? throw new BugException();
                            handler.endElement(attributes.getelemURI(),
                                attributes.getelemName(), attributes.getelemName());
                            yyvsp[0].attr = null;
                            break;
                        }
                    case 47: {
                            Attributes attributes = yyvsp[-2].attr ?? throw new BugException();
                            handler.endElement(attributes.getelemURI(),
                                attributes.getelemName(), attributes.getelemName());
                            yyvsp[-2].attr = null;
                            yyvsp[0].str = null;
                            break;
                        }
                    case 48: {
                            Attributes attributes = yyvsp[-1].attr ?? throw new BugException();
                            handler.startElement(attributes.getelemURI(),
                                attributes.getelemName(), attributes.getelemName(),
                                attributes);
                            yyval.attr = yyvsp[-1].attr;
                            break;
                        }
                    case 49: {
                            Attributes attributes = yyvsp[-2].attr ?? throw new BugException();
                            handler.startElement(attributes.getelemURI(),
                                attributes.getelemName(), attributes.getelemName(),
                                attributes);
                            yyval.attr = yyvsp[-2].attr;
                            break;
                        }
                    case 50: {
                            Attributes attributes = yyvsp[-2].attr ?? throw new BugException();
                            handler.startElement(attributes.getelemURI(),
                                attributes.getelemName(), attributes.getelemName(),
                                attributes);
                            yyval.attr = yyvsp[-2].attr;
                            break;
                        }
                    case 51: {
                            Attributes attributes = yyvsp[-3].attr ?? throw new BugException();
                            handler.startElement(attributes.getelemURI(),
                                attributes.getelemName(), attributes.getelemName(),
                                attributes);
                            yyval.attr = yyvsp[-3].attr;
                            break;
                        }
                    case 52:
                        yyval.attr = new Attributes(yyvsp[0].str ?? throw new BugException());
                        global_scan.setmode(XmlScan.mode.SNameMode);
                        break;
                    case 53: {
                            NameValue pair = yyvsp[0].pair ?? throw new BugException();
                            yyval.attr = yyvsp[-1].attr ?? throw new BugException();
                            yyval.attr.add_attribute(pair.name, pair.value);
                            yyvsp[0].pair = null;
                            global_scan.setmode(XmlScan.mode.SNameMode);
                            break;
                        }
                    case 54:
                        yyval.pair = new NameValue() {
                            name = yyvsp[-2].str ?? throw new BugException(),
                            value = yyvsp[0].str ?? throw new BugException()
                        };
                        break;
                    case 55:
                        global_scan.setmode(XmlScan.mode.NameMode);
                        yyvsp[-1].str = null;
                        break;
                    case 56:
                        yyval.str = yyvsp[-1].str;
                        break;
                    case 57:
                        yyval.str = yyvsp[-2].str;
                        break;
                    case 58:
                        global_scan.setmode(XmlScan.mode.CharDataMode);
                        break;
                    case 59:
                        print_content(yyvsp[0].str ?? throw new BugException());
                        yyvsp[0].str = null;
                        global_scan.setmode(XmlScan.mode.CharDataMode);
                        break;
                    case 60:
                        global_scan.setmode(XmlScan.mode.CharDataMode);
                        break;
                    case 61: {
                            string tmp = string.Empty;
                            tmp += (char)(yyvsp[0].i);
                            print_content(tmp);
                            global_scan.setmode(XmlScan.mode.CharDataMode);
                            break;
                        }
                    case 62: {
                            print_content(yyvsp[0].str ?? throw new BugException());
                            yyvsp[0].str = null;
                            global_scan.setmode(XmlScan.mode.CharDataMode);
                        }
                        break;
                    case 63:
                        global_scan.setmode(XmlScan.mode.CharDataMode);
                        break;
                    case 64:
                        global_scan.setmode(XmlScan.mode.CharDataMode);
                        break;
                    case 65:
                        yyval.i = convertEntityRef(yyvsp[0].str ?? throw new BugException());
                        yyvsp[0].str = null;
                        break;
                    case 66:
                        yyval.i = convertCharRef(yyvsp[0].str ?? throw new BugException());
                        yyvsp[0].str = null;
                        break;
                    case 67:
                        global_scan.setmode(XmlScan.mode.NameMode);
                        break;
                    case 68:
                        global_scan.setmode(XmlScan.mode.CharRefMode);
                        break;
                    case 69:
                        yyval.str = yyvsp[-1].str;
                        break;
                    case 70:
                        yyval.str = yyvsp[-1].str;
                        break;
                    default:
                        break;
                }
                /* User semantic actions sometimes alter yychar, and that requires
                    that yytoken be updated with the new translation.  We take the
                    approach of translating immediately before every use of yytoken.
                    One alternative is translating here after every semantic action,
                    but that translation would be missed if the semantic action invokes
                    YYABORT, YYACCEPT, or YYERROR immediately after altering yychar or
                    if it invokes YYBACKUP.  In the case of YYABORT or YYACCEPT, an
                    incorrect destructor might then be invoked immediately.  In the
                    case of YYERROR or YYBACKUP, subsequent parser actions might lead
                    to an incorrect destructor call or verbose syntax error message
                    before the lookahead is translated.  */
                YY_SYMBOL_PRINT(". $$ =", yyr1[yyn], pyyval);

                YYPOPSTACK(yylen);
                yylen = 0;
                YY_STACK_PRINT(yyss, yyssp);

                *++yyvsp = yyval;

                /* Now 'shift' the result of the reduction.  Determine what state
                    that goes to, based on the state we popped back to and the rule
                    number reduced by.  */

                yyn = yyr1[yyn];

                yystate = yypgoto[yyn - YYNTOKENS] + *yyssp;
                yystate = (0 <= yystate && yystate <= YYLAST && yycheck[yystate] == *yyssp)
                    ? yytable[yystate]
                    : yydefgoto[yyn - YYNTOKENS];
                goto yynewstate;

            /*--------------------------------------.
            | yyerrlab -- here on detecting error.  |
            `--------------------------------------*/
            yyerrlab:
                /* Make sure we have latest lookahead translation.  See comments at
                    user semantic actions for why this is necessary.  */
                yytoken = yychar == YYEMPTY ? YYEMPTY : YYTRANSLATE(yychar);

                /* If not already recovering from an error, report this error.  */
                if (0 == yyerrstatus) {
                    ++yynerrs;
// #if !YYERROR_VERBOSE
                    xmlerror("syntax error");
//#else
//#define YYSYNTAX_ERROR yysyntax_error (&yymsg_alloc, &yymsg, yyssp, yytoken)
//                    {
//                    char *yymsgp = "syntax error";
//                    int yysyntax_error_status;
//                    yysyntax_error_status = YYSYNTAX_ERROR;
//                    if (yysyntax_error_status == 0)
//                        yymsgp = yymsg;
//                    else if (yysyntax_error_status == 1)
//                        {
//                        if (yymsg != yymsgbuf)
//                            YYSTACK_FREE (yymsg);
//                        yymsg = (char *) YYSTACK_ALLOC (yymsg_alloc);
//                        if (!yymsg)
//                            {
//                            yymsg = yymsgbuf;
//                            yymsg_alloc = sizeof yymsgbuf;
//                            yysyntax_error_status = 2;
//                            }
//                        else
//                            {
//                            yysyntax_error_status = YYSYNTAX_ERROR;
//                            yymsgp = yymsg;
//                            }
//                        }
//                    xmlerror (yymsgp);
//                    if (yysyntax_error_status == 2)
//                        goto yyexhaustedlab;
//                    }
//#undef YYSYNTAX_ERROR
//#endif
                }

                if (yyerrstatus == 3) {
                    /* If just tried and failed to reuse lookahead token after an
                        error, discard it.  */
                    if (yychar <= YYEOF) {
                        /* Return failure if at end of input.  */
                        if (yychar == YYEOF) {
                            goto yyabortlab;
                        }
                    }
                    else {
                        yydestruct("Error: discarding", yytoken, pyylval);
                        yychar = YYEMPTY;
                    }
                }

                /* Else will try to reuse lookahead token after shifting the error token. */
                goto yyerrlab1;

            /*---------------------------------------------------.
            | yyerrorlab -- error raised explicitly by YYERROR.  |
            `---------------------------------------------------*/
            yyerrorlab:

                /* Pacify compilers like GCC when the user code never invokes YYERROR and
                    * the label yyerrorlab therefore never appears in user code. */
                //if (/*CONSTCOND*/ 0)
                //    goto yyerrorlab;

                /* Do not reclaim the symbols of the rule whose action triggered
                    this YYERROR.  */
                YYPOPSTACK(yylen);
                yylen = 0;
                YY_STACK_PRINT(yyss, yyssp);
                yystate = *yyssp;
                goto yyerrlab1;

            /*-------------------------------------------------------------.
            | yyerrlab1 -- common code for both syntax error and YYERROR.  |
            `-------------------------------------------------------------*/
            yyerrlab1:
                /* Each real token shifted decrements this.  */
                yyerrstatus = 3;

                for (; ; ) {
                    yyn = yypact[yystate];
                    if (!yypact_value_is_default(yyn)) {
                        yyn += YYTERROR;
                        if (0 <= yyn && yyn <= YYLAST && yycheck[yyn] == YYTERROR) {
                            yyn = yytable[yyn];
                            if (0 < yyn)
                                break;
                        }
                    }

                    /* Pop the current state because it cannot handle the error token.  */
                    if (yyssp == yyss) {
                        goto yyabortlab;
                    }
                    yydestruct("Error: popping", yystos[yystate], yyvsp);
                    YYPOPSTACK(1);
                    yystate = *yyssp;
                    YY_STACK_PRINT(yyss, yyssp);
                }
                // YY_IGNORE_MAYBE_UNINITIALIZED_BEGIN
                * ++yyvsp = yylval;
                // YY_IGNORE_MAYBE_UNINITIALIZED_END

                /* Shift the error token.  */
                YY_SYMBOL_PRINT("Shifting", yystos[yyn], yyvsp);

                yystate = yyn;
                goto yynewstate;

            /*-------------------------------------.
            | yyacceptlab -- YYACCEPT comes here.  |
            `-------------------------------------*/
            yyacceptlab:
                yyresult = 0;
                goto yyreturn;

            /*-----------------------------------.
            | yyabortlab -- YYABORT comes here.  |
            `-----------------------------------*/
            yyabortlab:
                yyresult = 1;
                goto yyreturn;

#if !yyoverflow || YYERROR_VERBOSE
            /*-------------------------------------------------.
            | yyexhaustedlab -- memory exhaustion comes here.  |
            `-------------------------------------------------*/
            yyexhaustedlab:
                xmlerror("memory exhausted");
                yyresult = 2;
            /* Fall through.  */
#endif

            yyreturn:
                if (yychar != YYEMPTY) {
                    /* Make sure we have latest lookahead translation.  See comments at
                        user semantic actions for why this is necessary.  */
                    yytoken = YYTRANSLATE(yychar);
                    yydestruct("Cleanup: discarding lookahead", yytoken, pyylval);
                }
                /* Do not reclaim the symbols of the rule whose action triggered
                    this YYABORT or YYACCEPT.  */
                YYPOPSTACK(yylen);
                YY_STACK_PRINT(yyss, yyssp);
                while (yyssp != yyss) {
                    yydestruct("Cleanup: popping", yystos[*yyssp], yyvsp);
                    YYPOPSTACK(1);
                }
#if !yyoverflow
                if (yyss != pyyssa) {
                    // YYSTACK_FREE(yyss);
                }
#endif
#if YYERROR_VERBOSE
                if (yymsg != yymsgbuf) {
                    // YYSTACK_FREE (yymsg);
                }
#endif
                return yyresult;
            }
        }

        /// Send character data to the ContentHandler
        private static unsafe void print_content(string str)
        {
            int i;
            for (i = 0; i < str.Length; ++i) {
                switch (str[i]) {
                    case ' ':
                    case '\n':
                    case '\r':
                    case '\t':
                        continue;
                    default:
                        break;
                }
                break;
            }
            fixed (char* pString = str) {
                if (i == str.Length) {
                    handler.ignorableWhitespace(pString, 0, str.Length);
                }
                else {
                    handler.characters(pString, 0, str.Length);
                }
            }
        }

        /// Convert an XML entity to its equivalent character
        private static int convertEntityRef(string @ref)
        {
            switch (@ref) {
                case "lt":
                    return '<';
                case "amp":
                    return '&';
                case "gt":
                    return '>';
                case "quot":
                    return '"';
                case "apos":
                    return '\'';
                default:
                    return -1;
            }
        }

        /// Convert an XML character reference to its equivalent character
        private static int convertCharRef(string @ref)
        {
            int i;
            int mult, val, cur;

            if (@ref[0] == 'x') {
                i = 1;
                mult = 16;
            }
            else {
                i = 0;
                mult = 10;
            }
            val = 0;
            for (; i < @ref.Length; ++i) {
                if (@ref[i] <= '9') {
                    cur = @ref[i] - '0';
                }
                else if (@ref[i] <= 'F') {
                    cur = 10 + @ref[i] - 'A';
                }
                else {
                    cur = 10 + @ref[i] - 'a';
                }
                val *= mult;
                val += cur;
            }
            return val;
        }

        private static void yyerrok()
        {
            yyerrstatus = 0;
        }

        private static void yyclearin()
        {
            yychar = YYEMPTY;
        }

        //#define YYACCEPT        goto yyacceptlab
        //#define YYABORT         goto yyabortlab
        //#define YYERROR         goto yyerrorlab

        //#define YYRECOVERING()  (!!yyerrstatus)

        //#define YYBACKUP(Token, Value)                                  \
        //do                                                              \
        //  if (yychar == YYEMPTY) {                                                           \
        //      yychar = (Token);                                         \
        //      yylval = (Value);                                         \
        //      YYPOPSTACK (yylen);                                       \
        //      yystate = *yyssp;                                         \
        //      goto yybackup;                                            \
        //    }                                                           \
        //  else {                                                           \
        //      xmlerror ("syntax error: cannot back up"); \
        //      goto yyerrorlab;                                                  \
        //    }                                                           \
        //while (0)

        /* Enable debugging if requested.  */

        private static void YYFPRINTF(TextWriter output, string format, params object[] args)
        {
#if XMLDEBUG
            output.Write(string.Format(format, args));
#endif
        }

        private static void YYDPRINTF(TextWriter output, string format, params object[] args)
        {
#if XMLDEBUG
            if (yydebug) {
                YYFPRINTF(output, format, args);
            }
#endif
        }

        /*----------------------------------------.
        | Print this symbol's value on YYOUTPUT.  |
        `----------------------------------------*/
        private static unsafe void yy_symbol_value_print(TextWriter yyoutput, int yytype,
            XMLSTYPE* yyvaluep)
        {
#if XMLDEBUG
            FILE* yyo = yyoutput;
            // YYUSE(yyo);
            if (!yyvaluep) {
                return;
            }
#if YYPRINT
            if (yytype < YYNTOKENS) {
                YYPRINT(yyoutput, yytoknum[yytype], *yyvaluep);
            }
#endif
            // YYUSE(yytype);
#endif
        }

        /*--------------------------------.
        | Print this symbol on YYOUTPUT.  |
        `--------------------------------*/
        private static unsafe void yy_symbol_print(TextWriter yyoutput, int yytype,
            XMLSTYPE* yyvaluep)
        {
#if XMLDEBUG
            YYFPRINTF(yyoutput, 
                $"{(yytype < YYNTOKENS ? "token" : "nterm")} {yytname[yytype]} (");
            yy_symbol_value_print(yyoutput, yytype, yyvaluep);
            YYFPRINTF(yyoutput, ")");
#endif
        }

        ///*------------------------------------------------------------------.
        //| yy_stack_print -- Print the state stack from its BOTTOM up to its |
        //| TOP (included).                                                   |
        //`------------------------------------------------------------------*/
        private static unsafe void yy_stack_print(short* yybottom, short* yytop)
        {
#if XMLDEBUG
            YYFPRINTF(Console.Error, "Stack now");
            for (; yybottom <= yytop; yybottom++) {
                int yybot = *yybottom;
                YYFPRINTF(Console.Error, $" {yybot}");
            }
            YYFPRINTF(Console.Error, "\n");
#endif
        }

        private static unsafe void YY_STACK_PRINT(short* yybottom, short* yytop)
        {
#if XMLDEBUG
            if (yydebug) {
                yy_stack_print((Bottom), (Top));
            }
#endif
        }

        /*------------------------------------------------.
        | Report that the YYRULE is going to be reduced.  |
        `------------------------------------------------*/
        private static unsafe void yy_reduce_print(short* yyssp, XMLSTYPE* yyvsp,
            int yyrule)
        {
#if XMLDEBUG
            ulong yylno = yyrline[yyrule];
            int yynrhs = yyr2[yyrule];
            int yyi;
            YYFPRINTF(Console.Error, $"Reducing stack by rule {yyrule - 1} (line {yylno}):\n");
            /* The symbols being reduced.  */
            for (yyi = 0; yyi < yynrhs; yyi++) {
                YYFPRINTF(Console.Error, $"   ${yyi + 1} = ");
                yy_symbol_print(Console.Error, yystos[yyssp[yyi + 1 - yynrhs]],
                    &(yyvsp[(yyi + 1) - (yynrhs)]));
                YYFPRINTF(Console.Error, "\n");
            }
#endif
        }

        private static void YY_REDUCE_PRINT(int Rule)
        {
#if XMLDEBUG
            if (yydebug) {
                yy_reduce_print(yyssp, yyvsp, Rule);
            }
#endif
        }


        //#if YYERROR_VERBOSE
        //# ifndef yystrlen
        //#  if defined __GLIBC__ && defined _STRING_H
        //#   define yystrlen strlen
        //#  else
        ///* Return the length of YYSTR.  */
        //static ulong yystrlen (char *yystr)
        //{
        //  ulong yylen;
        //  for (yylen = 0; yystr[yylen]; yylen++)
        //    continue;
        //  return yylen;
        //}
        //# endif
        //# endif

        //# ifndef yystpcpy
        //#  if defined __GLIBC__ && defined _STRING_H && defined _GNU_SOURCE
        //#   define yystpcpy stpcpy
        //#  else
        ///* Copy YYSRC to YYDEST, returning the address of the terminating '\0' in YYDEST.  */
        //static char *yystpcpy (char *yydest, char *yysrc)
        //{
        //  char *yyd = yydest;
        //  char *yys = yysrc;

        //  while ((*yyd++ = *yys++) != '\0')
        //    continue;

        //  return yyd - 1;
        //}
        //#  endif
        //# endif

#if !yytnamerr
        /* Copy to YYRES the contents of YYSTR after stripping away unnecessary
           quotes and backslashes, so that it's suitable for yyerror.  The
           heuristic is that double-quoting is unnecessary unless the string
           contains an apostrophe, a comma, or backslash (other than
           backslash-backslash).  YYSTR is taken from yytname.  If YYRES is
           null, do not copy; instead, return the length of what the result
           would have been.  */
        private static unsafe int yytnamerr(string yyres, string yystr)
        {
            if (yystr[0] == '"') {
                int yyn = 0;
                for (int index = 0;;)
                    switch (yystr[++index]) {
                        case '\'':
                        case ',':
                            goto do_not_strip_quotes;
                        case '\\':
                            if (yystr[++index] != '\\') {
                                goto do_not_strip_quotes;
                            }
                            /* Fall through.  */
                            goto default;
                        default:
                            if (null != yyres) {
                                fixed (char* pyyres = yyres) {
                                    *(pyyres + yyn) = yystr[index];
                                }
                            }
                            yyn++;
                            break;
                        case '"':
                            if (null != yyres) {
                                fixed (char* pyyres = yyres) { *(pyyres + yyn) = '\0'; }
                            }
                            return yyn;
                    }
                do_not_strip_quotes:
                    ;
            }
            return (null == yyres) 
                ? yystr.Length
                : (yyres = yystr).Length;
        }
#endif

        ///* Copy into *YYMSG, which is of size *YYMSG_ALLOC, an error message
        //   about the unexpected token YYTOKEN for the state stack whose top is
        //   YYSSP.

        //   Return 0 if *YYMSG was successfully written.  Return 1 if *YYMSG is
        //   not large enough to hold the message.  In that case, also set
        //   *YYMSG_ALLOC to the required number of bytes.  Return 2 if the
        //   required number of bytes is too large to store.  */
        //static int yysyntax_error (ulong *yymsg_alloc, char **yymsg,
        //                short *yyssp, int yytoken)
        //{
        //  ulong yysize0 = yytnamerr (YY_NULLPTR, yytname[yytoken]);
        //  ulong yysize = yysize0;
        //  enum { YYERROR_VERBOSE_ARGS_MAXIMUM = 5 };
        //  /* Internationalized format string. */
        //  char *yyformat = YY_NULLPTR;
        //  /* Arguments of yyformat. */
        //  char *yyarg[YYERROR_VERBOSE_ARGS_MAXIMUM];
        //  /* Number of reported tokens (one for the "unexpected", one per
        //     "expected"). */
        //  int yycount = 0;

        //  /* There are many possibilities here to consider:
        //     - If this state is a consistent state with a default action, then
        //       the only way this function was invoked is if the default action
        //       is an error action.  In that case, don't check for expected
        //       tokens because there are none.
        //     - The only way there can be no lookahead present (in yychar) is if
        //       this state is a consistent state with a default action.  Thus,
        //       detecting the absence of a lookahead is sufficient to determine
        //       that there is no unexpected or expected token to report.  In that
        //       case, just report a simple "syntax error".
        //     - Don't assume there isn't a lookahead just because this state is a
        //       consistent state with a default action.  There might have been a
        //       previous inconsistent state, consistent state with a non-default
        //       action, or user semantic action that manipulated yychar.
        //     - Of course, the expected token list depends on states to have
        //       correct lookahead information, and it depends on the parser not
        //       to perform extra reductions after fetching a lookahead from the
        //       scanner and before detecting a syntax error.  Thus, state merging
        //       (from LALR or IELR) and default reductions corrupt the expected
        //       token list.  However, the list is correct for canonical LR with
        //       one exception: it will still contain any token that will not be
        //       accepted due to an error action in a later state.
        //  */
        //  if (yytoken != YYEMPTY)
        //    {
        //      int yyn = yypact[*yyssp];
        //      yyarg[yycount++] = yytname[yytoken];
        //      if (!yypact_value_is_default (yyn))
        //        {
        //          /* Start YYX at -YYN if negative to avoid negative indexes in
        //             YYCHECK.  In other words, skip the first -YYN actions for
        //             this state because they are default actions.  */
        //          int yyxbegin = yyn < 0 ? -yyn : 0;
        //          /* Stay within bounds of both yycheck and yytname.  */
        //          int yychecklim = YYLAST - yyn + 1;
        //          int yyxend = yychecklim < YYNTOKENS ? yychecklim : YYNTOKENS;
        //          int yyx;

        //          for (yyx = yyxbegin; yyx < yyxend; ++yyx)
        //            if (yycheck[yyx + yyn] == yyx && yyx != YYTERROR
        //                && !yytable_value_is_error (yytable[yyx + yyn]))
        //              {
        //                if (yycount == YYERROR_VERBOSE_ARGS_MAXIMUM)
        //                  {
        //                    yycount = 1;
        //                    yysize = yysize0;
        //                    break;
        //                  }
        //                yyarg[yycount++] = yytname[yyx];
        //                {
        //                  ulong yysize1 = yysize + yytnamerr (YY_NULLPTR, yytname[yyx]);
        //                  if (! (yysize <= yysize1
        //                         && yysize1 <= YYSTACK_ALLOC_MAXIMUM))
        //                    return 2;
        //                  yysize = yysize1;
        //                }
        //              }
        //        }
        //    }

        //  switch (yycount)
        //    {
        //# define YYCASE_(N, S)                      \
        //      case N:                               \
        //        yyformat = S;                       \
        //      break
        //      YYCASE_(0, "syntax error");
        //      YYCASE_(1, "syntax error, unexpected %s");
        //      YYCASE_(2, "syntax error, unexpected %s, expecting %s");
        //      YYCASE_(3, "syntax error, unexpected %s, expecting %s or %s");
        //      YYCASE_(4, "syntax error, unexpected %s, expecting %s or %s or %s");
        //      YYCASE_(5, "syntax error, unexpected %s, expecting %s or %s or %s or %s");
        //# undef YYCASE_
        //    }

        //  {
        //    ulong yysize1 = yysize + yyformat.Length;
        //    if (! (yysize <= yysize1 && yysize1 <= YYSTACK_ALLOC_MAXIMUM))
        //      return 2;
        //    yysize = yysize1;
        //  }

        //  if (*yymsg_alloc < yysize)
        //    {
        //      *yymsg_alloc = 2 * yysize;
        //      if (! (yysize <= *yymsg_alloc
        //             && *yymsg_alloc <= YYSTACK_ALLOC_MAXIMUM))
        //        *yymsg_alloc = YYSTACK_ALLOC_MAXIMUM;
        //      return 1;
        //    }

        //  /* Avoid sprintf, as that infringes on the user's name space.
        //     Don't have undefined behavior even if the translation
        //     produced a string with the wrong number of "%s"s.  */
        //  {
        //    char *yyp = *yymsg;
        //    int yyi = 0;
        //    while ((*yyp = *yyformat) != '\0')
        //      if (*yyp == '%' && yyformat[1] == 's' && yyi < yycount)
        //        {
        //          yyp += yytnamerr (yyp, yyarg[yyi++]);
        //          yyformat += 2;
        //        }
        //      else
        //        {
        //          yyp++;
        //          yyformat++;
        //        }
        //  }
        //  return 0;
        //}
        //#endif /* YYERROR_VERBOSE */

        /*-----------------------------------------------.
        | Release the memory associated to this symbol.  |
        `-----------------------------------------------*/

        private static unsafe void yydestruct(string yymsg, int yytype,
            XMLSTYPE *yyvaluep)
        {
            /// YYUSE(yyvaluep);
            if (null == yymsg) {
                yymsg = "Deleting";
            }
            YY_SYMBOL_PRINT(yymsg, yytype, yyvaluep);
            // YY_IGNORE_MAYBE_UNINITIALIZED_BEGIN;
            // YYUSE(yytype);
            // YY_IGNORE_MAYBE_UNINITIALIZED_END;
        }

        private static int xmllex()
        {
            int res = (int)global_scan.nexttoken();
            if (res > 255) {
                yylval.str = global_scan.lval();
            }
            return res;
        }

        private static int xmlerror(string str)
        {
            handler.setError(str);
            return 0;
        }
    }
}