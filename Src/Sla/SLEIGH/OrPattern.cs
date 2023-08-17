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
        private List<DisjointPattern> orlist = new List<DisjointPattern>();
        
        public OrPattern()
        {
        }

        public OrPattern(DisjointPattern a, DisjointPattern b)
        {
            orlist.Add(a);
            orlist.Add(b);
        }

        public OrPattern(List<DisjointPattern> list)
        {
            foreach (DisjointPattern pattern in list)
                orlist.Add(pattern);
        }

        ~OrPattern()
        {
            //foreach (DisjointPattern pattern in orlist)
            //    delete* iter;
        }

        public override Pattern simplifyClone()
        {
            // Look for alwaysTrue eliminate alwaysFalse
            foreach (DisjointPattern pattern in orlist) // Look for alwaysTrue
                if (pattern.alwaysTrue())
                    return new InstructionPattern(true);

            List<DisjointPattern> newlist = new List<DisjointPattern>();
            foreach (DisjointPattern pattern in orlist) // Look for alwaysFalse
                if (!pattern.alwaysFalse())
                    newlist.Add((DisjointPattern)(pattern).simplifyClone());

            if (newlist.empty())
                return new InstructionPattern(false);
            else if (newlist.size() == 1)
                return newlist[0];
            return new OrPattern(newlist);
        }

        public override void shiftInstruction(int sa)
        {
            foreach (DisjointPattern pattern in orlist)
                pattern.shiftInstruction(sa);
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
        {
            // This isn't quite right because different branches may cover the entire gamut
            foreach (DisjointPattern pattern in orlist)
                if (pattern.alwaysTrue()) return true;
            return false;
        }

        public override bool alwaysFalse()
        {
            foreach (DisjointPattern pattern in orlist)
                if (!pattern.alwaysFalse()) return false;
            return true;
        }

        public override bool alwaysInstructionTrue()
        {
            foreach (DisjointPattern pattern in orlist)
                if (!pattern.alwaysInstructionTrue()) return false;
            return true;
        }

        public override Pattern doOr(Pattern b, int sa)
        {
            OrPattern? b2 = b as OrPattern;
            List<DisjointPattern> newlist = new List<DisjointPattern>();

            foreach (DisjointPattern pattern in orlist)
                newlist.Add((DisjointPattern)(pattern).simplifyClone());
            if (sa < 0)
                foreach (DisjointPattern pattern in orlist)
                    pattern.shiftInstruction(-sa);

            if (b2 == (OrPattern)null)
                newlist.Add((DisjointPattern)b.simplifyClone());
            else {
                foreach (DisjointPattern pattern in b2.orlist)
                    newlist.Add((DisjointPattern)(pattern).simplifyClone());
            }
            if (sa > 0)
                for (int i = 0; i < newlist.size(); ++i)
                    newlist[i].shiftInstruction(sa);

            OrPattern tmpor = new OrPattern(newlist);
            return tmpor;
        }

        public override Pattern doAnd(Pattern b, int sa)
        {
            OrPattern? b2 = b as OrPattern;
            List<DisjointPattern> newlist = new List<DisjointPattern>();
            DisjointPattern tmp;
            OrPattern tmpor;

            if (b2 == (OrPattern)null) {
                foreach (DisjointPattern pattern in orlist) {
                    tmp = (DisjointPattern)(pattern).doAnd(b, sa);
                    newlist.Add(tmp);
                }
            }
            else {
                foreach (DisjointPattern pattern in orlist)
                    foreach (DisjointPattern b2Pattern in b2.orlist) {
                        tmp = (DisjointPattern)(pattern).doAnd(b2Pattern, sa);
                        newlist.Add(tmp);
                    }
            }
            tmpor = new OrPattern(newlist);
            return tmpor;
        }

        public override Pattern commonSubPattern(Pattern b, int sa)
        {
            IEnumerator<DisjointPattern> iter = orlist.GetEnumerator();
            Pattern res, next;

            if (!iter.MoveNext()) throw new BugException();
            res = iter.Current.commonSubPattern(b, sa);

            if (sa > 0)
                sa = 0;
            while (iter.MoveNext()) {
                next = iter.Current.commonSubPattern(res, sa);
                // delete res;
                res = next;
            }
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<or_pat>");
            for (int i = 0; i < orlist.size(); ++i)
                orlist[i].saveXml(s);
            s.WriteLine("</or_pat>");
        }

        public override void restoreXml(Element el)
        {
            foreach (Element subel in el.getChildren()) {
                DisjointPattern pat = DisjointPattern.restoreDisjoint(subel);
                orlist.Add(pat);
            }
        }
    }
}
