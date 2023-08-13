﻿using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class DisassemblyEngine : PcodeEmit
    {
        private Translate trans;
        private List<Address> jumpaddr;
        private set<ulong> targetoffsets;
        private OpCode lastop;
        private bool hascall;
        private bool hitsaddress;
        private ulong targethit;
        
        public void init(Translate t)
        {
            trans = t;
            jumpaddr.Clear();
            targetoffsets.clear();
        }

        public override void dump(Address addr,OpCode opc, VarnodeData? outvar,VarnodeData[] vars,
            int isize)
        {
            lastop = opc;
            switch (opc) {
                case OpCode.CPUI_CALL:
                    hascall = true;
                    // fallthru
                    goto case OpCode.CPUI_BRANCH;
                case OpCode.CPUI_BRANCH:
                case OpCode.CPUI_CBRANCH:
                    jumpaddr.Add(new Address(vars[0].space, vars[0].offset));
                    break;
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_BRANCHIND:
                case OpCode.CPUI_CALLIND:
                    if (targetoffsets.end() != targetoffsets.find(vars[0].offset)) {
                        hitsaddress = true;
                        targethit = vars[0].offset;
                    }
                    break;
                case OpCode.CPUI_LOAD:
                    if (targetoffsets.end() != targetoffsets.find(vars[1].offset)) {
                        hitsaddress = true;
                        targethit = vars[1].offset;
                    }
                    break;
                default:
                    break;
            }
        }

        public void disassemble(Address addr, DisassemblyResult res)
        {
            jumpaddr.Clear();
            lastop = OpCode.CPUI_COPY;
            hascall = false;
            hitsaddress = false;
            res.flags = 0;
            try {
                res.length = trans.oneInstruction(this, addr);
            }
            catch (BadDataError) {
                res.success = false;
                return;
            }
            catch (DataUnavailError) {
                res.success = false;
                return;
            }
            catch (UnimplError err) {
                res.length = err.instruction_length;
            }
            res.success = true;
            if (hascall)
                res.flags |= CodeUnit.Flags.call;
            if (hitsaddress) {
                res.flags |= CodeUnit.Flags.targethit;
                res.targethit = targethit;
            }
            Address lastaddr = addr + res.length;
            switch (lastop) {
                case OpCode.CPUI_BRANCH:
                case OpCode.CPUI_BRANCHIND:
                    if (hitsaddress)
                        res.flags |= CodeUnit.Flags.thunkhit; // Hits target via indirect jump
                    break;
                case OpCode.CPUI_RETURN:
                    break;
                default:
                    res.flags |= CodeUnit.Flags.fallthru;
                    break;
            }
            for (int i = 0; i < jumpaddr.size(); ++i) {
                if (jumpaddr[i] == lastaddr)
                    res.flags |= CodeUnit.Flags.fallthru;
                else if (jumpaddr[i] != addr) {
                    res.flags |= CodeUnit.Flags.jump;
                    res.jumpaddress = jumpaddr[i];
                }
            }
        }

        public void addTarget(Address addr)
        {
            targetoffsets.insert(addr.getOffset() );
        }
    }
}
