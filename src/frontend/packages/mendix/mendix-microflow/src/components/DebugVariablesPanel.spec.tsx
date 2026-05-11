// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

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
    Typography: { Text: (props: React.HTMLAttributes<HTMLSpanElement>) => <span {...props}>{props.children}</span> },
  };
});

import { DebugVariablesPanel } from "./DebugVariablesPanel";

afterEach(() => cleanup());

describe("DebugVariablesPanel", () => {
  it("renders variables and emits selection", () => {
    const onSelectVariable = vi.fn();
    render(
      <DebugVariablesPanel
        title="Variables"
        variables={[
          { name: "amount", valuePreview: "42", type: "decimal", scope: "local" },
          { name: "$status", valuePreview: "ok", type: "string" },
        ]}
        activeVariableName="$amount"
        onSelectVariable={onSelectVariable}
      />,
    );

    expect(screen.getByText("Variables")).toBeTruthy();
    fireEvent.click(screen.getByTestId("microflow-debug-variable--amount"));
    fireEvent.click(screen.getByTestId("microflow-debug-variable--status"));

    expect(onSelectVariable).toHaveBeenCalledWith("amount");
    expect(onSelectVariable).toHaveBeenCalledWith("$status");
    expect(screen.getByText("Decimal")).toBeTruthy();
    expect(screen.getByText("local")).toBeTruthy();
  });

  it("prioritizes $latestError and supports expanded object/list preview", () => {
    render(
      <DebugVariablesPanel
        title="Variables"
        variables={[
          { name: "zVar", valuePreview: "1", type: "integer" },
          { name: "$latestError", valuePreview: "{\"Message\":\"boom\",\"ErrorType\":\"X\"}", type: "error" },
          { name: "$items", valuePreview: "[1,2,3,4,5,6,7,8,9,10,11]", type: "list" },
        ]}
      />,
    );

    const firstRow = screen.getByTestId("microflow-debug-variable-row--latestError");
    expect(firstRow.textContent).toContain("$latestError");
    fireEvent.click(screen.getByTestId("microflow-debug-variable-expand--latestError"));
    expect(screen.getByText("/Message boom")).toBeTruthy();
    fireEvent.click(screen.getByTestId("microflow-debug-variable-expand--items"));
    expect(screen.getByText("List[11]")).toBeTruthy();
    expect(screen.getByText("... 共 11 项")).toBeTruthy();
  });

  it("marks variable as changed when value updates", () => {
    const { rerender } = render(
      <DebugVariablesPanel
        title="Variables"
        variables={[{ name: "amount", valuePreview: "42", type: "decimal" }]}
      />,
    );

    rerender(
      <DebugVariablesPanel
        title="Variables"
        variables={[{ name: "amount", valuePreview: "43", type: "decimal" }]}
      />,
    );

    expect(screen.getByText("●")).toBeTruthy();
  });
});
