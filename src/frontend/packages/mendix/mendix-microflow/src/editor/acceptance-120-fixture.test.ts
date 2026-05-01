import { describe, expect, it } from "vitest";

import { createMicroflowDesignSchema } from "../flowgram/flowgram-native-schema";
import { toExecutionPlan } from "../runtime/to-execution-plan";
import { validateExecutionPlan } from "../runtime/runtime-execution-plan";
import { buildAcceptance120Schema } from "./acceptance-120-fixture";

describe("MF_AllNodeComplexComputation_Test fixture", () => {
  it("compiles branch, loop, object type, and gateway semantics into a valid ExecutionPlan", () => {
    const schema = buildAcceptance120Schema(createMicroflowDesignSchema({
      id: "mf-all-node-complex-computation",
      name: "MF_AllNodeComplexComputation_Test",
      moduleId: "sales",
    }));
    const plan = toExecutionPlan(schema);
    const validation = validateExecutionPlan(plan);

    expect(validation.valid).toBe(true);
    expect(schema.parameters).toEqual([
      expect.objectContaining({ id: "numbers", name: "numbers", required: true }),
    ]);
    expect(plan.startNodeId).toBe("start");
    expect(plan.endNodeIds).toEqual(["end"]);
    expect(plan.decisionFlows).toEqual(expect.arrayContaining([
      expect.objectContaining({ flowId: "f-decision-true", edgeKind: "decisionCondition" }),
      expect.objectContaining({ flowId: "f-decision-false", edgeKind: "decisionCondition" }),
    ]));
    expect(plan.objectTypeFlows.map(flow => flow.flowId)).toEqual(["f-object-student", "f-object-fallback"]);
    expect(plan.ignoredFlows).toEqual(expect.arrayContaining([
      expect.objectContaining({ flowId: "f-loop-body-break-decision", edgeKind: "loopBody", controlFlow: "ignored" }),
    ]));
    expect(plan.loopCollections.find(loop => loop.loopObjectId === "loop-numbers")).toMatchObject({
      collectionId: "loop-numbers-body",
      nodeIds: expect.arrayContaining(["break-check", "continue-check", "loop-touch"]),
      flowIds: expect.arrayContaining(["f-loop-body-break-decision", "f-break-true", "f-continue-false"]),
    });
    expect(plan.gateways.find(gateway => gateway.objectId === "parallel-fork")).toMatchObject({
      role: "split",
      branchFlowIds: ["f-parallel-a", "f-parallel-b"],
    });
    expect(plan.gateways.find(gateway => gateway.objectId === "inclusive-fork")).toMatchObject({
      role: "split",
      branchFlowIds: ["f-inclusive-a", "f-inclusive-b"],
    });
    expect(plan.flows.find(flow => flow.flowId === "f-inclusive-a")).toMatchObject({
      edgeKind: "sequence",
      caseValues: [expect.objectContaining({ kind: "expression", expression: "$hasFive = true" })],
    });
  });
});
