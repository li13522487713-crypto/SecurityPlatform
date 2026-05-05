// @vitest-environment jsdom
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { ConditionBuilderValue } from "./shared/ConditionBuilder";
import { InlineConditionEditor } from "./InlineConditionEditor";

const conditionBuilderMock = vi.fn();

vi.mock("./shared/ConditionBuilder", () => ({
  ConditionBuilder: (props: unknown) => {
    conditionBuilderMock(props);
    return <div data-testid="condition-builder-mock" />;
  },
}));

afterEach(() => {
  cleanup();
  conditionBuilderMock.mockReset();
});

function getBuilderProps() {
  const latest = conditionBuilderMock.mock.calls.at(-1)?.[0] as {
    value: ConditionBuilderValue;
    onChange: (value: ConditionBuilderValue) => void;
    onChangeRaw?: (raw: string) => void;
    variables?: Array<{ name: string; source: string; sourceNode: string }>;
  } | undefined;
  expect(latest).toBeDefined();
  return latest!;
}

describe("InlineConditionEditor", () => {
  it("parses condition expression into builder value", () => {
    render(<InlineConditionEditor value="$riskScore >= 80" />);
    const props = getBuilderProps();
    expect(props.value.left).toBe("$riskScore");
    expect(props.value.operator).toBe("greater or equal");
    expect(props.value.right).toBe("80");
  });

  it("normalizes builder structured change into condition expression", () => {
    const onCommit = vi.fn();
    render(<InlineConditionEditor value="$riskScore >= 80" onCommit={onCommit} />);
    const props = getBuilderProps();
    props.onChange({
      left: "$riskScore",
      operator: "greater than",
      right: "90",
      logic: "AND",
      raw: "",
    });
    expect(onCommit).toHaveBeenCalledWith("$riskScore > 90");
  });

  it("prefers raw expression when provided", () => {
    const onCommit = vi.fn();
    render(<InlineConditionEditor value="$riskScore >= 80" onCommit={onCommit} />);
    const props = getBuilderProps();
    props.onChange({
      left: "$riskScore",
      operator: "greater than",
      right: "90",
      logic: "AND",
      raw: "$customExpr",
    });
    expect(onCommit).toHaveBeenCalledWith("$customExpr");
  });

  it("maps options into context variable candidates", () => {
    render(
      <InlineConditionEditor
        value="$riskScore >= 80"
        options={[
          { label: "inputs::incidentId", value: "$incidentId" },
          { label: "riskScore", value: "$riskScore" },
        ]}
      />,
    );
    const props = getBuilderProps();
    expect(props.variables).toEqual([
      { name: "$incidentId", source: "inputs", sourceNode: "incidentId" },
      { name: "$riskScore", source: "context", sourceNode: "riskScore" },
    ]);
  });

  it("commits raw expression directly through onChangeRaw", () => {
    const onCommit = vi.fn();
    render(<InlineConditionEditor value="$riskScore >= 80" onCommit={onCommit} />);
    const props = getBuilderProps();
    props.onChangeRaw?.("$riskScore in [80,90]");
    expect(onCommit).toHaveBeenCalledWith("$riskScore in [80,90]");
  });
});
