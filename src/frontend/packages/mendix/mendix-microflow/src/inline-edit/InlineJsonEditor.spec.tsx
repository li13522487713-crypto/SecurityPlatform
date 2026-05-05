// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  TextArea: (props: {
    value?: string;
    onChange?: (value: string) => void;
    onKeyDown?: (event: KeyboardEvent) => void;
  }) => (
    <textarea
      data-testid="inline-json-textarea"
      value={props.value ?? ""}
      onChange={event => props.onChange?.(event.currentTarget.value)}
      onKeyDown={event => props.onKeyDown?.(event as unknown as KeyboardEvent)}
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

import { InlineJsonEditor } from "./InlineJsonEditor";

afterEach(() => {
  cleanup();
});

describe("InlineJsonEditor", () => {
  it("appends selected variable token into json draft and commits with ctrl+enter", () => {
    const onCommit = vi.fn();
    render(
      <InlineJsonEditor
        value='{"incidentId":"$incidentId"}'
        onCommit={onCommit}
        options={[{ label: "context::riskScore|type=number", value: "$riskScore" }]}
      />,
    );

    fireEvent.change(screen.getByTestId("picker-select"), { target: { value: "$riskScore" } });
    fireEvent.keyDown(screen.getByTestId("inline-json-textarea"), { key: "Enter", ctrlKey: true });
    expect(onCommit).toHaveBeenCalledWith('{"incidentId":"$incidentId"} $.riskScore');
  });
});
