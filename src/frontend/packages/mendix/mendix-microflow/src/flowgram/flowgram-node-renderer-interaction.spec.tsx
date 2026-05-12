// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const startDragMock = vi.fn();
const selectNodeMock = vi.fn();

vi.mock("@douyinfe/semi-ui", () => ({
  Badge: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  Button: ({ children, onClick, icon, type }: { children?: React.ReactNode; onClick?: (event: React.MouseEvent<HTMLButtonElement>) => void; icon?: React.ReactNode; type?: "button" | "submit" | "reset" }) => (
    <button type={type ?? "button"} onClick={onClick}>
      {icon}
      {children}
    </button>
  ),
  Spin: () => <span>spin</span>,
  Tag: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  Tooltip: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  Typography: {
    Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconChevronDown: () => <span>down</span>,
  IconChevronUp: () => <span>up</span>,
  IconEdit: () => <span>edit</span>,
  IconTickCircle: () => <span>tick</span>,
  IconMore: () => <span>more</span>,
}));

vi.mock("../inline-edit", () => ({
  InlineNodeEditor: ({
    onApplyQuickFix,
    onCommitField,
  }: {
    onApplyQuickFix?: (input: { id: string; actionKind: string; fieldPath?: string; value?: string; editType?: string }) => void;
    onCommitField?: (field: { fieldPath: string; editType: string }, value: string) => void;
  }) => (
    <div data-testid="inline-node-editor">
      inline-node-editor
      <button
        type="button"
        onClick={() => onCommitField?.({ fieldPath: "data.action.request.urlExpression.raw", editType: "http" }, "/api/inline-from-node")}
      >
        commit-field
      </button>
      <button
        type="button"
        onClick={() => onApplyQuickFix?.({
          id: "fix-1",
          actionKind: "setFieldValue",
          fieldPath: "data.action.request.urlExpression.raw",
          value: "/api/inline-fixed",
          editType: "http",
        })}
      >
        apply-fix
      </button>
    </div>
  ),
}));

vi.mock("./FlowGramMicroflowPortRenderer", () => ({
  FlowGramMicroflowPortRenderer: () => null,
}));

vi.mock("./flowgram-node-drag", () => ({
  focusMicroflowNodeDragRoot: () => undefined,
  isMicroflowNodeDragBlockedTarget: () => false,
}));

vi.mock("@flowgram-adapter/free-layout-editor", () => ({
  FlowNodeFormData: Symbol("FlowNodeFormData"),
  usePlaygroundReadonlyState: () => false,
  useNodeRender: () => ({
    selected: false,
    activated: false,
    ports: [],
    selectNode: selectNodeMock,
    nodeRef: { current: document.createElement("div") },
    startDrag: startDragMock,
    onFocus: () => undefined,
    onBlur: () => undefined,
  }),
}));

import { FlowGramMicroflowNodeRenderer } from "./FlowGramMicroflowNodeRenderer";
import { subscribeInlineNodeToggle } from "./inline-events";
import { MicroflowNodeViewModesContext } from "./FlowGramMicroflowTypes";

afterEach(() => {
  cleanup();
  startDragMock.mockReset();
  selectNodeMock.mockReset();
});

function buildNodeValue(viewMode: "compact" | "expanded" = "compact", objectKind = "exclusiveSplit") {
  return {
    objectId: "node-1",
    objectKind,
    collectionId: "nodes",
    title: "判断",
    validationState: "valid",
    issueCount: 0,
    inlineConfig: {
      viewMode,
      summaryLines: [{ id: "s1", value: "if $riskScore >= 80", kind: "condition" }],
      sections: [],
    },
  };
}

function renderNode(viewMode: "compact" | "expanded" = "compact", objectKind = "exclusiveSplit") {
  const value = buildNodeValue(viewMode, objectKind);
  const node = {
    id: "node-1",
    getData: () => ({
      getFormModel: () => ({
        getFormItemValueByPath: () => value,
      }),
    }),
  };
  return render(
    <MicroflowNodeViewModesContext.Provider value={viewMode === "expanded" ? { "node-1": "expanded" } : {}}>
      <FlowGramMicroflowNodeRenderer node={node as never} />
    </MicroflowNodeViewModesContext.Provider>
  );
}

describe("FlowGramMicroflowNodeRenderer interaction", () => {
  it("renders compact summary lines from inlineConfig", () => {
    renderNode();
    const text = screen.getByTestId("microflow-node-summary-node-1").textContent ?? "";
    expect(text.includes("if $riskScore >= 80")).toBe(true);
  });

  it("renders usage consumer tag and source/consumer classes", () => {
    const value = {
      ...buildNodeValue(),
      usageSourceHighlight: true,
      usageConsumerHighlight: true,
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    const element = screen.getByTestId("microflow-node-node-1");
    expect(element.className).toContain("is-usage-source");
    expect(element.className).toContain("is-usage-consumer");
    expect(screen.getByText("Usage")).toBeTruthy();
  });

  it("uses projected inlineConfig view mode when FlowGram renders outside React context", () => {
    const value = buildNodeValue("expanded");
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };

    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    expect(screen.getByRole("button", { name: "收起节点" })).toBeTruthy();
    expect(screen.getByTestId("inline-node-editor")).toBeTruthy();
  });

  it("prefers projected doc JSON inline state over stale FlowGram form data", () => {
    const staleFormValue = buildNodeValue("compact");
    const projectedJsonValue = buildNodeValue("expanded");
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => staleFormValue,
        }),
      }),
      toJSON: () => ({
        data: projectedJsonValue,
      }),
    };

    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    expect(screen.getByRole("button", { name: "收起节点" })).toBeTruthy();
    expect(screen.getByTestId("inline-node-editor")).toBeTruthy();
  });

  it("dispatches inline node toggle event on double click", () => {
    const events: Array<{ nodeId?: string; expanded?: boolean }> = [];
    const unsub = subscribeInlineNodeToggle(detail => events.push(detail));
    try {
      renderNode();
      fireEvent.doubleClick(screen.getByTestId("microflow-node-node-1"));
      expect(events).toEqual([expect.objectContaining({ nodeId: "node-1", expanded: true })]);
    } finally {
      unsub();
    }
  });

  it("dispatches inline node toggle event from header expand button", () => {
    const events: Array<{ nodeId?: string; expanded?: boolean }> = [];
    const unsub = subscribeInlineNodeToggle(detail => events.push(detail));
    try {
      renderNode();
      fireEvent.click(screen.getByRole("button", { name: "展开节点" }));
      expect(events).toEqual([expect.objectContaining({ nodeId: "node-1", expanded: true })]);
    } finally {
      unsub();
    }
  });

  it("dispatches inline node toggle once from pointer activation", () => {
    const events: Array<{ nodeId?: string; expanded?: boolean }> = [];
    const unsub = subscribeInlineNodeToggle(detail => events.push(detail));
    try {
      renderNode();
      const button = screen.getByRole("button", { name: "展开节点" });
      fireEvent.pointerDown(button);
      fireEvent.click(button);
      expect(events).toEqual([expect.objectContaining({ nodeId: "node-1", expanded: true })]);
    } finally {
      unsub();
    }
  });

  it("dispatches inline quick-fix apply event with expanded editor payload", () => {
    const events: Array<Record<string, unknown>> = [];
    const listener = (event: Event) => {
      events.push((event as CustomEvent<Record<string, unknown>>).detail);
    };
    window.addEventListener("atlas:microflow-inline-quick-fix-apply", listener as EventListener);
    try {
      renderNode("expanded");
      fireEvent.click(screen.getByText("apply-fix"));
      expect(events).toEqual([
        expect.objectContaining({
          nodeId: "node-1",
          suggestionId: "fix-1",
          actionKind: "setFieldValue",
          fieldPath: "data.action.request.urlExpression.raw",
          value: "/api/inline-fixed",
          editType: "http",
        }),
      ]);
    } finally {
      window.removeEventListener("atlas:microflow-inline-quick-fix-apply", listener as EventListener);
    }
  });

  it("dispatches inline field commit event with node context", () => {
    const events: Array<Record<string, unknown>> = [];
    const listener = (event: Event) => {
      events.push((event as CustomEvent<Record<string, unknown>>).detail);
    };
    window.addEventListener("atlas:microflow-inline-field-commit", listener as EventListener);
    try {
      renderNode("expanded");
      fireEvent.click(screen.getByText("commit-field"));
      expect(events).toEqual([
        expect.objectContaining({
          nodeId: "node-1",
          fieldPath: "data.action.request.urlExpression.raw",
          editType: "http",
          value: "/api/inline-from-node",
        }),
      ]);
    } finally {
      window.removeEventListener("atlas:microflow-inline-field-commit", listener as EventListener);
    }
  });

  it("renders dedicated event styles for error/continue/break events", () => {
    const cases = [
      { kind: "errorEvent", tone: "error" },
      { kind: "continueEvent", tone: "continue" },
      { kind: "breakEvent", tone: "break" },
    ] as const;
    for (const item of cases) {
      cleanup();
      renderNode("compact", item.kind);
      const node = screen.getByTestId("microflow-node-node-1");
      expect(node.className).toContain(`microflow-flowgram-node--${item.tone}`);
      const dot = node.querySelector(".microflow-event-dot");
      expect(dot?.getAttribute("data-node-tone")).toBe(item.tone);
      expect(node.querySelector(".microflow-event-dot__icon")).not.toBeNull();
    }
  });

  it("renders loop badge and summary for for-each and while loop modes", () => {
    const cases = [
      {
        value: {
          ...buildNodeValue("compact", "loopedActivity"),
          title: "Loop Orders",
          loopSource: {
            kind: "iterableList",
            listVariableName: "$OrderList",
            iteratorVariableName: "IteratorOrder",
          },
          listVariableName: "$OrderList",
          iteratorVariableName: "IteratorOrder",
        },
        expectedBadge: "for",
        expectedBody: "For each IteratorOrder in $OrderList",
      },
      {
        value: {
          ...buildNodeValue("compact", "loopedActivity"),
          title: "Loop While",
          loopSource: {
            kind: "while",
            expression: { raw: "$Counter <= 5" },
          },
        },
        expectedBadge: "while",
        expectedBody: "while $Counter <= 5",
      },
    ] as const;

    for (const item of cases) {
      cleanup();
      const node = {
        id: "node-1",
        getData: () => ({
          getFormModel: () => ({
            getFormItemValueByPath: () => item.value,
          }),
        }),
      };
      render(
        <MicroflowNodeViewModesContext.Provider value={{}}>
          <FlowGramMicroflowNodeRenderer node={node as never} />
        </MicroflowNodeViewModesContext.Provider>
      );

      expect(screen.getByText(item.expectedBadge)).toBeTruthy();
      expect(screen.getByText(item.expectedBody)).toBeTruthy();
    }
  });

  it("renders start/end events as solid dots without inner icon", () => {
    const cases = [
      { kind: "startEvent", tone: "start" },
      { kind: "endEvent", tone: "end" },
    ] as const;
    for (const item of cases) {
      cleanup();
      renderNode("compact", item.kind);
      const node = screen.getByTestId("microflow-node-node-1");
      const dot = node.querySelector(".microflow-event-dot");
      expect(dot?.getAttribute("data-node-tone")).toBe(item.tone);
      expect(node.querySelector(".microflow-event-dot__icon")).toBeNull();
    }
  });

  it("keeps Start Event selectable but blocks drag start", () => {
    renderNode("compact", "startEvent");
    const node = screen.getByTestId("microflow-node-node-1");

    fireEvent.mouseDown(node, { button: 0, detail: 1, clientX: 12, clientY: 12 });

    expect(selectNodeMock).toHaveBeenCalledTimes(1);
    expect(startDragMock).not.toHaveBeenCalled();
  });

  it("renders inheritance split with object-type decision styling", () => {
    renderNode("compact", "inheritanceSplit");
    const node = screen.getByTestId("microflow-node-node-1");
    const diamond = node.querySelector(".microflow-decision-compact__diamond");
    expect(diamond).not.toBeNull();
    expect(diamond?.className).toContain("is-object-type");
    expect(diamond?.getAttribute("data-decision-kind")).toBe("objectType");
  });

  it("renders exclusive split with branch decision icon instead of hollow diamond glyph", () => {
    renderNode("compact", "exclusiveSplit");
    const node = screen.getByTestId("microflow-node-node-1");
    const iconSvg = node.querySelector(".microflow-decision-compact__diamond svg");
    expect(iconSvg?.querySelector("path")).not.toBeNull();
    expect(iconSvg?.querySelector("polygon")).toBeNull();
  });

  it("renders parameter object as compact oval pill with type line", () => {
    const value = {
      ...buildNodeValue("compact", "parameterObject"),
      title: "OrderList",
      parameterKind: "list",
      parameterTypeLabel: "List of Order",
    };
    const nodeInput = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={nodeInput as never} />
      </MicroflowNodeViewModesContext.Provider>
    );
    const node = screen.getByTestId("microflow-node-node-1");
    expect(node.className).toContain("microflow-flowgram-node--parameter");
    const parameter = node.querySelector(".microflow-parameter-compact");
    expect(parameter).not.toBeNull();
    expect(parameter?.getAttribute("data-parameter-kind")).toBe("list");
    expect(node.textContent?.includes("OrderList")).toBe(true);
    expect(node.textContent?.includes("List of Order")).toBe(true);
  });

  it("renders error-handling corner badges for action, loop, and decision nodes", () => {
    const value = {
      ...buildNodeValue("compact", "actionActivity"),
      errorHandlingType: "customWithRollback",
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    const { rerender } = render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    let badge = screen.getByLabelText("error-handling-R");
    expect(badge.textContent).toBe("R");

    const loopValue = {
      ...buildNodeValue("compact", "loopedActivity"),
      errorHandlingType: "customWithoutRollback",
    };
    const loopNode = {
      ...node,
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => loopValue,
        }),
      }),
    };
    rerender(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={loopNode as never} />
      </MicroflowNodeViewModesContext.Provider>
    );
    badge = screen.getByLabelText("error-handling-C");
    expect(badge.textContent).toBe("C");

    const decisionValue = {
      ...buildNodeValue("compact", "exclusiveSplit"),
      errorHandlingType: "continue",
    };
    const decisionNode = {
      ...node,
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => decisionValue,
        }),
      }),
    };
    rerender(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={decisionNode as never} />
      </MicroflowNodeViewModesContext.Provider>
    );
    badge = screen.getByLabelText("error-handling-!");
    expect(badge.textContent).toBe("!");
  });

  it("applies action background color palette when backgroundColor is set", () => {
    const value = {
      ...buildNodeValue("compact", "actionActivity"),
      backgroundColor: "yellow",
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );
    const icon = screen.getByTestId("microflow-node-node-1").querySelector(".microflow-activity-compact__icon") as HTMLElement | null;
    expect(icon).not.toBeNull();
    const style = icon?.getAttribute("style") ?? "";
    expect(style.includes("74, 48, 0")).toBe(true);
    expect(style.includes("252, 211, 77")).toBe(true);
  });

  it("applies common background color palette to decision and loop surfaces", () => {
    const decisionValue = {
      ...buildNodeValue("compact", "exclusiveSplit"),
      backgroundColor: "purple",
    };
    const decisionNode = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => decisionValue,
        }),
      }),
    };
    const { rerender } = render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={decisionNode as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    let surface = screen.getByTestId("microflow-node-node-1").querySelector(".microflow-decision-compact__diamond") as HTMLElement | null;
    let style = surface?.getAttribute("style") ?? "";
    expect(style.includes("50, 30, 90")).toBe(true);
    expect(style.includes("196, 181, 253")).toBe(true);

    const loopValue = {
      ...buildNodeValue("compact", "loopedActivity"),
      backgroundColor: "green",
      loopSource: {
        kind: "iterableList",
        listVariableName: "$OrderList",
        iteratorVariableName: "IteratorOrder",
      },
      listVariableName: "$OrderList",
      iteratorVariableName: "IteratorOrder",
    };
    const loopNode = {
      ...decisionNode,
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => loopValue,
        }),
      }),
    };
    rerender(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={loopNode as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    surface = screen.getByTestId("microflow-node-node-1").querySelector(".microflow-loop-frame__header") as HTMLElement | null;
    style = surface?.getAttribute("style") ?? "";
    expect(style.includes("13, 56, 36")).toBe(true);
    expect(style.includes("110, 231, 183")).toBe(true);
  });

  it("renders disabled title marker on compact nodes", () => {
    const value = {
      ...buildNodeValue("compact", "actionActivity"),
      title: "Change Order",
      disabled: true,
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    const element = screen.getByTestId("microflow-node-node-1");
    expect(element.className).toContain("is-disabled");
    expect(screen.getByText("Change Order [Disabled]")).toBeTruthy();
  });

  it("renders node breakpoint marker for normal and conditional breakpoints", () => {
    const value = {
      ...buildNodeValue("compact", "actionActivity"),
      hasBreakpoint: true,
      breakpointKind: "normal",
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    const { rerender } = render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    let marker = screen.getByLabelText("breakpoint");
    expect(marker.className).toContain("is-normal");

    const conditionalValue = {
      ...value,
      breakpointKind: "conditional",
    };
    const conditionalNode = {
      ...node,
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => conditionalValue,
        }),
      }),
    };
    rerender(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={conditionalNode as never} />
      </MicroflowNodeViewModesContext.Provider>
    );
    marker = screen.getByLabelText("conditional-breakpoint");
    expect(marker.className).toContain("is-conditional");
  });

  it("applies paused runtime class when node runtimeState is paused", () => {
    const value = {
      ...buildNodeValue("compact", "actionActivity"),
      runtimeState: "paused",
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );
    expect(screen.getByTestId("microflow-node-node-1").className).toContain("is-runtime-paused");
  });

  it("renders decision runtime result pill from selected branch label", () => {
    const value = {
      ...buildNodeValue("compact", "exclusiveSplit"),
      inlineConfig: {
        ...buildNodeValue("compact", "exclusiveSplit").inlineConfig,
        runtime: {
          selectedBranchLabel: "true",
          success: true,
        },
      },
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    expect(screen.getByTestId("microflow-decision-result-node-1").textContent).toContain("true");
  });

  it("renders loop iteration progress pill on loop header when runtime progress is available", () => {
    const value = {
      ...buildNodeValue("compact", "loopedActivity"),
      loopSource: {
        kind: "iterableList",
        listVariableName: "$OrderList",
        iteratorVariableName: "IteratorOrder",
      },
      loopIteration: {
        iterationIndex: 3,
        totalIterations: 8,
      },
    };
    const node = {
      id: "node-1",
      getData: () => ({
        getFormModel: () => ({
          getFormItemValueByPath: () => value,
        }),
      }),
    };
    render(
      <MicroflowNodeViewModesContext.Provider value={{}}>
        <FlowGramMicroflowNodeRenderer node={node as never} />
      </MicroflowNodeViewModesContext.Provider>
    );

    expect(screen.getByTestId("microflow-loop-iteration-node-1").textContent).toContain("第 3 / 8 次");
  });

  it("does not render decision runtime result pill when selected branch label is empty", () => {
    renderNode("compact", "exclusiveSplit");
    expect(screen.queryByTestId("microflow-decision-result-node-1")).toBeNull();
  });
});
