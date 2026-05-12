// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");
  const List = ({ dataSource = [], renderItem }: { dataSource?: unknown[]; renderItem: (item: unknown, index?: number) => React.ReactNode }) => (
    <div>
      {dataSource.map((item, index) => (
        <div key={index}>{renderItem(item, index)}</div>
      ))}
    </div>
  );
  List.Item = ({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) => <div {...props}>{children}</div>;
  return {
    List,
    Space: ({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) => <div {...props}>{children}</div>,
    Tag: ({ children, ...props }: React.HTMLAttributes<HTMLSpanElement>) => <span {...props}>{children}</span>,
    Typography: { Text: ({ children, ...props }: React.HTMLAttributes<HTMLSpanElement>) => <span {...props}>{children}</span> },
  };
});

import { DebugCallStackPanel } from "./DebugCallStackPanel";

afterEach(() => cleanup());

describe("DebugCallStackPanel", () => {
  it("renders active frame indicator for the deepest frame", () => {
    const { container } = render(
      <DebugCallStackPanel
        frames={[
          { id: "frame-1", name: "MF_Parent", microflowId: "mf-parent", depth: 0 },
          { id: "frame-2", name: "MF_Child", microflowId: "mf-child", depth: 1 },
        ]}
      />,
    );

    expect(screen.getByTestId("microflow-debug-callstack-active-frame-1").textContent).not.toContain("▶");
    expect(screen.getByTestId("microflow-debug-callstack-active-frame-2").textContent).toContain("▶");
    expect(screen.getByText("MF_Child")).toBeTruthy();
    expect(screen.getByText("MF_Parent")).toBeTruthy();
    expect(container.textContent).toContain("▶");
  });

  it("falls back to treating the last frame as active when depth metadata is absent", () => {
    render(
      <DebugCallStackPanel
        frames={[
          { id: "frame-1", name: "MF_Parent", microflowId: "mf-parent" },
          { id: "frame-2", name: "MF_Child", microflowId: "mf-child" },
        ]}
      />,
    );
    expect(screen.getByTestId("microflow-debug-callstack-active-frame-1").textContent).not.toContain("▶");
    expect(screen.getByTestId("microflow-debug-callstack-active-frame-2").textContent).toContain("▶");
  });

  it("invokes onSelectFrame when clicking selectable frame", () => {
    const onSelectFrame = vi.fn();
    render(
      <DebugCallStackPanel
        frames={[
          { id: "frame-1", name: "MF_Parent", microflowId: "mf-parent", depth: 0 },
          { id: "frame-2", name: "MF_Child", microflowId: "mf-child", depth: 1 },
        ]}
        onSelectFrame={onSelectFrame}
      />,
    );

    fireEvent.click(screen.getByText("MF_Child"));

    expect(onSelectFrame).toHaveBeenCalledTimes(1);
    expect(onSelectFrame).toHaveBeenCalledWith(
      expect.objectContaining({ id: "frame-2", microflowId: "mf-child" }),
      1,
    );
  });

  it("renders frame status and current node caption when provided", () => {
    render(
      <DebugCallStackPanel
        frames={[
          { id: "frame-1", name: "MF_Parent", microflowId: "mf-parent", depth: 0, status: "paused", currentNodeCaption: "Change Order" },
          { id: "frame-2", name: "MF_Child", microflowId: "mf-child", depth: 1, status: "success" },
        ]}
      />,
    );

    expect(screen.getByTestId("microflow-debug-callstack-status-frame-1").textContent).toBe("paused");
    expect(screen.getByTestId("microflow-debug-callstack-status-frame-2").textContent).toBe("success");
    expect(screen.getByTestId("microflow-debug-callstack-node-frame-1").textContent).toContain("@ Change Order");
  });

  it("does not invoke onSelectFrame when frame has no microflow id", () => {
    const onSelectFrame = vi.fn();
    render(
      <DebugCallStackPanel
        frames={[
          { id: "frame-1", name: "MF_Unknown" },
        ]}
        onSelectFrame={onSelectFrame}
      />,
    );

    fireEvent.click(screen.getByText("MF_Unknown"));
    expect(onSelectFrame).not.toHaveBeenCalled();
  });
});
