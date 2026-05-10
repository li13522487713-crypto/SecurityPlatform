import { describe, expect, it } from "vitest";

import type { MicroflowDesignSchema, MicroflowWorkflowEdgeJSON } from "../schema/types";
import { stripTransientDesignSchema, stripTransientWorkflowState } from "./transient-workflow-state";

describe("transient workflow state", () => {
  it("removes canvas-only node and edge state before persistence", () => {
    const workflow = stripTransientWorkflowState({
      nodes: [
        {
          id: "node-1",
          type: "createVariable",
          data: {
            objectId: "node-1",
            objectKind: "createVariable",
            title: "Create variable",
            inlineConfig: { viewMode: "expanded", summaryLines: [], sections: [] },
            runtimeState: "success",
            runtimeErrorCode: "ERR",
            runtimeErrorMessage: "boom",
            validationState: "error",
            issueCount: 7,
          },
        },
      ],
      edges: [
        {
          id: "flow-1",
          sourceNodeID: "node-1",
          targetNodeID: "node-2",
          data: {
            flowId: "flow-1",
            runtimeState: "selectedCase",
            validationState: "warning",
            sourceNodeId: "node-1",
            sourceObjectKind: "createVariable",
            targetNodeId: "node-2",
            targetObjectKind: "changeVariable",
          },
        },
      ],
    });

    expect(workflow.nodes?.[0]?.data).toEqual({
      objectId: "node-1",
      objectKind: "createVariable",
      title: "Create variable",
    });
    const edge = workflow.edges?.[0] as MicroflowWorkflowEdgeJSON | undefined;
    expect(edge?.data).toEqual({
      flowId: "flow-1",
    });
  });

  it("keeps design schema stable when a node is only expanded for editing", () => {
    const schema = {
      schemaVersion: "flowgram.microflow.v1",
      id: "mf-1",
      moduleId: "module-1",
      name: "ag",
      displayName: "ag",
      workflow: {
        nodes: [
          {
            id: "node-1",
            type: "createVariable",
            data: {
              objectId: "node-1",
              objectKind: "createVariable",
              title: "Create variable",
              inlineConfig: { viewMode: "editing", summaryLines: [], sections: [] },
            },
          },
        ],
        edges: [],
      },
      editor: { viewport: { x: 0, y: 0, zoom: 1 }, selection: {} },
      parameters: [],
      returnType: "Nothing",
      variables: [],
      validation: { issues: [] },
      audit: {},
    } as unknown as MicroflowDesignSchema;

    const stripped = stripTransientDesignSchema(schema);

    expect(stripped.workflow.nodes[0]?.data).toEqual({
      objectId: "node-1",
      objectKind: "createVariable",
      title: "Create variable",
    });
  });
});
