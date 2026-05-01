import type { MicroflowGatewayBranchTrace, MicroflowTraceFrame } from "./trace-types";
import { extractGatewayBranchTrace } from "./trace-types";

export interface MicroflowNodeIoViewModel {
  summary: {
    objectId: string;
    nodeKind: string;
    actionKind?: string;
    status: MicroflowTraceFrame["status"];
    durationMs: number;
  };
  input: {
    input: MicroflowTraceFrame["input"];
    actionInput: MicroflowTraceFrame["actionInput"];
    inputVariables: MicroflowTraceFrame["inputVariables"];
  };
  output: {
    output: MicroflowTraceFrame["output"];
    outputVariables: MicroflowTraceFrame["outputVariables"];
    variableDelta: MicroflowTraceFrame["variableDelta"];
  };
  flow: {
    incomingFlowId: MicroflowTraceFrame["incomingFlowId"];
    outgoingFlowId: MicroflowTraceFrame["outgoingFlowId"];
    selectedCaseValue: MicroflowTraceFrame["selectedCaseValue"];
    loopIteration: MicroflowTraceFrame["loopIteration"];
    handoffPayload: MicroflowTraceFrame["handoffPayload"];
    branchTrace: MicroflowGatewayBranchTrace[];
  };
  runtime: {
    evaluatedExpressions: MicroflowTraceFrame["evaluatedExpressions"];
    transactionEffect: MicroflowTraceFrame["transactionEffect"];
    error: MicroflowTraceFrame["error"];
  };
}

export function buildMicroflowNodeIoViewModel(frame: MicroflowTraceFrame): MicroflowNodeIoViewModel {
  return {
    summary: {
      objectId: frame.objectId,
      nodeKind: frame.nodeKind ?? frame.nodeType ?? "node",
      actionKind: frame.actionKind ?? frame.actionId,
      status: frame.status,
      durationMs: frame.durationMs,
    },
    input: {
      input: frame.input,
      actionInput: frame.actionInput,
      inputVariables: frame.inputVariables,
    },
    output: {
      output: frame.output,
      outputVariables: frame.outputVariables,
      variableDelta: frame.variableDelta,
    },
    flow: {
      incomingFlowId: frame.incomingFlowId,
      outgoingFlowId: frame.outgoingFlowId,
      selectedCaseValue: frame.selectedCaseValue,
      loopIteration: frame.loopIteration,
      handoffPayload: frame.handoffPayload,
      branchTrace: extractGatewayBranchTrace(frame),
    },
    runtime: {
      evaluatedExpressions: frame.evaluatedExpressions,
      transactionEffect: frame.transactionEffect,
      error: frame.error,
    },
  };
}
