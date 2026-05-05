// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

let readonlyMode = false;
vi.mock("@flowgram-adapter/free-layout-editor", () => ({
  usePlaygroundReadonlyState: () => readonlyMode,
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Input: (props: {
    value?: string;
    className?: string;
    autoFocus?: boolean;
    onChange?: (value: string) => void;
    onKeyDown?: (event: KeyboardEvent) => void;
    onBlur?: () => void;
  }) => (
    <input
      data-testid="mock-inline-input"
      autoFocus={props.autoFocus}
      className={props.className}
      value={props.value ?? ""}
      onChange={event => props.onChange?.(event.currentTarget.value)}
      onKeyDown={event => props.onKeyDown?.(event as unknown as KeyboardEvent)}
      onBlur={() => props.onBlur?.()}
    />
  ),
}));

import { FlowGramMicroflowLineRenderer } from "./FlowGramMicroflowLineRenderer";
import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";

afterEach(() => {
  cleanup();
  readonlyMode = false;
});

function renderLine(dataPatch: Partial<FlowGramMicroflowEdgeData>) {
  const data: FlowGramMicroflowEdgeData = {
    flowId: "flow-1",
    flowKind: "sequence",
    edgeKind: "sequence",
    isErrorHandler: false,
    caseValues: [],
    validationState: "valid",
    ...dataPatch,
  };
  return render(
    <FlowGramMicroflowLineRenderer
      key="line-1"
      lineType={"polyline" as never}
      version="1"
      line={{ data } as never}
    />,
  );
}

describe("FlowGramMicroflowLineRenderer interaction", () => {
  it("commits trimmed label on Enter", () => {
    const committed: Array<{ flowId: string; value: string }> = [];
    const listener = (event: Event) => {
      const detail = (event as CustomEvent<{ flowId: string; value: string }>).detail;
      committed.push(detail);
    };
    window.addEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    try {
      renderLine({ label: "true" });
      fireEvent.click(screen.getByRole("button"));
      const input = screen.getByTestId("mock-inline-input");
      fireEvent.change(input, { target: { value: "  approved  " } });
      fireEvent.keyDown(input, { key: "Enter" });
      expect(committed).toEqual([{ flowId: "flow-1", value: "approved" }]);
    } finally {
      window.removeEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    }
  });

  it("does not commit on Escape and restores display label", () => {
    const committed: Array<{ flowId: string; value: string }> = [];
    const listener = (event: Event) => {
      committed.push((event as CustomEvent<{ flowId: string; value: string }>).detail);
    };
    window.addEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    try {
      renderLine({ label: "fallback" });
      fireEvent.click(screen.getByRole("button"));
      const input = screen.getByTestId("mock-inline-input");
      fireEvent.change(input, { target: { value: "error" } });
      fireEvent.keyDown(input, { key: "Escape" });
      expect(committed).toEqual([]);
      expect(screen.getByRole("button").textContent).toBe("fallback");
    } finally {
      window.removeEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    }
  });

  it("commits trimmed label on blur", () => {
    const committed: Array<{ flowId: string; value: string }> = [];
    const listener = (event: Event) => {
      committed.push((event as CustomEvent<{ flowId: string; value: string }>).detail);
    };
    window.addEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    try {
      renderLine({ label: "true" });
      fireEvent.click(screen.getByRole("button"));
      const input = screen.getByTestId("mock-inline-input");
      fireEvent.change(input, { target: { value: "  timeout  " } });
      fireEvent.blur(input);
      expect(committed).toEqual([{ flowId: "flow-1", value: "timeout" }]);
    } finally {
      window.removeEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    }
  });

  it("does not enter editing in readonly mode", () => {
    readonlyMode = true;
    const committed: Array<{ flowId: string; value: string }> = [];
    const listener = (event: Event) => {
      committed.push((event as CustomEvent<{ flowId: string; value: string }>).detail);
    };
    window.addEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    try {
      renderLine({ label: "fallback" });
      fireEvent.click(screen.getByRole("button"));
      expect(screen.queryByTestId("mock-inline-input")).toBeNull();
      expect(committed).toEqual([]);
    } finally {
      window.removeEventListener("atlas:microflow-inline-line-label-commit", listener as EventListener);
    }
  });

  it("applies runtime label state class for active/skipped/error", () => {
    const { rerender } = render(
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data: {
          flowId: "flow-1",
          flowKind: "sequence",
          edgeKind: "decisionCondition",
          isErrorHandler: false,
          caseValues: [{ kind: "boolean", value: true }],
          validationState: "valid",
          runtimeState: "selectedCase",
        } } as never}
      />,
    );
    expect(screen.getByRole("button").className).toContain("is-runtime-active");

    rerender(
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data: {
          flowId: "flow-1",
          flowKind: "sequence",
          edgeKind: "decisionCondition",
          isErrorHandler: false,
          caseValues: [{ kind: "boolean", value: false }],
          validationState: "valid",
          runtimeState: "skipped",
        } } as never}
      />,
    );
    expect(screen.getByRole("button").className).toContain("is-runtime-skipped");

    rerender(
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data: {
          flowId: "flow-1",
          flowKind: "sequence",
          edgeKind: "errorHandler",
          isErrorHandler: true,
          caseValues: [],
          validationState: "valid",
          runtimeState: "failed",
        } } as never}
      />,
    );
    expect(screen.getByRole("button").className).toContain("is-runtime-error");

    rerender(
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data: {
          flowId: "flow-1",
          flowKind: "sequence",
          edgeKind: "errorHandler",
          isErrorHandler: true,
          caseValues: [],
          validationState: "valid",
          runtimeState: "errorHandlerVisited",
        } } as never}
      />,
    );
    expect(screen.getByRole("button").className).toContain("is-runtime-error");
  });

  it("shows warning style when branch target is missing", () => {
    renderLine({
      label: "else",
      edgeKind: "decisionCondition",
      targetNodeId: undefined,
      validationState: "warning",
    });
    const button = screen.getByRole("button");
    expect(button.className).toContain("is-warning");
    expect(button.getAttribute("title")).toContain("缺少目标节点");
    expect(button.querySelector(".microflow-branch-label__warning-dot")).toBeTruthy();
    expect(button.textContent).toBe("else");
  });

  it("derives labels for approval/loop/error edges from edge kind and source port", () => {
    const { rerender } = render(
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data: {
          flowId: "flow-1",
          flowKind: "sequence",
          edgeKind: "sequence",
          isErrorHandler: false,
          caseValues: [],
          validationState: "valid",
          sourcePortId: "approval:rejected",
        } } as never}
      />,
    );
    expect(screen.getByRole("button").textContent).toContain("rejected");

    rerender(
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data: {
          flowId: "flow-1",
          flowKind: "sequence",
          edgeKind: "sequence",
          isErrorHandler: false,
          caseValues: [],
          validationState: "valid",
          sourcePortId: "loop:continue",
        } } as never}
      />,
    );
    expect(screen.getByRole("button").textContent).toContain("continue");

    rerender(
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data: {
          flowId: "flow-1",
          flowKind: "sequence",
          edgeKind: "errorHandler",
          isErrorHandler: true,
          caseValues: [],
          validationState: "valid",
          sourcePortId: "error:fallback",
        } } as never}
      />,
    );
    expect(screen.getByRole("button").textContent).toContain("fallback");
  });
});
