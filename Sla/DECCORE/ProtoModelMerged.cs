﻿using ghidra;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static ghidra.ProtoModel;

namespace ghidra
{
    /// \brief A prototype model made by merging together other models
    ///
    /// This model serves as a placeholder for multiple models, when the exact model
    /// hasn't been immediately determined. At the time of active parameter recovery
    /// the correct model is selected for the given set of trials
    /// from among the constituent prototype models used to build \b this,
    /// by calling the method selectModel().
    /// Up to this time, \b this serves as a merged form of the models
    /// so that all potential parameter trials will be included in the analysis.  The parameter recovery
    /// for the output part of the model is currently limited, so the constituent models must all share
    /// the same output model, and this part is \e not currently merged.
    internal class ProtoModelMerged : ProtoModel
    {
        private List<ProtoModel> modellist;                  ///< Constituent models being merged

        /// Fold EffectRecords into \b this model
        /// The EffectRecord lists are intersected. Anything in \b this that is not also in the
        /// given EffectRecord list is removed.
        /// \param efflist is the given EffectRecord list
        private void intersectEffects(List<EffectRecord> efflist)
        {
            vector<EffectRecord> newlist;

            int4 i = 0;
            int4 j = 0;
            while ((i < effectlist.size()) && (j < efflist.size()))
            {
                const EffectRecord &eff1(effectlist[i]);
                const EffectRecord &eff2(efflist[j]);

                if (EffectRecord::compareByAddress(eff1, eff2))
                    i += 1;
                else if (EffectRecord::compareByAddress(eff2, eff1))
                    j += 1;
                else
                {
                    if (eff1 == eff2)
                        newlist.push_back(eff1);
                    i += 1;
                    j += 1;
                }
            }
            effectlist.swap(newlist);
        }

        /// Fold \e likelytrash locations into \b this model
        /// The \e likely-trash locations are intersected. Anything in \b this that is not also in the
        /// given \e likely-trash list is removed.
        /// \param trashlist is the given \e likely-trash list
        private void intersectLikelyTrash(List<VarnodeData> trashlist)
        {
            vector<VarnodeData> newlist;

            int4 i = 0;
            int4 j = 0;
            while ((i < likelytrash.size()) && (j < trashlist.size()))
            {
                const VarnodeData &trs1(likelytrash[i]);
                const VarnodeData &trs2(trashlist[j]);

                if (trs1 < trs2)
                    i += 1;
                else if (trs2 < trs1)
                    j += 1;
                else
                {
                    newlist.push_back(trs1);
                    i += 1;
                    j += 1;
                }
            }
            likelytrash = newlist;
        }

        public ProtoModelMerged(Architecture g)
            : base(g)
        {
        }

        ~ProtoModelMerged()
        {
        }

        /// Get the number of constituent models
        public int4 numModels() => modellist.size();

        /// Get the i-th model
        public ProtoModel getModel(int4 i) => modellist[i];

        /// Fold-in an additional prototype model
        /// \param model is the new prototype model to add to the merge
        public void foldIn(ProtoModel model)
        {
            if (model->glb != glb) throw LowlevelError("Mismatched architecture");
            if ((model->input->getType() != ParamList::p_standard) &&
                (model->input->getType() != ParamList::p_register))
                throw LowlevelError("Can only resolve between standard prototype models");
            if (input == (ParamList*)0)
            { // First fold in
                input = new ParamListMerged();
                output = new ParamListStandardOut(*(ParamListStandardOut*)model->output);
                ((ParamListMerged*)input)->foldIn(*(ParamListStandard*)model->input); // Fold in the parameter lists
                extrapop = model->extrapop;
                effectlist = model->effectlist;
                injectUponEntry = model->injectUponEntry;
                injectUponReturn = model->injectUponReturn;
                likelytrash = model->likelytrash;
                localrange = model->localrange;
                paramrange = model->paramrange;
            }
            else
            {
                ((ParamListMerged*)input)->foldIn(*(ParamListStandard*)model->input);
                // We assume here that the output models are the same, but we don't check
                if (extrapop != model->extrapop)
                    extrapop = ProtoModel::extrapop_unknown;
                if ((injectUponEntry != model->injectUponEntry) || (injectUponReturn != model->injectUponReturn))
                    throw LowlevelError("Cannot merge prototype models with different inject ids");
                intersectEffects(model->effectlist);
                intersectLikelyTrash(model->likelytrash);
                // Take the union of the localrange and paramrange
                set<Range>::const_iterator iter;
                for (iter = model->localrange.begin(); iter != model->localrange.end(); ++iter)
                    localrange.insertRange((*iter).getSpace(), (*iter).getFirst(), (*iter).getLast());
                for (iter = model->paramrange.begin(); iter != model->paramrange.end(); ++iter)
                    paramrange.insertRange((*iter).getSpace(), (*iter).getFirst(), (*iter).getLast());
            }
        }

        /// Select the best model given a set of trials
        /// The model that best matches the given set of input parameter trials is
        /// returned. This method currently uses the ScoreProtoModel object to
        /// score the different prototype models.
        /// \param active is the set of parameter trials
        /// \return the prototype model that scores the best
        public ProtoModel selectModel(ParamActive active)
        {
            int4 bestscore = 500;
            int4 bestindex = -1;
            for (int4 i = 0; i < modellist.size(); ++i)
            {
                int4 numtrials = active->getNumTrials();
                ScoreProtoModel scoremodel = new ScoreProtoModel(true, modellist[i], numtrials);
                for (int4 j = 0; j < numtrials; ++j)
                {
                    ParamTrial trial = new ParamTrial(active->getTrial(j));
                    if (trial.isActive())
                        scoremodel.addParameter(trial.getAddress(), trial.getSize());
                }
                scoremodel.doScore();
                int4 score = scoremodel.getScore();
                if (score < bestscore)
                {
                    bestscore = score;
                    bestindex = i;
                    if (bestscore == 0)
                        break;          // Can't get any lower
                }
            }
            if (bestindex >= 0)
                return modellist[bestindex];
            throw LowlevelError("No model matches : missing default");
        }

        public override bool isMerged() => true;

        public override void decode(Decoder decoder)
        {
            uint4 elemId = decoder.openElement(ELEM_RESOLVEPROTOTYPE);
            name = decoder.readString(ATTRIB_NAME);
            for (; ; )
            { // A tag for each merged prototype
                uint4 subId = decoder.openElement();
                if (subId != ELEM_MODEL) break;
                string modelName = decoder.readString(ATTRIB_NAME);
                ProtoModel* mymodel = glb->getModel(modelName);
                if (mymodel == (ProtoModel*)0)
                    throw LowlevelError("Missing prototype model: " + modelName);
                decoder.closeElement(subId);
                foldIn(mymodel);
                modellist.push_back(mymodel);
            }
            decoder.closeElement(elemId);
            ((ParamListMerged*)input)->finalize();
            ((ParamListMerged*)output)->finalize();
        }
    }
}