import { describe, expect, it } from "vitest";

import { quickInsertGroupKeyFromItem, resolveIncomingQuickInsertChoices } from "./quick-insert-utils";

describe("quick-insert-utils", () => {
  it("maps registry item to grouped quick-insert categories", () => {
    expect(quickInsertGroupKeyFromItem({ group: "Events" } as never)).toBe("events");
    expect(quickInsertGroupKeyFromItem({ group: "Activities", subgroup: "object" } as never)).toBe("object");
    expect(quickInsertGroupKeyFromItem({ group: "Activities", subgroup: "list" } as never)).toBe("list");
    expect(quickInsertGroupKeyFromItem({ group: "Activities", subgroup: "variable" } as never)).toBe("variable");
    expect(quickInsertGroupKeyFromItem({ group: "Activities", subgroup: "integration" } as never)).toBe("call");
    expect(quickInsertGroupKeyFromItem({ group: "Decisions" } as never)).toBe("flow");
  });

  it("resolves incoming insert edges and filters non-insertable edge kinds", () => {
    const choices = resolveIncomingQuickInsertChoices(
      {
        nodes: [
          { objectId: "source-a", title: "Source A" },
          { objectId: "target" },
        ] as never,
        edges: [
          {
            id: "edge-1",
            flowId: "flow-1",
            sourceNodeId: "node-source-a",
            targetNodeId: "node-target",
            edgeKind: "sequence",
          },
          {
            id: "edge-2",
            flowId: "flow-2",
            sourceNodeId: "node-source-b",
            targetNodeId: "node-target",
            edgeKind: "annotation",
          },
          {
            id: "edge-3",
            flowId: "flow-3",
            sourceNodeId: "node-source-c",
            targetNodeId: "node-target",
            edgeKind: "errorHandler",
          },
          {
            id: "edge-4",
            flowId: "",
            sourceNodeId: "node-source-d",
            targetNodeId: "node-target",
            edgeKind: "sequence",
          },
          {
            id: "edge-5",
            flowId: "flow-5",
            sourceNodeId: "node-source-z",
            targetNodeId: "node-other",
            edgeKind: "sequence",
          },
        ] as never,
      },
      "target",
    );

    expect(choices).toEqual([
      {
        flowId: "flow-1",
        sourceObjectId: "source-a",
        sourceTitle: "Source A",
        edgeKind: "sequence",
      },
    ]);
  });

  it("falls back to source object id when source title is missing", () => {
    const choices = resolveIncomingQuickInsertChoices(
      {
        nodes: [{ objectId: "target" }] as never,
        edges: [
          {
            id: "edge-1",
            flowId: "flow-1",
            sourceNodeId: "node-unknown-source",
            targetNodeId: "node-target",
            edgeKind: "sequence",
          },
        ] as never,
      },
      "target",
    );
    expect(choices[0]?.sourceTitle).toBe("unknown-source");
  });
});
