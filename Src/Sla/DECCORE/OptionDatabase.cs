using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A Dispatcher for possible ArchOption commands
    ///
    /// An \b option \b command is a specific request by a user to change the configuration options
    /// for an Architecture.  This class takes care of dispatching the command to the proper ArchOption
    /// derived class, which does the work of actually modifying the configuration. The command is issued
    /// either through the set() method directly, or via an element handed to the decode() method.
    /// The decode() method expects an \<optionslist> element with one or more children. The child names
    /// match the registered name of the option and have up to three child elements, \<param1>, \<param2> and \<param3>,
    /// whose content is provided as the optional parameters to command.
    internal class OptionDatabase
    {
        /// The Architecture affected by the contained ArchOption
        private Architecture glb;
        /// A map from option id to registered ArchOption instance
        private Dictionary<uint4, ArchOption> optionmap;

        /// Map from ArchOption name to its class instance
        /// To facilitate command parsing, enter the new ArchOption instance into
        /// the map based on its name
        /// \param option is the new ArchOption instance
        private void registerOption(ArchOption option)
        {
            uint4 id = ElementId::find(option->getName());  // Option name must match a known element name
            optionmap[id] = option;
        }

        /// Construct given the owning Architecture
        /// Register all possible ArchOption objects with this database and set-up the parsing map.
        /// \param g is the Architecture owning \b this database
        public OptionDatabase(Architecture g)
        {
            glb = g;
            registerOption(new OptionExtraPop());
            registerOption(new OptionReadOnly());
            registerOption(new OptionIgnoreUnimplemented());
            registerOption(new OptionErrorUnimplemented());
            registerOption(new OptionErrorReinterpreted());
            registerOption(new OptionErrorTooManyInstructions());
            registerOption(new OptionDefaultPrototype());
            registerOption(new OptionInferConstPtr());
            registerOption(new OptionForLoops());
            registerOption(new OptionInline());
            registerOption(new OptionNoReturn());
            registerOption(new OptionStructAlign());
            registerOption(new OptionProtoEval());
            registerOption(new OptionWarning());
            registerOption(new OptionNullPrinting());
            registerOption(new OptionInPlaceOps());
            registerOption(new OptionConventionPrinting());
            registerOption(new OptionNoCastPrinting());
            registerOption(new OptionMaxLineWidth());
            registerOption(new OptionIndentIncrement());
            registerOption(new OptionCommentIndent());
            registerOption(new OptionCommentStyle());
            registerOption(new OptionCommentHeader());
            registerOption(new OptionCommentInstruction());
            registerOption(new OptionIntegerFormat());
            registerOption(new OptionCurrentAction());
            registerOption(new OptionAllowContextSet());
            registerOption(new OptionSetAction());
            registerOption(new OptionSetLanguage());
            registerOption(new OptionJumpTableMax());
            registerOption(new OptionJumpLoad());
            registerOption(new OptionToggleRule());
            registerOption(new OptionAliasBlock());
            registerOption(new OptionMaxInstruction());
            registerOption(new OptionNamespaceStrategy());
            registerOption(new OptionSplitDatatypes());
        }

        ~OptionDatabase()
        {
            map<uint4, ArchOption*>::iterator iter;
            for (iter = optionmap.begin(); iter != optionmap.end(); ++iter)
                delete(*iter).second;
        }

        /// Issue an option command
        /// Perform an \e option \e command directly, given its id and optional parameters
        /// \param nameId is the id of the option
        /// \param p1 is the first optional parameter
        /// \param p2 is the second optional parameter
        /// \param p3 is the third optional parameter
        /// \return the confirmation/failure method after trying to apply the option
        public string set(uint4 nameId, string p1="", string p2="", string p3="")
        {
            map<uint4, ArchOption*>::const_iterator iter;
            iter = optionmap.find(nameId);
            if (iter == optionmap.end())
                throw ParseError("Unknown option");
            ArchOption* opt = (*iter).second;
            return opt->apply(glb, p1, p2, p3);
        }

        ///< Parse and execute a single option element
        /// Scan the name and optional parameters and call method set()
        /// \param decoder is the stream decoder
        public void decodeOne(Decoder decoder)
        {
            string p1, p2, p3;

            uint4 elemId = decoder.openElement();
            uint4 subId = decoder.openElement();
            if (subId == ELEM_PARAM1)
            {
                p1 = decoder.readString(ATTRIB_CONTENT);
                decoder.closeElement(subId);
                subId = decoder.openElement();
                if (subId == ELEM_PARAM2)
                {
                    p2 = decoder.readString(ATTRIB_CONTENT);
                    decoder.closeElement(subId);
                    subId = decoder.openElement();
                    if (subId == ELEM_PARAM3)
                    {
                        p3 = decoder.readString(ATTRIB_CONTENT);
                        decoder.closeElement(subId);
                    }
                }
            }
            else if (subId == 0)
                p1 = decoder.readString(ATTRIB_CONTENT);    // If no children, content is param 1
            decoder.closeElement(elemId);
            set(elemId, p1, p2, p3);
        }

        /// Execute a series of \e option \e commands parsed from a stream
        /// Parse an \<optionslist> element, treating each child as an \e option \e command.
        /// \param decoder is the stream decoder
        public void decode(Decoder decoder)
        {
            uint4 elemId = decoder.openElement(ELEM_OPTIONSLIST);

            while (decoder.peekElement() != 0)
                decodeOne(decoder);
            decoder.closeElement(elemId);
        }
    }
}
