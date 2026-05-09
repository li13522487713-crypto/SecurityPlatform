// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { MicroflowDesignSchema } from "../schema";
import type { MicroflowRunSession } from "./trace-types";
import { MicroflowTestRunModal } from "./MicroflowTestRunModal";

vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");

  return {
    Button: ({ children, onClick, disabled }: { children?: React.ReactNode; onClick?: () => void; disabled?: boolean }) => (
      <button type="button" onClick={onClick} disabled={disabled}>{children}</button>
    ),
    Card: ({ children }: { children?: React.ReactNode }) => <section>{children}</section>,
    Input: ({ value, placeholder, onChange }: { value?: string; placeholder?: string; onChange?: (value: string) => void }) => (
      <input value={value ?? ""} placeholder={placeholder} onChange={event => onChange?.(event.currentTarget.value)} />
    ),
    Modal: ({ children, visible }: { children?: React.ReactNode; visible?: boolean }) => visible ? <div>{children}</div> : null,
    Select: ({ value }: { value?: string }) => <select value={value ?? ""} onChange={() => {}} />,
    Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Switch: ({ checked, onChange }: { checked?: boolean; onChange?: (checked: boolean) => void }) => (
      <input type="checkbox" checked={Boolean(checked)} onChange={event => onChange?.(event.currentTarget.checked)} />
    ),
    Tag: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    TextArea: ({ value, placeholder, onChange }: { value?: string; placeholder?: string; onChange?: (value: string) => void }) => (
      <textarea value={value ?? ""} placeholder={placeholder} onChange={event => onChange?.(event.currentTarget.value)} />
    ),
    Toast: {
      error: vi.fn(),
      success: vi.fn(),
    },
    Tooltip: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
    Typography: {
      Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    },
  };
});

afterEach(() => {
  cleanup();
});

function designSchema(): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id: "mf-test-run",
    moduleId: "module-1",
    name: "mf-test-run",
    displayName: "Test Run",
    workflow: { nodes: [], edges: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {}, gridEnabled: true, showMiniMap: true },
    parameters: [],
    returnType: "Nothing",
    returnVariableName: "result",
    variables: [],
    validation: { issues: [] },
    audit: {},
  } as unknown as MicroflowDesignSchema;
}

function runSession(): MicroflowRunSession {
  return {
    id: "run-1",
    schemaId: "mf-test-run",
    startedAt: "2026-05-05T10:00:00.000Z",
    endedAt: "2026-05-05T10:00:01.000Z",
    status: "success",
    input: { numbers: [1, 2, 3] },
    output: { total: 6 },
    logs: [],
    variables: [],
    trace: [
      {
        id: "frame-calc",
        runId: "run-1",
        objectId: "calc-1",
        nodeTitle: "计算总数",
        nodeKind: "actionActivity",
        actionKind: "changeVariable",
        status: "success",
        startedAt: "2026-05-05T10:00:00.200Z",
        durationMs: 7,
        input: { expression: "$numbers.sum()" },
        output: { expressionResult: { success: true, value: 6 } },
        outputVariables: {
          total: {
            name: "total",
            type: { kind: "integer" } as never,
            valuePreview: "6",
            rawValue: 6,
          },
        },
        variableDelta: { added: ["total"], changed: [], removed: [] },
      },
    ],
  };
}

describe("MicroflowTestRunModal", () => {
  it("在测试运行输出 JSON 中包含每个节点的计算输出和变量结果", () => {
    render(
      <MicroflowTestRunModal
        visible
        schema={designSchema()}
        lastSession={runSession()}
        onCancel={() => {}}
        onRun={() => {}}
      />,
    );

    const output = screen.getByTestId("microflow-test-run-output-json").textContent ?? "";
    expect(output).toContain("\"traceSummaries\"");
    expect(output).toContain("\"objectId\": \"calc-1\"");
    expect(output).toContain("\"expressionResult\"");
    expect(output).toContain("\"value\": 6");
    expect(output).toContain("\"outputVariables\"");
    expect(output).toContain("\"rawValue\": 6");
    expect(output).toContain("\"variableDelta\"");
  });
});
