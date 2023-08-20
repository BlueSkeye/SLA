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
        private List<Tuple<Constructor, Constructor>> identerrors =
            new List<Tuple<Constructor, Constructor>>();
        private List<Tuple<Constructor, Constructor>> conflicterrors =
            new List<Tuple<Constructor, Constructor>>();
        
        public void identicalPattern(Constructor a, Constructor b)
        {
            // Note that -a- and -b- have identical patterns
            if ((!a.isError()) && (!b.isError())) {
                a.setError(true);
                b.setError(true);
                identerrors.Add(new Tuple<Constructor, Constructor>(a, b));
            }
        }

        public void conflictingPattern(Constructor a, Constructor b)
        {
            // Note that -a- and -b- have (potentially) conflicting patterns
            if ((!a.isError()) && (!b.isError())) {
                a.setError(true);
                b.setError(true);

                conflicterrors.Add(new Tuple<Constructor, Constructor>(a, b));
            }
        }

        public List<Tuple<Constructor, Constructor>> getIdentErrors() => identerrors;

        public List<Tuple<Constructor, Constructor>> getConflictErrors() => conflicterrors;
    }
}
