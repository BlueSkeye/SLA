using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcDuplicateHash : IfaceDecompCommand
    {
        /// For each duplicate discovered, a message is written to the provided stream.
        /// \param fd is the given function to search
        /// \param s is the stream to write messages to
        public static void check(Funcdata fd, TextWriter s)
        {
            DynamicHash dhash;

            VarnodeLocSet::const_iterator iter, enditer;
            pair<set<ulong>::iterator, bool> res;
            iter = fd.beginLoc();
            enditer = fd.endLoc();
            while (iter != enditer)
            {
                Varnode* vn = *iter;
                ++iter;
                if (vn.isAnnotation()) continue;
                if (vn.isConstant())
                {
                    PcodeOp* op = vn.loneDescend();
                    int slot = op.getSlot(vn);
                    if (slot == 0)
                    {
                        if (op.code() == OpCode.CPUI_LOAD) continue;
                        if (op.code() == OpCode.CPUI_STORE) continue;
                        if (op.code() == OpCode.CPUI_RETURN) continue;
                    }
                }
                else if (vn.getSpace().getType() != spacetype.IPTR_INTERNAL)
                    continue;
                else if (vn.isImplied())
                    continue;
                dhash.uniqueHash(vn, fd);
                if (dhash.getHash() == 0)
                {
                    // We have a duplicate
                    PcodeOp op;
                    if (vn.beginDescend() != vn.endDescend())
                        op = *vn.beginDescend();
                    else
                        op = vn.getDef();
                    s << "Could not get unique hash for : ";
                    vn.printRaw(s);
                    s << " : ";
                    op.printRaw(s);
                    s << endl;
                    return;
                }
                uint total = DynamicHash::getTotalFromHash(dhash.getHash());
                if (total != 1)
                {
                    PcodeOp op;
                    if (vn.beginDescend() != vn.endDescend())
                        op = *vn.beginDescend();
                    else
                        op = vn.getDef();
                    s << "Duplicate : ";
                    s << dec << DynamicHash::getPositionFromHash(dhash.getHash()) << " out of " << total << " : ";
                    vn.printRaw(s);
                    s << " : ";
                    op.printRaw(s);
                    s << endl;
                }
            }
        }

        /// \class IfcDuplicateHash
        /// \brief Check for duplicate hashes in functions: `duplicate hash`
        ///
        /// All functions in the architecture/program are decompiled, and for each
        /// a check is made for Varnode pairs with identical hash values.
        public override void execute(TextReader s)
        {
            iterateFunctionsAddrOrder();
        }

        public override void iterationCallback(Funcdata fd)
        {
            clock_t start_time, end_time;
            float duration;

            if (fd.hasNoCode())
            {
                *status.optr << "No code for " << fd.getName() << endl;
                return;
            }
            try
            {
                dcp.conf.clearAnalysis(fd); // Clear any old analysis
                dcp.conf.allacts.getCurrent().reset(*fd);
                start_time = clock();
                dcp.conf.allacts.getCurrent().perform(*fd);
                end_time = clock();
                *status.optr << "Decompiled " << fd.getName();
                //	  *status.optr << ": " << hex << fd.getAddress().getOffset();
                *status.optr << '(' << dec << fd.getSize() << ')';
                duration = ((float)(end_time - start_time)) / CLOCKS_PER_SEC;
                duration *= 1000.0;
                *status.optr << " time=" << fixed << setprecision(0) << duration << " ms" << endl;
                check(fd, *status.optr);
            }
            catch (LowlevelError err) {
                *status.optr << "Skipping " << fd.getName() << ": " << err.ToString() << endl;
            }
            dcp.conf.clearAnalysis(fd);
        }
    }
}
