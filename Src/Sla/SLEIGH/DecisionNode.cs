﻿using Sla.CORE;
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
    internal class DecisionNode
    {
        private List<pair<DisjointPattern, Constructor>> list;
        private List<DecisionNode> children;
        private int4 num;           // Total number of patterns we distinguish
        private bool contextdecision;       // True if this is decision based on context
        private int4 startbit, bitsize;        // Bits in the stream on which to base the decision
        private DecisionNode parent;

        private void chooseOptimalField()
        {
            double score = 0.0;

            int4 sbit, size;        // The current field
            bool context;
            double sc;

            int4 maxlength, numfixed, maxfixed;

            maxfixed = 1;
            context = true;
            do
            {
                maxlength = 8 * getMaximumLength(context);
                for (sbit = 0; sbit < maxlength; ++sbit)
                {
                    numfixed = getNumFixed(sbit, 1, context); // How may patterns specify this bit
                    if (numfixed < maxfixed) continue; // Skip this bit, if we don't have maximum specification
                    sc = getScore(sbit, 1, context);

                    // if we got more patterns this time than previously, and a positive score, reset
                    // the high score (we prefer this bit, because it has a higher numfixed, regardless
                    // of the difference in score, as long as the new score is positive).
                    if ((numfixed > maxfixed) && (sc > 0.0))
                    {
                        score = sc;
                        maxfixed = numfixed;
                        startbit = sbit;
                        bitsize = 1;
                        contextdecision = context;
                        continue;
                    }
                    // We have maximum patterns
                    if (sc > score)
                    {
                        score = sc;
                        startbit = sbit;
                        bitsize = 1;
                        contextdecision = context;
                    }
                }
                context = !context;
            } while (!context);

            context = true;
            do
            {
                maxlength = 8 * getMaximumLength(context);
                for (size = 2; size <= 8; ++size)
                {
                    for (sbit = 0; sbit < maxlength - size + 1; ++sbit)
                    {
                        if (getNumFixed(sbit, size, context) < maxfixed) continue; // Consider only maximal fields
                        sc = getScore(sbit, size, context);
                        if (sc > score)
                        {
                            score = sc;
                            startbit = sbit;
                            bitsize = size;
                            contextdecision = context;
                        }
                    }
                }
                context = !context;
            } while (!context);
            if (score <= 0.0)       // If we failed to get a positive score
                bitsize = 0;        // treat the node as terminal
        }

        private double getScore(int4 low, int4 size, bool context)
        {
            int4 numBins = 1 << size;       // size is between 1 and 8
            int4 i;
            uintm val, mask;
            uintm m = ((uintm)1) << size;
            m = m - 1;

            int4 total = 0;
            vector<int4> count(numBins,0);

            for (i = 0; i < list.size(); ++i)
            {
                mask = list[i].first->getMask(low, size, context);
                if ((mask & m) != m) continue;  // Skip if field not fully specified
                val = list[i].first->getValue(low, size, context);
                total += 1;
                count[val] += 1;
            }
            if (total <= 0) return -1.0;
            double sc = 0.0;
            for (i = 0; i < numBins; ++i)
            {
                if (count[i] <= 0) continue;
                if (count[i] >= list.size()) return -1.0;
                double p = ((double)count[i]) / total;
                sc -= p * log(p);
            }
            return (sc / log(2.0));
        }

        private int4 getNumFixed(int4 low, int4 size, bool context)
        {               // Get number of patterns that specify this field
            int4 count = 0;
            uintm mask;
            // Bits which must be specified in the mask
            uintm m = (size == 8 * sizeof(uintm)) ? 0 : (((uintm)1) << size);
            m = m - 1;

            for (int4 i = 0; i < list.size(); ++i)
            {
                mask = list[i].first->getMask(low, size, context);
                if ((mask & m) == m)
                    count += 1;
            }
            return count;
        }

        private int4 getMaximumLength(bool context)
        {               // Get maximum length of instruction pattern in bytes
            int4 max = 0;
            int4 val, i;

            for (i = 0; i < list.size(); ++i)
            {
                val = list[i].first->getLength(context);
                if (val > max)
                    max = val;
            }
            return max;
        }

        private void consistentValues(List<uint4> bins, DisjointPattern pat)
        {               // Produce all possible values of -pat- by
                        // iterating through all possible values of the
                        // "don't care" bits within the value of -pat-
                        // that intersects with this node (startbit,bitsize,context)
            uintm m = (bitsize == 8 * sizeof(uintm)) ? 0 : (((uintm)1) << bitsize);
            m = m - 1;
            uintm commonMask = m & pat->getMask(startbit, bitsize, contextdecision);
            uintm commonValue = commonMask & pat->getValue(startbit, bitsize, contextdecision);
            uintm dontCareMask = m ^ commonMask;

            for (uintm i = 0; i <= dontCareMask; ++i)
            { // Iterate over values that contain all don't care bits
                if ((i & dontCareMask) != i) continue; // If all 1 bits in the value are don't cares
                bins.push_back(commonValue | i); // add 1 bits into full value and store
            }
        }

        public DecisionNode()
        {
        }

        public DecisionNode(DecisionNode p
{
            parent = p;
            num = 0;
            startbit = 0;
            bitsize = 0;
            contextdecision = false;
        }

        ~DecisionNode()
        {               // We own sub nodes
            vector<DecisionNode*>::iterator iter;
            for (iter = children.begin(); iter != children.end(); ++iter)
                delete* iter;
            vector<pair<DisjointPattern*, Constructor*>>::iterator piter;
            for (piter = list.begin(); piter != list.end(); ++piter)
                delete(*piter).first;   // Delete the patterns
        }

        public Constructor resolve(ParserWalker walker)
        {
            if (bitsize == 0)
            {       // The node is terminal
                vector<pair<DisjointPattern*, Constructor*>>::const_iterator iter;
                for (iter = list.begin(); iter != list.end(); ++iter)
                    if ((*iter).first->isMatch(walker))
                        return (*iter).second;
                ostringstream s;
                s << walker.getAddr().getShortcut();
                walker.getAddr().printRaw(s);
                s << ": Unable to resolve constructor";
                throw BadDataError(s.str());
            }
            uintm val;
            if (contextdecision)
                val = walker.getContextBits(startbit, bitsize);
            else
                val = walker.getInstructionBits(startbit, bitsize);
            return children[val]->resolve(walker);
        }

        public void addConstructorPair(DisjointPattern pat, Constructor ct)
        {
            DisjointPattern* clone = (DisjointPattern*)pat->simplifyClone(); // We need to own pattern
            list.push_back(pair<DisjointPattern*, Constructor*>(clone, ct));
            num += 1;
        }

        public void split(DecisionProperties props)
        {
            if (list.size() <= 1)
            {
                bitsize = 0;        // Only one pattern, terminal node by default
                return;
            }

            chooseOptimalField();
            if (bitsize == 0)
            {
                orderPatterns(props);
                return;
            }
            if ((parent != (DecisionNode*)0) && (list.size() >= parent->num))
                throw new LowlevelError("Child has as many Patterns as parent");

            int4 numChildren = 1 << bitsize;

            for (int4 i = 0; i < numChildren; ++i)
            {
                DecisionNode* nd = new DecisionNode(this);
                children.push_back(nd);
            }
            for (int4 i = 0; i < list.size(); ++i)
            {
                vector<uint4> vals;     // Bins this pattern belongs in
                                        // If the pattern does not care about some
                                        // bits in the field we are splitting on, that
                                        // pattern will get put into multiple bins
                consistentValues(vals, list[i].first);
                for (int4 j = 0; j < vals.size(); ++j)
                    children[vals[j]]->addConstructorPair(list[i].first, list[i].second);
                delete list[i].first;   // We no longer need original pattern
            }
            list.clear();

            for (int4 i = 0; i < numChildren; ++i)
                children[i]->split(props);
        }

        public void orderPatterns(DecisionProperties props)
        {
            // This is a tricky routine.  When this routine is called, the patterns remaining in the
            // the decision node can no longer be distinguished by examining additional bits. The basic
            // idea here is that the patterns should be ordered so that the most specialized should come
            // first in the list. Pattern 1 is a specialization of pattern 2, if the set of instructions
            // matching 1 is contained in the set matching 2.  So in the simplest case, the pattern order
            // should represent a strict nesting.  Unfortunately, there are many potential situations where
            // patterns don't necessarily nest.
            //   1) An "or" of two patterns.  This can be an explicit '|' operator in the Constructor, in
            //      which case this can be detected because the two patterns point to the same constructor
            //      But the "or" can be implied across two constructors that do the same thing.  This should
            //      probably be flagged as an error except in the following case.
            //   2) Two patterns aren't properly nested, but they are "resolved" by a third pattern which
            //      covers the intersection of the first two patterns.  Sometimes its easier to specify
            //      three cases that need to be distinguished in this way.
            //   3) Recursive constructors that use a "guard" context bit.  The guard bit is used to prevent
            //      the recursive constructor from matching repeatedly, but it's too much work to put a
            //      constraint an the bit for every other pattern.
            //   4) Other situations where the ability to distinguish between constructors is hidden in
            //      the subconstructors.
            // This routine can determine if an intersection results from case 1) or case 2)
            int4 i, j, k;
            vector<pair<DisjointPattern*, Constructor*>> newlist;
            vector<pair<DisjointPattern*, Constructor*>> conflictlist;

            // Check for identical patterns
            for (i = 0; i < list.size(); ++i)
            {
                for (j = 0; j < i; ++j)
                {
                    DisjointPattern* ipat = list[i].first;
                    DisjointPattern* jpat = list[j].first;
                    if (ipat->identical(jpat))
                        props.identicalPattern(list[i].second, list[j].second);
                }
            }

            newlist = list;
            for (i = 0; i < list.size(); ++i)
            {
                for (j = 0; j < i; ++j)
                {
                    DisjointPattern* ipat = newlist[i].first;
                    DisjointPattern* jpat = list[j].first;
                    if (ipat->specializes(jpat))
                        break;
                    if (!jpat->specializes(ipat))
                    { // We have a potential conflict
                        Constructor* iconst = newlist[i].second;
                        Constructor* jconst = list[j].second;
                        if (iconst == jconst)
                        { // This is an OR in the pattern for ONE constructor
                          // So there is no conflict
                        }
                        else
                        {           // A true conflict that needs to be resolved
                            conflictlist.push_back(pair<DisjointPattern*, Constructor*>(ipat, iconst));
                            conflictlist.push_back(pair<DisjointPattern*, Constructor*>(jpat, jconst));
                        }
                    }
                }
                for (k = i - 1; k >= j; --k)
                    list[k + 1] = list[k];
                list[j] = newlist[i];
            }

            // Check if intersection patterns are present, which resolve conflicts
            for (i = 0; i < conflictlist.size(); i += 2)
            {
                DisjointPattern* pat1,*pat2;
                Constructor* const1,*const2;
                pat1 = conflictlist[i].first;
                const1 = conflictlist[i].second;
                pat2 = conflictlist[i + 1].first;
                const2 = conflictlist[i + 1].second;
                bool resolved = false;
                for (j = 0; j < list.size(); ++j)
                {
                    DisjointPattern* tpat = list[j].first;
                    Constructor* tconst = list[j].second;
                    if ((tpat == pat1) && (tconst == const1)) break; // Ran out of possible specializations
                    if ((tpat == pat2) && (tconst == const2)) break;
                    if (tpat->resolvesIntersect(pat1, pat2))
                    {
                        resolved = true;
                        break;
                    }
                }
                if (!resolved)
                    props.conflictingPattern(const1, const2);
            }
        }

        public void saveXml(TextWriter s)
        {
            s << "<decision";
            s << " number=\"" << dec << num << "\"";
            s << " context=\"";
            if (contextdecision)
                s << "true\"";
            else
                s << "false\"";
            s << " start=\"" << startbit << "\"";
            s << " size=\"" << bitsize << "\"";
            s << ">\n";
            for (int4 i = 0; i < list.size(); ++i)
            {
                s << "<pair id=\"" << dec << list[i].second->getId() << "\">\n";
                list[i].first->saveXml(s);
                s << "</pair>\n";
            }
            for (int4 i = 0; i < children.size(); ++i)
                children[i]->saveXml(s);
            s << "</decision>\n";
        }

        public void restoreXml(Element el, DecisionNode par,SubtableSymbol sub)
        {
            parent = par;
            {
                istringstream s(el->getAttributeValue("number"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> num;
            }
            contextdecision = xml_readbool(el->getAttributeValue("context"));
            {
                istringstream s(el->getAttributeValue("start"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> startbit;
            }
            {
                istringstream s(el->getAttributeValue("size"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> bitsize;
            }
            const List &childlist(el->getChildren());
            List::const_iterator iter;
            iter = childlist.begin();
            while (iter != childlist.end())
            {
                if ((*iter)->getName() == "pair")
                {
                    Constructor* ct;
                    DisjointPattern* pat;
                    uintm id;
                    istringstream s((* iter)->getAttributeValue("id"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> id;
                    ct = sub->getConstructor(id);
                    pat = DisjointPattern::restoreDisjoint((*iter)->getChildren().front());
                    //This increments num      addConstructorPair(pat,ct);
                    list.push_back(pair<DisjointPattern*, Constructor*>(pat, ct));
                    //delete pat;		// addConstructorPair makes its own copy
                }
                else if ((*iter)->getName() == "decision")
                {
                    DecisionNode* subnode = new DecisionNode();
                    subnode->restoreXml(*iter, this, sub);
                    children.push_back(subnode);
                }
                ++iter;
            }
        }
    }
}