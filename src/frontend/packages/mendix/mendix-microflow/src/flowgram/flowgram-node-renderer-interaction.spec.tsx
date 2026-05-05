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
  Typography: {
    Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconChevronDown: () => <span>down</span>,
  IconChevronUp: () => <span>up</span>,
  IconEdit: () => <span>edit</span>,
  IconMore: () => <span>more</span>,
}));

vi.mock("../inline-edit", () => ({
  InlineNodeEditor: ({ onApplyQuickFix }: { onApplyQuickFix?: (input: { id: string; actionKind: string; fieldPath?: string; value?: string; editType?: string }) => void }) => (
    <div data-testid="inline-node-editor">
      inline-node-editor
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
  return render(<FlowGramMicroflowNodeRenderer node={node as never} />);
}

describe("FlowGramMicroflowNodeRenderer interaction", () => {
  it("dispatches inline node toggle event on double click", () => {
    const events: Array<{ nodeId: string; expanded: boolean }> = [];
    const listener = (event: Event) => {
      events.push((event as CustomEvent<{ nodeId: string; expanded: boolean }>).detail);
    };
    window.addEventListener("atlas:microflow-inline-node-toggle", listener as EventListener);
    try {
      renderNode();
      fireEvent.doubleClick(screen.getByTestId("microflow-node-node-1"));
      expect(events).toEqual([{ nodeId: "node-1", expanded: true }]);
    } finally {
      window.removeEventListener("atlas:microflow-inline-node-toggle", listener as EventListener);
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
});
