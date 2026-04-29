// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("lottie-web", () => ({ default: {}, loadAnimation: vi.fn() }));
vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");
  const List = ({ dataSource = [], renderItem }: { dataSource?: unknown[]; renderItem: (item: unknown) => React.ReactNode }) => <div>{dataSource.map((item, index) => <div key={index}>{renderItem(item)}</div>)}</div>;
  List.Item = ({ children }: { children?: React.ReactNode }) => <div>{children}</div>;
  return {
    Button: (props: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props} />,
    Card: ({ title, children }: { title?: React.ReactNode; children?: React.ReactNode }) => <section><h2>{title}</h2>{children}</section>,
    List,
    Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Tag: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    TextArea: (props: React.TextareaHTMLAttributes<HTMLTextAreaElement> & { onChange?: (value: string) => void }) => <textarea {...props} onChange={event => props.onChange?.(event.currentTarget.value)} />,
    Typography: { Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span> },
  };
});

import { MicroflowStepDebugPanel, type MicroflowStepDebugPanelLabels } from "./step-debug-ui";

const labels: MicroflowStepDebugPanelLabels = {
  statusPrefix: "Status",
  nodePrefix: "Node",
  flowPrefix: "Flow",
  branchPrefix: "Branch",
  breakpointsTitle: "Breakpoints",
  staleBreakpoint: "Stale",
  logpoint: "Logpoint",
  variablesTitle: "Variables",
  watchesTitle: "Watches",
  callStackTitle: "Call stack",
  branchTreeTitle: "Branch tree",
  watchPlaceholder: "Watch expression",
  evaluate: "Evaluate",
  commands: {
    continue: "Continue",
    pause: "Pause",
    stepOver: "Step Over",
    stepInto: "Step Into",
    stepOut: "Step Out",
    runToNode: "Run to Node",
    cancel: "Cancel",
    stop: "Stop",
  },
};

afterEach(() => cleanup());

describe("MicroflowStepDebugPanel", () => {
  it("renders toolbar, execution marker and debug panels from labels", () => {
    render(
      <MicroflowStepDebugPanel
        status="paused"
        currentNodeId="node-a"
        currentFlowId="flow-a"
        currentBranchId="branch-1"
        labels={labels}
        variables={[{ name: "amount", valuePreview: "42" }]}
        watches={[{ expression: "$amount", value: "42" }]}
        breakpoints={[{ id: "bp-1", scope: "node", targetId: "node-a", condition: "$amount > 10", hitTarget: 2, stale: true, logpoint: true }]}
        callStack={[{ id: "frame-1", name: "Order.Submit" }]}
        branches={[{ branchId: "branch-1", status: "active" }]}
      />,
    );

    expect(screen.getByText("Status: paused")).toBeTruthy();
    expect(screen.getByText("Node: node-a")).toBeTruthy();
    expect(screen.getByText("Flow: flow-a")).toBeTruthy();
    expect(screen.getByText("Branch: branch-1")).toBeTruthy();
    expect(screen.getByText("Variables")).toBeTruthy();
    expect(screen.getByText("Breakpoints")).toBeTruthy();
    expect(screen.getByText("node: node-a ($amount > 10) #2 Logpoint Stale")).toBeTruthy();
    expect(screen.getByText("amount: 42")).toBeTruthy();
    expect(screen.getByText("$amount: 42")).toBeTruthy();
    expect(screen.getByText("Order.Submit")).toBeTruthy();
    expect(screen.getByText("branch-1: active")).toBeTruthy();
  });

  it("emits step and watch evaluation commands", () => {
    const onCommand = vi.fn();
    const onEvaluate = vi.fn();
    render(
      <MicroflowStepDebugPanel
        status="paused"
        labels={labels}
        onCommand={onCommand}
        onEvaluate={onEvaluate}
      />,
    );

    fireEvent.click(screen.getByText("Step Over"));
    fireEvent.change(screen.getByPlaceholderText("Watch expression"), { target: { value: "$amount" } });
    fireEvent.click(screen.getByText("Evaluate"));

    expect(onCommand).toHaveBeenCalledWith("stepOver");
    expect(onEvaluate).toHaveBeenCalledWith("$amount");
  });
});
