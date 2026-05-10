import { describe, expect, it } from "vitest";
import { buildRuntimeValueGroups } from "./runtime-value-view-model";
import type { MicroflowTraceFrame } from "./trace-types";

function frame(patch: Partial<MicroflowTraceFrame>): MicroflowTraceFrame {
  return {
    id: "frame-1",
    runId: "run-1",
    objectId: "node-1",
    status: "success",
    startedAt: "2026-05-10T00:00:00Z",
    durationMs: 1,
    ...patch,
  };
}

describe("runtime-value-view-model", () => {
  it("renders primitive output values", () => {
    const groups = buildRuntimeValueGroups(frame({
      outputVariables: {
        out: {
          name: "out",
          type: { kind: "integer" },
          valuePreview: "120",
          rawValueJson: "120",
        },
      },
    }));

    expect(groups.outputs.values[0]?.summary).toBe("out = 120");
    expect(groups.outputs.values[0]?.kind).toBe("primitive");
  });

  it("flattens object fields", () => {
    const groups = buildRuntimeValueGroups(frame({
      outputVariables: {
        order: {
          name: "order",
          type: { kind: "object", entityQualifiedName: "Sales.Order" },
          valuePreview: "{...}",
          rawValueJson: "{\"id\":1,\"customer\":{\"name\":\"Ada\"}}",
        },
      },
    }));

    expect(groups.outputs.values[0]?.kind).toBe("object");
    expect(groups.outputs.values[0]?.fields.map(row => row.path)).toContain("customer.name");
  });

  it("builds list object table columns", () => {
    const groups = buildRuntimeValueGroups(frame({
      outputVariables: {
        result: {
          name: "result",
          type: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } },
          valuePreview: "[...]",
          rawValueJson: "[{\"id\":1,\"score\":80},{\"id\":2,\"name\":\"B\"}]",
        },
      },
    }));

    const list = groups.outputs.values[0]?.list;
    expect(list?.rowCount).toBe(2);
    expect(list?.columns).toEqual(["id", "score", "name"]);
    expect(list?.rows[0]?.score).toBe("80");
  });

  it("builds primitive list table with value column", () => {
    const groups = buildRuntimeValueGroups(frame({
      outputVariables: {
        result: {
          name: "result",
          type: { kind: "list", itemType: { kind: "integer" } },
          valuePreview: "[1,2,3]",
          rawValueJson: "[1,2,3]",
        },
      },
    }));

    const list = groups.outputs.values[0]?.list;
    expect(list?.columns).toEqual(["value"]);
    expect(list?.rows[1]?.value).toBe("2");
  });

  it("falls back to valuePreview when rawValueJson cannot be parsed", () => {
    const groups = buildRuntimeValueGroups(frame({
      outputVariables: {
        bad: {
          name: "bad",
          type: { kind: "string" },
          valuePreview: "raw preview",
          rawValueJson: "{not-json",
        },
      },
    }));

    expect(groups.outputs.values[0]?.summary).toBe("bad = raw preview");
    expect(groups.outputs.values[0]?.json).toBe("{not-json");
  });
});
