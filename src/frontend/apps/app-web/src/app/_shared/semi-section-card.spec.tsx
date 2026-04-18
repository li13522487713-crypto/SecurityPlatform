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
    Title: ({ children }: { children: ReactNode }) => <h5 data-testid="mock-title">{children}</h5>,
    Text: ({ children }: { children: ReactNode }) => <span data-testid="mock-text">{children}</span>
  }
}));

import { SectionCard } from "./semi-section-card";

describe("SectionCard", () => {
  it("渲染 title / subtitle / actions / children", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <SectionCard
          title="区块"
          subtitle="说明"
          actions={<button data-testid="action-btn">add</button>}
          testId="section"
        >
          <div data-testid="content">x</div>
        </SectionCard>
      );
    });

    expect(container.querySelector('[data-testid="section"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="mock-title"]')?.textContent).toBe("区块");
    expect(container.querySelector('[data-testid="mock-text"]')?.textContent).toBe("说明");
    expect(container.querySelector('[data-testid="action-btn"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="content"]')).not.toBeNull();
  });

  it("无 title 与 actions 时跳过 header 区域", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <SectionCard testId="section-bare">
          <div data-testid="content">x</div>
        </SectionCard>
      );
    });

    expect(container.querySelector('[data-testid="mock-title"]')).toBeNull();
    expect(container.querySelector('[data-testid="content"]')).not.toBeNull();
  });
});
