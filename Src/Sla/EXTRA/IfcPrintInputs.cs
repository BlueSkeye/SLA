using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintInputs : IfaceDecompCommand
    {
        /// \class IfcPrintInputs
        /// \brief Print info about the current function's input Varnodes: `print inputs`
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            print(dcp.fd, *status.fileoptr);
        }

        /// Check for non-trivial use of given Varnode
        /// The use is non-trivial if it can be traced to any p-code operation except
        /// a COPY, CAST, INDIRECT, or MULTIEQUAL.
        /// \param vn is the given Varnode
        /// \return \b true if there is a non-trivial use
        public static bool nonTrivialUse(Varnode vn)
        {
            List<Varnode*> vnlist;
            bool res = false;
            vnlist.push_back(vn);
            uint proc = 0;
            while (proc < vnlist.size())
            {
                Varnode* tmpvn = vnlist[proc];
                proc += 1;
                list<PcodeOp*>::const_iterator iter;
                for (iter = tmpvn.beginDescend(); iter != tmpvn.endDescend(); ++iter)
                {
                    PcodeOp* op = *iter;
                    if ((op.code() == CPUI_COPY) ||
                    (op.code() == CPUI_CAST) ||
                    (op.code() == CPUI_INDIRECT) ||
                    (op.code() == CPUI_MULTIEQUAL))
                    {
                        Varnode* outvn = op.getOut();
                        if (!outvn.isMark())
                        {
                            outvn.setMark();
                            vnlist.push_back(outvn);
                        }
                    }
                    else
                    {
                        res = true;
                        break;
                    }
                }
            }
            for (int i = 0; i < vnlist.size(); ++i)
                vnlist[i].clearMark();
            return res;
        }

        /// Check if a Varnode is \e restored to its original input value
        /// Look for any value flowing into the Varnode coming from anything
        /// other than an input Varnode with the same storage.  The value can flow through
        /// a COPY, CAST, INDIRECT, or MULTIEQUAL
        /// \param vn is the given Varnode
        /// \return 0 if Varnode is restored, 1 otherwise
        public static int checkRestore(Varnode vn)
        {
            List<Varnode*> vnlist;
            int res = 0;
            vnlist.push_back(vn);
            uint proc = 0;
            while (proc < vnlist.size())
            {
                Varnode* tmpvn = vnlist[proc];
                proc += 1;
                if (tmpvn.isInput())
                {
                    if ((tmpvn.getSize() != vn.getSize()) ||
                    (tmpvn.getAddr() != vn.getAddr()))
                    {
                        res = 1;
                        break;
                    }
                }
                else if (!tmpvn.isWritten())
                {
                    res = 1;
                    break;
                }
                else
                {
                    PcodeOp* op = tmpvn.getDef();
                    if ((op.code() == CPUI_COPY) || (op.code() == CPUI_CAST))
                    {
                        tmpvn = op.getIn(0);
                        if (!tmpvn.isMark())
                        {
                            tmpvn.setMark();
                            vnlist.push_back(tmpvn);
                        }
                    }
                    else if (op.code() == CPUI_INDIRECT)
                    {
                        tmpvn = op.getIn(0);
                        if (!tmpvn.isMark())
                        {
                            tmpvn.setMark();
                            vnlist.push_back(tmpvn);
                        }
                    }
                    else if (op.code() == CPUI_MULTIEQUAL)
                    {
                        for (int i = 0; i < op.numInput(); ++i)
                        {
                            tmpvn = op.getIn(i);
                            if (!tmpvn.isMark())
                            {
                                tmpvn.setMark();
                                vnlist.push_back(tmpvn);
                            }
                        }
                    }
                    else
                    {
                        res = 1;
                        break;
                    }
                }
            }
            for (int i = 0; i < vnlist.size(); ++i)
                vnlist[i].clearMark();
            return res;
        }

        /// Check if storage is \e restored
        /// For the given storage location, check that it is \e restored
        /// from its original input value.
        /// \param vn is the given storage location
        /// \param fd is the function being analyzed
        public static bool findRestore(Varnode vn, Funcdata fd)
        {
            VarnodeLocSet::const_iterator iter, enditer;

            iter = fd.beginLoc(vn.getAddr());
            enditer = fd.endLoc(vn.getAddr());
            int count = 0;
            while (iter != enditer)
            {
                Varnode* vn = *iter;
                ++iter;
                if (!vn.hasNoDescend()) continue;
                if (!vn.isWritten()) continue;
                PcodeOp* op = vn.getDef();
                if (op.code() == CPUI_INDIRECT) continue; // Not a global return address force
                int res = checkRestore(vn);
                if (res != 0) return false;
                count += 1;
            }
            return (count > 0);
        }

        /// Print information about function inputs
        /// For each input Varnode, print information about the Varnode,
        /// any explicit symbol it represents, and info about how the value is used.
        /// \param fd is the function
        /// \param s is the output stream to write to
        public static void print(Funcdata fd, TextWriter s)
        {
            VarnodeDefSet::const_iterator iter, enditer;

            s << "Function: " << fd.getName() << endl;
            iter = fd.beginDef(Varnode::input);
            enditer = fd.endDef(Varnode::input);
            while (iter != enditer)
            {
                Varnode* vn = *iter;
                ++iter;
                vn.printRaw(s);
                if (fd.isHighOn())
                {
                    Symbol* sym = vn.getHigh().getSymbol();
                    if (sym != (Symbol*)0)
                        s << "    " << sym.getName();
                }
                bool findres = findRestore(vn, fd);
                bool nontriv = nonTrivialUse(vn);
                if (findres && !nontriv)
                    s << "     restored";
                else if (nontriv)
                    s << "     nontriv";
                s << endl;
            }
        }
    }
}
