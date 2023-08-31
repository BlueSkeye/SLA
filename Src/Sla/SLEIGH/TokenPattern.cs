using Sla.SLEIGH;

namespace Sla.SLEIGH
{
    internal class TokenPattern
    {
        private Pattern pattern;
        private List<Token> toklist = new List<Token>();
        private bool leftellipsis;
        private bool rightellipsis;

        private static PatternBlock buildSingle(int startbit, int endbit, uint byteval)
        {
            // Create a mask/value pattern within a single word
            // The field is given by the bitrange [startbit,endbit] bit 0 is the MOST sig bit of the word
            // use the least sig bits of byteval to fill in the field's value
            uint mask;
            int offset = 0;
            int size = endbit - startbit + 1;
            while (startbit >= 8) {
                offset += 1;
                startbit -= 8;
                endbit -= 8;
            }
            mask = (uint.MaxValue) << (sizeof(uint) * 8 - size);
            byteval = (byteval << (sizeof(uint) * 8 - size)) & mask;
            mask >>= startbit;
            byteval >>= startbit;
            return new PatternBlock(offset, mask, byteval);
        }

        private static PatternBlock buildBigBlock(int size, int bitstart, int bitend, long value)
        {
            // Build pattern block given a bigendian contiguous
            // range of bits and a value for those bits
            int tmpstart, startbit, endbit;
            PatternBlock tmpblock, block;

            startbit = 8 * size - 1 - bitend;
            endbit = 8 * size - 1 - bitstart;

            block = (PatternBlock)null;
            while (endbit >= startbit) {
                tmpstart = endbit - (endbit & 7);
                if (tmpstart < startbit)
                    tmpstart = startbit;
                tmpblock = buildSingle(tmpstart, endbit, (uint)value);
                if (block == (PatternBlock)null)
                    block = tmpblock;
                else {
                    PatternBlock newblock = block.intersect(tmpblock);
                    //delete block;
                    //delete tmpblock;
                    block = newblock;
                }
                value >>= (endbit - tmpstart + 1);
                endbit = tmpstart - 1;
            }
            return block;
        }

        private static PatternBlock buildLittleBlock(int size, int bitstart, int bitend, long value)
        {
            // Build pattern block given a littleendian contiguous
            // range of bits and a value for those bits
            PatternBlock tmpblock;
            int startbit, endbit;
            PatternBlock? block = (PatternBlock)null;

            // we need to convert a bit range specified on a little endian token where the
            // bit indices label the least sig bit as 0 into a bit range on big endian bytes
            // where the indices label the most sig bit as 0.  The reversal due to
            // little.big endian cancels part of the reversal due to least.most sig bit
            // labelling, but not on the lower 3 bits.  So the transform becomes
            // leave the upper bits the same, but transform the lower 3-bit value x into 7-x.
            startbit = (bitstart / 8) * 8;  // Get the high-order portion of little/LSB labelling
            endbit = (bitend / 8) * 8;
            bitend = bitend % 8;        // Get the low-order portion of little/LSB labelling
            bitstart = bitstart % 8;

            if (startbit == endbit) {
                startbit += 7 - bitend;
                endbit += 7 - bitstart;
                block = buildSingle(startbit, endbit, (uint)value);
            }
            else {
                block = buildSingle(startbit, startbit + (7 - bitstart), (uint)value);
                value >>= (8 - bitstart);   // Cut off bits we just encoded
                startbit += 8;
                while (startbit != endbit) {
                    tmpblock = buildSingle(startbit, startbit + 7, (uint)value);
                    if (block == (PatternBlock)null)
                        block = tmpblock;
                    else {
                        PatternBlock newblock = block.intersect(tmpblock);
                        //delete block;
                        //delete tmpblock;
                        block = newblock;
                    }
                    value >>= 8;
                    startbit += 8;
                }
                tmpblock = buildSingle(endbit + (7 - bitend), endbit + 7, (uint)value);
                if (block == (PatternBlock)null)
                    block = tmpblock;
                else {
                    PatternBlock newblock = block.intersect(tmpblock);
                    //delete block;
                    //delete tmpblock;
                    block = newblock;
                }
            }
            return block;
        }

        private int resolveTokens(TokenPattern tok1, TokenPattern tok2)
        {
            // Use the token lists to decide how the two patterns should be aligned relative to each other
            // return how much -tok2- needs to be shifted and set the resulting tokenlist and ellipses
            bool reversedirection = false;
            leftellipsis = false;
            rightellipsis = false;
            int ressa = 0;
            int minsize = tok1.toklist.size() < tok2.toklist.size() ? tok1.toklist.size() : tok2.toklist.size();
            if (minsize == 0) {
                // Check if pattern doesn't care about tokens
                if ((tok1.toklist.size() == 0) && (tok1.leftellipsis == false) && (tok1.rightellipsis == false)) {
                    toklist = tok2.toklist;
                    leftellipsis = tok2.leftellipsis;
                    rightellipsis = tok2.rightellipsis;
                    return 0;
                }
                else if (   (tok2.toklist.size() == 0)
                         && (tok2.leftellipsis == false)
                         && (tok2.rightellipsis == false))
                {
                    toklist = tok1.toklist;
                    leftellipsis = tok1.leftellipsis;
                    rightellipsis = tok1.rightellipsis;
                    return 0;
                }
                // If one of the ellipses is true then the pattern
                // still cares about tokens even though none are
                // specified
            }

            if (tok1.leftellipsis) {
                reversedirection = true;
                if (tok2.rightellipsis)
                    throw new SleighError("Right/left ellipsis");
                else if (tok2.leftellipsis)
                    leftellipsis = true;
                else if (tok1.toklist.size() != minsize) {
                    TextWriter msg = new StringWriter();
                    msg.Write($"Mismatched pattern sizes -- {tok1.toklist.size()} != {minsize}");
                    throw new SleighError(msg.ToString());
                }
                else if (tok1.toklist.size() == tok2.toklist.size())
                    throw new SleighError("Pattern size cannot vary (missing '...'?)");
            }
            else if (tok1.rightellipsis) {
                if (tok2.leftellipsis)
                    throw new SleighError("Left/right ellipsis");
                else if (tok2.rightellipsis)
                    rightellipsis = true;
                else if (tok1.toklist.size() != minsize) {
                    TextWriter msg = new StringWriter();
                    msg.Write($"Mismatched pattern sizes -- {tok1.toklist.size()} != {minsize}");
                    throw new SleighError(msg.ToString());
                }
                else if (tok1.toklist.size() == tok2.toklist.size())
                    throw new SleighError("Pattern size cannot vary (missing '...'?)");
            }
            else {
                if (tok2.leftellipsis) {
                    reversedirection = true;
                    if (tok2.toklist.size() != minsize) {
                        TextWriter msg = new StringWriter();
                        msg.Write($"Mismatched pattern sizes -- {tok2.toklist.size()} != {minsize}");
                        throw new SleighError(msg.ToString());
                    }
                    else if (tok1.toklist.size() == tok2.toklist.size())
                        throw new SleighError("Pattern size cannot vary (missing '...'?)");
                }
                else if (tok2.rightellipsis) {
                    if (tok2.toklist.size() != minsize) {
                        TextWriter msg = new StringWriter();
                        msg.Write($"Mismatched pattern sizes -- {tok2.toklist.size()} != {minsize}");
                        throw new SleighError(msg.ToString());
                    }
                    else if (tok1.toklist.size() == tok2.toklist.size())
                        throw new SleighError("Pattern size cannot vary (missing '...'?)");
                }
                else {
                    if (tok2.toklist.size() != tok1.toklist.size()) {
                        TextWriter msg = new StringWriter();
                        msg.Write($"Mismatched pattern sizes -- {tok2.toklist.size()} != {tok1.toklist.size()}");
                        throw new SleighError(msg.ToString());
                    }
                }
            }
            if (reversedirection) {
                for (int i = 0; i < minsize; ++i)
                    if (tok1.toklist[tok1.toklist.size() - 1 - i] != tok2.toklist[tok2.toklist.size() - 1 - i]) {
                        TextWriter msg = new StringWriter();
                        msg.Write($"Mismatched tokens when combining patterns -- {tok1.toklist[tok1.toklist.size() - 1 - i]} != {tok2.toklist[tok2.toklist.size() - 1 - i]}");
                        throw new SleighError(msg.ToString());
                    }
                if (tok1.toklist.size() <= tok2.toklist.size())
                    for (int i = minsize; i < tok2.toklist.size(); ++i)
                        ressa += tok2.toklist[tok2.toklist.size() - 1 - i].getSize();
                else
                    for (int i = minsize; i < tok1.toklist.size(); ++i)
                        ressa += tok1.toklist[tok1.toklist.size() - 1 - i].getSize();
                if (tok1.toklist.size() < tok2.toklist.size())
                    ressa = -ressa;
            }
            else {
                for (int i = 0; i < minsize; ++i)
                    if (tok1.toklist[i] != tok2.toklist[i]) {
                        TextWriter msg = new StringWriter();
                        msg.Write($"Mismatched tokens when combining patterns -- {tok1.toklist[i]} != {tok2.toklist[i]}");
                        throw new SleighError(msg.ToString());
                    }
            }
            // Save the results into -this-
            if (tok1.toklist.size() <= tok2.toklist.size())
                toklist = tok2.toklist;
            else
                toklist = tok1.toklist;
            return ressa;
        }

        private TokenPattern(Pattern pat)
        {
            pattern = pat;
            leftellipsis = false;
            rightellipsis = false;
        }

        // TRUE pattern unassociated with a token
        public TokenPattern()
        {
            leftellipsis = false;
            rightellipsis = false;
            pattern = new InstructionPattern(true);
        }

        // TRUE or FALSE pattern unassociated with a token
        public TokenPattern(bool tf)
        {
            // TRUE or FALSE pattern
            leftellipsis = false;
            rightellipsis = false;
            pattern = new InstructionPattern(tf);
        }

        // TRUE pattern associated with token -tok-
        public TokenPattern(Token tok)
        {
            leftellipsis = false;
            rightellipsis = false;
            pattern = new InstructionPattern(true);
            toklist.Add(tok);
        }

        public TokenPattern(Token tok, long value, int bitstart, int bitend)
        {
            // A basic instruction pattern
            toklist.Add(tok);
            leftellipsis = false;
            rightellipsis = false;
            PatternBlock block = (tok.isBigEndian())
                ? buildBigBlock(tok.getSize(), bitstart, bitend, value)
                : buildLittleBlock(tok.getSize(), bitstart, bitend, value);
            pattern = new InstructionPattern(block);
        }

        public TokenPattern(long value, int startbit, int endbit)
        {
            // A basic context pattern
            leftellipsis = false;
            rightellipsis = false;
            int size = (endbit / 8) + 1;
            PatternBlock block = buildBigBlock(size, size * 8 - 1 - endbit, size * 8 - 1 - startbit, value);
            pattern = new ContextPattern(block);
        }

        public TokenPattern(TokenPattern tokpat)
        {
            pattern = tokpat.pattern.simplifyClone();
            toklist = tokpat.toklist;
            leftellipsis = tokpat.leftellipsis;
            rightellipsis = tokpat.rightellipsis;
        }

        ~TokenPattern()
        {
            // delete pattern;
        }

        // TODO : Find assignment use and duplicate in a specific method.
        //public TokenPattern operator=(TokenPattern tokpat)
        //{
        //    // delete pattern;

        //    pattern = tokpat.pattern.simplifyClone();
        //    toklist = tokpat.toklist;
        //    leftellipsis = tokpat.leftellipsis;
        //    rightellipsis = tokpat.rightellipsis;
        //    return this;
        //}

        public void setLeftEllipsis(bool val)
        {
            leftellipsis = val;
        }

        public void setRightEllipsis(bool val)
        {
            rightellipsis = val;
        }

        public bool getLeftEllipsis() => leftellipsis;

        public bool getRightEllipsis() => rightellipsis;

        public TokenPattern doAnd(TokenPattern tokpat)
        {
            // Return -this- AND tokpat
            TokenPattern res = new TokenPattern((Pattern)null);
            int sa = res.resolveTokens(this, tokpat);

            res.pattern = pattern.doAnd(tokpat.pattern, sa);
            return res;
        }

        public TokenPattern doOr(TokenPattern tokpat)
        {               // Return -this- OR tokpat
            TokenPattern res = new TokenPattern((Pattern)null);
            int sa = res.resolveTokens(this, tokpat);

            res.pattern = pattern.doOr(tokpat.pattern, sa);
            return res;
        }

        public TokenPattern doCat(TokenPattern tokpat)
        {
            // Return Concatenation of -this- and -tokpat-
            TokenPattern res = new TokenPattern((Pattern)null);
            int sa;

            res.leftellipsis = leftellipsis;
            res.rightellipsis = rightellipsis;
            res.toklist = toklist;
            if (rightellipsis || tokpat.leftellipsis) {
                // Check for interior ellipsis
                if (rightellipsis) {
                    if (!tokpat.alwaysInstructionTrue())
                        throw new SleighError("Interior ellipsis in pattern");
                }
                if (tokpat.leftellipsis) {
                    if (!alwaysInstructionTrue())
                        throw new SleighError("Interior ellipsis in pattern");
                    res.leftellipsis = true;
                }
                sa = -1;
            }
            else {
                sa = 0;
                IEnumerator<Token> iter = toklist.GetEnumerator();

                while (iter.MoveNext())
                    sa += iter.Current.getSize();
                iter = tokpat.toklist.GetEnumerator();
                while (iter.MoveNext())
                    res.toklist.Add(iter.Current);
                res.rightellipsis = tokpat.rightellipsis;
            }
            if (res.rightellipsis && res.leftellipsis)
                throw new SleighError("Double ellipsis in pattern");
            res.pattern = (sa < 0)
                ? pattern.doAnd(tokpat.pattern, 0)
                : pattern.doAnd(tokpat.pattern, sa);
            return res;
        }

        public TokenPattern commonSubPattern(TokenPattern tokpat)
        {
            // Construct pattern that matches anything
            // that matches either -this- or -tokpat-
            TokenPattern patres = new TokenPattern((Pattern)null); // Empty shell
            int i;
            bool reversedirection = false;

            if (leftellipsis || tokpat.leftellipsis) {
                if (rightellipsis || tokpat.rightellipsis)
                    throw new SleighError("Right/left ellipsis in commonSubPattern");
                reversedirection = true;
            }

            // Find common subset of tokens and ellipses
            patres.leftellipsis = leftellipsis || tokpat.leftellipsis;
            patres.rightellipsis = rightellipsis || tokpat.rightellipsis;
            int minnum = toklist.size();
            int maxnum = tokpat.toklist.size();
            if (maxnum < minnum) {
                int tmp = minnum;
                minnum = maxnum;
                maxnum = tmp;
            }
            if (reversedirection) {
                for (i = 0; i < minnum; ++i) {
                    Token tok = toklist[toklist.size() - 1 - i];
                    if (tok == tokpat.toklist[tokpat.toklist.size() - 1 - i])
                        patres.toklist.Insert(0, tok);
                    else
                        break;
                }
                if (i < maxnum)
                    patres.leftellipsis = true;
            }
            else {
                for (i = 0; i < minnum; ++i) {
                    Token tok = toklist[i];
                    if (tok == tokpat.toklist[i])
                        patres.toklist.Add(tok);
                    else
                        break;
                }
                if (i < maxnum)
                    patres.rightellipsis = true;
            }
            patres.pattern = pattern.commonSubPattern(tokpat.pattern, 0);
            return patres;
        }

        public Pattern getPattern() => pattern;

        public int getMinimumLength()
        {
            // Add up length of concatenated tokens
            int length = 0;
            for (int i = 0; i < toklist.size(); ++i)
                length += toklist[i].getSize();
            return length;
        }

        public bool alwaysTrue() => pattern.alwaysTrue();

        public bool alwaysFalse() => pattern.alwaysFalse();

        public bool alwaysInstructionTrue() => pattern.alwaysInstructionTrue();
    }
}
