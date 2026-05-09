import { describe, expect, it } from "vitest";

import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowDesignSchema } from "../schema/types";
import { decorateWorkflow, runtimeStateFromTraceStatus } from "./flowgram-workflow-decorate";

function createSchema(): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id: "mf-runtime-inline",
    moduleId: "module-1",
    name: "runtime-inline",
    displayName: "runtime-inline",
    workflow: {
      nodes: [
        {
          id: "decision-1",
          type: "exclusiveSplit",
          data: {
            objectId: "decision-1",
            objectKind: "exclusiveSplit",
            collectionId: "nodes",
            title: "判断",
            validationState: "valid",
            issueCount: 0,
          },
          meta: { position: { x: 120, y: 120 } },
        },
      ],
      edges: [
        {
          id: "flow-true",
          sourceNodeID: "decision-1",
          sourcePortID: "decision:true",
          targetNodeID: "target-1",
          targetPortID: "sequence:in",
          data: { flowId: "flow-true" },
        },
      ],
    },
    editor: {
      viewport: { x: 0, y: 0, zoom: 1 },
      zoom: 1,
      selection: {},
      gridEnabled: true,
      showMiniMap: true,
    },
    parameters: [],
    returnType: "Nothing",
    returnVariableName: "result",
    variables: [],
    validation: { issues: [] },
    audit: {},
  } as unknown as MicroflowDesignSchema;
}

function traceFrame(patch: Partial<MicroflowTraceFrame>): MicroflowTraceFrame {
  return {
    id: "frame-1",
    runId: "run-1",
    objectId: "decision-1",
    status: "success",
    startedAt: "2026-05-05T00:00:00.000Z",
    durationMs: 10,
    ...patch,
  };
}

describe("decorateWorkflow runtime projection", () => {
  it("projects failed node runtime and inline view mode", () => {
    const decorated = decorateWorkflow({
      schema: createSchema(),
      validationIssues: [],
      runtimeTrace: [traceFrame({ status: "failed", error: { message: "boom" } as never })],
      nodeViewModes: { "decision-1": "inspectingError" },
    });
    const node = decorated.nodes?.[0] as { data?: { runtimeState?: string; inlineConfig?: { viewMode?: string } } };
    expect(node.data?.runtimeState).toBe("failed");
    expect(node.data?.inlineConfig?.viewMode).toBe("inspectingError");
  });

  it("projects branch runtime onto edge states", () => {
    const decorated = decorateWorkflow({
      schema: createSchema(),
      validationIssues: [],
      runtimeTrace: [
        traceFrame({
          output: {
            branchTrace: [{ flowId: "flow-true", branchId: "branch-true", selected: true, status: "completed" }],
          },
        }),
      ],
    });
    const edge = decorated.edges?.[0] as { data?: { runtimeState?: string } };
    expect(edge.data?.runtimeState).toBe("selectedCase");
  });

  it("resolves inline view modes by persisted object id aliases", () => {
    const schema = createSchema();
    schema.workflow.nodes = schema.workflow.nodes.map(node => ({
      ...node,
      id: "node-decision-1",
      data: {
        ...(node.data ?? {}),
        objectId: "decision-1",
      },
    }));
    schema.workflow.edges = schema.workflow.edges.map(edge => ({
      ...edge,
      sourceNodeID: "node-decision-1",
    }));

    const decorated = decorateWorkflow({
      schema,
      validationIssues: [],
      runtimeTrace: [],
      nodeViewModes: { "decision-1": "expanded" },
    });
    const node = decorated.nodes?.[0] as { data?: { inlineConfig?: { viewMode?: string } } };
    expect(node.data?.inlineConfig?.viewMode).toBe("expanded");
  });

  it("keeps latest frame status for the same node", () => {
    const decorated = decorateWorkflow({
      schema: createSchema(),
      validationIssues: [],
      runtimeTrace: [
        traceFrame({ status: "failed" }),
        traceFrame({ id: "frame-2", status: "success" }),
      ],
      nodeViewModes: { "decision-1": "running" },
    });
    const node = decorated.nodes?.[0] as { data?: { runtimeState?: string; inlineConfig?: { viewMode?: string } } };
    expect(node.data?.runtimeState).toBe("success");
    expect(node.data?.inlineConfig?.viewMode).toBe("running");
  });
});

describe("runtimeStateFromTraceStatus", () => {
  it("maps supported statuses to renderer runtime states", () => {
    expect(runtimeStateFromTraceStatus("running")).toBe("running");
    expect(runtimeStateFromTraceStatus("failed")).toBe("failed");
    expect(runtimeStateFromTraceStatus("unsupported")).toBe("unsupported");
    expect(runtimeStateFromTraceStatus(undefined)).toBe("idle");
  });
});
