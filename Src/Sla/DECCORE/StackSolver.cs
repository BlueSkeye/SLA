using Sla.CORE;
using Sla.DECCORE;

namespace Sla.DECCORE
{
    /// \brief A class that solves for stack-pointer changes across unknown sub-functions
    internal class StackSolver
    {
        /// Known equations based on operations that explicitly change the stack-pointer
        private List<StackEqn> eqs = new List<StackEqn>();
        /// Guessed equations for underdetermined systems
        private List<StackEqn> guess = new List<StackEqn>();
        /// The indexed set of variables, one for each reference to the stack-pointer
        private List<Varnode> vnlist = new List<Varnode>();
        /// Index of companion input for variable produced by OpCode.CPUI_INDIRECT
        private List<int> companion = new List<int>();
        /// Starting address of the stack-pointer
        private Address spacebase;
        /// Collected solutions (corresponding to array of variables)
        private List<int> soln = new List<int>();
        /// Number of variables for which we are missing an equation
        private int missedvariables;

        /// Duplicate each equation, multiplying by -1
        private void duplicate()
        {
            int i;
            StackEqn eqn = new StackEqn();

            int size = eqs.Count;
            for (i = 0; i < size; ++i) {
                eqn.var1 = eqs[i].var2;
                eqn.var2 = eqs[i].var1;
                eqn.rhs = -eqs[i].rhs;
                eqs.Add(eqn);
            }
            stable_sort(eqs.begin(), eqs.end(), StackEqn.compare);
        }

        /// Propagate solution for one variable to other variables
        /// Given a solution for one variable, look for equations containing the variable
        /// and attempt to solve for the other variable. Continue propagating new
        /// solutions to other equations to find even more solutions.  Populate
        /// the \b soln array with the solutions.
        /// \param varnum is the index of the initial variable
        /// \param val is the solution for the variable
        private void propagate(int varnum, int val)
        {
            if (soln[varnum] != 65535) {
                // This variable already specified
                return;
            }
            soln[varnum] = val;

            StackEqn eqn = new StackEqn();
            List<int> workstack = new List<int>();
            workstack.reserve(soln.Count);
            workstack.Add(varnum);
            IEnumerator<StackEqn> top;

            while (0 != workstack.Count) {
                varnum = workstack[workstack.Count - 1];
                workstack.RemoveAt(workstack.Count - 1);

                eqn.var1 = varnum;
                top = lower_bound(eqs.begin(), eqs.end(), eqn, StackEqn::compare);
                while ((top != eqs.end()) && ((*top).var1 == varnum)) {
                    int var2 = (*top).var2;
                    if (soln[var2] == 65535) {
                        soln[var2] = soln[varnum] - (*top).rhs;
                        workstack.Add(var2);
                    }
                    ++top;
                }
            }
        }

        /// Solve the system of equations
        public void solve()
        {
            // Use guesses to resolve subsystems not uniquely determined
            int i, size, var1, var2, count, lastcount;

            soln.Clear();
            soln.resize(vnlist.Count, 65535); // Initialize solutions List
            // Duplicate and sort the equations
            duplicate();

            // We know one variable
            propagate(0, 0);
            size = guess.Count;
            lastcount = size + 2;
            do {
                count = 0;
                for (i = 0; i < size; ++i) {
                    var1 = guess[i].var1;
                    var2 = guess[i].var2;
                    if ((soln[var1] != 65535) && (soln[var2] == 65535)) {
                        propagate(var2, soln[var1] - guess[i].rhs);
                    }
                    else if ((soln[var1] == 65535) && (soln[var2] != 65535)) {
                        propagate(var1, soln[var2] + guess[i].rhs);
                    }
                    else if ((soln[var1] == 65535) && (soln[var2] == 65535)) {
                        count += 1;
                    }
                }
                if (count == lastcount) {
                    break;
                }
                lastcount = count;
            } while (count > 0);
        }

        /// Build the system of equations
        /// Collect references to the stack-pointer as variables, and examine their defining PcodeOps
        /// to determine equations and coefficient.
        /// \param data is the function being analyzed
        /// \param id is the \e stack address space
        /// \param spcbase is the index, relative to the stack space, of the stack pointer
        public void build(Funcdata data,AddrSpace id, int spcbase)
        {
            VarnodeData spacebasedata = id.getSpacebase(spcbase);
            spacebase = new Address(spacebasedata.space, spacebasedata.offset);

            VarnodeLocSet.Enumerator begiter = data.beginLoc((int)spacebasedata.size, spacebase);
            // VarnodeLocSet.Enumerator enditer = data.endLoc(spacebasedata.size, spacebase);

            while (begiter.MoveNext()) {
                // All instances of the spacebase
                if (begiter.Current.isFree()) {
                    break;
                }
                vnlist.Add(begiter.Current);
                companion.Add(-1);
            }
            missedvariables = 0;
            if (0 == vnlist.Count) {
                return;
            }
            if (!vnlist[0].isInput()) {
                throw new LowlevelError("Input value of stackpointer is not used");
            }

            IEnumerator<Varnode> iter;
            StackEqn eqn;
            for (int i = 1; i < vnlist.Count; ++i) {
                Varnode vn = vnlist[i];
                Varnode othervn;
                Varnode constvn;
                PcodeOp op = vn.getDef() ?? throw new ApplicationException();

                if (op.code() == OpCode.CPUI_INT_ADD) {
                    othervn = op.getIn(0) ?? throw new ApplicationException();
                    constvn = op.getIn(1) ?? throw new ApplicationException();
                    if (othervn.isConstant()) {
                        constvn = othervn;
                        othervn = op.getIn(1) ?? throw new ApplicationException();
                    }
                    if (!constvn.isConstant()) {
                        missedvariables += 1;
                        continue;
                    }
                    if (othervn.getAddr() != spacebase) {
                        missedvariables += 1;
                        continue;
                    }
                    iter = lower_bound(vnlist.begin(), vnlist.end(), othervn, Varnode::comparePointers);
                    eqn.var1 = i;
                    eqn.var2 = iter - vnlist.begin();
                    eqn.rhs = (int)constvn.getOffset();
                    eqs.Add(eqn);
                }
                else if (op.code() == OpCode.CPUI_COPY) {
                    othervn = op.getIn(0);
                    if (othervn.getAddr() != spacebase) { missedvariables += 1; continue; }
                    iter = lower_bound(vnlist.begin(), vnlist.end(), othervn, Varnode::comparePointers);
                    eqn.var1 = i;
                    eqn.var2 = iter - vnlist.begin();
                    eqn.rhs = 0;
                    eqs.Add(eqn);
                }
                else if (op.code() == OpCode.CPUI_INDIRECT) {
                    othervn = op.getIn(0) ?? throw new ApplicationException();
                    if (othervn.getAddr() != spacebase) {
                        missedvariables += 1;
                        continue;
                    }
                    iter = lower_bound(vnlist.begin(), vnlist.end(), othervn, Varnode::comparePointers);
                    eqn.var1 = i;
                    eqn.var2 = iter - vnlist.begin();
                    companion[i] = eqn.var2;
                    Varnode iopvn = op.getIn(1) ?? throw new ApplicationException();
                    if (iopvn.getSpace().getType() == spacetype.IPTR_IOP) {
                        // If INDIRECT is due call
                        PcodeOp iop = PcodeOp.getOpFromConst(iopvn.getAddr());
                        // Look up function proto
                        FuncCallSpecs fc = data.getCallSpecs(iop) ?? throw new ApplicationException();
                        if (fc != null) {
                            if (fc.getExtraPop() != ProtoModel.extrapop_unknown) {
                                // Double check that extrapop is unknown
                                eqn.rhs = fc.getExtraPop(); // As the deindirect process may have filled it in
                                eqs.Add(eqn);
                                continue;
                            }
                        }
                    }
                    // Otherwise make a guess
                    eqn.rhs = 4;
                    guess.Add(eqn);
                }
                else if (op.code() == OpCode.CPUI_MULTIEQUAL) {
                    for (int j = 0; j < op.numInput(); ++j) {
                        othervn = op.getIn(j) ?? throw new ApplicationException();
                        if (othervn.getAddr() != spacebase) {
                            missedvariables += 1;
                            continue;
                        }
                        iter = lower_bound(vnlist.begin(), vnlist.end(), othervn, Varnode::comparePointers);
                        eqn.var1 = i;
                        eqn.var2 = iter - vnlist.begin();
                        eqn.rhs = 0;
                        eqs.Add(eqn);
                    }
                }
                else if (op.code() == OpCode.CPUI_INT_AND) {
                    // This can occur if a function aligns its stack pointer
                    othervn = op.getIn(0) ?? throw new ApplicationException();
                    constvn = op.getIn(1) ?? throw new ApplicationException();
                    if (othervn.isConstant()) {
                        constvn = othervn;
                        othervn = op.getIn(1) ?? throw new ApplicationException();
                    }
                    if (!constvn.isConstant()) {
                        missedvariables += 1;
                        continue;
                    }
                    if (othervn.getAddr() != spacebase) { missedvariables += 1; continue; }
                    iter = lower_bound(vnlist.begin(), vnlist.end(), othervn, Varnode::comparePointers);
                    eqn.var1 = i;
                    eqn.var2 = iter - vnlist.begin();
                    // Treat this as a copy
                    eqn.rhs = 0;
                    eqs.Add(eqn);
                }
                else {
                    missedvariables += 1;
                }
            }
        }

        /// Get the number of variables in the system
        public int getNumVariables() => vnlist.Count;

        /// Get the i-th Varnode variable
        public Varnode getVariable(int i) => vnlist[i];

        /// Get the i-th variable's companion index
        public int getCompanion(int i) => companion[i];

        /// Get the i-th variable's solution
        public int getSolution(int i) => soln[i];
    }
}
