// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { DebugBreakpointsPanel, type DebugBreakpointPanelItem } from "./DebugBreakpointsPanel";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, disabled, ...rest }: any) => <button type="button" disabled={disabled} onClick={onClick} {...rest}>{children}</button>,
  Card: ({ title, children }: any) => <section><h2>{title}</h2>{children}</section>,
  Checkbox: ({ checked, onChange, disabled, ...rest }: any) => <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event)} {...rest} />,
  Input: ({ value, onChange, onBlur, onKeyDown, disabled, placeholder, ...rest }: any) => (
    <input
      value={value ?? ""}
      disabled={disabled}
      placeholder={placeholder}
      onChange={event => onChange?.(event.currentTarget.value)}
      onBlur={onBlur}
      onKeyDown={onKeyDown}
      {...rest}
    />
  ),
  List: Object.assign(
    ({ dataSource, renderItem }: any) => <div>{(dataSource ?? []).map((item: any, index: number) => <div key={item.id ?? index}>{renderItem(item, index)}</div>)}</div>,
    { Item: ({ children, ...rest }: any) => <div {...rest}>{children}</div> },
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Tag: ({ children }: any) => <span>{children}</span>,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

afterEach(() => cleanup());

function renderPanel(breakpoints: DebugBreakpointPanelItem[]) {
  return render(
    <DebugBreakpointsPanel
      title="Breakpoints"
      breakpoints={breakpoints}
      staleBreakpointLabel="Stale"
      logpointLabel="Logpoint"
    />,
  );
}

describe("DebugBreakpointsPanel", () => {
  it("syncs condition inputs when parent breakpoint condition changes", () => {
    const breakpoints: DebugBreakpointPanelItem[] = [
      { id: "bp-1", targetId: "node-a", scope: "node", condition: "$amount > 100", enabled: true },
    ];
    const view = renderPanel(breakpoints);

    const input = screen.getByTestId("microflow-breakpoint-condition-bp-1") as HTMLInputElement;
    expect(input.value).toBe("$amount > 100");

    view.rerender(
      <DebugBreakpointsPanel
        title="Breakpoints"
        breakpoints={[{ ...breakpoints[0], condition: "$amount > 200" }]}
        staleBreakpointLabel="Stale"
        logpointLabel="Logpoint"
      />,
    );

    expect((screen.getByTestId("microflow-breakpoint-condition-bp-1") as HTMLInputElement).value).toBe("$amount > 200");
  });

  it("preserves local draft when unrelated breakpoint fields rerender without condition change", () => {
    const breakpoints: DebugBreakpointPanelItem[] = [
      { id: "bp-2", targetId: "node-b", scope: "node", condition: "$amount > 100", enabled: true },
    ];
    const view = renderPanel(breakpoints);

    const input = screen.getByTestId("microflow-breakpoint-condition-bp-2") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "$amount > 150" } });
    expect(input.value).toBe("$amount > 150");

    view.rerender(
      <DebugBreakpointsPanel
        title="Breakpoints"
        breakpoints={[{ ...breakpoints[0], enabled: false }]}
        staleBreakpointLabel="Stale"
        logpointLabel="Logpoint"
      />,
    );

    expect((screen.getByTestId("microflow-breakpoint-condition-bp-2") as HTMLInputElement).value).toBe("$amount > 150");
  });

  it("drops removed breakpoint drafts and initializes new breakpoints from props", () => {
    const view = renderPanel([
      { id: "bp-3", targetId: "node-c", scope: "node", condition: "$x", enabled: true },
    ]);

    expect(screen.getByTestId("microflow-breakpoint-condition-bp-3")).toBeTruthy();

    view.rerender(
      <DebugBreakpointsPanel
        title="Breakpoints"
        breakpoints={[
          { id: "bp-4", targetId: "node-d", scope: "flow", condition: "$y", enabled: true },
        ]}
        staleBreakpointLabel="Stale"
        logpointLabel="Logpoint"
      />,
    );

    expect(screen.queryByTestId("microflow-breakpoint-condition-bp-3")).toBeNull();
    expect((screen.getByTestId("microflow-breakpoint-condition-bp-4") as HTMLInputElement).value).toBe("$y");
  });
});
