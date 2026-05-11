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
          { name: "amount", valuePreview: "42" },
          { name: "$status", valuePreview: "ok" },
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
  });
});
