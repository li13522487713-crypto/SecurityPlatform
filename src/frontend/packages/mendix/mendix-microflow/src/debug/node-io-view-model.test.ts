import { describe, expect, it } from "vitest";

import type { MicroflowTraceFrame } from "./trace-types";
import { buildMicroflowNodeIoViewModel } from "./node-io-view-model";

describe("buildMicroflowNodeIoViewModel", () => {
  it("groups every Node I/O debug contract field for the active frame", () => {
    const frame: MicroflowTraceFrame = {
      id: "frame-1",
      runId: "run-1",
      objectId: "gateway-1",
      nodeKind: "inclusiveGateway",
      actionKind: "evaluateBranches",
      incomingFlowId: "flow-in",
      outgoingFlowId: "flow-out",
      selectedCaseValue: { kind: "expression", expression: "$score > 10" },
      loopIteration: {
        loopObjectId: "loop-1",
        index: 2,
        iteratorVariableName: "item",
        iteratorValuePreview: "3",
      },
      status: "success",
      startedAt: "2026-05-02T00:00:00.000Z",
      durationMs: 12,
      input: { sourceList: [1, 2, 3] },
      actionInput: { expression: "$item > 2" },
      inputVariables: {
        item: {
          name: "item",
          type: { kind: "integer" },
          valuePreview: "3",
        },
      },
      evaluatedExpressions: [{ expression: "$score > 10", value: true }],
      output: {
        score: 18,
        branchTrace: [
          { flowId: "flow-a", branchId: "A", targetObjectId: "node-a", selected: true, status: "completed" },
          { flowId: "flow-b", branchId: "B", selected: false, status: "skipped" },
        ],
      },
      outputVariables: {
        score: {
          name: "score",
          type: { kind: "integer" },
          valuePreview: "18",
        },
      },
      variableDelta: { added: ["score"], changed: ["item"], removed: ["old"] },
      handoffPayload: { nextObjectId: "merge-1" },
      transactionEffect: { committed: false, rolledBack: true },
    };

    expect(buildMicroflowNodeIoViewModel(frame)).toEqual({
      summary: {
        objectId: "gateway-1",
        nodeKind: "inclusiveGateway",
        actionKind: "evaluateBranches",
        status: "success",
        durationMs: 12,
      },
      input: {
        input: { sourceList: [1, 2, 3] },
        actionInput: { expression: "$item > 2" },
        inputVariables: frame.inputVariables,
      },
      output: {
        output: frame.output,
        outputVariables: frame.outputVariables,
        variableDelta: { added: ["score"], changed: ["item"], removed: ["old"] },
      },
      flow: {
        incomingFlowId: "flow-in",
        outgoingFlowId: "flow-out",
        selectedCaseValue: { kind: "expression", expression: "$score > 10" },
        loopIteration: frame.loopIteration,
        handoffPayload: { nextObjectId: "merge-1" },
        branchTrace: [
          { flowId: "flow-a", branchId: "A", targetObjectId: "node-a", selected: true, status: "completed" },
          { flowId: "flow-b", branchId: "B", targetObjectId: undefined, selected: false, status: "skipped" },
        ],
      },
      runtime: {
        evaluatedExpressions: [{ expression: "$score > 10", value: true }],
        transactionEffect: { committed: false, rolledBack: true },
        error: undefined,
      },
    });
  });
});
