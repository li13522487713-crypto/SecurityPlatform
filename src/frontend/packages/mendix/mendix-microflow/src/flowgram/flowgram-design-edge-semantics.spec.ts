import { describe, expect, it } from "vitest";
import type { WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";
import {
  canDropRegistryObjectKindInLoop,
  findLoopParentAtPoint,
  isMicroflowDesignEdgeBusinessValid,
  normalizeMicroflowDesignEdges,
} from "./flowgram-design-edge-semantics";

function node(id: string, kind: string, x = 0, y = 0, patch: Record<string, unknown> = {}) {
  const data = patch.data as Record<string, unknown> | undefined;
  const meta = patch.meta as Record<string, unknown> | undefined;
  return {
    id,
    type: kind,
    data: {
      objectId: id,
      objectKind: kind,
      collectionId: "root-collection",
      title: id,
      officialType: kind,
      disabled: false,
      validationState: "valid",
      runtimeState: "idle",
      issueCount: 0,
      ...data,
    },
    meta: {
      position: { x, y },
      size: { width: 104, height: 78 },
      collectionId: "root-collection",
      ...meta,
    },
  };
}

function edge(id: string, source: string, target: string, data: Partial<FlowGramMicroflowEdgeData> = {}) {
  return {
    id,
    sourceNodeID: source,
    targetNodeID: target,
    sourcePortID: data.sourcePortId ?? "out",
    targetPortID: data.targetPortId ?? "in",
    data,
  };
}

describe("flowgram design edge semantics", () => {
  it("assigns boolean decision caseValues in branch order", () => {
    const workflow: WorkflowJSON = {
      nodes: [
        node("decision", "exclusiveSplit"),
        node("yes", "actionActivity"),
        node("no", "actionActivity"),
      ],
      edges: [
        edge("flow-1", "decision", "yes", { edgeKind: "decisionCondition", caseValues: [] }),
        edge("flow-2", "decision", "no", { edgeKind: "decisionCondition", caseValues: [] }),
      ],
    };

    const edges = normalizeMicroflowDesignEdges(workflow);

    expect((edges[0]?.data as unknown as FlowGramMicroflowEdgeData).caseValues[0]).toMatchObject({ kind: "boolean", value: true });
    expect((edges[1]?.data as unknown as FlowGramMicroflowEdgeData).caseValues[0]).toMatchObject({ kind: "boolean", value: false });
  });

  it("preserves non-empty caseValues and rejects duplicate boolean branches", () => {
    const workflow: WorkflowJSON = {
      nodes: [
        node("decision", "exclusiveSplit"),
        node("a", "actionActivity"),
        node("b", "actionActivity"),
      ],
      edges: [
        edge("flow-a", "decision", "a", {
          edgeKind: "decisionCondition",
          caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
        }),
        edge("flow-b", "decision", "b", {
          edgeKind: "decisionCondition",
          caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
        }),
      ],
    };
    const normalized = { ...workflow, edges: normalizeMicroflowDesignEdges(workflow) };

    expect((normalized.edges[0]?.data as unknown as FlowGramMicroflowEdgeData).caseValues[0]).toMatchObject({ kind: "boolean", value: true });
    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[1]!)).toBe(false);
  });

  it("forces canonical orthogonal line kind for edge routing", () => {
    const workflow: WorkflowJSON = {
      nodes: [
        node("root-a", "actionActivity"),
        node("root-b", "actionActivity"),
      ],
      edges: [
        edge("legacy-bezier", "root-a", "root-b", {
          edgeKind: "sequence",
          lineKind: "bezier",
        }),
      ],
    };
    const normalized = { ...workflow, edges: normalizeMicroflowDesignEdges(workflow) };

    expect((normalized.edges[0]?.data as unknown as FlowGramMicroflowEdgeData).lineKind).toBe("orthogonal");
  });

  it("keeps ordinary sequence flows inside one collection and blocks root to loop body crossing", () => {
    const loopChildPatch = {
      data: { collectionId: "loop-1-collection", parentObjectId: "loop-1" },
      meta: { collectionId: "loop-1-collection", parentObjectId: "loop-1" },
    };
    const workflow: WorkflowJSON = {
      nodes: [
        node("root-action", "actionActivity"),
        node("loop-1", "loopedActivity", 200, 200, { meta: { size: { width: 320, height: 190 } } }),
        node("loop-child", "actionActivity", 220, 250, loopChildPatch),
        node("loop-child-2", "actionActivity", 240, 320, loopChildPatch),
      ],
      edges: [
        edge("same-loop", "loop-child", "loop-child-2", { edgeKind: "sequence" }),
        edge("root-to-child", "root-action", "loop-child", { edgeKind: "sequence" }),
        edge("loop-entry", "loop-1", "loop-child", { edgeKind: "loopBody", sourcePortId: "bodyIn" }),
      ],
    };
    const normalized = { ...workflow, edges: normalizeMicroflowDesignEdges(workflow) };

    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[0]!)).toBe(true);
    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[1]!)).toBe(false);
    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[2]!)).toBe(true);
  });

  it("restricts cross-loop boundary flow and allows same-loop sequence", () => {
    const loopA = node("loop-A", "loopedActivity", 240, 200, {
      meta: {
        size: { width: 220, height: 140 },
        collectionId: "root-collection",
      },
    });
    const loopB = node("loop-B", "loopedActivity", 560, 200, {
      meta: {
        size: { width: 220, height: 140 },
        collectionId: "root-collection",
      },
    });
    const loopNodeA = node("in-loop-a", "actionActivity", 260, 220, {
      data: { collectionId: "root-collection", parentObjectId: "loop-A" },
      meta: { collectionId: "root-collection", parentObjectId: "loop-A" },
    });
    const loopNodeB = node("in-loop-b", "actionActivity", 320, 220, {
      data: { collectionId: "root-collection", parentObjectId: "loop-A" },
      meta: { collectionId: "root-collection", parentObjectId: "loop-A" },
    });
    const loopNodeC = node("in-loop-c", "actionActivity", 620, 220, {
      data: { collectionId: "root-collection", parentObjectId: "loop-B" },
      meta: { collectionId: "root-collection", parentObjectId: "loop-B" },
    });
    const rootNode = node("outside", "actionActivity", 80, 60, {
      meta: { collectionId: "root-collection" },
    });

    const workflow: WorkflowJSON = {
      nodes: [loopA, loopB, loopNodeA, loopNodeB, loopNodeC, rootNode],
      edges: [
        edge("inner", "in-loop-a", "in-loop-b", { edgeKind: "sequence" }),
        edge("cross-collection-same", "outside", "in-loop-a", { edgeKind: "sequence" }),
        edge("cross-loop", "in-loop-b", "in-loop-c", { edgeKind: "sequence" }),
        edge("loop-enter", "loop-A", "in-loop-a", { edgeKind: "loopBody", sourcePortId: "bodyIn" }),
      ],
    };
    const normalized = { ...workflow, edges: normalizeMicroflowDesignEdges(workflow) };

    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[0]!)).toBe(true);
    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[1]!)).toBe(false);
    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[2]!)).toBe(false);
    expect(isMicroflowDesignEdgeBusinessValid(normalized, normalized.edges[3]!)).toBe(true);
  });

  it("detects loop drop ownership and loop-only controls", () => {
    const workflow: WorkflowJSON = {
      nodes: [
        node("loop-1", "loopedActivity", 200, 200, { meta: { size: { width: 320, height: 190 } } }),
      ],
      edges: [],
    };

    expect(findLoopParentAtPoint(workflow, { x: 200, y: 200 })).toBe("loop-1");
    expect(canDropRegistryObjectKindInLoop("breakEvent", undefined)).toBe(false);
    expect(canDropRegistryObjectKindInLoop("breakEvent", "loop-1")).toBe(true);
    expect(canDropRegistryObjectKindInLoop("startEvent", "loop-1")).toBe(false);
  });
});
