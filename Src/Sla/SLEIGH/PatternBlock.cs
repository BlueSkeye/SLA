using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    // A mask/value pair viewed as two bitstreams
    internal class PatternBlock
    {
        private int4 offset;            // Offset to non-zero byte of mask
        private int4 nonzerosize;       // Last byte(+1) containing nonzero mask
        private List<uintm> maskvec;  // Mask
        private List<uintm> valvec;       // Value

        private void normalize()
        {
            if (nonzerosize <= 0)
            {       // Check if alwaystrue or alwaysfalse
                offset = 0;         // in which case we don't need mask and value
                maskvec.clear();
                valvec.clear();
                return;
            }
            vector<uintm>::iterator iter1, iter2;

            iter1 = maskvec.begin();    // Cut zeros from beginning of mask
            iter2 = valvec.begin();
            while ((iter1 != maskvec.end()) && ((*iter1) == 0))
            {
                iter1++;
                iter2++;
                offset += sizeof(uintm);
            }
            maskvec.erase(maskvec.begin(), iter1);
            valvec.erase(valvec.begin(), iter2);

            if (!maskvec.empty())
            {
                int4 suboff = 0;        // Cut off unaligned zeros from beginning of mask
                uintm tmp = maskvec[0];
                while (tmp != 0)
                {
                    suboff += 1;
                    tmp >>= 8;
                }
                suboff = sizeof(uintm) - suboff;
                if (suboff != 0)
                {
                    offset += suboff;       // Slide up maskvec by suboff bytes
                    for (int4 i = 0; i < maskvec.size() - 1; ++i)
                    {
                        tmp = maskvec[i] << (suboff * 8);
                        tmp |= (maskvec[i + 1] >> ((sizeof(uintm) - suboff) * 8));
                        maskvec[i] = tmp;
                    }
                    maskvec.back() <<= suboff * 8;
                    for (int4 i = 0; i < valvec.size() - 1; ++i)
                    { // Slide up valvec by suboff bytes
                        tmp = valvec[i] << (suboff * 8);
                        tmp |= (valvec[i + 1] >> ((sizeof(uintm) - suboff) * 8));
                        valvec[i] = tmp;
                    }
                    valvec.back() <<= suboff * 8;
                }

                iter1 = maskvec.end();  // Cut zeros from end of mask
                iter2 = valvec.end();
                while (iter1 != maskvec.begin())
                {
                    --iter1;
                    --iter2;
                    if ((*iter1) != 0) break; // Find last non-zero
                }
                if (iter1 != maskvec.end())
                {
                    iter1++;            // Find first zero, in last zero chain
                    iter2++;
                }
                maskvec.erase(iter1, maskvec.end());
                valvec.erase(iter2, valvec.end());
            }

            if (maskvec.empty())
            {
                offset = 0;
                nonzerosize = 0;        // Always true
                return;
            }
            nonzerosize = maskvec.size() * sizeof(uintm);
            uintm tmp = maskvec.back(); // tmp must be nonzero
            while ((tmp & 0xff) == 0)
            {
                nonzerosize -= 1;
                tmp >>= 8;
            }
        }

        public PatternBlock(int4 off, uintm msk, uintm val)
        {               // Define mask and value pattern, confined to one uintm
            offset = off;
            maskvec.push_back(msk);
            valvec.push_back(val);
            nonzerosize = sizeof(uintm);    // Assume all non-zero bytes before normalization
            normalize();
        }

        public PatternBlock(bool tf)
        {
            offset = 0;
            if (tf)
                nonzerosize = 0;
            else
                nonzerosize = -1;
        }

        public PatternBlock(PatternBlock a, PatternBlock b)
        {               // Construct PatternBlock by ANDing two others together
            PatternBlock* res = a->intersect(b);
            offset = res->offset;
            nonzerosize = res->nonzerosize;
            maskvec = res->maskvec;
            valvec = res->valvec;
            delete res;
        }

        public PatternBlock(List<PatternBlock> list)
        {               // AND several blocks together to construct new block
            PatternBlock* res,*next;

            if (list.empty())
            {       // If not ANDing anything
                offset = 0;         // make constructed block always true
                nonzerosize = 0;
                return;
            }
            res = list[0];
            for (int4 i = 1; i < list.size(); ++i)
            {
                next = res->intersect(list[i]);
                delete res;
                res = next;
            }
            offset = res->offset;
            nonzerosize = res->nonzerosize;
            maskvec = res->maskvec;
            valvec = res->valvec;
            delete res;
        }

        public PatternBlock commonSubPattern(PatternBlock b)
        {               // The resulting pattern has a 1-bit in the mask
                        // only if the two pieces have a 1-bit and the
                        // values agree
            PatternBlock* res = new PatternBlock(true);
            int4 maxlength = (getLength() > b->getLength()) ? getLength() : b->getLength();

            res->offset = 0;
            int4 offset = 0;
            uintm mask1, val1, mask2, val2;
            uintm resmask, resval;
            while (offset < maxlength)
            {
                mask1 = getMask(offset * 8, sizeof(uintm) * 8);
                val1 = getValue(offset * 8, sizeof(uintm) * 8);
                mask2 = b->getMask(offset * 8, sizeof(uintm) * 8);
                val2 = b->getValue(offset * 8, sizeof(uintm) * 8);
                resmask = mask1 & mask2 & ~(val1 ^ val2);
                resval = val1 & val2 & resmask;
                res->maskvec.push_back(resmask);
                res->valvec.push_back(resval);
                offset += sizeof(uintm);
            }
            res->nonzerosize = maxlength;
            res->normalize();
            return res;
        }

        public PatternBlock intersect(PatternBlock b)
        { // Construct the intersecting pattern
            if (alwaysFalse() || b->alwaysFalse())
                return new PatternBlock(false);
            PatternBlock* res = new PatternBlock(true);
            int4 maxlength = (getLength() > b->getLength()) ? getLength() : b->getLength();

            res->offset = 0;
            int4 offset = 0;
            uintm mask1, val1, mask2, val2, commonmask;
            uintm resmask, resval;
            while (offset < maxlength)
            {
                mask1 = getMask(offset * 8, sizeof(uintm) * 8);
                val1 = getValue(offset * 8, sizeof(uintm) * 8);
                mask2 = b->getMask(offset * 8, sizeof(uintm) * 8);
                val2 = b->getValue(offset * 8, sizeof(uintm) * 8);
                commonmask = mask1 & mask2; // Bits in mask shared by both patterns
                if ((commonmask & val1) != (commonmask & val2))
                {
                    res->nonzerosize = -1;  // Impossible pattern
                    res->normalize();
                    return res;
                }
                resmask = mask1 | mask2;
                resval = (mask1 & val1) | (mask2 & val2);
                res->maskvec.push_back(resmask);
                res->valvec.push_back(resval);
                offset += sizeof(uintm);
            }
            res->nonzerosize = maxlength;
            res->normalize();
            return res;
        }

        public bool specializes(PatternBlock op2)
        {               // does every masked bit in -this- match the corresponding
                        // masked bit in -op2-
            int4 length = 8 * op2->getLength();
            int4 tmplength;
            uintm mask1, mask2, value1, value2;
            int4 sbit = 0;
            while (sbit < length)
            {
                tmplength = length - sbit;
                if (tmplength > 8 * sizeof(uintm))
                    tmplength = 8 * sizeof(uintm);
                mask1 = getMask(sbit, tmplength);
                value1 = getValue(sbit, tmplength);
                mask2 = op2->getMask(sbit, tmplength);
                value2 = op2->getValue(sbit, tmplength);
                if ((mask1 & mask2) != mask2) return false;
                if ((value1 & mask2) != (value2 & mask2)) return false;
                sbit += tmplength;
            }
            return true;
        }

        public bool identical(PatternBlock op2)
        {               // Do the mask and value match exactly
            int4 tmplength;
            int4 length = 8 * op2->getLength();
            tmplength = 8 * getLength();
            if (tmplength > length)
                length = tmplength;     // Maximum of two lengths
            uintm mask1, mask2, value1, value2;
            int4 sbit = 0;
            while (sbit < length)
            {
                tmplength = length - sbit;
                if (tmplength > 8 * sizeof(uintm))
                    tmplength = 8 * sizeof(uintm);
                mask1 = getMask(sbit, tmplength);
                value1 = getValue(sbit, tmplength);
                mask2 = op2->getMask(sbit, tmplength);
                value2 = op2->getValue(sbit, tmplength);
                if (mask1 != mask2) return false;
                if ((mask1 & value1) != (mask2 & value2)) return false;
                sbit += tmplength;
            }
            return true;
        }

        public PatternBlock clone()
        {
            PatternBlock* res = new PatternBlock(true);

            res->offset = offset;
            res->nonzerosize = nonzerosize;
            res->maskvec = maskvec;
            res->valvec = valvec;
            return res;
        }

        public void shift(int4 sa)
        {
            offset += sa;
            normalize();
        }

        public int4 getLength() => offset+nonzerosize;

        public uintm getMask(int4 startbit, int4 size)
        {
            startbit -= 8 * offset;
            // Note the division and remainder here is unsigned.  Then it is recast to signed. 
            // If startbit is negative, then wordnum1 is either negative or very big,
            // if (unsigned size is same as sizeof int)
            // In either case, shift should come out between 0 and 8*sizeof(uintm)-1
            int4 wordnum1 = startbit / (8 * sizeof(uintm));
            int4 shift = startbit % (8 * sizeof(uintm));
            int4 wordnum2 = (startbit + size - 1) / (8 * sizeof(uintm));
            uintm res;

            if ((wordnum1 < 0) || (wordnum1 >= maskvec.size()))
                res = 0;
            else
                res = maskvec[wordnum1];

            res <<= shift;
            if (wordnum1 != wordnum2)
            {
                uintm tmp;
                if ((wordnum2 < 0) || (wordnum2 >= maskvec.size()))
                    tmp = 0;
                else
                    tmp = maskvec[wordnum2];
                res |= (tmp >> (8 * sizeof(uintm) - shift));
            }
            res >>= (8 * sizeof(uintm) - size);

            return res;
        }

        public uintm getValue(int4 startbit, int4 size)
        {
            startbit -= 8 * offset;
            int4 wordnum1 = startbit / (8 * sizeof(uintm));
            int4 shift = startbit % (8 * sizeof(uintm));
            int4 wordnum2 = (startbit + size - 1) / (8 * sizeof(uintm));
            uintm res;

            if ((wordnum1 < 0) || (wordnum1 >= valvec.size()))
                res = 0;
            else
                res = valvec[wordnum1];
            res <<= shift;
            if (wordnum1 != wordnum2)
            {
                uintm tmp;
                if ((wordnum2 < 0) || (wordnum2 >= valvec.size()))
                    tmp = 0;
                else
                    tmp = valvec[wordnum2];
                res |= (tmp >> (8 * sizeof(uintm) - shift));
            }
            res >>= (8 * sizeof(uintm) - size);

            return res;
        }

        public bool alwaysTrue() => (nonzerosize==0);

        public bool alwaysFalse() => (nonzerosize==-1);

        public bool isInstructionMatch(ParserWalker walker)
        {
            if (nonzerosize <= 0) return (nonzerosize == 0);
            int4 off = offset;
            for (int4 i = 0; i < maskvec.size(); ++i)
            {
                uintm data = walker.getInstructionBytes(off, sizeof(uintm));
                if ((maskvec[i] & data) != valvec[i]) return false;
                off += sizeof(uintm);
            }
            return true;
        }

        public bool isContextMatch(ParserWalker walker)
        {
            if (nonzerosize <= 0) return (nonzerosize == 0);
            int4 off = offset;
            for (int4 i = 0; i < maskvec.size(); ++i)
            {
                uintm data = walker.getContextBytes(off, sizeof(uintm));
                if ((maskvec[i] & data) != valvec[i]) return false;
                off += sizeof(uintm);
            }
            return true;
        }

        public void saveXml(TextWriter s)
        {
            s << "<pat_block ";
            s << "offset=\"" << dec << offset << "\" ";
            s << "nonzero=\"" << nonzerosize << "\">\n";
            for (int4 i = 0; i < maskvec.size(); ++i)
            {
                s << "  <mask_word ";
                s << "mask=\"0x" << hex << maskvec[i] << "\" ";
                s << "val=\"0x" << valvec[i] << "\"/>\n";
            }
            s << "</pat_block>\n";
        }

        public void restoreXml(Element el)
        {
            {
                istringstream s(el->getAttributeValue("offset"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> offset;
            }
            {
                istringstream s(el->getAttributeValue("nonzero"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> nonzerosize;
            }
            List list = el->getChildren();
            List::const_iterator iter;
            iter = list.begin();
            uintm mask, val;
            while (iter != list.end())
            {
                Element* subel = *iter;
                {
                    istringstream s(subel->getAttributeValue("mask"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> mask;
                }
                {
                    istringstream s(subel->getAttributeValue("val"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> val;
                }
                maskvec.push_back(mask);
                valvec.push_back(val);
                ++iter;
            }
            normalize();
        }
    }
}
