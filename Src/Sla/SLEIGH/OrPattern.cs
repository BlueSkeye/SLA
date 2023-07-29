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
using static System.Net.Mime.MediaTypeNames;

namespace Sla.SLEIGH
{
    internal class OrPattern : Pattern
    {
        private List<DisjointPattern> orlist;
        
        public OrPattern()
        {
        }

        public OrPattern(DisjointPattern a, DisjointPattern b)
        {
            orlist.push_back(a);
            orlist.push_back(b);
        }

        public OrPattern(List<DisjointPattern> list)
        {
            List<DisjointPattern*>::const_iterator iter;

            for (iter = list.begin(); iter != list.end(); ++iter)
                orlist.push_back(*iter);
        }

        ~OrPattern()
        {
            List<DisjointPattern*>::iterator iter;

            for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                delete* iter;
        }

        public override Pattern simplifyClone()
        {               // Look for alwaysTrue eliminate alwaysFalse
            List<DisjointPattern*>::const_iterator iter;

            for (iter = orlist.begin(); iter != orlist.end(); ++iter) // Look for alwaysTrue
                if ((*iter).alwaysTrue())
                    return new InstructionPattern(true);

            List<DisjointPattern*> newlist;
            for (iter = orlist.begin(); iter != orlist.end(); ++iter) // Look for alwaysFalse
                if (!(*iter).alwaysFalse())
                    newlist.push_back((DisjointPattern*)(*iter).simplifyClone());

            if (newlist.empty())
                return new InstructionPattern(false);
            else if (newlist.size() == 1)
                return newlist[0];
            return new OrPattern(newlist);
        }

        public override void shiftInstruction(int sa)
        {
            List<DisjointPattern*>::iterator iter;

            for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                (*iter).shiftInstruction(sa);
        }

        public override bool isMatch(ParserWalker walker)
        {
            for (int i = 0; i < orlist.size(); ++i)
                if (orlist[i].isMatch(walker))
                    return true;
            return false;
        }

        public override int numDisjoint() => orlist.size();

        public override DisjointPattern getDisjoint(int i) => orlist[i];

        public override bool alwaysTrue()
        {               // This isn't quite right because different branches
                        // may cover the entire gamut
            List<DisjointPattern*>::const_iterator iter;

            for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                if ((*iter).alwaysTrue()) return true;
            return false;
        }

        public override bool alwaysFalse()
        {
            List<DisjointPattern*>::const_iterator iter;

            for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                if (!(*iter).alwaysFalse()) return false;
            return true;
        }

        public override bool alwaysInstructionTrue()
        {
            List<DisjointPattern*>::const_iterator iter;

            for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                if (!(*iter).alwaysInstructionTrue()) return false;
            return true;
        }

        public override Pattern doOr(Pattern b, int sa)
        {
            OrPattern b2 = dynamic_cast <OrPattern> (b);
            List<DisjointPattern*> newlist;
            List<DisjointPattern*>::const_iterator iter;

            for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                newlist.push_back((DisjointPattern*)(*iter).simplifyClone());
            if (sa < 0)
                for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                    (*iter).shiftInstruction(-sa);

            if (b2 == (OrPattern*)0)
                newlist.push_back((DisjointPattern*)b.simplifyClone());
            else
            {
                for (iter = b2.orlist.begin(); iter != b2.orlist.end(); ++iter)
                    newlist.push_back((DisjointPattern*)(*iter).simplifyClone());
            }
            if (sa > 0)
                for (int i = 0; i < newlist.size(); ++i)
                    newlist[i].shiftInstruction(sa);

            OrPattern* tmpor = new OrPattern(newlist);
            return tmpor;
        }

        public override Pattern doAnd(Pattern b, int sa)
        {
            OrPattern b2 = dynamic_cast <OrPattern*> (b);
            List<DisjointPattern*> newlist;
            List<DisjointPattern*>::const_iterator iter, iter2;
            DisjointPattern* tmp;
            OrPattern* tmpor;

            if (b2 == (OrPattern*)0) {
                for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                {
                    tmp = (DisjointPattern*)(*iter).doAnd(b, sa);
                    newlist.push_back(tmp);
                }
            }
            else
            {
                for (iter = orlist.begin(); iter != orlist.end(); ++iter)
                    for (iter2 = b2.orlist.begin(); iter2 != b2.orlist.end(); ++iter2)
                    {
                        tmp = (DisjointPattern*)(*iter).doAnd(*iter2, sa);
                        newlist.push_back(tmp);
                    }
            }
            tmpor = new OrPattern(newlist);
            return tmpor;
        }

        public override Pattern commonSubPattern(Pattern b, int sa)
        {
            List<DisjointPattern*>::const_iterator iter;
            Pattern* res,*next;

            iter = orlist.begin();
            res = (*iter).commonSubPattern(b, sa);
            iter++;

            if (sa > 0)
                sa = 0;
            while (iter != orlist.end())
            {
                next = (*iter).commonSubPattern(res, sa);
                delete res;
                res = next;
                ++iter;
            }
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<or_pat>\n";
            for (int i = 0; i < orlist.size(); ++i)
                orlist[i].saveXml(s);
            s << "</or_pat>\n";
        }

        public override void restoreXml(Element el)
        {
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            while (iter != list.end())
            {
                DisjointPattern* pat = DisjointPattern::restoreDisjoint(*iter);
                orlist.push_back(pat);
                ++iter;
            }
        }
    }
}
