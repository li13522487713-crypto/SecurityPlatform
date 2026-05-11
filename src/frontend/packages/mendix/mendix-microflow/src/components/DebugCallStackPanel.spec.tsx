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
    Typography: { Text: ({ children, ...props }: React.HTMLAttributes<HTMLSpanElement>) => <span {...props}>{children}</span> },
  };
});

import { DebugCallStackPanel } from "./DebugCallStackPanel";

afterEach(() => cleanup());

describe("DebugCallStackPanel", () => {
  it("renders active frame indicator and highlighted style for the top frame", () => {
    const { container } = render(
      <DebugCallStackPanel
        frames={[
          { id: "frame-1", name: "MF_Child", microflowId: "mf-child" },
          { id: "frame-2", name: "MF_Parent", microflowId: "mf-parent" },
        ]}
      />,
    );

    expect(screen.getByText("MF_Child")).toBeTruthy();
    expect(screen.getByText("MF_Parent")).toBeTruthy();
    expect(container.textContent).toContain("▶");
  });

  it("invokes onSelectFrame when clicking selectable frame", () => {
    const onSelectFrame = vi.fn();
    render(
      <DebugCallStackPanel
        frames={[
          { id: "frame-1", name: "MF_Child", microflowId: "mf-child" },
          { id: "frame-2", name: "MF_Parent", microflowId: "mf-parent" },
        ]}
        onSelectFrame={onSelectFrame}
      />,
    );

    fireEvent.click(screen.getByText("MF_Parent"));

    expect(onSelectFrame).toHaveBeenCalledTimes(1);
    expect(onSelectFrame).toHaveBeenCalledWith(
      expect.objectContaining({ id: "frame-2", microflowId: "mf-parent" }),
      1,
    );
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
