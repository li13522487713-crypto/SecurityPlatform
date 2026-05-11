// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

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
    selectNode: () => undefined,
    nodeRef: { current: document.createElement("div") },
    startDrag: () => undefined,
    onFocus: () => undefined,
    onBlur: () => undefined,
  }),
}));

import { FlowGramMicroflowNodeRenderer } from "./FlowGramMicroflowNodeRenderer";
import { subscribeInlineNodeToggle } from "./inline-events";
import { MicroflowNodeViewModesContext } from "./FlowGramMicroflowTypes";

afterEach(() => {
  cleanup();
});

function buildNodeValue(viewMode: "compact" | "expanded" = "compact") {
  return {
    objectId: "node-1",
    objectKind: "exclusiveSplit",
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

function renderNode(viewMode: "compact" | "expanded" = "compact") {
  const value = buildNodeValue(viewMode);
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
});
