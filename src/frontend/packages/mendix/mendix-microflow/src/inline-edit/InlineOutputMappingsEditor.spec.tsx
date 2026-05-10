// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, disabled, icon, onClick }: { children?: React.ReactNode; disabled?: boolean; icon?: React.ReactNode; onClick?: () => void }) => (
    <button type="button" disabled={disabled} onClick={onClick}>{icon}{children}</button>
  ),
  Input: (props: {
    value?: string;
    placeholder?: string;
    disabled?: boolean;
    onChange?: (value: string) => void;
    onKeyDown?: (event: KeyboardEvent) => void;
    onBlur?: () => void;
  }) => (
    <input
      placeholder={props.placeholder}
      disabled={props.disabled}
      value={props.value ?? ""}
      onChange={event => props.onChange?.(event.currentTarget.value)}
      onKeyDown={event => props.onKeyDown?.(event as unknown as KeyboardEvent)}
      onBlur={() => props.onBlur?.()}
    />
  ),
  Select: (props: {
    value?: string;
    disabled?: boolean;
    optionList?: Array<{ label: string; value: string }>;
    onChange?: (value?: string) => void;
  }) => (
    <select disabled={props.disabled} value={props.value ?? ""} onChange={event => props.onChange?.(event.currentTarget.value)}>
      {(props.optionList ?? []).map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
  Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Typography: {
    Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconMinusCircleStroked: () => <span>-</span>,
  IconPlusCircleStroked: () => <span>+</span>,
}));

import { InlineOutputMappingsEditor } from "./InlineOutputMappingsEditor";

afterEach(() => {
  cleanup();
});

describe("InlineOutputMappingsEditor", () => {
  it("keeps an empty draft row local until it is complete", () => {
    const onCommit = vi.fn();
    render(<InlineOutputMappingsEditor value="" onCommit={onCommit} />);

    expect(onCommit).toHaveBeenCalledTimes(0);
    fireEvent.click(screen.getByText("新增输出"));
    expect(onCommit).toHaveBeenCalledTimes(0);

    fireEvent.change(screen.getByPlaceholderText("输出字段名，例如: result"), { target: { value: "out" } });
    expect(onCommit).toHaveBeenCalledTimes(0);

    fireEvent.click(screen.getByText("选择输出变量"));
    fireEvent.change(screen.getByDisplayValue(""), { target: { value: "totalScore" } });
    fireEvent.keyDown(screen.getByDisplayValue("totalScore"), { key: "Enter" });

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(JSON.parse(onCommit.mock.calls[0]?.[0] ?? "[]")).toEqual([{
      key: "out",
      source: "variable",
      variableName: "totalScore",
    }]);
  });

  it("commits an empty array when the last persisted mapping is removed", () => {
    const onCommit = vi.fn();
    render(
      <InlineOutputMappingsEditor
        value={JSON.stringify([{ key: "out", source: "variable", variableName: "totalScore" }])}
        onCommit={onCommit}
      />,
    );

    fireEvent.click(screen.getByText("-").closest("button") as HTMLButtonElement);

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit.mock.calls[0]?.[0]).toBe("[]");
  });
});
