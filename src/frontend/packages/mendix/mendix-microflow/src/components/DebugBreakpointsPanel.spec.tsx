// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");
  const List = ({ dataSource = [], renderItem }: { dataSource?: unknown[]; renderItem: (item: unknown) => React.ReactNode }) => (
    <div>{dataSource.map((item, index) => <div key={index}>{renderItem(item)}</div>)}</div>
  );
  List.Item = ({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) => <div {...props}>{children}</div>;
  return {
    Button: (props: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props} />,
    Card: ({ title, children }: { title?: React.ReactNode; children?: React.ReactNode }) => <section><h2>{title}</h2>{children}</section>,
    Checkbox: ({ checked, onChange, ...props }: React.InputHTMLAttributes<HTMLInputElement>) => (
      <input
        {...props}
        type="checkbox"
        checked={Boolean(checked)}
        onChange={event => onChange?.(event)}
      />
    ),
    Input: (props: React.InputHTMLAttributes<HTMLInputElement> & { onChange?: (value: string) => void }) => (
      <input {...props} onChange={event => props.onChange?.(event.currentTarget.value)} />
    ),
    List,
    Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Tag: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    Typography: { Text: (props: React.HTMLAttributes<HTMLSpanElement>) => <span {...props}>{props.children}</span> },
  };
});

import { DebugBreakpointsPanel } from "./DebugBreakpointsPanel";

afterEach(() => cleanup());

describe("DebugBreakpointsPanel", () => {
  it("renders breakpoints with state tags", () => {
    render(
      <DebugBreakpointsPanel
        title="Breakpoints"
        staleBreakpointLabel="stale"
        logpointLabel="logpoint"
        breakpoints={[
          { id: "bp-1", scope: "node", targetId: "node-a", hitTarget: 2, stale: true, logpoint: true, enabled: true },
        ]}
      />,
    );

    expect(screen.getByText("Breakpoints")).toBeTruthy();
    expect(screen.getByText("node: node-a")).toBeTruthy();
    expect(screen.getByText("#2")).toBeTruthy();
    expect(screen.getByText("logpoint")).toBeTruthy();
    expect(screen.getByText("stale")).toBeTruthy();
  });

  it("emits toggle and delete events", () => {
    const onToggleEnabled = vi.fn();
    const onDelete = vi.fn();
    render(
      <DebugBreakpointsPanel
        title="Breakpoints"
        staleBreakpointLabel="stale"
        logpointLabel="logpoint"
        breakpoints={[
          { id: "bp-1", scope: "node", targetId: "node-a", enabled: true },
        ]}
        onToggleEnabled={onToggleEnabled}
        onDelete={onDelete}
      />,
    );

    fireEvent.click(screen.getByTestId("microflow-breakpoint-enabled-bp-1"));
    fireEvent.click(screen.getByTestId("microflow-breakpoint-delete-bp-1"));

    expect(onToggleEnabled).toHaveBeenCalledWith("bp-1", false);
    expect(onDelete).toHaveBeenCalledWith("bp-1");
  });

  it("commits condition edit on blur", () => {
    const onChangeCondition = vi.fn();
    render(
      <DebugBreakpointsPanel
        title="Breakpoints"
        staleBreakpointLabel="stale"
        logpointLabel="logpoint"
        breakpoints={[
          { id: "bp-1", scope: "node", targetId: "node-a", condition: "$amount > 10", enabled: true },
        ]}
        onChangeCondition={onChangeCondition}
      />,
    );

    const input = screen.getByTestId("microflow-breakpoint-condition-bp-1");
    fireEvent.change(input, { target: { value: "$amount > 100" } });
    fireEvent.blur(input);

    expect(onChangeCondition).toHaveBeenCalledWith("bp-1", "$amount > 100");
  });
});

