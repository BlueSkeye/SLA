using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class DecisionProperties
    {
        private List<pair<Constructor, Constructor>> identerrors;
        private List<pair<Constructor, Constructor>> conflicterrors;
        
        public void identicalPattern(Constructor a, Constructor b)
        { // Note that -a- and -b- have identical patterns
            if ((!a.isError()) && (!b.isError()))
            {
                a.setError(true);
                b.setError(true);

                identerrors.Add(make_pair(a, b));
            }
        }

        public void conflictingPattern(Constructor a, Constructor b)
        { // Note that -a- and -b- have (potentially) conflicting patterns
            if ((!a.isError()) && (!b.isError()))
            {
                a.setError(true);
                b.setError(true);

                conflicterrors.Add(make_pair(a, b));
            }
        }

        public List<pair<Constructor, Constructor>> getIdentErrors() => identerrors;

        public List<pair<Constructor, Constructor>> getConflictErrors() => conflicterrors;
    }
}
