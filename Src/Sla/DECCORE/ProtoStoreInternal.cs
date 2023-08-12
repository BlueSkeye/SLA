using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A collection of parameter descriptions without backing symbols
    ///
    /// Parameter descriptions are stored internally to the object and are not
    /// mirrored by a symbol table.
    internal class ProtoStoreInternal : ProtoStore
    {
        /// Cached reference to the \b void data-type
        private Datatype voidtype;
        /// Descriptions of input parameters
        private List<ProtoParameter> inparam;
        /// Description of the return value
        private ProtoParameter outparam;

        /// \param vt is the \b void data-type used for an unspecified return value
        public ProtoStoreInternal(Datatype vt)
        {
            voidtype = vt;
            outparam = (ProtoParameter)null;
            ParameterPieces pieces;
            pieces.type = voidtype;
            pieces.flags = 0;
            ProtoStoreInternal::setOutput(pieces);
        }

        ~ProtoStoreInternal()
        {
            if (outparam != (ProtoParameter)null)
                delete outparam;
            for (int i = 0; i < inparam.size(); ++i)
            {
                ProtoParameter* param = inparam[i];
                if (param != (ProtoParameter)null)
                    delete param;
            }
        }

        public override ProtoParameter setInput(int i, string nm, ParameterPieces pieces)
        {
            while (inparam.size() <= i)
                inparam.Add((ProtoParameter)null);
            if (inparam[i] != (ProtoParameter)null)
                delete inparam[i];
            inparam[i] = new ParameterBasic(nm, pieces.addr, pieces.type, pieces.flags);
            return inparam[i];
        }

        public override void clearInput(int i)
        {
            int sz = inparam.size();
            if (i >= sz) return;
            if (inparam[i] != (ProtoParameter)null)
                delete inparam[i];
            inparam[i] = (ProtoParameter)null;
            for (int j = i + 1; j < sz; ++j)
            {   // Renumber parameters with index > i
                inparam[j - 1] = inparam[j];
                inparam[j] = (ProtoParameter)null;
            }
            while (inparam.GetLastItem() == (ProtoParameter)null)
                inparam.RemoveLastItem();
        }

        public override void clearAllInputs()
        {
            for (int i = 0; i < inparam.size(); ++i)
            {
                if (inparam[i] != (ProtoParameter)null)
                    delete inparam[i];
            }
            inparam.clear();
        }

        public override int getNumInputs() => inparam.size();

        public override ProtoParameter getInput(int i)
        {
            if (i >= inparam.size())
                return (ProtoParameter)null;
            return inparam[i];
        }

        public override ProtoParameter setOutput(ParameterPieces piece)
        {
            if (outparam != (ProtoParameter)null)
                delete outparam;
            outparam = new ParameterBasic("", piece.addr, piece.type, piece.flags);
            return outparam;
        }

        public override void clearOutput()
        {
            if (outparam != (ProtoParameter)null)
                delete outparam;
            outparam = new ParameterBasic(voidtype);
        }

        public override ProtoParameter getOutput() => outparam;

        public override ProtoStore clone()
        {
            ProtoStoreInternal* res = new ProtoStoreInternal(voidtype);
            delete res.outparam;
            if (outparam != (ProtoParameter)null)
                res.outparam = outparam.clone();
            else
                res.outparam = (ProtoParameter)null;
            for (int i = 0; i < inparam.size(); ++i)
            {
                ProtoParameter* param = inparam[i];
                if (param != (ProtoParameter)null)
                    param = param.clone();
                res.inparam.Add(param);
            }
            return res;
        }

        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_INTERNALLIST);
            if (outparam != (ProtoParameter)null)
            {
                encoder.openElement(ElementId.ELEM_RETPARAM);
                if (outparam.isTypeLocked())
                    encoder.writeBool(AttributeId.ATTRIB_TYPELOCK, true);
                outparam.getAddress().encode(encoder);
                outparam.getType().encode(encoder);
                encoder.closeElement(ElementId.ELEM_RETPARAM);
            }
            else
            {
                encoder.openElement(ElementId.ELEM_RETPARAM);
                encoder.openElement(ElementId.ELEM_ADDR);
                encoder.closeElement(ElementId.ELEM_ADDR);
                encoder.openElement(ElementId.ELEM_VOID);
                encoder.closeElement(ElementId.ELEM_VOID);
                encoder.closeElement(ElementId.ELEM_RETPARAM);
            }

            for (int i = 0; i < inparam.size(); ++i)
            {
                ProtoParameter* param = inparam[i];
                encoder.openElement(ElementId.ELEM_PARAM);
                if (param.getName().size() != 0)
                    encoder.writeString(AttributeId.ATTRIB_NAME, param.getName());
                if (param.isTypeLocked())
                    encoder.writeBool(AttributeId.ATTRIB_TYPELOCK, true);
                if (param.isNameLocked())
                    encoder.writeBool(AttributeId.ATTRIB_NAMELOCK, true);
                if (param.isThisPointer())
                    encoder.writeBool(AttributeId.ATTRIB_THISPTR, true);
                if (param.isIndirectStorage())
                    encoder.writeBool(AttributeId.ATTRIB_INDIRECTSTORAGE, true);
                if (param.isHiddenReturn())
                    encoder.writeBool(AttributeId.ATTRIB_HIDDENRETPARM, true);
                param.getAddress().encode(encoder);
                param.getType().encode(encoder);
                encoder.closeElement(ElementId.ELEM_PARAM);
            }
            encoder.closeElement(ElementId.ELEM_INTERNALLIST);
        }

        public override void decode(Sla.CORE.Decoder decoder, ProtoModel model)
        {
            Architecture* glb = model.getArch();
            List<ParameterPieces> pieces;
            List<string> namelist;
            bool addressesdetermined = true;

            pieces.Add(ParameterPieces()); // Push on placeholder for output pieces
            namelist.Add("ret");
            pieces.GetLastItem().type = outparam.getType();
            pieces.GetLastItem().flags = 0;
            if (outparam.isTypeLocked())
                pieces.GetLastItem().flags |= ParameterPieces::typelock;
            if (outparam.isIndirectStorage())
                pieces.GetLastItem().flags |= ParameterPieces.Flags.indirectstorage;
            if (outparam.getAddress().isInvalid())
                addressesdetermined = false;

            uint elemId = decoder.openElement(ElementId.ELEM_INTERNALLIST);
            while(true)
            { // This is only the input params
                uint subId = decoder.openElement();        // <retparam> or <param>
                if (subId == 0) break;
                string name;
                uint flags = 0;
                while(true)
                {
                    uint attribId = decoder.getNextAttributeId();
                    if (attribId == 0) break;
                    if (attribId == AttributeId.ATTRIB_NAME)
                        name = decoder.readString();
                    else if (attribId == ATTRIB_TYPELOCK)
                    {
                        if (decoder.readBool())
                            flags |= ParameterPieces::typelock;
                    }
                    else if (attribId == ATTRIB_NAMELOCK)
                    {
                        if (decoder.readBool())
                            flags |= ParameterPieces::namelock;
                    }
                    else if (attribId == ATTRIB_THISPTR)
                    {
                        if (decoder.readBool())
                            flags |= ParameterPieces::isthis;
                    }
                    else if (attribId == ATTRIB_INDIRECTSTORAGE)
                    {
                        if (decoder.readBool())
                            flags |= ParameterPieces.Flags.indirectstorage;
                    }
                    else if (attribId == ATTRIB_HIDDENRETPARM)
                    {
                        if (decoder.readBool())
                            flags |= ParameterPieces.Flags.hiddenretparm;
                    }
                }
                if ((flags & ParameterPieces.Flags.hiddenretparm) == 0)
                    namelist.Add(name);
                pieces.emplace_back();
                ParameterPieces & curparam(pieces.GetLastItem());
                curparam.addr = Address.decode(decoder);
                curparam.type = glb.types.decodeType(decoder);
                curparam.flags = flags;
                if (curparam.addr.isInvalid())
                    addressesdetermined = false;
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
            ProtoParameter* curparam;
            if (!addressesdetermined)
            {
                // If addresses for parameters are not provided, use
                // the model to derive them from type info
                List<Datatype*> typelist;
                for (int i = 0; i < pieces.size(); ++i) // Save off the decoded types
                    typelist.Add(pieces[i].type);
                List<ParameterPieces> addrPieces;
                model.assignParameterStorage(typelist, addrPieces, true);
                addrPieces.swap(pieces);
                uint k = 0;
                for (uint i = 0; i < pieces.size(); ++i)
                {
                    if ((pieces[i].flags & ParameterPieces.Flags.hiddenretparm) != 0)
                        continue;   // Increment i but not k
                    pieces[i].flags = addrPieces[k].flags;      // Use the original flags
                    k = k + 1;
                }
                if (pieces[0].addr.isInvalid())
                {   // If could not get valid storage for output
                    pieces[0].flags &= ~((uint)ParameterPieces::typelock);     // Treat as unlocked void
                }
                curparam = setOutput(pieces[0]);
                curparam.setTypeLock((pieces[0].flags & ParameterPieces::typelock) != 0);
            }
            uint j = 1;
            for (uint i = 1; i < pieces.size(); ++i)
            {
                if ((pieces[i].flags & ParameterPieces.Flags.hiddenretparm) != 0)
                {
                    curparam = setInput(i - 1, "rethidden", pieces[i]);
                    curparam.setTypeLock((pieces[0].flags & ParameterPieces::typelock) != 0);   // Has output's typelock
                    continue;    // increment i but not j
                }
                curparam = setInput(i - 1, namelist[j], pieces[i]);
                curparam.setTypeLock((pieces[i].flags & ParameterPieces::typelock) != 0);
                curparam.setNameLock((pieces[i].flags & ParameterPieces::namelock) != 0);
                j = j + 1;
            }
        }
    }
}
