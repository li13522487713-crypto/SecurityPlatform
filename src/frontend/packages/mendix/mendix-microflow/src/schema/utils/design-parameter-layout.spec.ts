import { describe, expect, it } from "vitest";

import type { MicroflowDesignSchema } from "../types";
import { createMicroflowDesignSchema, createMicroflowWorkflowNode } from "../../flowgram/flowgram-native-schema";
import { alignRootDesignParameterNodesToStart, removeStaleDesignParameters } from "./design-parameter-layout";

function createSchema(): MicroflowDesignSchema {
  const schema = createMicroflowDesignSchema({
    id: "MF_DESIGN_PARAMETER_LAYOUT",
    name: "DesignParameterLayout",
    moduleId: "Sales",
  });
  return {
    ...schema,
    parameters: [
      {
        id: "param-a",
        stableId: "param-a",
        name: "A",
        dataType: { kind: "string" },
        type: { kind: "primitive", name: "string" },
        required: true,
      },
      {
        id: "param-b",
        stableId: "param-b",
        name: "B",
        dataType: { kind: "string" },
        type: { kind: "primitive", name: "string" },
        required: true,
      },
      {
        id: "param-unused",
        stableId: "param-unused",
        name: "Unused",
        dataType: { kind: "string" },
        type: { kind: "primitive", name: "string" },
        required: true,
      },
    ],
    workflow: {
      ...schema.workflow,
      nodes: [
        createMicroflowWorkflowNode({
          id: "start",
          registryKey: "startEvent",
          position: { x: 400, y: 240 },
          title: "Start",
        }) as never,
        createMicroflowWorkflowNode({
          id: "p-b",
          registryKey: "parameter",
          position: { x: 700, y: 40 },
          title: "B",
          data: { parameterId: "param-b", parameterName: "B" },
        }) as never,
        createMicroflowWorkflowNode({
          id: "p-a",
          registryKey: "parameter",
          position: { x: 120, y: 60 },
          title: "A",
          data: { parameterId: "param-a", parameterName: "A" },
        }) as never,
      ],
    },
  };
}

describe("design parameter layout", () => {
  it("aligns root parameter nodes above StartEvent by parameter order", () => {
    const schema = createSchema();
    const aligned = alignRootDesignParameterNodesToStart(schema);
    const positions = aligned.workflow.nodes
      .filter(node => node.type === "parameterObject")
      .map(node => ({ id: node.id, x: Number(node.meta?.position?.x ?? 0), y: Number(node.meta?.position?.y ?? 0) }))
      .sort((a, b) => a.x - b.x);

    expect(positions).toEqual([
      { id: "p-a", x: 340, y: 144 },
      { id: "p-b", x: 460, y: 144 },
    ]);
  });

  it("removes schema parameters that no longer have parameter nodes", () => {
    const schema = createSchema();
    const pruned = removeStaleDesignParameters(schema);
    expect(pruned.parameters.map(parameter => parameter.id)).toEqual(["param-a", "param-b"]);
  });
});

