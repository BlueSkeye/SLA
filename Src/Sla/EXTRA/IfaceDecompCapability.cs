using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.EXTRA
{
    /// \brief Interface capability point for all decompiler commands
    internal class IfaceDecompCapability : IfaceCapability
    {
        private static IfaceDecompCapability ifaceDecompCapability =
            new IfaceDecompCapability();
        
        private IfaceDecompCapability()
        {
            name = "decomp";
        }

        // private IfaceDecompCapability(IfaceDecompCapability op2);	///< Not implemented

        // private IfaceDecompCapability &operator=(const IfaceDecompCapability &op2);	///< Not implemented

        public override void registerCommands(IfaceStatus status)
        {
            status->registerCom(new IfcComment(), "//"); //Note: A space must follow this when used.
            status->registerCom(new IfcComment(), "#"); //Note: A space must follow this when used.
            status->registerCom(new IfcComment(), "%"); //Note: A space must follow this when used.
            status->registerCom(new IfcQuit(), "quit");
            status->registerCom(new IfcHistory(), "history");
            status->registerCom(new IfcOpenfile(), "openfile", "write");
            status->registerCom(new IfcOpenfileAppend(), "openfile", "append");
            status->registerCom(new IfcClosefile(), "closefile");
            status->registerCom(new IfcEcho(), "echo");

            status->registerCom(new IfcSource(), "source");
            status->registerCom(new IfcOption(), "option");
            status->registerCom(new IfcParseFile(), "parse", "file");
            status->registerCom(new IfcParseLine(), "parse", "line");
            status->registerCom(new IfcAdjustVma(), "adjust", "vma");
            status->registerCom(new IfcFuncload(), "load", "function");
            status->registerCom(new IfcAddrrangeLoad(), "load", "addr");
            status->registerCom(new IfcReadSymbols(), "read", "symbols");
            status->registerCom(new IfcCleararch(), "clear", "architecture");
            status->registerCom(new IfcMapaddress(), "map", "address");
            status->registerCom(new IfcMaphash(), "map", "hash");
            status->registerCom(new IfcMapfunction(), "map", "function");
            status->registerCom(new IfcMapexternalref(), "map", "externalref");
            status->registerCom(new IfcMaplabel(), "map", "label");
            status->registerCom(new IfcMapconvert(), "map", "convert");
            status->registerCom(new IfcMapunionfacet(), "map", "unionfacet");
            status->registerCom(new IfcPrintdisasm(), "disassemble");
            status->registerCom(new IfcDecompile(), "decompile");
            status->registerCom(new IfcDump(), "dump");
            status->registerCom(new IfcDumpbinary(), "binary");
            status->registerCom(new IfcForcegoto(), "force", "goto");
            status->registerCom(new IfcForceFormat(), "force", "varnode");
            status->registerCom(new IfcForceDatatypeFormat(), "force", "datatype");
            status->registerCom(new IfcProtooverride(), "override", "prototype");
            status->registerCom(new IfcJumpOverride(), "override", "jumptable");
            status->registerCom(new IfcFlowOverride(), "override", "flow");
            status->registerCom(new IfcDeadcodedelay(), "deadcode", "delay");
            status->registerCom(new IfcGlobalAdd(), "global", "add");
            status->registerCom(new IfcGlobalRemove(), "global", "remove");
            status->registerCom(new IfcGlobalify(), "global", "spaces");
            status->registerCom(new IfcGlobalRegisters(), "global", "registers");
            status->registerCom(new IfcGraphDataflow(), "graph", "dataflow");
            status->registerCom(new IfcGraphControlflow(), "graph", "controlflow");
            status->registerCom(new IfcGraphDom(), "graph", "dom");
            status->registerCom(new IfcPrintLanguage(), "print", "language");
            status->registerCom(new IfcPrintCStruct(), "print", "C");
            status->registerCom(new IfcPrintCFlat(), "print", "C", "flat");
            status->registerCom(new IfcPrintCGlobals(), "print", "C", "globals");
            status->registerCom(new IfcPrintCTypes(), "print", "C", "types");
            status->registerCom(new IfcPrintCXml(), "print", "C", "xml");
            status->registerCom(new IfcPrintParamMeasures(), "print", "parammeasures");
            status->registerCom(new IfcProduceC(), "produce", "C");
            status->registerCom(new IfcProducePrototypes(), "produce", "prototypes");
            status->registerCom(new IfcPrintRaw(), "print", "raw");
            status->registerCom(new IfcPrintInputs(), "print", "inputs");
            status->registerCom(new IfcPrintInputsAll(), "print", "inputs", "all");
            status->registerCom(new IfcListaction(), "list", "action");
            status->registerCom(new IfcListOverride(), "list", "override");
            status->registerCom(new IfcListprototypes(), "list", "prototypes");
            status->registerCom(new IfcSetcontextrange(), "set", "context");
            status->registerCom(new IfcSettrackedrange(), "set", "track");
            status->registerCom(new IfcBreakstart(), "break", "start");
            status->registerCom(new IfcBreakaction(), "break", "action");
            status->registerCom(new IfcPrintSpaces(), "print", "spaces");
            status->registerCom(new IfcPrintHigh(), "print", "high");
            status->registerCom(new IfcPrintTree(), "print", "tree", "varnode");
            status->registerCom(new IfcPrintBlocktree(), "print", "tree", "block");
            status->registerCom(new IfcPrintLocalrange(), "print", "localrange");
            status->registerCom(new IfcPrintMap(), "print", "map");
            status->registerCom(new IfcPrintVarnode(), "print", "varnode");
            status->registerCom(new IfcPrintCover(), "print", "cover", "high");
            status->registerCom(new IfcVarnodeCover(), "print", "cover", "varnode");
            status->registerCom(new IfcVarnodehighCover(), "print", "cover", "varnodehigh");
            status->registerCom(new IfcPrintExtrapop(), "print", "extrapop");
            status->registerCom(new IfcPrintActionstats(), "print", "actionstats");
            status->registerCom(new IfcResetActionstats(), "reset", "actionstats");
            status->registerCom(new IfcCountPcode(), "count", "pcode");
            status->registerCom(new IfcTypeVarnode(), "type", "varnode");
            status->registerCom(new IfcNameVarnode(), "name", "varnode");
            status->registerCom(new IfcRename(), "rename");
            status->registerCom(new IfcRetype(), "retype");
            status->registerCom(new IfcRemove(), "remove");
            status->registerCom(new IfcIsolate(), "isolate");
            status->registerCom(new IfcLockPrototype(), "prototype", "lock");
            status->registerCom(new IfcUnlockPrototype(), "prototype", "unlock");
            status->registerCom(new IfcCommentInstr(), "comment", "instruction");
            status->registerCom(new IfcDuplicateHash(), "duplicate", "hash");
            status->registerCom(new IfcCallGraphBuild(), "callgraph", "build");
            status->registerCom(new IfcCallGraphBuildQuick(), "callgraph", "build", "quick");
            status->registerCom(new IfcCallGraphDump(), "callgraph", "dump");
            status->registerCom(new IfcCallGraphLoad(), "callgraph", "load");
            status->registerCom(new IfcCallGraphList(), "callgraph", "list");
            status->registerCom(new IfcCallFixup(), "fixup", "call");
            status->registerCom(new IfcCallOtherFixup(), "fixup", "callother");
            status->registerCom(new IfcFixupApply(), "fixup", "apply");
            status->registerCom(new IfcVolatile(), "volatile");
            status->registerCom(new IfcReadonly(), "readonly");
            status->registerCom(new IfcPointerSetting(), "pointer", "setting");
            status->registerCom(new IfcPreferSplit(), "prefersplit");
            status->registerCom(new IfcStructureBlocks(), "structure", "blocks");
            status->registerCom(new IfcAnalyzeRange(), "analyze", "range");
            status->registerCom(new IfcLoadTestFile(), "load", "test", "file");
            status->registerCom(new IfcListTestCommands(), "list", "test", "commands");
            status->registerCom(new IfcExecuteTestCommand(), "execute", "test", "command");
#if CPUI_RULECOMPILE
            status->registerCom(new IfcParseRule(), "parse", "rule");
            status->registerCom(new IfcExperimentalRules(), "experimental", "rules");
#endif
            status->registerCom(new IfcContinue(), "continue");
#if OPACTION_DEBUG
            status->registerCom(new IfcDebugAction(), "debug", "action");
            status->registerCom(new IfcTraceBreak(), "trace", "break");
            status->registerCom(new IfcTraceAddress(), "trace", "address");
            status->registerCom(new IfcTraceEnable(), "trace", "enable");
            status->registerCom(new IfcTraceDisable(), "trace", "disable");
            status->registerCom(new IfcTraceClear(), "trace", "clear");
            status->registerCom(new IfcTraceList(), "trace", "list");
            status->registerCom(new IfcBreakjump(), "break", "jumptable");
#endif
        }
    }
}
