// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Select: (props: { optionList?: Array<{ label: string; value: string }>; onChange?: (value?: string) => void }) => (
    <select data-testid="picker-select" onChange={event => props.onChange?.(event.currentTarget.value)}>
      <option value="" />
      {(props.optionList ?? []).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
    </select>
  ),
  Typography: { Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span> },
}));

import { ContextVariablePicker } from "./ContextVariablePicker";

afterEach(() => {
  cleanup();
});

describe("ContextVariablePicker", () => {
  const variables = [{ name: "$incidentId", source: "input", type: { kind: "String" } }];

  it("returns appended token when insertionMode=append", () => {
    const onChange = vi.fn();
    render(
      <ContextVariablePicker
        value="POST /api/incidents"
        variables={variables}
        insertionMode="append"
        onChange={onChange}
      />,
    );
    fireEvent.change(screen.getByTestId("picker-select"), { target: { value: "$incidentId" } });
    expect(onChange).toHaveBeenCalledWith("POST /api/incidents $incidentId");
  });

  it("returns normalized json path when insertionMode=jsonPath", () => {
    const onChange = vi.fn();
    render(
      <ContextVariablePicker
        value=""
        variables={variables}
        insertionMode="jsonPath"
        onChange={onChange}
      />,
    );
    fireEvent.change(screen.getByTestId("picker-select"), { target: { value: "$incidentId" } });
    expect(onChange).toHaveBeenCalledWith("$.incidentId");
  });

  it("renders direct and indirect upstream group labels", () => {
    const onChange = vi.fn();
    render(
      <ContextVariablePicker
        value=""
        variables={[
          { name: "$directOut", source: "upstream-direct", type: { kind: "String" } },
          { name: "$indirectOut", source: "upstream-indirect", type: { kind: "String" } },
        ]}
        onChange={onChange}
      />,
    );
    const options = Array.from(screen.getByTestId("picker-select").querySelectorAll("option")).map(item => item.textContent ?? "");
    expect(options.some(label => label.includes("直接上游节点输出"))).toBe(true);
    expect(options.some(label => label.includes("间接上游节点输出"))).toBe(true);
  });
});
