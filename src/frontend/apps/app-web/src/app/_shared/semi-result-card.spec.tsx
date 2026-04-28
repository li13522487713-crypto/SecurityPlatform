// @vitest-environment jsdom

import type { ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Card: ({
    children,
    "data-testid": testId
  }: {
    children: ReactNode;
    "data-testid"?: string;
  }) => <section data-testid={testId ?? "mock-card"}>{children}</section>,
  Typography: {
    Title: ({ children }: { children: ReactNode }) => <h4 data-testid="mock-title">{children}</h4>,
    Text: ({ children }: { children: ReactNode }) => <span data-testid="mock-text">{children}</span>
  }
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconTickCircle: () => <i data-testid="icon-success" />,
  IconAlertCircle: () => <i data-testid="icon-warning" />,
  IconClose: () => <i data-testid="icon-error" />,
  IconInfoCircle: () => <i data-testid="icon-info" />
}));

import { ResultCard } from "./semi-result-card";

describe("ResultCard", () => {
  it.each([
    ["success", "icon-success"],
    ["warning", "icon-warning"],
    ["error", "icon-error"],
    ["info", "icon-info"]
  ] as const)("status=%s 渲染对应图标 %s", (status, iconTestId) => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <ResultCard
          status={status}
          title="标题"
          description="描述"
          testId={`result-${status}`}
          actions={<button data-testid="action">ok</button>}
          extra={<div data-testid="extra">extra</div>}
        />
      );
    });

    expect(container.querySelector(`[data-testid="${iconTestId}"]`)).not.toBeNull();
    expect(container.querySelector(`[data-testid="result-${status}"]`)).not.toBeNull();
    expect(container.querySelector('[data-testid="mock-title"]')?.textContent).toBe("标题");
    expect(container.querySelector('[data-testid="mock-text"]')?.textContent).toBe("描述");
    expect(container.querySelector('[data-testid="action"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="extra"]')).not.toBeNull();
  });

  it("默认 status=info", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(<ResultCard title="标题" testId="result-default" />);
    });

    expect(container.querySelector('[data-testid="icon-info"]')).not.toBeNull();
  });
});
