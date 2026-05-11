// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MicroflowEdge } from "./MicroflowEdge";

afterEach(() => cleanup());

describe("MicroflowEdge", () => {
  it("renders label and emits click events", () => {
    const onClick = vi.fn();
    render(
      <MicroflowEdge
        className="microflow-branch-label"
        flowId="flow-1"
        edgeKind="decisionCondition"
        label="true"
        onClick={onClick}
        editAdornment={<span data-testid="edge-edit">✎</span>}
      />,
    );

    const button = screen.getByTestId("microflow-flowgram-line-label");
    fireEvent.click(button);

    expect(button.getAttribute("data-flow-id")).toBe("flow-1");
    expect(button.getAttribute("data-edge-kind")).toBe("decisionCondition");
    expect(screen.getByText("true")).toBeTruthy();
    expect(screen.getByTestId("edge-edit")).toBeTruthy();
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("hides edit adornment in readonly mode and shows warning dot", () => {
    const { container } = render(
      <MicroflowEdge
        className="microflow-branch-label"
        flowId="flow-2"
        edgeKind="decisionCondition"
        label="else"
        readonly
        warningMissingTarget
        editAdornment={<span data-testid="edge-edit">✎</span>}
      />,
    );

    expect(screen.queryByTestId("edge-edit")).toBeNull();
    expect(container.querySelector(".microflow-branch-label__warning-dot")).toBeTruthy();
  });
});
