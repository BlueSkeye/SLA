using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief A class for expanding macro directives within a p-code section
    ///
    /// It is handed a (partial) list of p-code op templates (OpTpl).  The
    /// macro directive is established with the setMacroOp() method.  Then calling
    /// build() expands the macro into the list of OpTpls, providing parameter
    /// substitution.  The class is derived from PcodeBuilder, where the dump() method,
    /// instead of emitting raw p-code, clones the macro templates into the list
    /// of OpTpls.
    internal class MacroBuilder : PcodeBuilder
    {
        private SleighCompile slgh;        ///< The SLEIGH parsing object
        private bool haserror;      ///< Set to \b true by the build() method if there was an error
        private List<OpTpl> outvec; ///< The partial list of op templates to expand the macro into
        private List<HandleTpl> @params;    ///< List of parameters to substitute into the macro

        /// \brief Given a cloned OpTpl, substitute parameters and add to the output list
        ///
        /// VarnodesTpls used by the op are examined to see if they are derived from
        /// parameters of the macro. If so, details of the parameters actively passed
        /// as part of the specific macro invocation are substituted into the VarnodeTpl.
        /// Truncation operations on a macro parameter may cause additional OpCode.CPUI_SUBPIECE
        /// operators to be inserted as part of the expansion and certain forms are not
        /// permitted.
        /// \param op is the cloned op to emit
        /// \param params is the set of parameters specific to the macro invocation
        /// \return \b true if there are no illegal truncations
        private bool transferOp(OpTpl op, List<HandleTpl> @params)
        { // Fix handle details of a macro generated OpTpl relative to its specific invocation
          // and transfer it into the output stream
            VarnodeTpl* outvn = op.getOut();
            int handleIndex = 0;
            int plus;
            bool hasrealsize = false;
            ulong realsize = 0;

            if (outvn != (VarnodeTpl*)0)
            {
                plus = outvn.transfer(@params);
                if (plus >= 0)
                {
                    reportError((Location*)0, "Cannot currently assign to bitrange of macro parameter that is a temporary");
                    return false;
                }
            }
            for (int i = 0; i < op.numInput(); ++i)
            {
                VarnodeTpl* vn = op.getIn(i);
                if (vn.getOffset().getType() == ConstTpl.const_type.handle)
                {
                    handleIndex = vn.getOffset().getHandleIndex();
                    hasrealsize = (vn.getSize().getType() == ConstTpl.const_type.real);
                    realsize = vn.getSize().getReal();
                }
                plus = vn.transfer(@params);
                if (plus >= 0)
                {
                    if (!hasrealsize)
                    {
                        reportError((Location*)0, "Problem with bit range operator in macro");
                        return false;
                    }
                    ulong newtemp = slgh.getUniqueAddr(); // Generate a new temporary location

                    // Generate a SUBPIECE op that implements the offset_plus
                    OpTpl* subpieceop = new OpTpl(CPUI_SUBPIECE);
                    VarnodeTpl* newvn = new VarnodeTpl(ConstTpl(slgh.getUniqueSpace()), ConstTpl(ConstTpl.const_type.real, newtemp),
                                   ConstTpl(ConstTpl.const_type.real, realsize));
                    subpieceop.setOutput(newvn);
                    HandleTpl* hand = @params[handleIndex];
                    VarnodeTpl* origvn = new VarnodeTpl(hand.getSpace(), hand.getPtrOffset(), hand.getSize());
                    subpieceop.addInput(origvn);
                    VarnodeTpl* plusvn = new VarnodeTpl(ConstTpl(slgh.getConstantSpace()), ConstTpl(ConstTpl.const_type.real, plus),
                                     ConstTpl(ConstTpl.const_type.real, 4));
                    subpieceop.addInput(plusvn);
                    outvec.Add(subpieceop);

                    delete vn;        // Replace original varnode
                    op.setInput(new VarnodeTpl(*newvn), i); // with output of subpiece
                }
            }
            outvec.Add(op);
            return true;
        }
        
        protected virtual void dump(OpTpl op)
        {
            OpTpl* clone;
            VarnodeTpl* v_clone,*vn;

            clone = new OpTpl(op.getOpcode());
            vn = op.getOut();
            if (vn != (VarnodeTpl*)0)
            {
                v_clone = new VarnodeTpl(*vn);
                clone.setOutput(v_clone);
            }
            for (int i = 0; i < op.numInput(); ++i)
            {
                vn = op.getIn(i);
                v_clone = new VarnodeTpl(*vn);
                if (v_clone.isRelative())
                {
                    // Adjust relative index, depending on the labelbase
                    ulong val = v_clone.getOffset().getReal() + getLabelBase();
                    v_clone.setRelative(val);
                }
                clone.addInput(v_clone);
            }
            if (!transferOp(clone,params))
                delete clone;
        }

        /// Free resources used by the builder
        private void free()
        {
            List<HandleTpl*>::iterator iter;

            for (iter = @params.begin(); iter != @params.end(); ++iter)
                delete* iter;

            @params.clear();
        }

        /// Report error encountered expanding the macro
        /// The error is passed up to the main parse object and a note is made
        /// locally that an error occurred so parsing can be terminated immediately.
        /// \param loc is the parse location where the error occurred
        /// \param val is the error message
        private void reportError(Location loc, string val)
        {
            slgh.reportError(loc, val);
            haserror = true;
        }

        public MacroBuilder(SleighCompile sl, List<OpTpl> ovec, uint lbcnt) : PcodeBuilder(lbcnt),outvec(ovec)
        {
            slgh = sl; haserror = false;
        }

        ///< Establish the MACRO directive to expand
        /// Given the op corresponding to the invocation, set up the specific parameters.
        /// \param macroop is the given MACRO directive op
        public void setMacroOp(OpTpl macroop)
        {
            VarnodeTpl* vn;
            HandleTpl* hand;
            free();
            for (int i = 1; i < macroop.numInput(); ++i)
            {
                vn = macroop.getIn(i);
                hand = new HandleTpl(vn);
                @params.Add(hand);
            }
        }

        public bool hasError() => haserror; ///< Return \b true if there were errors during expansion

        ~MacroBuilder()
        {
            free();
        }

        public virtual void appendBuild(OpTpl bld, int secnum)
        {
            dump(bld);
        }

        public virtual void delaySlot(OpTpl op)
        {
            dump(op);
        }

        public virtual void setLabel(OpTpl op)
        { // A label within a macro is local to the macro, but when
          // we expand the macro, we have to adjust the index of
          // the label, which is local to the macro, so that it fits
          // in with other labels local to the parent
            OpTpl* clone;
            VarnodeTpl* v_clone;

            clone = new OpTpl(op.getOpcode());
            v_clone = new VarnodeTpl(*op.getIn(0)); // Clone the label index
                                                     // Make adjustment to macro local value so that it is parent local
            ulong val = v_clone.getOffset().getReal() + getLabelBase();
            v_clone.setOffset(val);
            clone.addInput(v_clone);
            outvec.Add(clone);
        }

        public virtual void appendCrossBuild(OpTpl bld, int secnum)
        {
            dump(bld);
        }
    }
}
