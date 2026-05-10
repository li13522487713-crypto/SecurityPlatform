import { describe, expect, it } from "vitest";
import type { WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

import {
  flattenFlowGramWorkflowForPersistence,
  nestLoopChildrenForFlowGram,
} from "./flowgram-native-schema";

function node(id: string, kind: string, x: number, y: number, parentObjectId?: string) {
  return {
    id,
    type: kind,
    data: {
      objectId: id,
      objectKind: kind,
      collectionId: parentObjectId ? "loop-body" : "root-collection",
      parentObjectId,
      title: id,
      officialType: kind,
      disabled: false,
      validationState: "valid",
      runtimeState: "idle",
      issueCount: 0,
    },
    meta: {
      position: { x, y },
      collectionId: parentObjectId ? "loop-body" : "root-collection",
      parentObjectId,
    },
  };
}

describe("flowgram native schema loop parenting", () => {
  it("nests loop body nodes into FlowGram blocks with parent extent", () => {
    const workflow: WorkflowJSON = {
      nodes: [
        node("start", "startEvent", 80, 240),
        node("loop", "loopedActivity", 300, 240),
        node("change-variable", "actionActivity", 300, 360, "loop"),
        node("break-event", "breakEvent", 420, 420, "loop"),
      ],
      edges: [],
    };

    const nested = nestLoopChildrenForFlowGram(workflow);
    const loop = nested.nodes.find(item => item.id === "loop") as typeof nested.nodes[number] & {
      blocks?: Array<{ id: string; parentNode?: string; extent?: string; meta?: { position?: { x: number; y: number } } }>;
    };

    expect(nested.nodes.map(item => item.id)).toEqual(["start", "loop"]);
    expect(loop.blocks?.map(item => item.id)).toEqual(["change-variable", "break-event"]);
    expect(loop.blocks?.[0]).toMatchObject({
      parentNode: "loop",
      extent: "parent",
      meta: { position: { x: 0, y: 120 }, parentObjectId: "loop" },
    });
  });

  it("flattens FlowGram block coordinates back to root canvas coordinates", () => {
    const nested = {
      nodes: [
        {
          ...node("loop", "loopedActivity", 300, 240),
          blocks: [
            {
              ...node("continue-event", "continueEvent", 80, 160, "loop"),
              parentNode: "loop",
              extent: "parent",
            },
          ],
        },
      ],
      edges: [],
    } as unknown as WorkflowJSON;

    const flattened = flattenFlowGramWorkflowForPersistence(nested);
    const child = flattened.nodes.find(item => item.id === "continue-event");

    expect(flattened.nodes.map(item => item.id)).toEqual(["loop", "continue-event"]);
    expect(child?.meta?.position).toEqual({ x: 380, y: 400 });
    expect(child?.meta?.parentObjectId).toBe("loop");
    expect((child as { parentNode?: string; extent?: string }).parentNode).toBeUndefined();
    expect((child as { parentNode?: string; extent?: string }).extent).toBeUndefined();
  });

  it("clears stale parent metadata when a FlowGram tree emits the node at root level", () => {
    const workflow = {
      nodes: [
        {
          ...node("loop", "loopedActivity", 300, 240),
          blocks: [node("inner", "actionActivity", 10, 20, "loop")],
        },
        node("moved-out", "actionActivity", 500, 240, "loop"),
      ],
      edges: [],
    } as unknown as WorkflowJSON;

    const flattened = flattenFlowGramWorkflowForPersistence(workflow);
    const movedOut = flattened.nodes.find(item => item.id === "moved-out");

    expect(movedOut?.meta?.parentObjectId).toBeUndefined();
    expect((movedOut?.data as { parentObjectId?: string } | undefined)?.parentObjectId).toBeUndefined();
  });
});
