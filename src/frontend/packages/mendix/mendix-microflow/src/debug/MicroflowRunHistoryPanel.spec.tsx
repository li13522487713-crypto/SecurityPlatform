// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { MicroflowRunHistoryItem } from "../runtime-adapter/types";
import { MicroflowRunHistoryPanel } from "./MicroflowRunHistoryPanel";

vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");
  return {
    Button: ({ children, onClick }: { children?: React.ReactNode; onClick?: () => void }) => <button type="button" onClick={onClick}>{children}</button>,
    Card: ({ children }: { children?: React.ReactNode }) => <section>{children}</section>,
    Empty: ({ title, description, children }: { title?: React.ReactNode; description?: React.ReactNode; children?: React.ReactNode }) => <div>{title}{description}{children}</div>,
    Select: ({ value, onChange }: { value?: string; onChange?: (value: string) => void }) => (
      <select value={value} onChange={event => onChange?.(event.target.value)}>
        <option value="all">all</option>
        <option value="success">succeeded</option>
        <option value="failed">failed</option>
        <option value="unsupported">unsupported</option>
        <option value="cancelled">cancelled</option>
      </select>
    ),
    Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Spin: ({ spinning }: { spinning?: boolean }) => spinning ? <div>loading</div> : null,
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

function createItem(): MicroflowRunHistoryItem {
  return {
    runId: "run-child-1",
    microflowId: "mf-child",
    status: "success",
    durationMs: 12,
    startedAt: "2026-05-12T03:46:00.000Z",
    completedAt: "2026-05-12T03:46:00.012Z",
    summary: "Run succeeded",
    parentRunId: "run-parent",
    rootRunId: "run-parent",
    callFrameId: "frame-1",
    traceFrameCount: 3,
    logCount: 1,
    finalized: true,
    childRunIds: ["run-grandchild"],
    callStackFrames: [
      {
        id: "frame-1",
        runId: "run-child-1",
        depth: 1,
        qualifiedName: "Sales.TraceChild",
      },
    ],
  };
}

describe("MicroflowRunHistoryPanel", () => {
  it("展示真实 history metadata 和 callStackFrames 摘要", () => {
    render(
      <MicroflowRunHistoryPanel
        items={[createItem()]}
        statusFilter="all"
        onChangeFilter={() => {}}
        onRefresh={() => {}}
        onSelectRun={() => {}}
      />,
    );

    expect(screen.getByText("parent run-parent · root run-parent · frame frame-1")).toBeTruthy();
    expect(screen.getByText("trace 3")).toBeTruthy();
    expect(screen.getByText("logs 1")).toBeTruthy();
    expect(screen.getByText("child 1")).toBeTruthy();
    expect(screen.getByText("finalized")).toBeTruthy();
    expect(screen.getByText("Sales.TraceChild")).toBeTruthy();
    expect(screen.getByText("root run-parent")).toBeTruthy();
  });

  it("点击 run 卡片时回调 runId", () => {
    const onSelectRun = vi.fn();
    render(
      <MicroflowRunHistoryPanel
        items={[createItem()]}
        statusFilter="all"
        onChangeFilter={() => {}}
        onRefresh={() => {}}
        onSelectRun={onSelectRun}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /run-child-1/ }));
    expect(onSelectRun).toHaveBeenCalledWith("run-child-1");
  });

  it("点击 related run 标签时回调对应 runId", () => {
    const onSelectRun = vi.fn();
    render(
      <MicroflowRunHistoryPanel
        items={[createItem()]}
        statusFilter="all"
        onChangeFilter={() => {}}
        onRefresh={() => {}}
        onSelectRun={onSelectRun}
      />,
    );

    fireEvent.click(screen.getByText("root run-parent"));
    expect(onSelectRun).toHaveBeenCalledWith("run-parent");
  });
});
