using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.ParameterPieces;
using static ghidra.ProtoModel;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Runtime.Intrinsics;
using System.Runtime.ConstrainedExecution;

namespace ghidra
{
    /// \brief A \b function \b prototype
    ///
    /// A description of the parameters and return value for a specific function.
    /// Parameter descriptions include both source code features like \e name and \e data-type
    /// but also give the storage location. Storage follows a specific parameter passing convention
    /// (ProtoModel), although individual parameters may be customized.  The prototype describes
    /// numerous properties related to calling the specific function:
    ///   - Side-effects on non-parameter storage locations (like save registers)
    ///   - P-code injection associated with the function (uponentry, uponreturn, callfixup)
    ///   - Additional bytes (\b extrapop) popped from the stack by the function
    ///   - Method flags (thiscall, is_constructor, is_destructor)
    internal class FuncProto
    {
        [Flags()]
        public enum FuncFlags
        {
            /// Set if \b this prototype takes variable arguments (varargs)
            dotdotdot = 1,
            /// Set if \b this prototype takes no inputs and is locked
            voidinputlock = 2,
            /// Set if the PrototypeModel is locked for \b this prototype
            modellock = 4,
            /// Should \b this be inlined (within calling function) by decompiler
            is_inline = 8,
            /// Function does not return
            no_return = 16,
            /// paramshift parameters have been added and removed
            paramshift_applied = 32,
            /// Set if the input parameters are not properly represented
            error_inputparam = 64,
            /// Set if the return value(s) are not properly represented
            error_outputparam = 128,
            /// Parameter storage is custom (not derived from ProtoModel)
            custom_storage = 256,
            /// Function is an (object-oriented) constructor
            is_constructor = 0x200,
            /// Function is an (object-oriented) destructor
            is_destructor = 0x400,
            /// Function is a method with a 'this' pointer as an argument
            has_thisptr = 0x800,
            /// Set if \b this prototype is created to override a single call site
            is_override = 0x1000
        }

        /// Model of for \b this prototype
        private ProtoModel model;
        /// Storage interface for parameters
        private ProtoStore store;
        /// Extra bytes popped from stack
        private int4 extrapop;
        /// Boolean properties of the function prototype
        private uint4 flags;
        /// Side-effects associated with non-parameter storage locations
        private List<EffectRecord> effectlist;
        /// Locations that may contain \e trash values
        private List<VarnodeData> likelytrash;
        /// (If non-negative) id of p-code snippet that should replace this function
        private int4 injectid;
        /// Number of bytes of return value that are consumed by callers (0 = all bytes)
        private int4 returnBytesConsumed;

        /// Make sure any "this" parameter is properly marked
        /// This is called after a new prototype is established (via decode or updateAllTypes)
        /// It makes sure that if the ProtoModel calls for a "this" parameter, then the appropriate parameter
        /// is explicitly marked as the "this".
        void updateThisPointer()
        {
            if (!model->hasThisPointer()) return;
            int4 numInputs = store->getNumInputs();
            if (numInputs == 0) return;
            ProtoParameter* param = store->getInput(0);
            if (param->isHiddenReturn())
            {
                if (numInputs < 2) return;
                param = store->getInput(1);
            }
            param->setThisPointer(true);
        }

        /// Encode any overriding EffectRecords to stream
        /// If the \e effectlist for \b this is non-empty, it contains the complete set of
        /// EffectRecords.  Save just those that override the underlying list from ProtoModel
        /// \param encoder is the stream encoder
        private void encodeEffect(Encoder encoder)
        {
            if (effectlist.empty()) return;
            vector <const EffectRecord*> unaffectedList;
            vector <const EffectRecord*> killedByCallList;
            const EffectRecord* retAddr = (const EffectRecord*)0;
            for (vector<EffectRecord>::const_iterator iter = effectlist.begin(); iter != effectlist.end(); ++iter)
            {
                const EffectRecord &curRecord(*iter);
                uint4 type = model->hasEffect(curRecord.getAddress(), curRecord.getSize());
                if (type == curRecord.getType()) continue;
                if (curRecord.getType() == EffectRecord::unaffected)
                    unaffectedList.push_back(&curRecord);
                else if (curRecord.getType() == EffectRecord::killedbycall)
                    killedByCallList.push_back(&curRecord);
                else if (curRecord.getType() == EffectRecord::return_address)
                    retAddr = &curRecord;
            }
            if (!unaffectedList.empty())
            {
                encoder.openElement(ELEM_UNAFFECTED);
                for (int4 i = 0; i < unaffectedList.size(); ++i)
                {
                    unaffectedList[i]->encode(encoder);
                }
                encoder.closeElement(ELEM_UNAFFECTED);
            }
            if (!killedByCallList.empty())
            {
                encoder.openElement(ELEM_KILLEDBYCALL);
                for (int4 i = 0; i < killedByCallList.size(); ++i)
                {
                    killedByCallList[i]->encode(encoder);
                }
                encoder.closeElement(ELEM_KILLEDBYCALL);
            }
            if (retAddr != (const EffectRecord*)0) {
                encoder.openElement(ELEM_RETURNADDRESS);
                retAddr->encode(encoder);
                encoder.closeElement(ELEM_RETURNADDRESS);
            }
        }

        /// Encode any overriding likelytrash registers to stream
        /// If the \b likelytrash list is not empty it overrides the underlying ProtoModel's list.
        /// Encode any VarnodeData that does not appear in the ProtoModel to the stream.
        /// \param encoder is the stream encoder
        private void encodeLikelyTrash(Encoder encoder)
        {
            if (likelytrash.empty()) return;
            vector<VarnodeData>::const_iterator iter1, iter2;
            iter1 = model->trashBegin();
            iter2 = model->trashEnd();
            encoder.openElement(ELEM_LIKELYTRASH);
            for (vector<VarnodeData>::const_iterator iter = likelytrash.begin(); iter != likelytrash.end(); ++iter)
            {
                const VarnodeData &cur(*iter);
                if (binary_search(iter1, iter2, cur)) continue; // Already exists in ProtoModel
                encoder.openElement(ELEM_ADDR);
                cur.space->encodeAttributes(encoder, cur.offset, cur.size);
                encoder.closeElement(ELEM_ADDR);
            }
            encoder.closeElement(ELEM_LIKELYTRASH);
        }

        /// Merge in any EffectRecord overrides
        /// EffectRecords read into \e effectlist by decode() override the list from ProtoModel.
        /// If this list is not empty, set up \e effectlist as a complete override containing
        /// all EffectRecords from ProtoModel plus all the overrides.
        private void decodeEffect()
        {
            if (effectlist.empty()) return;
            vector<EffectRecord> tmpList;
            tmpList.swap(effectlist);
            for (vector<EffectRecord>::const_iterator iter = model->effectBegin(); iter != model->effectEnd(); ++iter)
            {
                effectlist.push_back(*iter);
            }
            bool hasNew = false;
            int4 listSize = effectlist.size();
            for (vector<EffectRecord>::const_iterator iter = tmpList.begin(); iter != tmpList.end(); ++iter)
            {
                const EffectRecord &curRecord(*iter);
                int4 off = ProtoModel::lookupRecord(effectlist, listSize, curRecord.getAddress(), curRecord.getSize());
                if (off == -2)
                    throw LowlevelError("Partial overlap of prototype override with existing effects");
                else if (off >= 0)
                {
                    // Found matching record, change its type
                    effectlist[off] = curRecord;
                }
                else
                {
                    effectlist.push_back(curRecord);
                    hasNew = true;
                }
            }
            if (hasNew)
                sort(effectlist.begin(), effectlist.end(), EffectRecord::compareByAddress);
        }

        /// Merge in any \e likelytrash overrides
        /// VarnodeData read into \e likelytrash by decode() are additional registers over
        /// what is already in ProtoModel.  Make \e likelytrash in \b this a complete list by
        /// merging in everything from ProtoModel.
        private void decodeLikelyTrash()
        {
            if (likelytrash.empty()) return;
            vector<VarnodeData> tmpList;
            tmpList.swap(likelytrash);
            vector<VarnodeData>::const_iterator iter1, iter2;
            iter1 = model->trashBegin();
            iter2 = model->trashEnd();
            for (vector<VarnodeData>::const_iterator iter = iter1; iter != iter2; ++iter)
                likelytrash.push_back(*iter);
            for (vector<VarnodeData>::const_iterator iter = tmpList.begin(); iter != tmpList.end(); ++iter)
            {
                if (!binary_search(iter1, iter2, *iter))
                    likelytrash.push_back(*iter);       // Add in the new register
            }
            sort(likelytrash.begin(), likelytrash.end());
        }

        /// Add parameters to the front of the input parameter list
        /// Prepend the indicated number of input parameters to \b this.
        /// The new parameters have a data-type of xunknown4. If they were
        /// originally locked, the existing parameters are preserved.
        /// \param paramshift is the number of parameters to add (must be >0)
        protected void paramShift(int4 paramshift)
        {
            if ((model == (ProtoModel*)0) || (store == (ProtoStore*)0))
                throw LowlevelError("Cannot parameter shift without a model");

            vector<string> nmlist;
            vector<Datatype*> typelist;
            bool isdotdotdot = false;
            TypeFactory* typefactory = model->getArch()->types;

            if (isOutputLocked())
                typelist.push_back(getOutputType());
            else
                typelist.push_back(typefactory->getTypeVoid());
            nmlist.push_back("");

            Datatype* extra = typefactory->getBase(4, TYPE_UNKNOWN); // The extra parameters have this type
            for (int4 i = 0; i < paramshift; ++i)
            {
                nmlist.push_back("");
                typelist.push_back(extra);
            }

            if (isInputLocked())
            {   // Copy in the original parameter types
                int4 num = numParams();
                for (int4 i = 0; i < num; ++i)
                {
                    ProtoParameter* param = getParam(i);
                    nmlist.push_back(param->getName());
                    typelist.push_back(param->getType());
                }
            }
            else
                isdotdotdot = true;

            // Reassign the storage locations for this new parameter list
            vector<ParameterPieces> pieces;
            model->assignParameterStorage(typelist, pieces, false);

            delete store;

            // This routine always converts -this- to have a ProtoStoreInternal
            store = new ProtoStoreInternal(typefactory->getTypeVoid());

            store->setOutput(pieces[0]);
            uint4 j = 1;
            for (uint4 i = 1; i < pieces.size(); ++i)
            {
                if ((pieces[i].flags & ParameterPieces::hiddenretparm) != 0)
                {
                    store->setInput(i - 1, "rethidden", pieces[i]);
                    continue;   // increment i but not j
                }
                store->setInput(j, nmlist[j], pieces[i]);
                j = j + 1;
            }
            setInputLock(true);
            setDotdotdot(isdotdotdot);
        }

        /// Has a parameter shift been applied
        protected bool isParamshiftApplied() => ((flags&paramshift_applied)!=0);

        /// \brief Toggle whether a parameter shift has been applied
        protected void setParamshiftApplied(bool val)
        {
            flags = val ? (flags | paramshift_applied) : (flags & ~((uint4)paramshift_applied));
        }
    
        public FuncProto()
        {
            model = (ProtoModel*)0;
            store = (ProtoStore*)0;
            flags = 0;
            injectid = -1;
            returnBytesConsumed = 0;
        }

        ~FuncProto()
        {
            if (store != (ProtoStore*)0)
                delete store;
        }

        ///< Get the Architecture owning \b this
        public Architecture getArch() => model->getArch();

        /// Copy another function prototype
        /// \param op2 is the other function prototype to copy into \b this
        public void copy(FuncProto op2)
        {
            model = op2.model;
            extrapop = op2.extrapop;
            flags = op2.flags;
            if (store != (ProtoStore*)0)
                delete store;
            if (op2.store != (ProtoStore*)0)
                store = op2.store->clone();
            else
                store = (ProtoStore*)0;
            effectlist = op2.effectlist;
            likelytrash = op2.likelytrash;
            injectid = op2.injectid;
        }

        /// Copy properties that affect data-flow
        public void copyFlowEffects(FuncProto op2)
        {
            flags &= ~((uint4)(is_inline | no_return));
            flags |= op2.flags & (is_inline | no_return);
            injectid = op2.injectid;
        }

        /// Get the raw pieces of the prototype
        /// Copy out the raw pieces of \b this prototype as stand-alone objects,
        /// includings model, names, and data-types
        /// \param pieces will hold the raw pieces
        public void getPieces(PrototypePieces pieces)
        {
            pieces.model = model;
            if (store == (ProtoStore*)0) return;
            pieces.outtype = store->getOutput()->getType();
            int4 num = store->getNumInputs();
            for (int4 i = 0; i < num; ++i)
            {
                ProtoParameter* param = store->getInput(i);
                pieces.intypes.push_back(param->getType());
                pieces.innames.push_back(param->getName());
            }
            pieces.dotdotdot = isDotdotdot();
        }

        /// Set \b this prototype based on raw pieces
        /// The full function prototype is (re)set from a model, names, and data-types
        /// The new input and output parameters are both assumed to be locked.
        /// \param pieces is the raw collection of names and data-types
        public void setPieces(PrototypePieces pieces)
        {
            if (pieces.model != (ProtoModel*)0)
                setModel(pieces.model);
            vector<Datatype*> typelist;
            vector<string> nmlist;
            typelist.push_back(pieces.outtype);
            nmlist.push_back("");
            for (int4 i = 0; i < pieces.intypes.size(); ++i)
            {
                typelist.push_back(pieces.intypes[i]);
                nmlist.push_back(pieces.innames[i]);
            }
            updateAllTypes(nmlist, typelist, pieces.dotdotdot);
            setInputLock(true);
            setOutputLock(true);
            setModelLock(true);
        }

        /// Set a backing symbol Scope for \b this
        /// Input parameters are set based on an existing function Scope
        /// and if there is no prototype model the default model is set.
        /// Parameters that are added to \b this during analysis will automatically
        /// be reflected in the symbol table.
        /// This should only be called during initialization of \b this prototype.
        /// \param s is the Scope to set
        /// \param startpoint is a usepoint to associate with the parameters
        public void setScope(Scope s, Address startpoint)
        {
            store = new ProtoStoreSymbol(s, startpoint);
            if (model == (ProtoModel*)0)
                setModel(s->getArch()->defaultfp);
        }

        /// Set internal backing storage for \b this
        /// A prototype model is set, and any parameters added to \b this during analysis
        /// will be backed internally.
        /// \param m is the prototype model to set
        /// \param vt is the default \e void data-type to use if the return-value remains unassigned
        public void setInternal(ProtoModel m, Datatype vt)
        {
            store = new ProtoStoreInternal(vt);
            if (model == (ProtoModel*)0)
                setModel(m);
        }

        /// Set the prototype model for \b this
        /// Establish a specific prototype model for \b this function prototype.
        /// Some basic properties are inherited from the model, otherwise parameters
        /// are unchanged.
        /// \param m is the new prototype model to set
        public void setModel(ProtoModel m)
        {
            if (m != (ProtoModel*)0)
            {
                int4 expop = m->getExtraPop();
                // If a model previously existed don't overwrite extrapop with unknown
                if ((model == (ProtoModel*)0) || (expop != ProtoModel::extrapop_unknown))
                    extrapop = expop;
                if (m->hasThisPointer())
                    flags |= has_thisptr;
                if (m->isConstructor())
                    flags |= is_constructor;
                model = m;
            }
            else
            {
                model = m;
                extrapop = ProtoModel::extrapop_unknown;
            }
        }

        /// Does \b this prototype have a model
        public bool hasModel() => (model != (ProtoModel *)0);

        /// Does \b this use the given model
        public bool hasMatchingModel(ProtoModel op2) => (model == op2);

        /// Get the prototype model name
        public string getModelName() => model->getName();

        /// Get the \e extrapop of the prototype model
        public int4 getModelExtraPop() => model->getExtraPop();

        /// Return \b true if the prototype model is \e unknown
        public bool isModelUnknown() => model->isUnknown();

        /// Return \b true if the name should be printed in declarations
        public bool printModelInDecl() => model->printInDecl();

        /// Are input data-types locked
        public bool isInputLocked()
        {
            if ((flags & voidinputlock) != 0) return true;
            if (numParams() == 0) return false;
            ProtoParameter* param = getParam(0);
            if (param->isTypeLocked()) return true;
            return false;
        }

        /// Is the output data-type locked
        public bool isOutputLocked() => store->getOutput()->isTypeLocked();

        /// Is the prototype model for \b this locked
        public bool isModelLocked() => ((flags&modellock)!= 0);

        /// Is \b this a "custom" function prototype
        public bool hasCustomStorage() => ((flags&custom_storage)!= 0);

        /// Toggle the data-type lock on input parameters
        /// The lock on the data-type of input parameters is set as specified.
        /// A \b true value indicates that future analysis will not change the
        /// number of input parameters or their data-type.  Zero parameters
        /// or \e void can be locked.
        /// \param val is \b true to indicate a lock, \b false for unlocked
        public void setInputLock(bool val)
        {
            if (val)
                flags |= modellock;     // Locking input locks the model
            int4 num = numParams();
            if (num == 0)
            {
                flags = val ? (flags | voidinputlock) : (flags & ~((uint4)voidinputlock));
                return;
            }
            for (int4 i = 0; i < num; ++i)
            {
                ProtoParameter* param = getParam(i);
                param->setTypeLock(val);
            }
        }

        /// Toggle the data-type lock on the return value
        /// The lock of the data-type of the return value is set as specified.
        /// A \b true value indicates that future analysis will not change the
        /// presence of or the data-type of the return value. A \e void return
        /// value can be locked.
        /// \param val is \b true to indicate a lock, \b false for unlocked
        public void setOutputLock(bool val)
        {
            if (val)
                flags |= modellock;     // Locking output locks the model
            store->getOutput()->setTypeLock(val);
        }

        /// \brief Toggle the lock on the prototype model for \b this.
        ///
        /// The prototype model can be locked while still leaving parameters unlocked. Parameter
        /// recovery will follow the rules of the locked model.
        /// \param val is \b true to indicate a lock, \b false for unlocked
        public void setModelLock(bool val)
        {
            flags = val ? (flags | modellock) : (flags & ~((uint4)modellock));
        }

        /// Does this function get \e in-lined during decompilation.
        public bool isInline() => ((flags & is_inline)!= 0);

        /// \brief Toggle the \e in-line setting for functions with \b this prototype
        ///
        /// In-lining can be based on a \e call-fixup, or the full body of the function can be in-lined.
        /// \param val is \b true if in-lining should be performed.
        public void setInline(bool val)
        {
            flags = val ? (flags | is_inline) : (flags & ~((uint4)is_inline));
        }

        /// \brief Get the injection id associated with \b this.
        ///
        /// A non-negative id indicates a \e call-fixup is used to in-line function's with \b this prototype.
        /// \return the id value corresponding to the specific call-fixup or -1 if there is no call-fixup
        public int4 getInjectId() => injectid;

        /// \brief Get an estimate of the number of bytes consumed by callers of \b this prototype.
        ///
        /// A value of 0 means \e all possible bytes of the storage location are consumed.
        /// \return the number of bytes or 0
        public int4 getReturnBytesConsumed() => returnBytesConsumed;

        /// Set the number of bytes consumed by callers of \b this
        /// Provide a hint as to how many bytes of the return value are important.
        /// The smallest hint is used to inform the dead-code removal algorithm.
        /// \param val is the hint (number of bytes or 0 for all bytes)
        /// \return \b true if the smallest hint has changed
        public bool setReturnBytesConsumed(int4 val)
        {
            if (val == 0)
                return false;
            if (returnBytesConsumed == 0 || val < returnBytesConsumed)
            {
                returnBytesConsumed = val;
                return true;
            }
            return false;
        }

        /// \brief Does a function with \b this prototype never return
        public bool isNoReturn() => ((flags & no_return)!= 0);

        /// \brief Toggle the \e no-return setting for functions with \b this prototype
        ///
        /// \param val is \b true to treat the function as never returning
        public void setNoReturn(bool val)
        {
            flags = val ? (flags | no_return) : (flags & ~((uint4)no_return));
        }

        /// \brief Is \b this a prototype for a class method, taking a \e this pointer.
        public bool hasThisPointer() => ((flags & has_thisptr)!= 0);

        /// \brief Is \b this prototype for a class constructor method
        public bool isConstructor() => ((flags & is_constructor)!= 0);

        /// \brief Toggle whether \b this prototype is a \e constructor method
        ///
        /// \param val is \b true if \b this is a constructor, \b false otherwise
        public void setConstructor(bool val)
        {
            flags = val ? (flags | is_constructor) : (flags & ~((uint4)is_constructor));
        }

        /// \brief Is \b this prototype for a class destructor method
        public bool isDestructor() => ((flags & is_destructor)!= 0);

        /// \brief Toggle whether \b this prototype is a \e destructor method
        ///
        /// \param val is \b true if \b this is a destructor
        public void setDestructor(bool val)
        {
            flags = val ? (flags | is_destructor) : (flags & ~((uint4)is_destructor));
        }

        /// \brief Has \b this prototype been marked as having an incorrect input parameter descriptions
        public bool hasInputErrors() => ((flags&error_inputparam)!= 0);

        /// \brief Has \b this prototype been marked as having an incorrect return value description
        public bool hasOutputErrors() => ((flags&error_outputparam)!= 0);

        /// \brief Toggle the input error setting for \b this prototype
        ///
        /// \param val is \b true if input parameters should be marked as in error
        public void setInputErrors(bool val)
        {
            flags = val ? (flags | error_inputparam) : (flags & ~((uint4)error_inputparam));
        }

        /// \brief Toggle the output error setting for \b this prototype
        ///
        /// \param val is \b true if return value should be marked as in error
        public void setOutputErrors(bool val)
        {
            flags = val ? (flags | error_outputparam) : (flags & ~((uint4)error_outputparam));
        }

        public int4 getExtraPop() => extrapop; ///< Get the general \e extrapop setting for \b this prototype

        public void setExtraPop(int4 ep)
        {
            extrapop = ep;
        }          ///< Set the general \e extrapop for \b this prototype

        public int4 getInjectUponEntry() => model->getInjectUponEntry(); ///< Get any \e upon-entry injection id (or -1)
        
        public int4 getInjectUponReturn() => model->getInjectUponReturn(); ///< Get any \e upon-return injection id (or -1)

        /// \brief Assuming \b this prototype is locked, calculate the \e extrapop
        ///
        /// If \e extrapop is unknown and \b this prototype is locked, try to directly
        /// calculate what the \e extrapop should be.  This is really only designed to work with
        /// 32-bit x86 binaries.
        public void resolveExtraPop()
        {
            if (!isInputLocked()) return;
            int4 numparams = numParams();
            if (isDotdotdot())
            {
                if (numparams != 0)     // If this is a "standard" varargs, with fixed initial parameters
                    setExtraPop(4);     // then this must be __cdecl
                return;         // otherwise we can't resolve the extrapop, as in the FARPROC prototype
            }
            int4 expop = 4;         // Extrapop is at least 4 for the return address
            for (int4 i = 0; i < numparams; ++i)
            {
                ProtoParameter* param = getParam(i);
                const Address &addr(param->getAddress());
                if (addr.getSpace()->getType() != IPTR_SPACEBASE) continue;
                int4 cur = (int4)addr.getOffset() + param->getSize();
                cur = (cur + 3) & 0xffffffc;    // Must be 4-byte aligned
                if (cur > expop)
                    expop = cur;
            }
            setExtraPop(expop);
        }

        /// Clear input parameters that have not been locked
        public void clearUnlockedInput()
        {
            if (isInputLocked()) return;
            store->clearAllInputs();
        }

        /// Clear the return value if it has not been locked
        public void clearUnlockedOutput()
        {
            ProtoParameter* outparam = getOutput();
            if (outparam->isTypeLocked())
            {
                if (outparam->isSizeTypeLocked())
                {
                    if (model != (ProtoModel*)0)
                        outparam->resetSizeLockType(getArch()->types);
                }
            }
            else
                store->clearOutput();
            returnBytesConsumed = 0;
        }

        /// Clear all input parameters regardless of lock
        public void clearInput()
        {
            store->clearAllInputs();
            flags &= ~((uint4)voidinputlock); // If a void was locked in clear it
        }

        /// Associate a given injection with \b this prototype
        /// Set the id directly.
        /// \param id is the new id
        public void setInjectId(int4 id)
        {
            if (id < 0)
                cancelInjectId();
            else
            {
                injectid = id;
                flags |= is_inline;
            }
        }

        /// Turn-off any in-lining for this function
        public void cancelInjectId()
        {
            injectid = -1;
            flags &= ~((uint4)is_inline);
        }

        /// \brief If \b this has a \e merged model, pick the most likely model (from the merged set)
        /// The given parameter trials are used to pick from among the merged ProtoModels and
        /// \b this prototype is changed (specialized) to the pick
        /// \param active is the set of parameter trials to evaluate with
        public void resolveModel(ParamActive active)
        {
            if (model == (ProtoModel*)0) return;
            if (!model->isMerged()) return; // Already been resolved
            ProtoModelMerged* mergemodel = (ProtoModelMerged*)model;
            ProtoModel* newmodel = mergemodel->selectModel(active);
            setModel(newmodel);
            // we don't need to remark the trials, as this is accomplished by the ParamList::fillinMap method
        }

        /// \brief Given a list of input \e trials, derive the most likely inputs for \b this prototype
        ///
        /// Trials are sorted and marked as \e used or not.
        /// \param active is the collection of Varnode input trials
        public void deriveInputMap(ParamActive active)
        {
            model->deriveInputMap(active);
        }

        /// \brief Given a list of output \e trials, derive the most likely return value for \b this prototype
        ///
        /// One trial (at most) is marked \e used and moved to the front of the list
        /// \param active is the collection of output trials
        public void deriveOutputMap(ParamActive active)
        {
            model->deriveOutputMap(active);
        }

        /// \brief Check if the given two input storage locations can represent a single logical parameter
        ///
        /// For \b this prototype, do the two (hi/lo) locations represent
        /// consecutive input parameter locations that can be replaced by a single logical parameter.
        /// \param hiaddr is the address of the most significant part of the value
        /// \param hisz is the size of the most significant part in bytes
        /// \param loaddr is the address of the least significant part of the value
        /// \param losz is the size of the least significant part in bytes
        /// \return \b true if the two pieces can be joined
        public bool checkInputJoin(Address hiaddr, int4 hisz, Address loaddr, int4 losz)
        {
            return model->checkInputJoin(hiaddr, hisz, loaddr, losz);
        }

        /// \brief Check if it makes sense to split a single storage location into two input parameters
        ///
        /// A storage location and split point is provided, implying two new storage locations. Does
        /// \b this prototype allow these locations to be considered separate parameters.
        /// \param loc is the starting address of provided storage location
        /// \param size is the size of the location in bytes
        /// \param splitpoint is the number of bytes to consider in the first (in address order) piece
        /// \return \b true if the storage location can be split
        public bool checkInputSplit(Address loc, int4 size, int4 splitpoint) {
            return model->checkInputSplit(loc, size, splitpoint);
        }

        /// \brief Update input parameters based on Varnode trials
        ///
        /// If the input parameters are locked, don't do anything. Otherwise,
        /// given a list of Varnodes and their associated trial information,
        /// create an input parameter for each trial in order, grabbing data-type
        /// information from the Varnode.  Any old input parameters are cleared.
        /// \param data is the function containing the trial Varnodes
        /// \param triallist is the list of Varnodes
        /// \param activeinput is the trial container
        public void updateInputTypes(Funcdata data, List<Varnode> triallist,
            ParamActive activeinput)
        {
            if (isInputLocked()) return;    // Input is locked, do no updating
            store->clearAllInputs();
            int4 count = 0;
            int4 numtrials = activeinput->getNumTrials();
            for (int4 i = 0; i < numtrials; ++i)
            {
                ParamTrial & trial(activeinput->getTrial(i));
                if (trial.isUsed())
                {
                    Varnode* vn = triallist[trial.getSlot() - 1];
                    if (vn->isMark()) continue;
                    ParameterPieces pieces;
                    if (vn->isPersist())
                    {
                        int4 sz;
                        pieces.addr = data.findDisjointCover(vn, sz);
                        if (sz == vn->getSize())
                            pieces.type = vn->getHigh()->getType();
                        else
                            pieces.type = data.getArch()->types->getBase(sz, TYPE_UNKNOWN);
                        pieces.flags = 0;
                    }
                    else
                    {
                        pieces.addr = trial.getAddress();
                        pieces.type = vn->getHigh()->getType();
                        pieces.flags = 0;
                    }
                    store->setInput(count, "", pieces);
                    count += 1;
                    vn->setMark();
                }
            }
            for (int4 i = 0; i < triallist.size(); ++i)
                triallist[i]->clearMark();
            updateThisPointer();
        }

        /// \brief Update input parameters based on Varnode trials, but do not store the data-type
        ///
        /// This is accomplished in the same way as if there were data-types but instead of
        /// pulling a data-type from the Varnode, only the size is used.
        /// Undefined data-types are pulled from the given TypeFactory
        /// \param data is the function containing the trial Varnodes
        /// \param triallist is the list of Varnodes
        /// \param activeinput is the trial container
        public void updateInputNoTypes(Funcdata data, List<Varnode> triallist,
            ParamActive activeinput)
        {
            if (isInputLocked()) return;    // Input is locked, do no updating
            store->clearAllInputs();
            int4 count = 0;
            int4 numtrials = activeinput->getNumTrials();
            TypeFactory* factory = data.getArch()->types;
            for (int4 i = 0; i < numtrials; ++i)
            {
                ParamTrial & trial(activeinput->getTrial(i));
                if (trial.isUsed())
                {
                    Varnode* vn = triallist[trial.getSlot() - 1];
                    if (vn->isMark()) continue;
                    ParameterPieces pieces;
                    if (vn->isPersist())
                    {
                        int4 sz;
                        pieces.addr = data.findDisjointCover(vn, sz);
                        pieces.type = factory->getBase(sz, TYPE_UNKNOWN);
                        pieces.flags = 0;
                    }
                    else
                    {
                        pieces.addr = trial.getAddress();
                        pieces.type = factory->getBase(vn->getSize(), TYPE_UNKNOWN);
                        pieces.flags = 0;
                    }
                    store->setInput(count, "", pieces);
                    count += 1;
                    vn->setMark();      // Make sure vn is used only once
                }
            }
            for (int4 i = 0; i < triallist.size(); ++i)
                triallist[i]->clearMark();
        }

        /// \brief Update the return value based on Varnode trials
        ///
        /// If the output parameter is locked, don't do anything. Otherwise,
        /// given a list of (at most 1) Varnode, create a return value, grabbing
        /// data-type information from the Varnode. Any old return value is removed.
        /// \param triallist is the list of Varnodes
        public void updateOutputTypes(List<Varnode> triallist)
        {
            ProtoParameter* outparm = getOutput();
            if (!outparm->isTypeLocked())
            {
                if (triallist.empty())
                {
                    store->clearOutput();
                    return;
                }
            }
            else if (outparm->isSizeTypeLocked())
            {
                if (triallist.empty()) return;
                if ((triallist[0]->getAddr() == outparm->getAddress()) && (triallist[0]->getSize() == outparm->getSize()))
                    outparm->overrideSizeLockType(triallist[0]->getHigh()->getType());
                return;
            }
            else
                return;         // Locked

            if (triallist.empty()) return;
            // If we reach here, output is not locked, not sizelocked, and there is a valid trial
            ParameterPieces pieces;
            pieces.addr = triallist[0]->getAddr();
            pieces.type = triallist[0]->getHigh()->getType();
            pieces.flags = 0;
            store->setOutput(pieces);
        }

        /// \brief Update the return value based on Varnode trials, but don't store the data-type
        ///
        /// If the output parameter is locked, don't do anything. Otherwise,
        /// given a list of (at most 1) Varnode, create a return value, grabbing
        /// size information from the Varnode. An undefined data-type is created from the
        /// given TypeFactory. Any old return value is removed.
        /// \param triallist is the list of Varnodes
        /// \param factory is the given TypeFactory
        public void updateOutputNoTypes(List<Varnode> triallist, TypeFactory factory)
        {
            if (isOutputLocked()) return;
            if (triallist.empty())
            {
                store->clearOutput();
                return;
            }
            ParameterPieces pieces;
            pieces.type = factory->getBase(triallist[0]->getSize(), TYPE_UNKNOWN);
            pieces.addr = triallist[0]->getAddr();
            pieces.flags = 0;
            store->setOutput(pieces);
        }

        /// \brief Set \b this entire function prototype based on a list of names and data-types.
        ///
        /// Prototype information is provided as separate lists of names and data-types, where
        /// the first entry corresponds to the output parameter (return value) and the remaining
        /// entries correspond to input parameters. Storage locations and hidden return parameters are
        /// calculated, creating a complete function protototype. Existing locks are overridden.
        /// \param namelist is the list of parameter names
        /// \param typelist is the list of data-types
        /// \param dtdtdt is \b true if the new prototype accepts variable argument lists
        public void updateAllTypes(List<string> namelist, List<Datatype> typelist, bool dtdtdt)
        {
            setModel(model);        // This resets extrapop
            store->clearAllInputs();
            store->clearOutput();
            flags &= ~((uint4)voidinputlock);
            setDotdotdot(dtdtdt);

            vector<ParameterPieces> pieces;

            // Calculate what memory locations hold each type
            try
            {
                model->assignParameterStorage(typelist, pieces, false);
                store->setOutput(pieces[0]);
                uint4 j = 1;
                for (uint4 i = 1; i < pieces.size(); ++i)
                {
                    if ((pieces[i].flags & ParameterPieces::hiddenretparm) != 0)
                    {
                        store->setInput(i - 1, "rethidden", pieces[i]);
                        continue;       // increment i but not j
                    }
                    store->setInput(i - 1, namelist[j], pieces[i]);
                    j = j + 1;
                }
            }
            catch (ParamUnassignedError err) {
                flags |= error_inputparam;
            }
            updateThisPointer();
        }

        public ProtoParameter getParam(int4 i) => store->getInput(i); ///< Get the i-th input parameter

        public void removeParam(int4 i)
        {
            store->clearInput(i);
        }        ///< Remove the i-th input parameter

        public int4 numParams() => store->getNumInputs(); ///< Get the number of input parameters

        public ProtoParameter getOutput() => store->getOutput(); ///< Get the return value

        public Datatype getOutputType() => store->getOutput()->getType(); ///< Get the return value data-type

        public RangeList getLocalRange() => model->getLocalRange(); ///< Get the range of potential local stack variables

        public RangeList getParamRange() => model->getParamRange(); ///< Get the range of potential stack parameters

        public bool isStackGrowsNegative() => model->isStackGrowsNegative(); ///< Return \b true if the stack grows toward smaller addresses

        public bool isDotdotdot() => ((flags&dotdotdot)!= 0); ///< Return \b true if \b this takes a variable number of arguments

        public void setDotdotdot(bool val)
        {
            flags = val ? (flags | dotdotdot) : (flags & ~((uint4)dotdotdot));
        }    ///< Toggle whether \b this takes variable arguments

        public bool isOverride() => ((flags&is_override)!= 0); ///< Return \b true if \b this is a call site override

        /// Toggle whether \b this is a call site override
        public void setOverride(bool val)
        {
            flags = val ? (flags | is_override) : (flags & ~((uint4)is_override));
        }

        /// \brief Calculate the effect \b this has an a given storage location
        ///
        /// For a storage location that is active before and after a call to a function
        /// with \b this prototype, we determine the type of side-effect the function
        /// will have on the storage.
        /// \param addr is the starting address of the storage location
        /// \param size is the number of bytes in the storage
        /// \return the type of side-effect: EffectRecord::unaffected, EffectRecord::killedbycall, etc.
        public uint4 hasEffect(Address addr, int4 size)
        {
            if (effectlist.empty())
                return model->hasEffect(addr, size);

            return ProtoModel::lookupEffect(effectlist, addr, size);
        }

        /// Get iterator to front of EffectRecord list
        public IEnumerator<EffectRecord> effectBegin()
        {
            if (effectlist.empty())
                return model->effectBegin();
            return effectlist.begin();
        }

        /// Get iterator to end of EffectRecord list
        public IEnumerator<EffectRecord> effectEnd()
        {
            if (effectlist.empty())
                return model->effectEnd();
            return effectlist.end();
        }

        /// Get iterator to front of \e likelytrash list
        public IEnumerator<VarnodeData> trashBegin()
        {
            if (likelytrash.empty())
                return model->trashBegin();
            return likelytrash.begin();
        }

        /// Get iterator to end of \e likelytrash list
        /// \return the iterator to the end of the list
        public IEnumerator<VarnodeData> trashEnd()
        {
            if (likelytrash.empty())
                return model->trashEnd();
            return likelytrash.end();
        }

        /// \brief Decide whether a given storage location could be, or could hold, an input parameter
        ///
        /// If the input is locked, check if the location overlaps one of the current parameters.
        /// Otherwise, check if the location overlaps an entry in the prototype model.
        /// Return:
        ///   - no_containment - there is no containment between the range and any input parameter
        ///   - contains_unjustified - at least one parameter contains the range
        ///   - contains_justified - at least one parameter contains this range as its least significant bytes
        ///   - contained_by - no parameter contains this range, but the range contains at least one parameter
        /// \param addr is the starting address of the given storage location
        /// \param size is the number of bytes in the storage
        /// \return the characterization code
        public int4 characterizeAsInputParam(Address addr, int4 size)
        {
            if (!isDotdotdot())
            {       // If the proto is varargs, go straight to the model
                if ((flags & voidinputlock) != 0) return 0;
                int4 num = numParams();
                if (num > 0)
                {
                    bool locktest = false;  // Have tested against locked symbol
                    bool resContains = false;
                    bool resContainedBy = false;
                    for (int4 i = 0; i < num; ++i)
                    {
                        ProtoParameter* param = getParam(i);
                        if (!param->isTypeLocked()) continue;
                        locktest = true;
                        Address iaddr = param->getAddress();
                        // If the parameter already exists, the varnode must be justified in the parameter relative
                        // to the endianness of the space, irregardless of the forceleft flag
                        int4 off = iaddr.justifiedContain(param->getSize(), addr, size, false);
                        if (off == 0)
                            return ParamEntry::contains_justified;
                        else if (off > 0)
                            resContains = true;
                        if (iaddr.containedBy(param->getSize(), addr, size))
                            resContainedBy = true;
                    }
                    if (locktest)
                    {
                        if (resContains) return ParamEntry::contains_unjustified;
                        if (resContainedBy) return ParamEntry::contained_by;
                        return ParamEntry::no_containment;
                    }
                }
            }
            return model->characterizeAsInputParam(addr, size);
        }

        /// \brief Decide whether a given storage location could be, or could hold, the return value
        ///
        /// If the output is locked, check if the location overlaps the current return storage.
        /// Otherwise, check if the location overlaps an entry in the prototype model.
        /// Return:
        ///   - no_containment - there is no containment between the range and any output storage
        ///   - contains_unjustified - at least one output storage contains the range
        ///   - contains_justified - at least one output storage contains this range as its least significant bytes
        ///   - contained_by - no output storage contains this range, but the range contains at least one output storage
        /// \param addr is the starting address of the given storage location
        /// \param size is the number of bytes in the storage
        /// \return the characterization code
        public int4 characterizeAsOutput(Address addr, int4 size)
        {
            if (isOutputLocked())
            {
                ProtoParameter* outparam = getOutput();
                if (outparam->getType()->getMetatype() == TYPE_VOID)
                    return ParamEntry::no_containment;
                Address iaddr = outparam->getAddress();
                // If the output is locked, the varnode must be justified in the location relative
                // to the endianness of the space, irregardless of the forceleft flag
                int4 off = iaddr.justifiedContain(outparam->getSize(), addr, size, false);
                if (off == 0)
                    return ParamEntry::contains_justified;
                else if (off > 0)
                    return ParamEntry::contains_unjustified;
                if (iaddr.containedBy(outparam->getSize(), addr, size))
                    return ParamEntry::contained_by;
                return ParamEntry::no_containment;
            }
            return model->characterizeAsOutput(addr, size);
        }

        /// \brief Decide whether a given storage location could be an input parameter
        ///
        /// If the input is locked, check if the location matches one of the current parameters.
        /// Otherwise, check if the location \e could be a parameter based on the
        /// prototype model.
        /// \param addr is the starting address of the given storage location
        /// \param size is the number of bytes in the storage
        /// \return \b false if the location is definitely not an input parameter
        public bool possibleInputParam(Address addr, int4 size)
        {
            if (!isDotdotdot())
            {       // If the proto is varargs, go straight to the model
                if ((flags & voidinputlock) != 0) return false;
                int4 num = numParams();
                if (num > 0)
                {
                    bool locktest = false;  // Have tested against locked symbol
                    for (int4 i = 0; i < num; ++i)
                    {
                        ProtoParameter* param = getParam(i);
                        if (!param->isTypeLocked()) continue;
                        locktest = true;
                        Address iaddr = param->getAddress();
                        // If the parameter already exists, the varnode must be justified in the parameter relative
                        // to the endianness of the space, irregardless of the forceleft flag
                        if (iaddr.justifiedContain(param->getSize(), addr, size, false) == 0)
                            return true;
                    }
                    if (locktest) return false;
                }
            }
            return model->possibleInputParam(addr, size);
        }

        /// \brief Decide whether a given storage location could be a return value
        ///
        /// If the output is locked, check if the location matches the current return value.
        /// Otherwise, check if the location \e could be a return value based on the
        /// prototype model.
        /// \param addr is the starting address of the given storage location
        /// \param size is the number of bytes in the storage
        /// \return \b false if the location is definitely not the return value
        public bool possibleOutputParam(Address addr, int4 size)
        {
            if (isOutputLocked())
            {
                ProtoParameter* outparam = getOutput();
                if (outparam->getType()->getMetatype() == TYPE_VOID)
                    return false;
                Address iaddr = outparam->getAddress();
                // If the output is locked, the varnode must be justified in the location relative
                // to the endianness of the space, irregardless of the forceleft flag
                if (iaddr.justifiedContain(outparam->getSize(), addr, size, false) == 0)
                    return true;
                return false;
            }
            return model->possibleOutputParam(addr, size);
        }

        /// \brief Return the maximum heritage delay across all possible input parameters
        ///
        /// Depending on the address space, data-flow for a parameter may not be available until
        /// extra transform passes have completed. This method returns the number of passes
        /// that must occur before we can guarantee that all parameters have data-flow info.
        /// \return the maximum number of passes across all input parameters in \b this prototype
        public int4 getMaxInputDelay() => model->getMaxInputDelay();

        /// \brief Return the maximum heritage delay across all possible return values
        ///
        /// Depending on the address space, data-flow for a parameter may not be available until
        /// extra transform passes have completed. This method returns the number of passes
        /// that must occur before we can guarantee that any return value has data-flow info.
        /// \return the maximum number of passes across all output parameters in \b this prototype
        public int4 getMaxOutputDelay() => model->getMaxOutputDelay();

        /// \brief Check if the given storage location looks like an \e unjustified input parameter
        ///
        /// The storage for a value may be contained in a normal parameter location but be
        /// unjustified within that container, i.e. the least significant bytes are not being used.
        /// If this is the case, pass back the full parameter location and return \b true.
        /// If the input is locked, checking is againt the set parameters, otherwise the
        /// check is against the prototype model.
        /// \param addr is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the full parameter storage to pass back
        /// \return \b true if the given storage is unjustified within its parameter container
        public bool unjustifiedInputParam(Address addr, int4 size, VarnodeData res)
        {
            if (!isDotdotdot())
            {       // If the proto is varargs, go straight to the model
                if ((flags & voidinputlock) != 0) return false;
                int4 num = numParams();
                if (num > 0)
                {
                    bool locktest = false;  // Have tested against locked symbol
                    for (int4 i = 0; i < num; ++i)
                    {
                        ProtoParameter* param = getParam(i);
                        if (!param->isTypeLocked()) continue;
                        locktest = true;
                        Address iaddr = param->getAddress();
                        // If the parameter already exists, test if -addr- -size- is improperly contained in param
                        int4 just = iaddr.justifiedContain(param->getSize(), addr, size, false);
                        if (just == 0) return false; // Contained but not improperly
                        if (just > 0)
                        {
                            res.space = iaddr.getSpace();
                            res.offset = iaddr.getOffset();
                            res.size = param->getSize();
                            return true;
                        }
                    }
                    if (locktest) return false;
                }
            }
            return model->unjustifiedInputParam(addr, size, res);
        }

        /// \brief Get the type of extension and containing input parameter for the given storage
        ///
        /// If the given storage is properly contained within a normal parameter and the model
        /// typically extends a small value into the full container, pass back the full container
        /// and the type of extension.
        /// \param addr is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the parameter storage to pass back
        /// \return the extension operator (INT_ZEXT INT_SEXT) or INT_COPY if there is no extension.
        /// INT_PIECE indicates the extension is determined by the specific prototype.
        public OpCode assumedInputExtension(Address addr, int4 size, VarnodeData res)
        {
            return model->assumedInputExtension(addr, size, res);
        }

        /// \brief Get the type of extension and containing return value location for the given storage
        ///
        /// If the given storage is properly contained within a normal return value location and the model
        /// typically extends a small value into the full container, pass back the full container
        /// and the type of extension.
        /// \param addr is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the parameter storage to pass back
        /// \return the extension operator (INT_ZEXT INT_SEXT) or INT_COPY if there is no extension.
        /// INT_PIECE indicates the extension is determined by the specific prototype.
        public OpCode assumedOutputExtension(Address addr, int4 size, VarnodeData res)
        {
            return model->assumedOutputExtension(addr, size, res);
        }

        /// \brief Pass-back the biggest potential input parameter contained within the given range
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the range
        /// \param res will hold the parameter storage description being passed back
        /// \return \b true if there is at least one parameter contained in the range
        public bool getBiggestContainedInputParam(Address loc, int4 size, VarnodeData res)
        {
            if (!isDotdotdot())
            {       // If the proto is varargs, go straight to the model
                if ((flags & voidinputlock) != 0) return false;
                int4 num = numParams();
                if (num > 0)
                {
                    bool locktest = false;  // Have tested against locked symbol
                    res.size = 0;
                    for (int4 i = 0; i < num; ++i)
                    {
                        ProtoParameter* param = getParam(i);
                        if (!param->isTypeLocked()) continue;
                        locktest = true;
                        Address iaddr = param->getAddress();
                        if (iaddr.containedBy(param->getSize(), loc, size))
                        {
                            if (param->getSize() > res.size)
                            {
                                res.space = iaddr.getSpace();
                                res.offset = iaddr.getOffset();
                                res.size = param->getSize();
                            }
                        }
                    }
                    if (locktest)
                        return (res.size == 0);
                }
            }
            return model->getBiggestContainedInputParam(loc, size, res);
        }

        /// \brief Pass-back the biggest potential output storage location contained within the given range
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the range
        /// \param res will hold the output storage description being passed back
        /// \return \b true if there is at least one possible output contained in the range
        public bool getBiggestContainedOutput(Address loc, int4 size, VarnodeData res)
        {
            if (isOutputLocked())
            {
                ProtoParameter* outparam = getOutput();
                if (outparam->getType()->getMetatype() == TYPE_VOID)
                    return false;
                Address iaddr = outparam->getAddress();
                if (iaddr.containedBy(outparam->getSize(), loc, size))
                {
                    res.space = iaddr.getSpace();
                    res.offset = iaddr.getOffset();
                    res.size = outparam->getSize();
                    return true;
                }
                return false;
            }
            return model->getBiggestContainedOutput(loc, size, res);
        }

        /// Get the storage location associated with the "this" pointer
        /// A likely pointer data-type for "this" pointer is passed in, which can be pointer to void. As the
        /// storage of "this" may depend on the full prototype, if the prototype is not already locked in, we
        /// assume the prototype returns void and takes the given data-type as the single input parameter.
        /// \param dt is the given input data-type
        /// \return the starting address of storage for the "this" pointer
        public Address getThisPointerStorage(Datatype dt)
        {
            if (!model->hasThisPointer())
                return Address();
            vector<Datatype*> typelist;
            typelist.push_back(getOutputType());
            typelist.push_back(dt);
            vector<ParameterPieces> res;
            model->assignParameterStorage(typelist, res, true);
            for (int4 i = 1; i < res.size(); ++i)
            {
                if ((res[i].flags & ParameterPieces::hiddenretparm) != 0) continue;
                return res[i].addr;
            }
            return Address();
        }

        /// \brief Decide if \b this can be safely restricted to match another prototype
        ///
        /// Do \b this and another given function prototype share enough of
        /// their model, that if we restrict \b this to the other prototype, we know
        /// we won't miss data-flow.
        /// \param op2 is the other restricting prototype
        /// \return \b true if the two prototypes are compatible enough to restrict
        public bool isCompatible(FuncProto op2)
        {
            if (!model->isCompatible(op2.model)) return false;
            if (op2.isOutputLocked())
            {
                if (isOutputLocked())
                {
                    ProtoParameter* out1 = store->getOutput();
                    ProtoParameter* out2 = op2.store->getOutput();
                    if (*out1 != *out2) return false;
                }
            }
            if ((extrapop != ProtoModel::extrapop_unknown) &&
                (extrapop != op2.extrapop)) return false;
            if (isDotdotdot() != op2.isDotdotdot())
            { // Mismatch in varargs
                if (op2.isDotdotdot())
                {
                    // If -this- is a generic prototype, then the trials
                    // are still setup to recover varargs even though
                    // the prototype hasn't been marked as varargs
                    if (isInputLocked()) return false;
                }
                else
                    return false;
            }

            if (injectid != op2.injectid) return false;
            if ((flags & (is_inline | no_return)) != (op2.flags & (is_inline | no_return)))
                return false;
            if (effectlist.size() != op2.effectlist.size()) return false;
            for (int4 i = 0; i < effectlist.size(); ++i)
                if (effectlist[i] != op2.effectlist[i]) return false;

            if (likelytrash.size() != op2.likelytrash.size()) return false;
            for (int4 i = 0; i < likelytrash.size(); ++i)
                if (likelytrash[i] != op2.likelytrash[i]) return false;
            return true;
        }

        /// Get the \e stack address space
        public AddrSpace getSpacebase() => model->getSpacebase();

        /// \brief Print \b this prototype as a single line of text
        ///
        /// \param funcname is an identifier of the function using \b this prototype
        /// \param s is the output stream
        public void printRaw(string funcname, TextWriter s)
        {
            if (model != (ProtoModel*)0)
                s << model->getName() << ' ';
            else
                s << "(no model) ";
            getOutputType()->printRaw(s);
            s << ' ' << funcname << '(';
            int4 num = numParams();
            for (int4 i = 0; i < num; ++i)
            {
                if (i != 0)
                    s << ',';
                getParam(i)->getType()->printRaw(s);
            }
            if (isDotdotdot())
            {
                if (num != 0)
                    s << ',';
                s << "...";
            }
            s << ") extrapop=" << dec << extrapop;
        }

        /// \brief Get the comparable properties of \b this prototype
        ///
        /// Get properties not including locking, error, and inlining flags.
        /// \return the active set of flags for \b this prototype
        public uint4 getComparableFlags() => (flags & (dotdotdot | is_constructor | is_destructor | has_thisptr ));

        /// \brief Encode \b this to a stream as a \<prototype> element.
        ///
        /// Save everything under the control of this prototype, which
        /// may \e not include input parameters, as these are typically
        /// controlled by the function's symbol table scope.
        /// \param encoder is the stream encoder
        public void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_PROTOTYPE);
            encoder.writeString(ATTRIB_MODEL, model->getName());
            if (extrapop == ProtoModel::extrapop_unknown)
                encoder.writeString(ATTRIB_EXTRAPOP, "unknown");
            else
                encoder.writeSignedInteger(ATTRIB_EXTRAPOP, extrapop);
            if (isDotdotdot())
                encoder.writeBool(ATTRIB_DOTDOTDOT, true);
            if (isModelLocked())
                encoder.writeBool(ATTRIB_MODELLOCK, true);
            if ((flags & voidinputlock) != 0)
                encoder.writeBool(ATTRIB_VOIDLOCK, true);
            if (isInline())
                encoder.writeBool(ATTRIB_INLINE, true);
            if (isNoReturn())
                encoder.writeBool(ATTRIB_NORETURN, true);
            if (hasCustomStorage())
                encoder.writeBool(ATTRIB_CUSTOM, true);
            if (isConstructor())
                encoder.writeBool(ATTRIB_CONSTRUCTOR, true);
            if (isDestructor())
                encoder.writeBool(ATTRIB_DESTRUCTOR, true);
            ProtoParameter* outparam = store->getOutput();
            encoder.openElement(ELEM_RETURNSYM);
            if (outparam->isTypeLocked())
                encoder.writeBool(ATTRIB_TYPELOCK, true);
            outparam->getAddress().encode(encoder, outparam->getSize());
            outparam->getType()->encode(encoder);
            encoder.closeElement(ELEM_RETURNSYM);
            encodeEffect(encoder);
            encodeLikelyTrash(encoder);
            if (injectid >= 0)
            {
                Architecture* glb = model->getArch();
                encoder.openElement(ELEM_INJECT);
                encoder.writeString(ATTRIB_CONTENT, glb->pcodeinjectlib->getCallFixupName(injectid));
                encoder.closeElement(ELEM_INJECT);
            }
            store->encode(encoder);     // Store any internally backed prototyped symbols
            encoder.closeElement(ELEM_PROTOTYPE);
        }

        /// \brief Restore \b this from a \<prototype> element in the given stream
        ///
        /// The backing store for the parameters must already be established using either
        /// setStore() or setInternal().
        /// \param decoder is the given stream decoder
        /// \param glb is the Architecture owning the prototype
        public void decode(Decoder decoder, Architecture glb)
        {
            // Model must be set first
            if (store == (ProtoStore*)0)
                throw LowlevelError("Prototype storage must be set before restoring FuncProto");
            ProtoModel* mod = (ProtoModel*)0;
            bool seenextrapop = false;
            int4 readextrapop;
            flags = 0;
            injectid = -1;
            uint4 elemId = decoder.openElement(ELEM_PROTOTYPE);
            for (; ; )
            {
                uint4 attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_MODEL)
                {
                    string modelname = decoder.readString();
                    if (modelname.size() == 0 || modelname == "default")
                        mod = glb->defaultfp;   // Use the default model
                    else
                    {
                        mod = glb->getModel(modelname);
                        if (mod == (ProtoModel*)0)  // Model name is unrecognized
                            mod = glb->createUnknownModel(modelname);   // Create model with placeholder behavior
                    }
                }
                else if (attribId == ATTRIB_EXTRAPOP)
                {
                    seenextrapop = true;
                    readextrapop = decoder.readSignedIntegerExpectString("unknown", ProtoModel::extrapop_unknown);
                }
                else if (attribId == ATTRIB_MODELLOCK)
                {
                    if (decoder.readBool())
                        flags |= modellock;
                }
                else if (attribId == ATTRIB_DOTDOTDOT)
                {
                    if (decoder.readBool())
                        flags |= dotdotdot;
                }
                else if (attribId == ATTRIB_VOIDLOCK)
                {
                    if (decoder.readBool())
                        flags |= voidinputlock;
                }
                else if (attribId == ATTRIB_INLINE)
                {
                    if (decoder.readBool())
                        flags |= is_inline;
                }
                else if (attribId == ATTRIB_NORETURN)
                {
                    if (decoder.readBool())
                        flags |= no_return;
                }
                else if (attribId == ATTRIB_CUSTOM)
                {
                    if (decoder.readBool())
                        flags |= custom_storage;
                }
                else if (attribId == ATTRIB_CONSTRUCTOR)
                {
                    if (decoder.readBool())
                        flags |= is_constructor;
                }
                else if (attribId == ATTRIB_DESTRUCTOR)
                {
                    if (decoder.readBool())
                        flags |= is_destructor;
                }
            }
            if (mod != (ProtoModel*)0) // If a model was specified
                setModel(mod);      // This sets extrapop to model default
            if (seenextrapop)       // If explicitly set
                extrapop = readextrapop;

            uint4 subId = decoder.peekElement();
            if (subId != 0)
            {
                ParameterPieces outpieces;
                bool outputlock = false;

                if (subId == ELEM_RETURNSYM)
                {
                    decoder.openElement();
                    for (; ; )
                    {
                        uint4 attribId = decoder.getNextAttributeId();
                        if (attribId == 0) break;
                        if (attribId == ATTRIB_TYPELOCK)
                            outputlock = decoder.readBool();
                    }
                    int4 tmpsize;
                    outpieces.addr = Address::decode(decoder, tmpsize);
                    outpieces.type = glb->types->decodeType(decoder);
                    outpieces.flags = 0;
                    decoder.closeElement(subId);
                }
                else if (subId == ELEM_ADDR)
                { // Old-style specification of return (supported partially for backward compat)
                    int4 tmpsize;
                    outpieces.addr = Address::decode(decoder, tmpsize);
                    outpieces.type = glb->types->decodeType(decoder);
                    outpieces.flags = 0;
                }
                else
                    throw LowlevelError("Missing <returnsym> tag");

                store->setOutput(outpieces); // output may be missing storage at this point but ProtoStore should fillin
                store->getOutput()->setTypeLock(outputlock);
            }
            else
                throw LowlevelError("Missing <returnsym> tag");

            if (((flags & voidinputlock) != 0) || (isOutputLocked()))
                flags |= modellock;

            for (; ; )
            {
                subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ELEM_UNAFFECTED)
                {
                    decoder.openElement();
                    while (decoder.peekElement() != 0)
                    {
                        effectlist.emplace_back();
                        effectlist.back().decode(EffectRecord::unaffected, decoder);
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ELEM_KILLEDBYCALL)
                {
                    decoder.openElement();
                    while (decoder.peekElement() != 0)
                    {
                        effectlist.emplace_back();
                        effectlist.back().decode(EffectRecord::killedbycall, decoder);
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ELEM_RETURNADDRESS)
                {
                    decoder.openElement();
                    while (decoder.peekElement() != 0)
                    {
                        effectlist.emplace_back();
                        effectlist.back().decode(EffectRecord::return_address, decoder);
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ELEM_LIKELYTRASH)
                {
                    decoder.openElement();
                    while (decoder.peekElement() != 0)
                    {
                        likelytrash.emplace_back();
                        likelytrash.back().decode(decoder);
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ELEM_INJECT)
                {
                    decoder.openElement();
                    string injectString = decoder.readString(ATTRIB_CONTENT);
                    injectid = glb->pcodeinjectlib->getPayloadId(InjectPayload::CALLFIXUP_TYPE, injectString);
                    flags |= is_inline;
                    decoder.closeElement(subId);
                }
                else if (subId == ELEM_INTERNALLIST)
                {
                    store->decode(decoder, model);
                }
            }
            decoder.closeElement(elemId);
            decodeEffect();
            decodeLikelyTrash();
            if (!isModelLocked())
            {
                if (isInputLocked())
                    flags |= modellock;
            }
            if (extrapop == ProtoModel::extrapop_unknown)
                resolveExtraPop();

            ProtoParameter* outparam = store->getOutput();
            if ((outparam->getType()->getMetatype() != TYPE_VOID) && outparam->getAddress().isInvalid())
            {
                throw LowlevelError("<returnsym> tag must include a valid storage address");
            }
            updateThisPointer();
        }
    }
}
