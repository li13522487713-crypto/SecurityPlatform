// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ExpressionParseSuggestion } from "../utils/expression-engine";

vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");
  const List = ({ dataSource = [], renderItem }: { dataSource?: unknown[]; renderItem: (item: unknown) => React.ReactNode }) => <div>{dataSource.map((item, index) => <div key={index}>{renderItem(item)}</div>)}</div>;
  List.Item = ({ children }: { children?: React.ReactNode }) => <div>{children}</div>;
  return {
    Button: (props: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props} />,
    Card: ({ title, children }: { title?: React.ReactNode; children?: React.ReactNode }) => <section><h2>{title}</h2>{children}</section>,
    Input: (props: React.InputHTMLAttributes<HTMLInputElement> & { onChange?: (value: string) => void }) => <input {...props} onChange={event => props.onChange?.(event.currentTarget.value)} />,
    List,
    Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Typography: { Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span> },
  };
});

import { VariablePicker } from "./VariablePicker";

afterEach(() => cleanup());

describe("VariablePicker", () => {
  it("supports typing and selecting suggestions", () => {
    const onChange = vi.fn();
    const onPickSuggestion = vi.fn();
    const suggestions: ExpressionParseSuggestion[] = [
      { label: "orderId", type: "variable", detail: "String", insertText: "orderId" },
      { label: "toUpperCase", type: "function", detail: "toUpperCase(String) -> String", insertText: "toUpperCase" },
    ];
    render(
      <VariablePicker
        value=""
        onChange={onChange}
        suggestions={suggestions}
        onPickSuggestion={onPickSuggestion}
      />,
    );

    fireEvent.change(screen.getByPlaceholderText("Type expression..."), { target: { value: "$ord" } });
    fireEvent.click(screen.getByText("orderId"));
    fireEvent.click(screen.getByText("toUpperCase"));

    expect(onChange).toHaveBeenCalledWith("$ord");
    expect(onPickSuggestion).toHaveBeenCalledWith(suggestions[0]);
    expect(onPickSuggestion).toHaveBeenCalledWith(suggestions[1]);
  });
});
