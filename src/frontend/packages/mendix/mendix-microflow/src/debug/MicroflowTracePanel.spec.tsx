// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { MicroflowRunSession } from "./trace-types";
import { MicroflowTracePanel } from "./MicroflowTracePanel";

vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");
  const Tabs = ({ children }: { children?: React.ReactNode }) => <div>{children}</div>;
  Tabs.TabPane = ({ tab, children }: { tab?: React.ReactNode; children?: React.ReactNode }) => (
    <section>
      <h3>{tab}</h3>
      {children}
    </section>
  );

  return {
    Button: ({ children, onClick }: { children?: React.ReactNode; onClick?: () => void }) => <button type="button" onClick={onClick}>{children}</button>,
    Card: ({ children }: { children?: React.ReactNode }) => <section>{children}</section>,
    Empty: ({ title, description }: { title?: React.ReactNode; description?: React.ReactNode }) => <div>{title}{description}</div>,
    Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Tabs,
    Tag: ({ children, onClick }: { children?: React.ReactNode; onClick?: () => void }) => (
      <button type="button" onClick={onClick}>{children}</button>
    ),
    Typography: {
      Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    },
  };
});

afterEach(() => {
  cleanup();
});

function createSession(): MicroflowRunSession {
  return {
    id: "run-1",
    schemaId: "schema-1",
    startedAt: "2026-05-05T10:00:00.000Z",
    endedAt: "2026-05-05T10:00:05.000Z",
    status: "failed",
    input: { requestId: "r-1" },
    output: { result: "failed" },
    logs: [],
    variables: [],
    error: {
      code: "RUNTIME_REST_TIMEOUT",
      message: "上游超时",
      objectId: "rest-1",
    },
    trace: [
      {
        id: "frame-start",
        runId: "run-1",
        objectId: "start",
        nodeTitle: "开始",
        nodeKind: "startEvent",
        status: "success",
        startedAt: "2026-05-05T10:00:00.000Z",
        durationMs: 3,
        input: { riskScore: 92, tenantId: "t-1" },
        output: { nextObjectId: "rest-1" },
      },
      {
        id: "frame-rest",
        runId: "run-1",
        objectId: "rest-1",
        nodeTitle: "调用接口",
        nodeKind: "actionActivity",
        incomingFlowId: "flow-in",
        outgoingFlowId: "flow-out",
        status: "failed",
        startedAt: "2026-05-05T10:00:01.000Z",
        durationMs: 12,
        input: { url: "/api/order", timeoutMs: 800 },
        output: { statusCode: 504, calculatedScore: 188, branchTrace: [] },
        outputVariables: {
          calculatedScore: {
            name: "calculatedScore",
            type: { kind: "integer" } as never,
            valuePreview: "188",
            rawValue: 188,
          },
        },
        variableDelta: { added: ["calculatedScore"], changed: [], removed: [] },
        error: {
          code: "RUNTIME_REST_TIMEOUT",
          message: "请求超时",
          objectId: "rest-1",
          flowId: "flow-out",
        },
      },
    ],
  };
}

describe("MicroflowTracePanel", () => {
  it("在执行路径节点卡片显示输入/输出摘要和错误根因", () => {
    render(
      <MicroflowTracePanel
        microflowId="mf-1"
        microflowName="订单审批"
        session={createSession()}
        onSelectFrame={() => {}}
        onSelectFlow={() => {}}
      />,
    );

    expect(screen.getByText("in{riskScore, tenantId} · out{nextObjectId}")).toBeTruthy();
    expect(screen.getByText("in{url, timeoutMs} · out{statusCode, calculatedScore, branchTrace}")).toBeTruthy();
    expect(screen.getByText("RUNTIME_REST_TIMEOUT: 请求超时")).toBeTruthy();
  });

  it("在 Node Results 中保留逐节点输出变量、计算值和变量增量", () => {
    render(
      <MicroflowTracePanel
        microflowId="mf-1"
        microflowName="订单审批"
        session={createSession()}
        onSelectFrame={() => {}}
        onSelectFlow={() => {}}
      />,
    );

    expect(document.body.textContent).toContain("\"outputSnapshot\"");
    expect(document.body.textContent).toContain("\"calculatedScore\": 188");
    expect(document.body.textContent).toContain("\"outputVariables\"");
    expect(document.body.textContent).toContain("\"rawValue\": 188");
    expect(document.body.textContent).toContain("\"variableDelta\"");
    expect(document.body.textContent).toContain("\"added\"");
  });

  it("点击 trace 交互可回调到画布对象", () => {
    const onSelectFrame = vi.fn();
    const onSelectFlow = vi.fn();
    const onSelectError = vi.fn();

    render(
      <MicroflowTracePanel
        microflowId="mf-1"
        session={createSession()}
        onSelectFrame={onSelectFrame}
        onSelectFlow={onSelectFlow}
        onSelectError={onSelectError}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /2\. 调用接口/ }));
    fireEvent.click(screen.getByRole("button", { name: "in" }));
    const focusButtons = screen.getAllByRole("button", { name: "定位到节点/连线" });
    fireEvent.click(focusButtons[focusButtons.length - 1]);

    expect(onSelectFrame).toHaveBeenCalledWith(expect.objectContaining({ id: "frame-rest", objectId: "rest-1" }));
    expect(onSelectFlow).toHaveBeenCalledWith("flow-in");
    expect(onSelectError).toHaveBeenCalledWith(expect.objectContaining({ code: "RUNTIME_REST_TIMEOUT", objectId: "rest-1" }));
  });
});
