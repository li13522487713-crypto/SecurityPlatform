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
import { MicroflowEdgeDataContext, type FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";

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
    <div className="gedit-flow-activity-edge">
      <FlowGramMicroflowLineRenderer
        key="line-1"
        lineType={"polyline" as never}
        version="1"
        line={{ data } as never}
      />
    </div>,
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
      fireEvent.mouseEnter(screen.getByTestId("microflow-flowgram-line-label"));
      fireEvent.click(screen.getByRole("button", { name: "编辑分支标签 true" }));
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
      fireEvent.mouseEnter(screen.getByTestId("microflow-flowgram-line-label"));
      fireEvent.click(screen.getByRole("button", { name: "编辑分支标签 fallback" }));
      const input = screen.getByTestId("mock-inline-input");
      fireEvent.change(input, { target: { value: "error" } });
      fireEvent.keyDown(input, { key: "Escape" });
      expect(committed).toEqual([]);
      expect(screen.getByTestId("microflow-flowgram-line-label").textContent).toContain("fallback");
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
      fireEvent.mouseEnter(screen.getByTestId("microflow-flowgram-line-label"));
      fireEvent.click(screen.getByRole("button", { name: "编辑分支标签 true" }));
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
      fireEvent.mouseEnter(screen.getByTestId("microflow-flowgram-line-label"));
      expect(screen.queryByRole("button", { name: "编辑分支标签 fallback" })).toBeNull();
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
    expect(screen.getByTestId("microflow-flowgram-line-label").querySelector(".microflow-branch-label")?.className).toContain("is-runtime-active");

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
    expect(screen.getByTestId("microflow-flowgram-line-label").querySelector(".microflow-branch-label")?.className).toContain("is-runtime-skipped");

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
    expect(screen.getByTestId("microflow-flowgram-line-label").querySelector(".microflow-branch-label")?.className).toContain("is-runtime-error");

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
    expect(screen.getByTestId("microflow-flowgram-line-label").querySelector(".microflow-branch-label")?.className).toContain("is-runtime-error");
  });

  it("shows warning style when branch target is missing", () => {
    renderLine({
      label: "else",
      edgeKind: "decisionCondition",
      targetNodeId: undefined,
      validationState: "warning",
    });
    const button = screen.getByTestId("microflow-flowgram-line-label");
    expect(button.querySelector(".microflow-branch-label")?.className).toContain("is-warning");
    expect(button.getAttribute("title")).toContain("缺少目标节点");
    expect(button.querySelector(".microflow-branch-label__warning-dot")).toBeTruthy();
    expect(button.textContent).toContain("else");
  });

  it("renders (empty) pill style for explicit empty/noCase decision branches", () => {
    const { rerender } = renderLine({
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "empty", officialType: "Microflows$NoCase" }],
    });
    let button = screen.getByTestId("microflow-flowgram-line-label");
    expect(button.textContent).toContain("(empty)");
    expect(button.querySelector(".microflow-branch-label")?.className).toContain("is-empty");

    rerender(
      <div className="gedit-flow-activity-edge">
        <FlowGramMicroflowLineRenderer
          key="line-1"
          lineType={"polyline" as never}
          version="1"
          line={{ data: {
            flowId: "flow-1",
            flowKind: "sequence",
            edgeKind: "decisionCondition",
            isErrorHandler: false,
            caseValues: [{ kind: "noCase", officialType: "Microflows$NoCase" }],
            validationState: "valid",
          } } as never}
        />
      </div>,
    );
    button = screen.getByTestId("microflow-flowgram-line-label");
    expect(button.textContent).toContain("(empty)");
    expect(button.querySelector(".microflow-branch-label")?.className).toContain("is-empty");
  });

  it("derives labels for approval/loop edges from source port and keeps error-handler labels canonical", () => {
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
    expect(screen.getByTestId("microflow-flowgram-line-label").textContent).toContain("rejected");

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
    expect(screen.getByTestId("microflow-flowgram-line-label").textContent).toContain("continue");

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
    expect(screen.getByTestId("microflow-flowgram-line-label").textContent).toContain("Error");
  });

  it("projects edge kind classes onto the closest line wrapper", () => {
    const { container } = renderLine({
      edgeKind: "errorHandler",
      isErrorHandler: true,
      validationState: "error",
      runtimeState: "errorHandlerVisited",
    });

    const wrapper = container.querySelector(".gedit-flow-activity-edge");
    expect(wrapper?.className).toContain("microflow-flowgram-line--errorHandler");
    expect(wrapper?.className).toContain("is-runtime-errorHandlerVisited");
  });

  it("projects rollback mode classes for error-handler edges", () => {
    const { container, rerender } = render(
      <div className="gedit-flow-activity-edge">
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
            sourceErrorHandlingType: "customWithoutRollback",
          } } as never}
        />
      </div>,
    );
    let wrapper = container.querySelector(".gedit-flow-activity-edge");
    expect(wrapper?.className).toContain("microflow-flowgram-line--error-handler-customWithoutRollback");
    expect(screen.getByTestId("microflow-flowgram-line-label").querySelector(".microflow-branch-label")?.className).toContain("is-error-handler-customWithoutRollback");

    rerender(
      <div className="gedit-flow-activity-edge">
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
            sourceErrorHandlingType: "customWithRollback",
          } } as never}
        />
      </div>,
    );
    wrapper = container.querySelector(".gedit-flow-activity-edge");
    expect(wrapper?.className).toContain("microflow-flowgram-line--error-handler-customWithRollback");
    expect(screen.getByTestId("microflow-flowgram-line-label").querySelector(".microflow-branch-label")?.className).toContain("is-error-handler-customWithRollback");
  });

  it("renders decision labels from workflow edge context when FlowGram line JSON has no data payload", () => {
    const data: FlowGramMicroflowEdgeData = {
      flowId: "flow-decision-true",
      flowKind: "sequence",
      edgeKind: "decisionCondition",
      isErrorHandler: false,
      caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
      validationState: "valid",
    };
    const edgeDataByLineKey = new Map([
      ["decision::out::decision-true::in", data],
    ]);

    render(
      <MicroflowEdgeDataContext.Provider value={edgeDataByLineKey}>
        <FlowGramMicroflowLineRenderer
          key="line-1"
          lineType={"polyline" as never}
          version="1"
          line={{
            info: { from: "decision", fromPort: "out", to: "decision-true", toPort: "in" },
            toJSON: () => ({
              sourceNodeID: "decision",
              sourcePortID: "out",
              targetNodeID: "decision-true",
              targetPortID: "in",
            }),
          } as never}
        />
      </MicroflowEdgeDataContext.Provider>,
    );

    const label = screen.getByTestId("microflow-flowgram-line-label");
    expect(label.textContent).toContain("true");
    expect(label.getAttribute("data-flow-id")).toBe("flow-decision-true");
  });
});
