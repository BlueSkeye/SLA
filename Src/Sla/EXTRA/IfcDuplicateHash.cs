using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcDuplicateHash : IfaceDecompCommand
    {
        /// For each duplicate discovered, a message is written to the provided stream.
        /// \param fd is the given function to search
        /// \param s is the stream to write messages to
        public static void check(Funcdata fd, TextWriter s)
        {
            DynamicHash dhash = new DynamicHash();

            VarnodeLocSet.Enumerator iter = fd.beginLoc();
            while (iter.MoveNext()) {
                Varnode vn = iter.Current;
                if (vn.isAnnotation()) continue;
                if (vn.isConstant()) {
                    PcodeOp op = vn.loneDescend() ?? throw new BugException();
                    int slot = op.getSlot(vn);
                    if (slot == 0) {
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
                if (dhash.getHash() == 0) {
                    // We have a duplicate
                    PcodeOp op = (vn.beginDescend() != vn.endDescend()) ? vn.beginDescend() : vn.getDef();
                    s.Write("Could not get unique hash for : ");
                    vn.printRaw(s);
                    s.Write(" : ");
                    op.printRaw(s);
                    s.WriteLine();
                    return;
                }
                uint total = DynamicHash.getTotalFromHash(dhash.getHash());
                if (total != 1) {
                    PcodeOp op = (vn.beginDescend() != vn.endDescend()) ? vn.beginDescend() : vn.getDef();
                    s.Write("Duplicate : ");
                    s.Write($"{DynamicHash.getPositionFromHash(dhash.getHash())} out of {total} : ");
                    vn.printRaw(s);
                    s.Write(" : ");
                    op.printRaw(s);
                    s.WriteLine();
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
            DateTime start_time, end_time;

            if (fd.hasNoCode()) {
                status.optr.WriteLine("No code for {fd.getName()}");
                return;
            }
            try {
                dcp.conf.clearAnalysis(fd); // Clear any old analysis
                dcp.conf.allacts.getCurrent().reset(fd);
                start_time = DateTime.UtcNow;
                dcp.conf.allacts.getCurrent().perform(fd);
                end_time = DateTime.UtcNow;
                status.optr.Write($"Decompiled {fd.getName()}");
                //	  *status.optr << ": " << hex << fd.getAddress().getOffset();
                status.optr.Write($"({fd.getSize()})");
                TimeSpan duration = (end_time - start_time);
                status.optr.WriteLine($" time={(int)duration.TotalMilliseconds} ms");
                check(fd, status.optr);
            }
            catch (LowlevelError) {
                status.optr.WriteLine("Skipping {fd.getName()}: {err.ToString()}");
            }
            dcp.conf.clearAnalysis(fd);
        }
    }
}
