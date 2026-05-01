import { describe, expect, it } from "vitest";

import type { MicroflowDesignSchema } from "../schema";
import { toExecutionPlan } from "./to-execution-plan";
import { validateExecutionPlan, type MicroflowExecutionPlan } from "./runtime-execution-plan";

describe("microflow execution plan gateway semantics", () => {
  it("compiles gateway descriptors and validates inclusive conditional branches", () => {
    const plan = toExecutionPlan(schema([
      node("start", "startEvent"),
      node("fork", "inclusiveGateway"),
      node("left", "actionActivity"),
      node("right", "actionActivity"),
      node("join", "inclusiveGateway"),
      node("end", "endEvent"),
    ], [
      flow("f1", "start", "fork"),
      conditionFlow("f2", "fork", "left", "1 = 1"),
      conditionFlow("f3", "fork", "right", "2 = 2"),
      flow("f4", "left", "join"),
      flow("f5", "right", "join"),
      flow("f6", "join", "end"),
    ]));

    expect(plan.decisionFlows.map(item => item.flowId)).toEqual(["f2", "f3"]);
    expect(plan.gateways.find(item => item.objectId === "fork")).toMatchObject({
      role: "split",
      incomingFlowIds: ["f1"],
      outgoingFlowIds: ["f2", "f3"],
      branchFlowIds: ["f2", "f3"],
    });
    expect(plan.gateways.find(item => item.objectId === "join")).toMatchObject({
      role: "merge",
      incomingFlowIds: ["f4", "f5"],
      outgoingFlowIds: ["f6"],
    });
    expect(validateExecutionPlan(plan).issues).not.toContainEqual(expect.objectContaining({ code: "RUNTIME_DECISION_FLOW_SOURCE_INVALID" }));
  });

  it("reports invalid gateway descriptors before runtime", () => {
    const plan: MicroflowExecutionPlan = {
      id: "plan-invalid",
      schemaId: "schema-invalid",
      parameters: [],
      variableDeclarations: [],
      actionOutputs: [],
      loopVariables: [],
      systemVariables: [],
      errorContextVariables: [],
      variableScopes: [],
      variableDiagnostics: [],
      nodes: [
        { objectId: "start", kind: "startEvent", officialType: "Microflows$StartEvent", config: { objectKind: "startEvent", officialType: "Microflows$StartEvent" }, supportLevel: "supported", runtimeBehavior: "executable" },
        { objectId: "fork", kind: "parallelGateway", officialType: "Microflows$ParallelGateway", config: { objectKind: "parallelGateway", officialType: "Microflows$ParallelGateway" }, supportLevel: "supported", runtimeBehavior: "executable" },
        { objectId: "end", kind: "endEvent", officialType: "Microflows$EndEvent", config: { objectKind: "endEvent", officialType: "Microflows$EndEvent" }, supportLevel: "supported", runtimeBehavior: "terminal" },
      ],
      flows: [
        { flowId: "f1", kind: "sequence", edgeKind: "sequence", originObjectId: "start", destinationObjectId: "fork", caseValues: [], isErrorHandler: false, controlFlow: "normal" },
        { flowId: "f2", kind: "sequence", edgeKind: "sequence", originObjectId: "fork", destinationObjectId: "end", caseValues: [], isErrorHandler: false, controlFlow: "normal" },
      ],
      normalFlows: [],
      decisionFlows: [],
      objectTypeFlows: [],
      errorHandlerFlows: [],
      loopCollections: [],
      gateways: [
        { objectId: "fork", kind: "parallelGateway", role: "split", incomingFlowIds: ["f1"], outgoingFlowIds: ["f2", "missing-flow"], branchFlowIds: ["f2"] },
        { objectId: "missing-gateway", kind: "parallelGateway", role: "merge", incomingFlowIds: [], outgoingFlowIds: [], branchFlowIds: [] },
      ],
      startNodeId: "start",
      endNodeIds: ["end"],
      metadataRefs: [],
      unsupportedActions: [],
      createdAt: "2026-05-01T00:00:00.000Z",
    };

    expect(validateExecutionPlan(plan).issues).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: "RUNTIME_GATEWAY_FLOW_NOT_FOUND", flowId: "missing-flow" }),
      expect.objectContaining({ code: "RUNTIME_GATEWAY_SPLIT_BRANCH_MISSING", objectId: "fork" }),
      expect.objectContaining({ code: "RUNTIME_GATEWAY_NODE_NOT_FOUND", objectId: "missing-gateway" }),
    ]));
  });
});

function schema(nodes: MicroflowDesignSchema["workflow"]["nodes"], edges: MicroflowDesignSchema["workflow"]["edges"]): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id: "mf-gateway",
    moduleId: "module",
    name: "GatewayPlan",
    displayName: "Gateway Plan",
    workflow: { nodes, edges },
    editor: {} as MicroflowDesignSchema["editor"],
    parameters: [],
    returnType: { kind: "void" },
    variables: [],
    validation: {} as MicroflowDesignSchema["validation"],
    audit: {} as MicroflowDesignSchema["audit"],
  };
}

function node(id: string, kind: string): MicroflowDesignSchema["workflow"]["nodes"][number] {
  return {
    id,
    type: kind,
    data: {
      objectId: id,
      objectKind: kind,
      officialType: `Microflows$${kind}`,
      action: kind === "actionActivity"
        ? { id: `${id}-action`, kind: "createVariable", variableName: `${id}Value`, dataType: { kind: "integer" }, initialValue: "1" }
        : undefined,
    },
  };
}

function flow(id: string, source: string, target: string): MicroflowDesignSchema["workflow"]["edges"][number] {
  return {
    id,
    sourceNodeID: source,
    targetNodeID: target,
    data: {
      flowId: id,
      edgeKind: "sequence",
      caseValues: [],
    },
  };
}

function conditionFlow(id: string, source: string, target: string, expression: string): MicroflowDesignSchema["workflow"]["edges"][number] {
  return {
    id,
    sourceNodeID: source,
    targetNodeID: target,
    data: {
      flowId: id,
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "expression", expression }],
    },
  };
}
