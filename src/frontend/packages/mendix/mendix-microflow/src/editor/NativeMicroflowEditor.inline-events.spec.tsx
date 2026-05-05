// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Toast } from "@douyinfe/semi-ui";

import type { FlowGramMicroflowNativeCanvasProps } from "../flowgram/FlowGramMicroflowNativeCanvas";
import type { MicroflowDesignSchema } from "../schema/types";
import { NativeMicroflowEditor } from "./NativeMicroflowEditor";

let lastCanvasProps: FlowGramMicroflowNativeCanvasProps | undefined;
let lastTestRunModalProps: { onRun?: (input: { parameters: Record<string, unknown>; options?: Record<string, unknown> }) => Promise<void> | void } | undefined;

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, icon, type }: { children?: React.ReactNode; onClick?: (event: React.MouseEvent<HTMLButtonElement>) => void; icon?: React.ReactNode; type?: "button" | "submit" | "reset" }) => (
    <button type={type ?? "button"} onClick={onClick}>{icon}{children}</button>
  ),
  Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Tag: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  Tooltip: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  Typography: {
    Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    Title: ({ children }: { children?: React.ReactNode }) => <h3>{children}</h3>,
  },
  Toast: {
    warning: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconClose: () => <span>x</span>,
  IconDelete: () => <span>del</span>,
  IconPlay: () => <span>run</span>,
  IconRefresh: () => <span>refresh</span>,
  IconSave: () => <span>save</span>,
  IconSetting: () => <span>setting</span>,
  IconTickCircle: () => <span>ok</span>,
  IconUndo: () => <span>undo</span>,
  IconRedo: () => <span>redo</span>,
}));

vi.mock("../flowgram/FlowGramMicroflowNativeCanvas", () => ({
  FlowGramMicroflowNativeCanvas: (props: FlowGramMicroflowNativeCanvasProps) => {
    lastCanvasProps = props;
    return <div data-testid="mock-flowgram-canvas" />;
  },
}));

vi.mock("../node-panel", () => ({
  MicroflowNodePanel: () => <div data-testid="mock-node-panel" />,
}));

vi.mock("../debug/MicroflowTestRunModal", () => ({
  MicroflowTestRunModal: (props: { onRun?: (input: { parameters: Record<string, unknown>; options?: Record<string, unknown> }) => Promise<void> | void }) => {
    lastTestRunModalProps = props;
    return null;
  },
}));

afterEach(() => {
  cleanup();
  lastCanvasProps = undefined;
  lastTestRunModalProps = undefined;
});

function createSchema(): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id: "mf-inline-events",
    moduleId: "module-1",
    name: "mf-inline-events",
    displayName: "mf-inline-events",
    workflow: {
      nodes: [
        {
          id: "start",
          type: "event",
          data: { objectId: "start", objectKind: "startEvent", collectionId: "nodes", title: "Start", validationState: "valid", issueCount: 0 },
          meta: { position: { x: 120, y: 120 } },
        },
        {
          id: "decision",
          type: "decision",
          data: { objectId: "decision", objectKind: "exclusiveSplit", collectionId: "nodes", title: "Decision", validationState: "valid", issueCount: 0 },
          meta: { position: { x: 320, y: 120 } },
        },
        {
          id: "end",
          type: "event",
          data: { objectId: "end", objectKind: "endEvent", collectionId: "nodes", title: "End", validationState: "valid", issueCount: 0 },
          meta: { position: { x: 560, y: 120 } },
        },
      ],
      edges: [
        {
          id: "flow-start-decision",
          sourceNodeID: "start",
          targetNodeID: "decision",
          sourcePortID: "out",
          targetPortID: "in",
          data: { flowId: "flow-start-decision", edgeKind: "sequence" },
        },
        {
          id: "flow-true",
          sourceNodeID: "decision",
          targetNodeID: "end",
          sourcePortID: "decision:true",
          targetPortID: "in",
          caseValues: [{ kind: "boolean", value: true }],
          data: { flowId: "flow-true", edgeKind: "decisionCondition", label: "true" },
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

function createActionSchema(): MicroflowDesignSchema {
  return {
    ...createSchema(),
    workflow: {
      nodes: [
        {
          id: "decision",
          type: "decision",
          data: {
            objectId: "decision",
            objectKind: "exclusiveSplit",
            collectionId: "nodes",
            title: "Decision",
            validationState: "valid",
            issueCount: 0,
            splitCondition: { kind: "expression", expression: { raw: "$riskScore >= 80" } },
          },
          meta: { position: { x: 120, y: 120 } },
        },
        {
          id: "rest-1",
          type: "activity",
          data: {
            objectId: "rest-1",
            objectKind: "actionActivity",
            collectionId: "nodes",
            title: "REST",
            validationState: "valid",
            issueCount: 0,
            actionKind: "restCall",
            action: {
              request: { method: "POST", urlExpression: { raw: "/api/incidents" } },
              response: { handling: { kind: "storeToVariable", outputVariableName: "response" } },
            },
          },
          meta: { position: { x: 320, y: 120 } },
        },
        {
          id: "var-1",
          type: "activity",
          data: {
            objectId: "var-1",
            objectKind: "actionActivity",
            collectionId: "nodes",
            title: "变量",
            validationState: "valid",
            issueCount: 0,
            actionKind: "changeVariable",
            action: { targetVariableName: "approvalStatus", newValueExpression: { raw: "\"pending\"" } },
          },
          meta: { position: { x: 520, y: 120 } },
        },
        {
          id: "call-1",
          type: "activity",
          data: {
            objectId: "call-1",
            objectKind: "actionActivity",
            collectionId: "nodes",
            title: "调用子流程",
            validationState: "valid",
            issueCount: 0,
            actionKind: "callMicroflow",
            action: {
              calledMicroflowId: "CalculateRiskScore",
              argumentMappings: [{ parameterName: "incidentId", valueExpression: { raw: "$incidentId" } }],
              outputVariableName: "riskScore",
            },
          },
          meta: { position: { x: 720, y: 120 } },
        },
      ],
      edges: [],
    },
  } as unknown as MicroflowDesignSchema;
}

function createCompatibilitySchema(): MicroflowDesignSchema {
  const schema = createActionSchema();
  const restNode = schema.workflow.nodes.find(item => item.id === "rest-1");
  if (restNode?.data && typeof restNode.data === "object") {
    const action = ((restNode.data as { action?: Record<string, unknown> }).action ?? {}) as Record<string, unknown>;
    action.request = {
      method: "POST",
      urlExpression: { raw: "/api/request-path" },
    };
    action.restRequest = {
      method: "POST",
      urlExpression: { raw: "/api/rest-request-path" },
    };
    (restNode.data as { action?: Record<string, unknown> }).action = action;
  }
  return schema;
}

function createExtendedInlineSchema(): MicroflowDesignSchema {
  return {
    ...createActionSchema(),
    workflow: {
      nodes: [
        ...createActionSchema().workflow.nodes,
        {
          id: "approval-1",
          type: "activity",
          data: {
            objectId: "approval-1",
            objectKind: "approvalActivity",
            collectionId: "nodes",
            title: "人工审批",
            validationState: "valid",
            issueCount: 0,
            actionKind: "approval",
            action: { approverExpression: { raw: "$manager" }, resultVariableName: "approvalResult" },
          },
          meta: { position: { x: 920, y: 120 } },
        },
        {
          id: "loop-1",
          type: "activity",
          data: {
            objectId: "loop-1",
            objectKind: "loopedActivity",
            collectionId: "nodes",
            title: "循环",
            validationState: "valid",
            issueCount: 0,
            actionKind: "loop",
            action: { iteratorName: "item", collectionExpression: { raw: "$assetList" } },
          },
          meta: { position: { x: 1120, y: 120 } },
        },
        {
          id: "error-1",
          type: "activity",
          data: {
            objectId: "error-1",
            objectKind: "errorHandler",
            collectionId: "nodes",
            title: "错误处理",
            validationState: "valid",
            issueCount: 0,
            actionKind: "errorHandler",
            action: { errorVariableName: "error", catchType: "HTTP_ERROR" },
          },
          meta: { position: { x: 1320, y: 120 } },
        },
      ],
      edges: [
        {
          id: "flow-approval-rejected",
          sourceNodeID: "approval-1",
          targetNodeID: "end",
          sourcePortID: "approval:rejected",
          targetPortID: "in",
          data: { flowId: "flow-approval-rejected", edgeKind: "sequence", label: "rejected" },
        },
        {
          id: "flow-loop-continue",
          sourceNodeID: "loop-1",
          targetNodeID: "end",
          sourcePortID: "loop:continue",
          targetPortID: "in",
          data: { flowId: "flow-loop-continue", edgeKind: "sequence", label: "continue" },
        },
        {
          id: "flow-error-fallback",
          sourceNodeID: "error-1",
          targetNodeID: "end",
          sourcePortID: "error:fallback",
          targetPortID: "in",
          data: { flowId: "flow-error-fallback", edgeKind: "errorHandler", label: "fallback" },
        },
      ],
    },
  } as MicroflowDesignSchema;
}

describe("NativeMicroflowEditor inline events", () => {
  it("keeps bottom dock collapsed by default and after node selection", async () => {
    const onLayoutStateChange = vi.fn();
    render(
      <NativeMicroflowEditor
        schema={createSchema()}
        onLayoutStateChange={onLayoutStateChange}
      />,
    );

    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());
    await waitFor(() => {
      const latest = onLayoutStateChange.mock.calls.at(-1)?.[0] as { bottomDockMode?: string } | undefined;
      expect(latest?.bottomDockMode).toBe("collapsed");
    });
    expect(screen.queryByTestId("microflow-property-panel-legacy-fallback")).toBeNull();

    lastCanvasProps?.onSelectionChange({
      mode: "single",
      objectId: "decision",
      objectIds: ["decision"],
      flowIds: [],
      collectionId: "nodes",
    });
    await waitFor(() => {
      const latest = onLayoutStateChange.mock.calls.at(-1)?.[0] as { bottomDockMode?: string } | undefined;
      expect(latest?.bottomDockMode).toBe("collapsed");
    });
    expect(screen.queryByTestId("microflow-property-panel-legacy-fallback")).toBeNull();
  });

  it("consumes node toggle + field commit + line label commit and updates schema/view mode", async () => {
    const onSchemaChange = vi.fn();
    render(
      <NativeMicroflowEditor
        schema={createSchema()}
        onSchemaChange={onSchemaChange}
      />,
    );

    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-node-toggle", {
      detail: { nodeId: "decision", expanded: true },
    }));
    await waitFor(() => {
      expect(lastCanvasProps?.nodeViewModes?.decision).toBe("expanded");
    });
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-node-toggle", {
      detail: { nodeId: "decision", runtimeNodeId: "node-decision", expanded: false },
    }));
    await waitFor(() => {
      expect(lastCanvasProps?.nodeViewModes?.decision).toBe("compact");
      expect(lastCanvasProps?.nodeViewModes?.["node-decision"]).toBe("compact");
    });

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: {
        nodeId: "end",
        fieldPath: "returnVariableName",
        value: "approvalResult",
        editType: "text",
      },
    }));
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      expect(latestSchema?.returnVariableName).toBe("approvalResult");
    });

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-line-label-commit", {
      detail: { flowId: "flow-true", value: " TRUE " },
    }));
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const flow = (latestSchema?.workflow.edges ?? []).find(item => String((item.data as { flowId?: string } | undefined)?.flowId ?? item.id) === "flow-true");
      expect((flow?.data as { label?: string } | undefined)?.label).toBe("true");
      expect(flow?.sourcePortID).toBe("decision:true");
      expect(flow?.caseValues).toEqual([{ kind: "boolean", value: true }]);
    });
  });

  it("commits custom decision line label without changing the original branch semantics", async () => {
    const onSchemaChange = vi.fn();
    render(
      <NativeMicroflowEditor
        schema={createSchema()}
        onSchemaChange={onSchemaChange}
      />,
    );

    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-line-label-commit", {
      detail: { flowId: "flow-true", value: "High risk case" },
    }));

    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const flow = (latestSchema?.workflow.edges ?? []).find(item => String((item.data as { flowId?: string } | undefined)?.flowId ?? item.id) === "flow-true");
      const sourceNode = latestSchema?.workflow.nodes.find(item => item.id === "decision")?.data as Record<string, unknown> | undefined;
      expect((flow?.data as { label?: string } | undefined)?.label).toBe("High risk case");
      expect(flow?.sourcePortID).toBe("decision:true");
      expect(flow?.caseValues).toEqual([{ kind: "boolean", value: true }]);
      expect((sourceNode?.branchLabels as Record<string, string> | undefined)?.true).toBe("High risk case");
    });
  });

  it("ignores invalid inline field paths and edge paths", async () => {
    const onSchemaChange = vi.fn();
    render(
      <NativeMicroflowEditor
        schema={createSchema()}
        onSchemaChange={onSchemaChange}
      />,
    );

    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "decision", fieldPath: "data.notExists.path", value: "x", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "decision", fieldPath: "edge:flow-true.data.notExists.label", value: "x", editType: "branch" },
    }));

    await waitFor(() => {
      expect(onSchemaChange).toHaveBeenCalledTimes(0);
    });
  });

  it("supports undo/redo after inline field commit", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createActionSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.request.urlExpression.raw", value: "/api/incidents/v2", editType: "http" },
    }));
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      expect((node?.action as { request?: { urlExpression?: { raw?: string } } } | undefined)?.request?.urlExpression?.raw).toBe("/api/incidents/v2");
    });

    screen.getByText("undo").click();
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      expect((node?.action as { request?: { urlExpression?: { raw?: string } } } | undefined)?.request?.urlExpression?.raw).toBe("/api/incidents");
    });

    screen.getByText("redo").click();
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      expect((node?.action as { request?: { urlExpression?: { raw?: string } } } | undefined)?.request?.urlExpression?.raw).toBe("/api/incidents/v2");
    });
  });

  it("applies inline quick-fix field commit through the same schema/history chain", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createActionSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-quick-fix-apply", {
      detail: {
        nodeId: "rest-1",
        suggestionId: "fix-rest-url",
        actionKind: "setFieldValue",
        fieldPath: "data.action.request.urlExpression.raw",
        value: "/api/incidents/fixed",
        editType: "http",
      },
    }));
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      expect((node?.action as { request?: { urlExpression?: { raw?: string } } } | undefined)?.request?.urlExpression?.raw).toBe("/api/incidents/fixed");
    });

    screen.getByText("undo").click();
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      expect((node?.action as { request?: { urlExpression?: { raw?: string } } } | undefined)?.request?.urlExpression?.raw).toBe("/api/incidents");
    });

    screen.getByText("redo").click();
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      expect((node?.action as { request?: { urlExpression?: { raw?: string } } } | undefined)?.request?.urlExpression?.raw).toBe("/api/incidents/fixed");
    });
  });

  it("applies createMissingFlow quick-fix for decision boolean branch with undo/redo", async () => {
    const onSchemaChange = vi.fn();
    const schema = createSchema();
    schema.workflow.edges = (schema.workflow.edges ?? []).filter(edge => String((edge.data as { flowId?: string } | undefined)?.flowId ?? edge.id) !== "flow-true");
    render(<NativeMicroflowEditor schema={schema} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-quick-fix-apply", {
      detail: {
        nodeId: "decision",
        suggestionId: "create-missing-true",
        actionKind: "createMissingFlow",
        value: true,
      },
    }));
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const matched = (latestSchema?.workflow.edges ?? []).some(edge =>
        edge.sourceNodeID === "decision"
        && (Array.isArray(edge.caseValues) ? edge.caseValues : []).some(caseValue => caseValue.kind === "boolean" && caseValue.value === true),
      );
      expect(matched).toBe(true);
    });

    screen.getByText("undo").click();
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const matched = (latestSchema?.workflow.edges ?? []).some(edge =>
        edge.sourceNodeID === "decision"
        && (Array.isArray(edge.caseValues) ? edge.caseValues : []).some(caseValue => caseValue.kind === "boolean" && caseValue.value === true),
      );
      expect(matched).toBe(false);
    });

    screen.getByText("redo").click();
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const matched = (latestSchema?.workflow.edges ?? []).some(edge =>
        edge.sourceNodeID === "decision"
        && (Array.isArray(edge.caseValues) ? edge.caseValues : []).some(caseValue => caseValue.kind === "boolean" && caseValue.value === true),
      );
      expect(matched).toBe(true);
    });
  });

  it("does not mutate schema when createMissingFlow quick-fix has no explicit boolean value", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-quick-fix-apply", {
      detail: {
        nodeId: "decision",
        suggestionId: "create-missing-branch",
        actionKind: "createMissingFlow",
      },
    }));

    await waitFor(() => {
      expect((Toast.warning as unknown as ReturnType<typeof vi.fn>).mock.calls.length).toBeGreaterThan(0);
    });
    expect(onSchemaChange).toHaveBeenCalledTimes(0);
  });

  it("ignores inline field commit when readonly", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createActionSchema()} onSchemaChange={onSchemaChange} readonly />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "decision", fieldPath: "data.splitCondition.expression.raw", value: "$riskScore >= 95", editType: "condition" },
    }));

    await waitFor(() => {
      expect(onSchemaChange).toHaveBeenCalledTimes(0);
    });
  });

  it("projects runtime trace to canvas and marks failed node as inspectingError", async () => {
    const apiClient = {
      testRunMicroflow: vi.fn(async () => ({
        status: "failed",
        session: {
          id: "session-1",
          status: "failed",
          startedAt: "2026-05-05T10:00:00.000Z",
          endedAt: "2026-05-05T10:00:01.000Z",
          input: { riskScore: 92 },
          output: {},
          trace: [
            { objectId: "start", status: "succeeded" },
            { objectId: "decision", status: "failed", durationMs: 12, error: { message: "boom" } },
          ],
        },
      })),
    };
    render(
      <NativeMicroflowEditor
        schema={createSchema()}
        apiClient={apiClient as never}
      />,
    );

    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());
    await waitFor(() => expect(lastTestRunModalProps?.onRun).toBeTypeOf("function"));
    await lastTestRunModalProps?.onRun?.({ parameters: {}, options: {} });

    await waitFor(() => {
      expect(lastCanvasProps?.runtimeTrace.length).toBe(2);
      expect(lastCanvasProps?.nodeViewModes?.decision).toBe("inspectingError");
      expect(lastCanvasProps?.nodeViewModes?.start).toBeUndefined();
    });
  });

  it("keeps bottom dock collapsed after test run and node selection", async () => {
    const onLayoutStateChange = vi.fn();
    const apiClient = {
      testRunMicroflow: vi.fn(async () => ({
        status: "succeeded",
        session: {
          id: "session-keep-collapsed",
          status: "succeeded",
          startedAt: "2026-05-05T10:00:00.000Z",
          endedAt: "2026-05-05T10:00:01.000Z",
          input: {},
          output: {},
          trace: [
            { objectId: "start", status: "succeeded" },
            { objectId: "decision", status: "succeeded" },
          ],
        },
      })),
    };

    render(
      <NativeMicroflowEditor
        schema={createSchema()}
        apiClient={apiClient as never}
        onLayoutStateChange={onLayoutStateChange}
      />,
    );
    await waitFor(() => expect(lastTestRunModalProps?.onRun).toBeTypeOf("function"));
    await lastTestRunModalProps?.onRun?.({ parameters: {}, options: {} });

    lastCanvasProps?.onSelectionChange({
      mode: "single",
      objectId: "decision",
      objectIds: ["decision"],
      flowIds: [],
      collectionId: "nodes",
    });

    await waitFor(() => {
      const latest = onLayoutStateChange.mock.calls.at(-1)?.[0] as { bottomDockMode?: string } | undefined;
      expect(latest?.bottomDockMode).toBe("collapsed");
    });
  });

  it("uses the first failed frame as runtime inspect target", async () => {
    const apiClient = {
      testRunMicroflow: vi.fn(async () => ({
        status: "failed",
        session: {
          id: "session-2",
          status: "failed",
          startedAt: "2026-05-05T10:00:00.000Z",
          endedAt: "2026-05-05T10:00:01.000Z",
          input: {},
          output: {},
          trace: [
            { objectId: "end", status: "failed", error: { message: "end failed" } },
            { objectId: "decision", status: "failed", error: { message: "decision failed" } },
          ],
        },
      })),
    };

    render(
      <NativeMicroflowEditor
        schema={createSchema()}
        apiClient={apiClient as never}
      />,
    );

    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());
    await waitFor(() => expect(lastTestRunModalProps?.onRun).toBeTypeOf("function"));
    await lastTestRunModalProps?.onRun?.({ parameters: {}, options: {} });

    await waitFor(() => {
      expect(lastCanvasProps?.nodeViewModes?.end).toBe("inspectingError");
      expect(lastCanvasProps?.nodeViewModes?.decision).toBe("expanded");
    });
  });

  it("clears failed inspect mode after a subsequent successful run", async () => {
    const apiClient = {
      testRunMicroflow: vi.fn()
        .mockResolvedValueOnce({
          status: "failed",
          session: {
            id: "session-failed",
            status: "failed",
            startedAt: "2026-05-05T10:00:00.000Z",
            endedAt: "2026-05-05T10:00:01.000Z",
            input: {},
            output: {},
            trace: [
              { objectId: "decision", status: "failed", error: { message: "boom" } },
            ],
          },
        })
        .mockResolvedValueOnce({
          status: "succeeded",
          session: {
            id: "session-success",
            status: "succeeded",
            startedAt: "2026-05-05T10:01:00.000Z",
            endedAt: "2026-05-05T10:01:01.000Z",
            input: {},
            output: {},
            trace: [
              { objectId: "start", status: "succeeded" },
              { objectId: "decision", status: "succeeded" },
            ],
          },
        }),
    };

    render(<NativeMicroflowEditor schema={createSchema()} apiClient={apiClient as never} />);
    await waitFor(() => expect(lastTestRunModalProps?.onRun).toBeTypeOf("function"));

    await lastTestRunModalProps?.onRun?.({ parameters: {}, options: {} });
    await waitFor(() => {
      expect(lastCanvasProps?.nodeViewModes?.decision).toBe("inspectingError");
    });

    await lastTestRunModalProps?.onRun?.({ parameters: {}, options: {} });
    await waitFor(() => {
      expect(lastCanvasProps?.nodeViewModes?.start).toBe("inspectingRuntime");
      expect(lastCanvasProps?.nodeViewModes?.decision).not.toBe("inspectingError");
    });
  });

  it("switches inspect mode from error to runtime for the same node", async () => {
    render(<NativeMicroflowEditor schema={createSchema()} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-node-inspect", {
      detail: { nodeId: "decision", inspect: "error" },
    }));
    await waitFor(() => {
      expect(lastCanvasProps?.nodeViewModes?.decision).toBeDefined();
    });

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-node-inspect", {
      detail: { nodeId: "decision", inspect: "runtime" },
    }));
    await waitFor(() => {
      expect(lastCanvasProps?.nodeViewModes?.decision).toBe("inspectingRuntime");
    });
  });

  it("commits decision/rest/variable/callMicroflow inline field paths into schema", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createActionSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "decision", fieldPath: "data.splitCondition.expression.raw", value: "$riskScore >= 90", editType: "condition" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.request.urlExpression.raw", value: "/api/incidents/v2", editType: "http" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.request.queryParameters", value: "incidentId=$incidentId\nseverity=$severity", editType: "mapping" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.request.headers", value: "Authorization=Bearer $token", editType: "mapping" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.request.timeoutMs", value: "186", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "var-1", fieldPath: "data.action.newValueExpression.raw", value: "\"approved\"", editType: "assignment" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "call-1", fieldPath: "data.action.argumentMappings.0.valueExpression.raw", value: "$incidentNo", editType: "mapping" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "call-1", fieldPath: "data.action.targetMicroflowDisplayName", value: "CalculateRiskScoreV2", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "call-1", fieldPath: "data.action.targetMicroflowName", value: "CalculateRiskScore", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "call-1", fieldPath: "data.action.targetMicroflowId", value: "mf-calc-risk-001", editType: "text" },
    }));

    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const nodes = latestSchema?.workflow.nodes ?? [];
      const byId = (id: string) => nodes.find(item => item.id === id)?.data as Record<string, unknown> | undefined;
      const decision = byId("decision");
      const restNode = byId("rest-1");
      const variableNode = byId("var-1");
      const callNode = byId("call-1");
      expect((decision?.splitCondition as { expression?: { raw?: string } } | undefined)?.expression?.raw).toBe("$riskScore >= 90");
      expect((restNode?.action as { request?: { urlExpression?: { raw?: string } } } | undefined)?.request?.urlExpression?.raw).toBe("/api/incidents/v2");
      const restRequest = (restNode?.action as { request?: { queryParameters?: Array<{ key?: string; valueExpression?: { raw?: string } }>; headers?: Array<{ key?: string; valueExpression?: { raw?: string } }>; timeoutMs?: unknown } } | undefined)?.request;
      expect(restRequest?.queryParameters?.[0]?.key).toBe("incidentId");
      expect(restRequest?.queryParameters?.[0]?.valueExpression?.raw).toBe("$incidentId");
      expect(restRequest?.headers?.[0]?.key).toBe("Authorization");
      expect(restRequest?.headers?.[0]?.valueExpression?.raw).toBe("Bearer $token");
      expect(restRequest?.timeoutMs).toBe(186);
      expect((variableNode?.action as { newValueExpression?: { raw?: string } } | undefined)?.newValueExpression?.raw).toBe("\"approved\"");
      expect((callNode?.action as { argumentMappings?: Array<{ valueExpression?: { raw?: string } }> } | undefined)?.argumentMappings?.[0]?.valueExpression?.raw).toBe("$incidentNo");
      const callAction = callNode?.action as { targetMicroflowDisplayName?: string; targetMicroflowName?: string; targetMicroflowId?: string } | undefined;
      expect(callAction?.targetMicroflowDisplayName).toBe("CalculateRiskScoreV2");
      expect(callAction?.targetMicroflowName).toBe("CalculateRiskScore");
      expect(callAction?.targetMicroflowId).toBe("mf-calc-risk-001");
    });
  });

  it("normalizes mapping-like inline values by trimming empty/invalid lines", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createActionSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: {
        nodeId: "rest-1",
        fieldPath: "data.action.request.queryParameters",
        value: "\nincidentId=$incidentId\n=\nseverity=$severity\ninvalidOnlyKey\n",
        editType: "mapping",
      },
    }));

    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const restNode = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      const request = (restNode?.action as { request?: { queryParameters?: Array<{ key?: string; valueExpression?: { raw?: string } }> } } | undefined)?.request;
      const query = request?.queryParameters ?? [];
      expect(query).toHaveLength(3);
      expect(query[0]?.key).toBe("incidentId");
      expect(query[0]?.valueExpression?.raw).toBe("$incidentId");
      expect(query[1]?.key).toBe("severity");
      expect(query[1]?.valueExpression?.raw).toBe("$severity");
      expect(query[2]?.key).toBe("invalidOnlyKey");
      expect(query[2]?.valueExpression?.raw ?? "").toBe("");
    });
  });

  it("updates only targeted request/restRequest path when both compatibility fields coexist", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createCompatibilitySchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.request.urlExpression.raw", value: "/api/request-new", editType: "http" },
    }));
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      const action = (node?.action as { request?: { urlExpression?: { raw?: string } }; restRequest?: { urlExpression?: { raw?: string } } } | undefined);
      expect(action?.request?.urlExpression?.raw).toBe("/api/request-new");
      expect(action?.restRequest?.urlExpression?.raw).toBe("/api/rest-request-path");
    });

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.restRequest.urlExpression.raw", value: "/api/rest-request-new", editType: "http" },
    }));
    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const node = latestSchema?.workflow.nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      const action = (node?.action as { request?: { urlExpression?: { raw?: string } }; restRequest?: { urlExpression?: { raw?: string } } } | undefined);
      expect(action?.request?.urlExpression?.raw).toBe("/api/request-new");
      expect(action?.restRequest?.urlExpression?.raw).toBe("/api/rest-request-new");
    });
  });

  it("commits start/end/approval/loop/error inline field paths into schema", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createExtendedInlineSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "end", fieldPath: "returnVariableName", value: "finalResult", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "approval-1", fieldPath: "data.action.approverExpression.raw", value: "$director", editType: "approval" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "approval-1", fieldPath: "data.description", value: "请确认是否允许继续", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "approval-1", fieldPath: "data.dueTime", value: "PT2H", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "loop-1", fieldPath: "data.action.iteratorName", value: "assetItem", editType: "loop" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "loop-1", fieldPath: "data.resultsVariableName", value: "loopResults", editType: "variable" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "error-1", fieldPath: "data.action.errorVariableName", value: "runtimeError", editType: "text" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "error-1", fieldPath: "data.fallbackResultVariable", value: "fallbackResult", editType: "variable" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "error-1", fieldPath: "data.rethrow", value: "true", editType: "select" },
    }));

    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const nodes = latestSchema?.workflow.nodes ?? [];
      const byId = (id: string) => nodes.find(item => item.id === id)?.data as Record<string, unknown> | undefined;
      const approvalNode = byId("approval-1");
      const loopNode = byId("loop-1");
      const errorNode = byId("error-1");
      expect(latestSchema?.returnVariableName).toBe("finalResult");
      expect((approvalNode?.action as { approverExpression?: { raw?: string } } | undefined)?.approverExpression?.raw).toBe("$director");
      expect(approvalNode?.description).toBe("请确认是否允许继续");
      expect(approvalNode?.dueTime).toBe("PT2H");
      expect((loopNode?.action as { iteratorName?: string } | undefined)?.iteratorName).toBe("assetItem");
      expect(loopNode?.resultsVariableName).toBe("loopResults");
      expect((errorNode?.action as { errorVariableName?: string } | undefined)?.errorVariableName).toBe("runtimeError");
      expect(errorNode?.fallbackResultVariable).toBe("fallbackResult");
      expect(errorNode?.rethrow).toBe(true);
    });
  });

  it("syncs edited line label into source node branchLabels", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createExtendedInlineSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-line-label-commit", {
      detail: { flowId: "flow-approval-rejected", value: "rejected" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-line-label-commit", {
      detail: { flowId: "flow-loop-continue", value: "continue" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-line-label-commit", {
      detail: { flowId: "flow-error-fallback", value: "fallback" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "approval-1", fieldPath: "data.branchLabels.approved", value: "approved", editType: "branch" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "loop-1", fieldPath: "data.branchLabels.done", value: "done", editType: "branch" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "error-1", fieldPath: "data.branchLabels.rethrow", value: "rethrow", editType: "branch" },
    }));

    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const nodes = latestSchema?.workflow.nodes ?? [];
      const approvalNode = nodes.find(item => item.id === "approval-1")?.data as Record<string, unknown> | undefined;
      const loopNode = nodes.find(item => item.id === "loop-1")?.data as Record<string, unknown> | undefined;
      const errorNode = nodes.find(item => item.id === "error-1")?.data as Record<string, unknown> | undefined;
      expect((approvalNode?.branchLabels as Record<string, string> | undefined)?.rejected).toBe("rejected");
      expect((approvalNode?.branchLabels as Record<string, string> | undefined)?.approved).toBe("approved");
      expect((loopNode?.branchLabels as Record<string, string> | undefined)?.continue).toBe("continue");
      expect((loopNode?.branchLabels as Record<string, string> | undefined)?.done).toBe("done");
      expect((errorNode?.branchLabels as Record<string, string> | undefined)?.fallback).toBe("fallback");
      expect((errorNode?.branchLabels as Record<string, string> | undefined)?.rethrow).toBe("rethrow");
    });
  });

  it("commits extra data paths for full-inline fallback fields, including $. expression and json payload", async () => {
    const onSchemaChange = vi.fn();
    render(<NativeMicroflowEditor schema={createActionSchema()} onSchemaChange={onSchemaChange} />);
    await waitFor(() => expect(screen.getByTestId("mock-flowgram-canvas")).not.toBeNull());

    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.responseTransformer.expression.raw", value: "$.response.body.score", editType: "expression" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "rest-1", fieldPath: "data.action.customPayload", value: "{\"ticket\":\"$.response.id\",\"ok\":true}", editType: "json" },
    }));
    window.dispatchEvent(new CustomEvent("atlas:microflow-inline-field-commit", {
      detail: { nodeId: "call-1", fieldPath: "data.action.customMapping.raw", value: "$.riskScore.value", editType: "expression" },
    }));

    await waitFor(() => {
      const latestSchema = onSchemaChange.mock.calls.at(-1)?.[0] as MicroflowDesignSchema | undefined;
      const nodes = latestSchema?.workflow.nodes ?? [];
      const restNode = nodes.find(item => item.id === "rest-1")?.data as Record<string, unknown> | undefined;
      const callNode = nodes.find(item => item.id === "call-1")?.data as Record<string, unknown> | undefined;
      const restAction = restNode?.action as Record<string, unknown> | undefined;
      const callAction = callNode?.action as Record<string, unknown> | undefined;
      expect((((restAction?.responseTransformer as Record<string, unknown> | undefined)?.expression as Record<string, unknown> | undefined)?.raw)).toBe("$.response.body.score");
      expect(restAction?.customPayload).toEqual({ ticket: "$.response.id", ok: true });
      expect(((callAction?.customMapping as Record<string, unknown> | undefined)?.raw)).toBe("$.riskScore.value");
    });
  });
});
