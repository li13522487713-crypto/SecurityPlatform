import { describe, expect, it } from "vitest";
import { collectFlowsRecursive } from "./object-utils";

describe("collectFlowsRecursive", () => {
  it("tolerates missing objectCollection in authoring payload", () => {
    const schema = {
      flows: [
        {
          id: "flow-1",
          stableId: "flow-1",
          kind: "annotation",
          officialType: "Microflows$Annotation",
          originObjectId: "obj-a",
          destinationObjectId: "obj-b",
          originConnectionIndex: 0,
          destinationConnectionIndex: 0,
          editor: { edgeKind: "annotation", label: "" },
          line: { style: { strokeType: "solid", arrow: "target" } },
        },
      ],
    } as unknown as Record<string, unknown>;

    expect(collectFlowsRecursive(schema)).toHaveLength(1);
  });
});
