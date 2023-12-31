﻿using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class PcodeCompile
    {
        private AddrSpace defaultspace;
        private AddrSpace constantspace;
        private AddrSpace uniqspace;
        private uint local_labelcount; // Number of labels in current constructor
        private bool enforceLocalKey;       // Force slaspec to use 'local' keyword when defining temporary varnodes

        protected abstract uint allocateTemp();

        protected abstract void addSymbol(SleighSymbol sym);

        public PcodeCompile()
        {
            defaultspace = (AddrSpace)null; constantspace = (AddrSpace)null;
            uniqspace = (AddrSpace)null; local_labelcount = 0; enforceLocalKey = false;
        }
        
        ~PcodeCompile()
        {
        }
  
        public abstract Location? getLocation(SleighSymbol sym);

        public abstract void reportError(Location loc, string msg);

        public abstract void reportWarning(Location loc, string msg);

        public void resetLabelCount()
        {
            local_labelcount = 0;
        }

        public void setDefaultSpace(AddrSpace spc)
        {
            defaultspace = spc;
        }

        public void setConstantSpace(AddrSpace spc)
        {
            constantspace = spc;
        }

        public void setUniqueSpace(AddrSpace spc)
        {
            uniqspace = spc;
        }

        public void setEnforceLocalKey(bool val)
        {
            enforceLocalKey = val;
        }

        public AddrSpace getDefaultSpace() => defaultspace;

        public AddrSpace getConstantSpace() => constantspace;

        public VarnodeTpl buildTemporary()
        {
            // Build temporary variable (with zerosize)
            VarnodeTpl res = new VarnodeTpl(new ConstTpl(uniqspace),
                new ConstTpl(ConstTpl.const_type.real, allocateTemp()), new ConstTpl(ConstTpl.const_type.real, 0));
            res.setUnnamed(true);
            return res;
        }

        public LabelSymbol defineLabel(string name)
        {
            // Create a label symbol
            LabelSymbol labsym = new LabelSymbol(name, local_labelcount++);
            // delete name;
            addSymbol(labsym);      // Add symbol to local scope
            return labsym;
        }

        public List<OpTpl> placeLabel(LabelSymbol labsym)
        {
            // Create placeholder OpTpl for a label
            if (labsym.isPlaced()) {
                reportError(getLocation(labsym), $"Label '{labsym.getName()}' is placed more than once");
            }
            labsym.setPlaced();
            List<OpTpl> res = new List<OpTpl>();
            OpTpl op = new OpTpl(OpCode.LABELBUILD);
            VarnodeTpl idvn = new VarnodeTpl(new ConstTpl(constantspace),
                new ConstTpl(ConstTpl.const_type.real, labsym.getIndex()),
                new ConstTpl(ConstTpl.const_type.real, 4));
            op.addInput(idvn);
            res.Add(op);
            return res;
        }

        public List<OpTpl> newOutput(bool usesLocalKey, ExprTree rhs, string varname, uint size = 0)
        {
            VarnodeSymbol sym;
            VarnodeTpl tmpvn = buildTemporary();
            if (size != 0)
                tmpvn.setSize(new ConstTpl(ConstTpl.const_type.real, size)); // Size was explicitly specified
            else if ((rhs.getSize().getType() == ConstTpl.const_type.real) && (rhs.getSize().getReal() != 0))
                tmpvn.setSize(rhs.getSize()); // Inherit size from unnamed expression result
                                                // Only inherit if the size is real, otherwise we
                                                // cannot build the VarnodeSymbol with a placeholder constant
            rhs.setOutput(tmpvn);
            sym = new VarnodeSymbol(varname, tmpvn.getSpace().getSpace(), tmpvn.getOffset().getReal(),
                (int)tmpvn.getSize().getReal()); // Create new symbol regardless
            addSymbol(sym);
            if ((!usesLocalKey) && enforceLocalKey)
                reportError(getLocation(sym), $"Must use 'local' keyword to define symbol '{varname}'");
            // delete varname;
            return ExprTree.toVector(rhs);
        }

        public void newLocalDefinition(string varname, uint size = 0)
        { // Create a new temporary symbol (without generating any pcode)
            VarnodeSymbol sym;
            sym = new VarnodeSymbol(varname, uniqspace, allocateTemp(), (int)size);
            addSymbol(sym);
            // delete varname;
        }

        public ExprTree createOp(OpCode opc, ExprTree vn)
        {
            // Create new expression with output -outvn-
            // built by performing -opc- on input vn.
            // Free input expression
            VarnodeTpl outvn = buildTemporary();
            OpTpl op = new OpTpl(opc);
            op.addInput(vn.outvn);
            op.setOutput(outvn);
            vn.ops.Add(op);
            vn.outvn = new VarnodeTpl(outvn);
            return vn;
        }

        public ExprTree createOp(OpCode opc, ExprTree vn1, ExprTree vn2)
        {               // Create new expression with output -outvn-
                        // built by performing -opc- on inputs vn1 and vn2.
                        // Free input expressions
            VarnodeTpl outvn = buildTemporary();
            vn1.ops.AddRange(vn2);
            vn2.ops.Clear();
            OpTpl op = new OpTpl(opc);
            op.addInput(vn1.outvn);
            op.addInput(vn2.outvn);
            vn2.outvn = (VarnodeTpl)null;
            op.setOutput(outvn);
            vn1.ops.Add(op);
            vn1.outvn = new VarnodeTpl(outvn);
            // delete vn2;
            return vn1;
        }

        public ExprTree createOpOut(VarnodeTpl outvn, OpCode opc, ExprTree vn1, ExprTree vn2)
        { // Create an op with explicit output and two inputs
            vn1.ops.AddRange(vn2.ops);
            vn2.ops.Clear();
            OpTpl op = new OpTpl(opc);
            op.addInput(vn1.outvn);
            op.addInput(vn2.outvn);
            vn2.outvn = (VarnodeTpl)null;
            op.setOutput(outvn);
            vn1.ops.Add(op);
            vn1.outvn = new VarnodeTpl(outvn);
            // delete vn2;
            return vn1;
        }

        public ExprTree createOpOutUnary(VarnodeTpl outvn, OpCode opc, ExprTree vn)
        { // Create an op with explicit output and 1 input
            OpTpl op = new OpTpl(opc);
            op.addInput(vn.outvn);
            op.setOutput(outvn);
            vn.ops.Add(op);
            vn.outvn = new VarnodeTpl(outvn);
            return vn;
        }

        public List<OpTpl> createOpNoOut(OpCode opc, ExprTree vn)
        {
            // Create new expression by creating op with given -opc-
            // and single input vn.   Free the input expression
            OpTpl op = new OpTpl(opc);
            op.addInput(vn.outvn);
            vn.outvn = (VarnodeTpl)null; // There is no longer an output to this expression
            List<OpTpl> res = vn.ops;
            vn.ops = (List<OpTpl>)null;
            // delete vn;
            res.Add(op);
            return res;
        }

        public List<OpTpl> createOpNoOut(OpCode opc, ExprTree vn1, ExprTree vn2)
        {
            // Create new expression by creating op with given -opc-
            // and inputs vn1 and vn2. Free the input expressions
            List<OpTpl> res = vn1.ops;
            vn1.ops = (List<OpTpl>)null;
            res.AddRange(vn2.ops);
            vn2.ops.Clear();
            OpTpl op = new OpTpl(opc);
            op.addInput(vn1.outvn);
            vn1.outvn = (VarnodeTpl)null;
            op.addInput(vn2.outvn);
            vn2.outvn = (VarnodeTpl)null;
            res.Add(op);
            //delete vn1;
            //delete vn2;
            return res;
        }

        public List<OpTpl> createOpConst(OpCode opc, ulong val)
        {
            VarnodeTpl vn = new VarnodeTpl(new ConstTpl(constantspace),
                new ConstTpl(ConstTpl.const_type.real, val), new ConstTpl(ConstTpl.const_type.real, 4));
            List<OpTpl> res = new List<OpTpl>();
            OpTpl op = new OpTpl(opc);
            op.addInput(vn);
            res.Add(op);
            return res;
        }

        public ExprTree createLoad(StarQuality qual, ExprTree ptr)
        {               // Create new load expression, free ptr expression
            VarnodeTpl outvn = buildTemporary();
            OpTpl op = new OpTpl(OpCode.CPUI_LOAD);
            // The first varnode input to the load is a constant reference to the AddrSpace being loaded
            // from.  Internally, we really store the pointer to the AddrSpace as the reference, but this
            // isn't platform independent. So officially, we assume that the constant reference will be the
            // AddrSpace index.  We can safely assume this always has size 4.
            VarnodeTpl spcvn = new VarnodeTpl(new ConstTpl(constantspace), qual.id,
                new ConstTpl(ConstTpl.const_type.real, 8));
            op.addInput(spcvn);
            op.addInput(ptr.outvn);
            op.setOutput(outvn);
            ptr.ops.Add(op);
            if (qual.size > 0)
                force_size(outvn, new ConstTpl(ConstTpl.const_type.real, qual.size), ptr.ops);
            ptr.outvn = new VarnodeTpl(outvn);
            // delete qual;
            return ptr;
        }

        public List<OpTpl> createStore(StarQuality qual, ExprTree ptr, ExprTree val)
        {
            List<OpTpl> res = ptr.ops;
            ptr.ops = (List<OpTpl>)null;
            res.AddRange(val.ops);
            val.ops.Clear();
            OpTpl op = new OpTpl(OpCode.CPUI_STORE);
            // The first varnode input to the store is a constant reference to the AddrSpace being loaded
            // from.  Internally, we really store the pointer to the AddrSpace as the reference, but this
            // isn't platform independent. So officially, we assume that the constant reference will be the
            // AddrSpace index.  We can safely assume this always has size 4.
            VarnodeTpl spcvn = new VarnodeTpl(new ConstTpl(constantspace), qual.id,
                new ConstTpl(ConstTpl.const_type.real, 8));
            op.addInput(spcvn);
            op.addInput(ptr.outvn);
            op.addInput(val.outvn);
            res.Add(op);
            force_size(val.outvn, new ConstTpl(ConstTpl.const_type.real, qual.size), res);
            ptr.outvn = (VarnodeTpl)null;
            val.outvn = (VarnodeTpl)null;
            //delete ptr;
            //delete val;
            //delete qual;
            return res;
        }

        public ExprTree createUserOp(UserOpSymbol sym, List<ExprTree> param)
        {
            // Create userdefined pcode op, given symbol and parameters
            VarnodeTpl outvn = buildTemporary();
            ExprTree res = new ExprTree();
            res.ops = createUserOpNoOut(sym, param);
            res.ops.GetLastItem().setOutput(outvn);
            res.outvn = new VarnodeTpl(outvn);
            return res;
        }

        public List<OpTpl>? createUserOpNoOut(UserOpSymbol sym, List<ExprTree> param)
        {
            OpTpl op = new OpTpl(OpCode.CPUI_CALLOTHER);
            VarnodeTpl vn = new VarnodeTpl(new ConstTpl(constantspace),
                new ConstTpl(ConstTpl.const_type.real, sym.getIndex()),
                new ConstTpl(ConstTpl.const_type.real, 4));
            op.addInput(vn);
            return ExprTree.appendParams(op, param);
        }

        public ExprTree createVariadic(OpCode opc, List<ExprTree> param)
        {
            VarnodeTpl outvn = buildTemporary();
            ExprTree res = new ExprTree();
            OpTpl op = new OpTpl(opc);
            res.ops = ExprTree.appendParams(op, param);
            res.ops.GetLastItem().setOutput(outvn);
            res.outvn = new VarnodeTpl(outvn);
            return res;
        }

        public void appendOp(OpCode opc, ExprTree res, ulong constval, int constsz)
        {
            // Take output of res expression, combine with constant,
            // using opc operation, return the resulting expression
            OpTpl op = new OpTpl(opc);
            VarnodeTpl constvn = new VarnodeTpl(new ConstTpl(constantspace),
                new ConstTpl(ConstTpl.const_type.real, constval),
                new ConstTpl(ConstTpl.const_type.real, (ulong)constsz));
            VarnodeTpl outvn = buildTemporary();
            op.addInput(res.outvn);
            op.addInput(constvn);
            op.setOutput(outvn);
            res.ops.Add(op);
            res.outvn = new VarnodeTpl(outvn);
        }

        public VarnodeTpl? buildTruncatedVarnode(VarnodeTpl basevn, uint bitoffset, uint numbits)
        {
            // Build a truncated form -basevn- that matches the bitrange [ -bitoffset-, -numbits- ] if possible
            // using just ConstTpl mechanics, otherwise return null
            uint byteoffset = bitoffset / 8; // Convert to byte units
            uint numbytes = numbits / 8;
            ulong fullsz = 0;
            if (basevn.getSize().getType() == ConstTpl.const_type.real) {
                // If we know the size of base, make sure the bit range is in bounds
                fullsz = basevn.getSize().getReal();
                if (fullsz == 0) return (VarnodeTpl)null;
                if (byteoffset + numbytes > fullsz)
                    throw new SleighError("Requested bit range out of bounds");
            }

            if ((bitoffset % 8) != 0) return (VarnodeTpl)null;
            if ((numbits % 8) != 0) return (VarnodeTpl)null;

            if (basevn.getSpace().isUniqueSpace()) // Do we really want to prevent truncated uniques??
                return (VarnodeTpl)null;

            ConstTpl.const_type offset_type = basevn.getOffset().getType();
            if ((offset_type != ConstTpl.const_type.real) && (offset_type != ConstTpl.const_type.handle))
                return (VarnodeTpl)null;

            ConstTpl specialoff;
            if (offset_type == ConstTpl.const_type.handle) {
                // We put in the correct adjustment to offset assuming things are little endian
                // We defer the correct big endian calculation until after the consistency check
                // because we need to know the subtable export sizes
                specialoff = new ConstTpl(ConstTpl.const_type.handle, basevn.getOffset().getHandleIndex(),
                    ConstTpl.v_field.v_offset_plus, byteoffset);
            }
            else {
                if (basevn.getSize().getType() != ConstTpl.const_type.real)
                    throw new SleighError("Could not construct requested bit range");
                ulong plus;
                if (defaultspace.isBigEndian())
                    plus = fullsz - (byteoffset + numbytes);
                else
                    plus = byteoffset;
                specialoff = new ConstTpl(ConstTpl.const_type.real, basevn.getOffset().getReal() + plus);
            }
            VarnodeTpl res = new VarnodeTpl(basevn.getSpace(), specialoff,
                new ConstTpl(ConstTpl.const_type.real, numbytes));
            return res;
        }

        public List<OpTpl> assignBitRange(VarnodeTpl vn, uint bitoffset, uint numbits, ExprTree rhs)
        {
            // Create an expression assigning the rhs to a bitrange within sym
            string errmsg = string.Empty;
            if (numbits == 0)
                errmsg = "Size of bitrange is zero";
            uint smallsize = (numbits + 7) / 8; // Size of input (output of rhs)
            bool shiftneeded = (bitoffset != 0);
            bool zextneeded = true;
            ulong mask = (ulong)2;
            mask = ~(((mask << (numbits - 1)) - 1) << bitoffset);

            if (vn.getSize().getType() == ConstTpl.const_type.real) {
                // If we know the size of the bitranged varnode, we can
                // do some immediate checks, and possibly simplify things
                uint symsize = (uint)vn.getSize().getReal();
                if (symsize > 0)
                    zextneeded = (symsize > smallsize);
                symsize *= 8;       // Convert to number of bits
                if ((bitoffset >= symsize) || (bitoffset + numbits > symsize))
                    errmsg = "Assigned bitrange is bad";
                else if ((bitoffset == 0) && (numbits == symsize))
                    errmsg = "Assigning to bitrange is superfluous";
            }

            if (errmsg.Length > 0) {
                // Was there an error condition
                reportError((Location)null, errmsg);    // Report the error
                // delete vn;          // Clean up
                List<OpTpl> resops = rhs.ops; // Passthru old expression
                rhs.ops = (List<OpTpl>)null;
                // delete rhs;
                return resops;
            }

            // We know what the size of the input has to be
            force_size(rhs.outvn, new ConstTpl(ConstTpl.const_type.real, smallsize), rhs.ops);

            ExprTree res;
            VarnodeTpl? finalout = buildTruncatedVarnode(vn, bitoffset, numbits);
            if (finalout != (VarnodeTpl)null) {
                // delete vn;  // Don't keep the original Varnode object
                res = createOpOutUnary(finalout, OpCode.CPUI_COPY, rhs);
            }
            else {
                if (bitoffset + numbits > 64)
                    errmsg = "Assigned bitrange extends past first 64 bits";
                res = new ExprTree(vn);
                appendOp(OpCode.CPUI_INT_AND, res, mask, 0);
                if (zextneeded)
                    createOp(OpCode.CPUI_INT_ZEXT, rhs);
                if (shiftneeded)
                    appendOp(OpCode.CPUI_INT_LEFT, rhs, bitoffset, 4);

                finalout = new VarnodeTpl(vn);
                res = createOpOut(finalout, OpCode.CPUI_INT_OR, res, rhs);
            }
            if (errmsg.Length > 0)
                reportError((Location)null, errmsg);
            List<OpTpl> resops = res.ops;
            res.ops = (List<OpTpl>)null;
            // delete res;
            return resops;
        }

        public ExprTree createBitRange(SpecificSymbol sym, uint bitoffset, uint numbits)
        {
            // Create an expression computing the indicated bitrange of sym
            // The result is truncated to the smallest byte size that can
            // contain the indicated number of bits. The result has the
            // desired bits shifted all the way to the right
            string errmsg = string.Empty;
            if (numbits == 0)
                errmsg = "Size of bitrange is zero";
            VarnodeTpl vn = sym.getVarnode();
            uint finalsize = (numbits + 7) / 8; // Round up to neareast byte size
            uint truncshift = 0;
            bool maskneeded = ((numbits % 8) != 0);
            bool truncneeded = true;
            ExprTree res;

            // Special case where we can set the size, without invoking
            // a truncation operator
            if ((errmsg.Length == 0) && (bitoffset == 0) && (!maskneeded)) {
                if ((vn.getSpace().getType() == ConstTpl.const_type.handle) && vn.isZeroSize()) {
                    vn.setSize(new ConstTpl(ConstTpl.const_type.real, finalsize));
                    res = new ExprTree(vn);
                    //      VarnodeTpl *cruft = buildTemporary();
                    //      delete cruft;
                    return res;
                }
            }

            if (errmsg.Length == 0) {
                VarnodeTpl? truncvn = buildTruncatedVarnode(vn, bitoffset, numbits);
                if (truncvn != (VarnodeTpl)null) {
                    // If we are able to construct a simple truncated varnode
                    res = new ExprTree(truncvn); // Return just the varnode as an expression
                    // delete vn;
                    return res;
                }
            }

            if (vn.getSize().getType() == ConstTpl.const_type.real) {
                // If we know the size of the input varnode, we can
                // do some immediate checks, and possibly simplify things
                uint insize = (uint)vn.getSize().getReal();
                if (insize > 0) {
                    truncneeded = (finalsize < insize);
                    insize *= 8;        // Convert to number of bits
                    if ((bitoffset >= insize) || (bitoffset + numbits > insize))
                        errmsg = "Bitrange is bad";
                    if (maskneeded && ((bitoffset + numbits) == insize))
                        maskneeded = false;
                }
            }

            ulong mask = (ulong)2;
            mask = ((mask << (int)(numbits - 1)) - 1);

            if (truncneeded && ((bitoffset % 8) == 0)) {
                truncshift = bitoffset / 8;
                bitoffset = 0;
            }

            if ((bitoffset == 0) && (!truncneeded) && (!maskneeded))
                errmsg = "Superfluous bitrange";
            if (maskneeded && (finalsize > 8))
                errmsg = "Illegal masked bitrange producing varnode larger than 64 bits: " + sym.getName();

            res = new ExprTree(vn);
            if (errmsg.Length > 0) {
                // Check for error condition
                reportError(getLocation(sym), errmsg);
                return res;
            }

            if (bitoffset != 0)
                appendOp(OpCode.CPUI_INT_RIGHT, res, bitoffset, 4);
            if (truncneeded)
                appendOp(OpCode.CPUI_SUBPIECE, res, truncshift, 4);
            if (maskneeded)
                appendOp(OpCode.CPUI_INT_AND, res, mask, (int)finalsize);
            force_size(res.outvn, new ConstTpl(ConstTpl.const_type.real, finalsize), res.ops);
            return res;
        }

        public VarnodeTpl addressOf(VarnodeTpl var, uint size)
        {
            // Produce constant varnode that is the offset
            // portion of varnode -var-
            if (size == 0) {
                // If no size specified
                if (var.getSpace().getType() == ConstTpl.const_type.spaceid) {
                    AddrSpace spc = var.getSpace().getSpace();    // Look to the particular space
                    size = spc.getAddrSize(); // to see if it has a standard address size
                }
            }
            VarnodeTpl res;
            if ((var.getOffset().getType() == ConstTpl.const_type.real) && (var.getSpace().getType() == ConstTpl.const_type.spaceid))
            {
                AddrSpace spc = var.getSpace().getSpace();
                ulong off = AddrSpace.byteToAddress(var.getOffset().getReal(), spc.getWordSize());
                res = new VarnodeTpl(new ConstTpl(constantspace), new ConstTpl(ConstTpl.const_type.real, off),
                    new ConstTpl(ConstTpl.const_type.real, size));
            }
            else
                res = new VarnodeTpl(new ConstTpl(constantspace), var.getOffset(),
                    new ConstTpl(ConstTpl.const_type.real, size));
            // delete var;
            return res;
        }

        public static void force_size(VarnodeTpl vt, ConstTpl size, List<OpTpl> ops)
        {
            if ((vt.getSize().getType() != ConstTpl.const_type.real) || (vt.getSize().getReal() != 0))
                return;         // Size already exists

            vt.setSize(size);
            if (!vt.isLocalTemp()) return;
            // If the variable is a local temporary
            // The size may need to be propagated to the various
            // uses of the variable
            OpTpl op;
            VarnodeTpl? vn;

            for (int i = 0; i < ops.size(); ++i) {
                op = ops[i];
                vn = op.getOut();
                if ((vn != (VarnodeTpl)null) && (vn.isLocalTemp())) {
                    if (vn.getOffset() == vt.getOffset()) {
                        if ((size.getType() == ConstTpl.const_type.real) && (vn.getSize().getType() == ConstTpl.const_type.real) &&
                            (vn.getSize().getReal() != 0) && (vn.getSize().getReal() != size.getReal()))
                            throw new SleighError("Localtemp size mismatch");
                        vn.setSize(size);
                    }
                }
                for (int j = 0; j < op.numInput(); ++j) {
                    vn = op.getIn(j);
                    if (vn.isLocalTemp() && (vn.getOffset() == vt.getOffset())) {
                        if ((size.getType() == ConstTpl.const_type.real) && (vn.getSize().getType() == ConstTpl.const_type.real) &&
                            (vn.getSize().getReal() != 0) && (vn.getSize().getReal() != size.getReal()))
                            throw new SleighError("Localtemp size mismatch");
                        vn.setSize(size);
                    }
                }
            }
        }

        public static void matchSize(int j, OpTpl op, bool inputonly, List<OpTpl> ops)
        {
            // Find something to fill in zero size varnode
            // j is the slot we are trying to fill (-1=output)
            // Don't check output for non-zero if inputonly is true
            VarnodeTpl? match = (VarnodeTpl)null;
            VarnodeTpl vt;
            int i, inputsize;

            vt = (j == -1) ? op.getOut() : op.getIn(j);
            if (!inputonly) {
                if (op.getOut() != (VarnodeTpl)null)
                    if (!op.getOut().isZeroSize())
                        match = op.getOut();
            }
            inputsize = op.numInput();
            for (i = 0; i < inputsize; ++i) {
                if (match != (VarnodeTpl)null) break;
                if (op.getIn(i).isZeroSize()) continue;
                match = op.getIn(i);
            }
            if (match != (VarnodeTpl)null)
                force_size(vt, match.getSize(), ops);
        }

        public static void fillinZero(OpTpl op, List<OpTpl> ops)
        {
            // Try to get rid of zero size varnodes in op
            // Right now this is written assuming operands for the constructor are
            // are built before any other pcode in the constructor is generated
            int inputsize, i;

            switch (op.getOpcode()) {
                case OpCode.CPUI_COPY:         // Instructions where all inputs and output are same size
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_SUB:
                case OpCode.CPUI_INT_2COMP:
                case OpCode.CPUI_INT_NEGATE:
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                case OpCode.CPUI_INT_MULT:
                case OpCode.CPUI_INT_DIV:
                case OpCode.CPUI_INT_SDIV:
                case OpCode.CPUI_INT_REM:
                case OpCode.CPUI_INT_SREM:
                case OpCode.CPUI_FLOAT_ADD:
                case OpCode.CPUI_FLOAT_DIV:
                case OpCode.CPUI_FLOAT_MULT:
                case OpCode.CPUI_FLOAT_SUB:
                case OpCode.CPUI_FLOAT_NEG:
                case OpCode.CPUI_FLOAT_ABS:
                case OpCode.CPUI_FLOAT_SQRT:
                case OpCode.CPUI_FLOAT_CEIL:
                case OpCode.CPUI_FLOAT_FLOOR:
                case OpCode.CPUI_FLOAT_ROUND:
                    if ((op.getOut() != (VarnodeTpl)null) && (op.getOut().isZeroSize()))
                        matchSize(-1, op, false, ops);
                    inputsize = op.numInput();
                    for (i = 0; i < inputsize; ++i)
                        if (op.getIn(i).isZeroSize())
                            matchSize(i, op, false, ops);
                    break;
                case OpCode.CPUI_INT_EQUAL:        // Instructions with bool output
                case OpCode.CPUI_INT_NOTEQUAL:
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_INT_CARRY:
                case OpCode.CPUI_INT_SCARRY:
                case OpCode.CPUI_INT_SBORROW:
                case OpCode.CPUI_FLOAT_EQUAL:
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                case OpCode.CPUI_FLOAT_LESS:
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                case OpCode.CPUI_FLOAT_NAN:
                case OpCode.CPUI_BOOL_NEGATE:
                case OpCode.CPUI_BOOL_XOR:
                case OpCode.CPUI_BOOL_AND:
                case OpCode.CPUI_BOOL_OR:
                    if (op.getOut().isZeroSize())
                        force_size(op.getOut(), new ConstTpl(ConstTpl.const_type.real, 1), ops);
                    inputsize = op.numInput();
                    for (i = 0; i < inputsize; ++i)
                        if (op.getIn(i).isZeroSize())
                            matchSize(i, op, true, ops);
                    break;
                // The shift amount does not necessarily have to be the same size
                // But if no size is specified, assume it is the same size
                case OpCode.CPUI_INT_LEFT:
                case OpCode.CPUI_INT_RIGHT:
                case OpCode.CPUI_INT_SRIGHT:
                    if (op.getOut().isZeroSize()) {
                        if (!op.getIn(0).isZeroSize())
                            force_size(op.getOut(), op.getIn(0).getSize(), ops);
                    }
                    else if (op.getIn(0).isZeroSize())
                        force_size(op.getIn(0), op.getOut().getSize(), ops);
                    // fallthru to subpiece constant check
                    goto case OpCode.CPUI_SUBPIECE;
                case OpCode.CPUI_SUBPIECE:
                    if (op.getIn(1).isZeroSize())
                        force_size(op.getIn(1), new ConstTpl(ConstTpl.const_type.real, 4), ops);
                    break;
                case OpCode.CPUI_CPOOLREF:
                    if (op.getOut().isZeroSize() && (!op.getIn(0).isZeroSize()))
                        force_size(op.getOut(), op.getIn(0).getSize(), ops);
                    if (op.getIn(0).isZeroSize() && (!op.getOut().isZeroSize()))
                        force_size(op.getIn(0), op.getOut().getSize(), ops);
                    for (i = 1; i < op.numInput(); ++i) {
                        if (op.getIn(i).isZeroSize())
                            force_size(op.getIn(i), new ConstTpl(ConstTpl.const_type.real, sizeof(ulong)), ops);
                    }
                    break;
                default:
                    break;
            }
        }

        public static bool propagateSize(ConstructTpl ct)
        {
            // Fill in size for varnodes with size 0
            // Return first OpTpl with a size 0 varnode
            // that cannot be filled in or NULL otherwise
            List<OpTpl> zerovec = new List<OpTpl>();
            List<OpTpl> zerovec2 = new List<OpTpl>();
            int lastsize;

            foreach (OpTpl op in ct.getOpvec())
                if (op.isZeroSize()) {
                    fillinZero(op, ct.getOpvec());
                    if (op.isZeroSize())
                        zerovec.Add(op);
                }
            lastsize = zerovec.size() + 1;
            while (zerovec.size() < lastsize) {
                lastsize = zerovec.size();
                zerovec2.Clear();
                foreach (OpTpl op in zerovec) {
                    fillinZero(op, ct.getOpvec());
                    if (op.isZeroSize())
                        zerovec2.Add(op);
                }
                zerovec = zerovec2;
            }
            if (lastsize != 0) return false;
            return true;
        }
    }
}
