﻿using Sla.CORE;

namespace Sla.SLEIGH
{
    internal abstract class DisjointPattern : Pattern
    {
        // A pattern with no ORs in it
        protected abstract PatternBlock? getBlock(bool context);
        
        public override int numDisjoint() => 0;

        public override DisjointPattern getDisjoint(int i) => (DisjointPattern)null;

        public uint getMask(int startbit, int size, bool context)
        {
            PatternBlock? block = getBlock(context);
            if (block != (PatternBlock)null)
                return block.getMask(startbit, size);
            return 0;
        }

        public uint getValue(int startbit, int size, bool context)
        {
            PatternBlock? block = getBlock(context);
            if (block != (PatternBlock)null)
                return block.getValue(startbit, size);
            return 0;
        }

        public int getLength(bool context)
        {
            PatternBlock? block = getBlock(context);
            if (block != (PatternBlock)null)
                return block.getLength();
            return 0;
        }

        public bool specializes(DisjointPattern op2)
        {
            // Return true, if everywhere this's mask is non-zero
            // op2's mask is non-zero and op2's value match this's
            PatternBlock a;
            PatternBlock b;

            a = getBlock(false);
            b = op2.getBlock(false);
            if ((b != (PatternBlock)null) && (!b.alwaysTrue())) {
                // a must match existing block
                if (a == (PatternBlock)null) return false;
                if (!a.specializes(b)) return false;
            }
            a = getBlock(true);
            b = op2.getBlock(true);
            if ((b != (PatternBlock)null) && (!b.alwaysTrue())) {
                // a must match existing block
                if (a == (PatternBlock)null) return false;
                if (!a.specializes(b)) return false;
            }
            return true;
        }

        public bool identical(DisjointPattern op2)
        { 
            // Return true if patterns match exactly
            PatternBlock? a = getBlock(false);
            PatternBlock? b = op2.getBlock(false);
            if (b != (PatternBlock)null) {
                // a must match existing block
                if (a == (PatternBlock)null) {
                    if (!b.alwaysTrue())
                        return false;
                }
                else if (!a.identical(b))
                    return false;
            }
            else {
                if ((a != (PatternBlock)null) && (!a.alwaysTrue()))
                    return false;
            }
            a = getBlock(true);
            b = op2.getBlock(true);
            if (b != (PatternBlock)null) {
                // a must match existing block
                if (a == (PatternBlock)null) {
                    if (!b.alwaysTrue())
                        return false;
                }
                else if (!a.identical(b))
                    return false;
            }
            else {
                if ((a != (PatternBlock)null) && (!a.alwaysTrue()))
                    return false;
            }
            return true;
        }

        public bool resolvesIntersect(DisjointPattern op1, DisjointPattern op2)
        {
            // Is this pattern equal to the intersection of -op1- and -op2-
            if (!resolveIntersectBlock(op1.getBlock(false), op2.getBlock(false), getBlock(false)))
                return false;
            return resolveIntersectBlock(op1.getBlock(true), op2.getBlock(true), getBlock(true));
        }

        public static DisjointPattern restoreDisjoint(Element el)
        {
            // DisjointPattern factory
            DisjointPattern res;
            if (el.getName() == "instruct_pat")
                res = new InstructionPattern();
            else if (el.getName() == "context_pat")
                res = new ContextPattern();
            else
                res = new CombinePattern();
            res.restoreXml(el);
            return res;
        }

        private static bool resolveIntersectBlock(PatternBlock bl1, PatternBlock bl2,
            PatternBlock thisblock)
        {
            PatternBlock inter;
            bool allocated = false;
            bool res = true;

            if (bl1 == (PatternBlock)null)
                inter = bl2;
            else if (bl2 == (PatternBlock)null)
                inter = bl1;
            else {
                allocated = true;
                inter = bl1.intersect(bl2);
            }
            if (inter == (PatternBlock)null) {
                if (thisblock != (PatternBlock)null)
                    res = false;
            }
            else if (thisblock == (PatternBlock)null)
                res = false;
            else
                res = thisblock.identical(inter);
            //if (allocated)
            //    delete inter;
            return res;
        }

    }
}
