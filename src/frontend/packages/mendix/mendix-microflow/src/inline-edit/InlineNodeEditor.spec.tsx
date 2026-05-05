// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, type }: { children?: React.ReactNode; onClick?: () => void; type?: "button" | "submit" | "reset" }) => (
    <button type={type ?? "button"} onClick={onClick}>{children}</button>
  ),
  Input: (props: {
    value?: string;
    onChange?: (value: string) => void;
    onKeyDown?: (event: KeyboardEvent) => void;
    onBlur?: () => void;
  }) => (
    <input
      data-testid="inline-input"
      value={props.value ?? ""}
      onChange={event => props.onChange?.(event.currentTarget.value)}
      onKeyDown={event => props.onKeyDown?.(event as unknown as KeyboardEvent)}
      onBlur={() => props.onBlur?.()}
    />
  ),
  Select: (props: { value?: string; optionList?: Array<{ label: string; value: string }>; onChange?: (value?: string) => void }) => (
    <select data-testid="inline-select" value={props.value ?? ""} onChange={event => props.onChange?.(event.currentTarget.value)}>
      {(props.optionList ?? []).map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
  Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Tag: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  Typography: {
    Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconChevronDown: () => <span>down</span>,
  IconChevronRight: () => <span>right</span>,
}));

import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { InlineNodeEditor } from "./InlineNodeEditor";

afterEach(() => {
  cleanup();
});

function buildConfig(): MicroflowNodeInlineConfig {
  return {
    viewMode: "expanded",
    summaryLines: [{ id: "sum-1", value: "summary", kind: "text" }],
    sections: [{
      id: "inputs",
      title: "输入",
      kind: "inputs",
      fields: [{
        id: "f1",
        label: "incidentId",
        value: "$incidentId",
        fieldPath: "data.action.input",
        editType: "text",
      }],
    }],
    runtime: {
      failed: true,
      durationMs: 12,
      inputPreview: "riskScore=92",
      outputPreview: "statusCode=500",
      selectedBranchLabel: "true",
      error: {
        code: "RUNTIME_ASSERTION_FAILED",
        message: "boom",
        stackPreview: "node-1\nnode-2",
        fixSuggestions: [{ id: "fix-1", label: "展开错误详情", actionKind: "expandError" }],
      },
    },
  };
}

describe("InlineNodeEditor", () => {
  it("renders sections and runtime/error blocks", () => {
    render(
      <InlineNodeEditor
        inlineConfig={buildConfig()}
        onCommitField={() => undefined}
      />,
    );

    expect(screen.getByText("输入")).not.toBeNull();
    expect(screen.getByText("incidentId")).not.toBeNull();
    expect(screen.getByText("input: riskScore=92")).not.toBeNull();
    expect(screen.getByText("output: statusCode=500")).not.toBeNull();
    expect(screen.getByText("selected: true")).not.toBeNull();
    expect(screen.getByText("RUNTIME_ASSERTION_FAILED")).not.toBeNull();
    expect(screen.getByText("boom")).not.toBeNull();
    expect(screen.getByText("展开错误详情")).not.toBeNull();
  });

  it("commits inline field edit and applies quick fix", () => {
    const onCommitField = vi.fn();
    const onApplyQuickFix = vi.fn();
    render(
      <InlineNodeEditor
        inlineConfig={buildConfig()}
        onCommitField={onCommitField}
        onApplyQuickFix={onApplyQuickFix}
      />,
    );

    fireEvent.click(screen.getByText("$incidentId"));
    fireEvent.change(screen.getByTestId("inline-input"), { target: { value: "$incidentNo" } });
    fireEvent.keyDown(screen.getByTestId("inline-input"), { key: "Enter" });
    expect(onCommitField).toHaveBeenCalledTimes(1);
    expect(onCommitField.mock.calls[0]?.[0]?.fieldPath).toBe("data.action.input");
    expect(onCommitField.mock.calls[0]?.[1]).toBe("$incidentNo");

    fireEvent.click(screen.getByText("展开错误详情"));
    expect(onApplyQuickFix).toHaveBeenCalledTimes(1);
    expect(onApplyQuickFix.mock.calls[0]?.[0]?.actionKind).toBe("expandError");
  });

  it("passes extended quick-fix payload", () => {
    const onApplyQuickFix = vi.fn();
    const config = buildConfig();
    if (config.runtime?.error?.fixSuggestions?.[0]) {
      config.runtime.error.fixSuggestions[0] = {
        ...config.runtime.error.fixSuggestions[0],
        actionKind: "setFieldValue",
        fieldPath: "data.action.request.urlExpression.raw",
        value: "/api/incidents/fix",
        editType: "http",
      };
    }
    render(<InlineNodeEditor inlineConfig={config} onCommitField={() => undefined} onApplyQuickFix={onApplyQuickFix} />);
    fireEvent.click(screen.getByText("展开错误详情"));
    expect(onApplyQuickFix).toHaveBeenCalledTimes(1);
    expect(onApplyQuickFix.mock.calls[0]?.[0]?.fieldPath).toBe("data.action.request.urlExpression.raw");
    expect(onApplyQuickFix.mock.calls[0]?.[0]?.value).toBe("/api/incidents/fix");
    expect(onApplyQuickFix.mock.calls[0]?.[0]?.editType).toBe("http");
  });
});
