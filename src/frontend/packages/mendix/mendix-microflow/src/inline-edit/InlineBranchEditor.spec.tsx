// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: (props: { value?: string; onChange?: (value: string) => void }) => (
    <input
      data-testid="inline-input"
      value={props.value ?? ""}
      onChange={event => props.onChange?.(event.currentTarget.value)}
    />
  ),
  Button: ({ children, onClick }: { children?: React.ReactNode; onClick?: () => void }) => (
    <button type="button" onClick={onClick}>{children}</button>
  ),
  Popover: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Select: (props: { optionList?: Array<{ label: string; value: string }>; onChange?: (value?: string) => void }) => (
    <select data-testid="picker-select" onChange={event => props.onChange?.(event.currentTarget.value)}>
      <option value="" />
      {(props.optionList ?? []).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
    </select>
  ),
  Typography: { Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span> },
}));

import { InlineBranchEditor } from "./InlineBranchEditor";

afterEach(() => {
  cleanup();
});

describe("InlineBranchEditor", () => {
  it("supports variable token insertion for branch labels", () => {
    const onCommit = vi.fn();
    render(
      <InlineBranchEditor
        value="approved ->"
        onCommit={onCommit}
        options={[{ label: "context::nextNode|type=string", value: "$nextNode" }]}
      />,
    );
    fireEvent.change(screen.getByTestId("picker-select"), { target: { value: "$nextNode" } });
    expect(onCommit).toHaveBeenCalledWith("approved -> $nextNode");
  });
});

