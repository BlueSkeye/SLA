using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcJumpOverride : IfaceDecompCommand
    {
        /// \class IfcJumpOverride
        /// \brief Provide an overriding jump-table for an indirect branch: `override jumptable ...`
        ///
        /// The command expects the address of an indirect branch in the \e current function,
        /// followed by the keyword \b table then a list of possible target addresses of the branch.
        /// \code
        ///    override jumptable r0x1000 table r0x1020 r0x1030 r0x1043 ...
        /// \endcode
        /// The command can optionally take the keyword \b startval followed by an
        /// integer indicating the value taken by the \e normalized switch variable that
        /// produces the first address in the table.
        /// \code
        ///    override jumptable startval 10 table r0x1020 r0x1030 ...
        /// \endcode
        public override void execute(TextReader s)
        {
            int4 discard;

            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            s >> ws;
            Address jmpaddr(parse_machaddr(s, discard,* dcp.conf.types));
            JumpTable* jt = dcp.fd.installJumpTable(jmpaddr);
            vector<Address> adtable;
            Address naddr;
            uintb h = 0;
            uintb sv = 0;
            string token;
            s >> token;
            //   if (token == "norm") {
            //     naddr = parse_machaddr(s,discard,*dcp.conf.types);
            //     s >> ws;
            //     s >> h;
            //     s >> token;
            //   }
            if (token == "startval")
            {
                s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
                s >> sv;
                s >> token;
            }
            if (token == "table")
            {
                s >> ws;
                while (!s.eof())
                {
                    Address addr(parse_machaddr(s, discard,* dcp.conf.types));
                    adtable.push_back(addr);
                }
            }
            if (adtable.empty())
                throw IfaceExecutionError("Missing jumptable address entries");
            jt.setOverride(adtable, naddr, h, sv);
            *status.optr << "Successfully installed jumptable override" << endl;
        }
    }
}
