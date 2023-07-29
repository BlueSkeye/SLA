using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Deal with situations that look like double precision parameters
    ///
    /// Check each sub-function for parameter concatenation situations:
    ///    - if the sub-function is in the middle of parameter recovery, check if the CONCAT
    ///         is an artifact of the heritage process and arbitrarily grouping parameters together.
    ///    - if the CONCAT is correct, producing a locked double precision parameter, make
    ///         sure the pieces are properly labeled.
    internal class ActionParamDouble : Action
    {
        public ActionParamDouble(string g)
            : base(0, "paramdouble", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionParamDouble(getGroup());
        }

        public override int apply(Funcdata data)
        {
            for (int4 i = 0; i < data.numCalls(); ++i)
            {
                FuncCallSpecs* fc = data.getCallSpecs(i);
                PcodeOp* op = fc->getOp();
                if (fc->isInputActive())
                {
                    ParamActive* active = fc->getActiveInput();
                    for (int4 j = 0; j < active->getNumTrials(); ++j)
                    {
                        ParamTrial paramtrial = active->getTrial(j);
                        if (paramtrial.isChecked()) continue;
                        if (paramtrial.isUnref()) continue;
                        AddrSpace* spc = paramtrial.getAddress().getSpace();
                        if (spc->getType() != IPTR_SPACEBASE) continue;
                        int4 slot = paramtrial.getSlot();
                        Varnode* vn = op->getIn(slot);
                        if (!vn->isWritten()) continue;
                        PcodeOp* concatop = vn->getDef();
                        if (concatop->code() != CPUI_PIECE) continue;
                        if (!fc->hasModel()) continue;
                        Varnode* mostvn = concatop->getIn(0);
                        Varnode* leastvn = concatop->getIn(1);
                        int4 splitsize = spc->isBigEndian() ? mostvn->getSize() : leastvn->getSize();
                        if (fc->checkInputSplit(paramtrial.getAddress(), paramtrial.getSize(), splitsize))
                        {
                            active->splitTrial(j, splitsize);
                            if (spc->isBigEndian())
                            {
                                data.opInsertInput(op, mostvn, slot);
                                data.opSetInput(op, leastvn, slot + 1);
                            }
                            else
                            {
                                data.opInsertInput(op, leastvn, slot);
                                data.opSetInput(op, mostvn, slot + 1);
                            }
                            count += 1;     // Indicate that a change was made

                            j -= 1; // Note we decrement j here, so that we can check nested CONCATs
                        }
                    }
                }
                else if ((!fc->isInputLocked()) && (data.isDoublePrecisOn()))
                {
                    // Search for double precision objects that might become params
                    int4 max = op->numInput() - 1;
                    // Look for adjacent slots that form pieces of a double precision whole
                    for (int4 j = 1; j < max; ++j)
                    {
                        Varnode* vn1 = op->getIn(j);
                        Varnode* vn2 = op->getIn(j + 1);
                        SplitVarnode whole;
                        bool isslothi;
                        if (whole.inHandHi(vn1))
                        {
                            if (whole.getLo() != vn2) continue;
                            isslothi = true;
                        }
                        else if (whole.inHandLo(vn1))
                        {
                            if (whole.getHi() != vn2) continue;
                            isslothi = false;
                        }
                        else
                            continue;
                        if (fc->checkInputJoin(j, isslothi, vn1, vn2))
                        {
                            data.opSetInput(op, whole.getWhole(), j);
                            data.opRemoveInput(op, j + 1);
                            fc->doInputJoin(j, isslothi);
                            max = op->numInput() - 1;
                            count += 1;
                        }
                    }
                }
            }


            FuncProto fp = data.getFuncProto();
            if (fp.isInputLocked() && data.isDoublePrecisOn())
            {
                // Search for locked parameters that are being split into hi and lo components
                vector<Varnode*> lovec;
                vector<Varnode*> hivec;
                int4 minDoubleSize = data.getArch()->getDefaultSize();  // Minimum size to consider
                int4 numparams = fp.numParams();
                for (int4 i = 0; i < numparams; ++i)
                {
                    ProtoParameter* param = fp.getParam(i);
                    Datatype* tp = param->getType();
                    type_metatype mt = tp->getMetatype();
                    if ((mt == TYPE_ARRAY) || (mt == TYPE_STRUCT)) continue; // Not double precision objects
                    Varnode* vn = data.findVarnodeInput(tp->getSize(), param->getAddress());
                    if (vn == (Varnode*)0) continue;
                    if (vn->getSize() < minDoubleSize) continue;
                    int4 halfSize = vn->getSize() / 2;
                    lovec.clear();
                    hivec.clear();
                    bool otherUse = false;      // Have we seen use other than splitting into hi and lo
                    list<PcodeOp*>::const_iterator iter, enditer;
                    iter = vn->beginDescend();
                    enditer = vn->endDescend();
                    while (iter != enditer)
                    {
                        PcodeOp* subop = *iter;
                        ++iter;
                        if (subop->code() != CPUI_SUBPIECE) continue;
                        Varnode* outvn = subop->getOut();
                        if (outvn->getSize() != halfSize) continue;
                        if (subop->getIn(1)->getOffset() == 0)  // Possible lo precision piece
                            lovec.push_back(outvn);
                        else if (subop->getIn(1)->getOffset() == halfSize)  // Possible hi precision piece
                            hivec.push_back(outvn);
                        else
                        {
                            otherUse = true;
                            break;
                        }
                    }
                    if ((!otherUse) && (!lovec.empty()) && (!hivec.empty()))
                    {   // Seen (only) hi and lo uses
                        for (int4 j = 0; j < lovec.size(); ++j)
                        {
                            Varnode* piecevn = lovec[j];
                            if (!piecevn->isPrecisLo())
                            {
                                piecevn->setPrecisLo();
                                count += 1;     // Indicate we made change
                            }
                        }
                        for (int4 j = 0; j < hivec.size(); ++j)
                        {
                            Varnode* piecevn = hivec[j];
                            if (!piecevn->isPrecisHi())
                            {
                                piecevn->setPrecisHi();
                                count += 1;
                            }
                        }
                    }
                }
            }
            return 0;
        }
    }
}
