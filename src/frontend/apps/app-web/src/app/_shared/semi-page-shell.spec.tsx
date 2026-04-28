// @vitest-environment jsdom

import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Spin: ({ size }: { size?: string }) => (
    <div data-testid="mock-spin" data-size={size ?? ""}>
      spin
    </div>
  )
}));

import { PageShell } from "./semi-page-shell";

describe("PageShell", () => {
  it("loading=true 时只渲染 Spin，忽略 children", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <PageShell loading testId="page-loading" loadingTip="加载中">
          <div data-testid="hidden-child">hidden</div>
        </PageShell>
      );
    });

    expect(container.querySelector('[data-testid="mock-spin"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="hidden-child"]')).toBeNull();
    expect(container.querySelector('[data-testid="page-loading"]')).not.toBeNull();
    expect(container.textContent).toContain("加载中");
  });

  it("centered=true 时包裹 children 居中容器，并透传 testId", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <PageShell centered testId="page-centered">
          <div data-testid="content">payload</div>
        </PageShell>
      );
    });

    const root2 = container.querySelector('[data-testid="page-centered"]');
    expect(root2).not.toBeNull();
    expect(root2?.querySelector('[data-testid="content"]')?.textContent).toBe("payload");
  });

  it("默认非居中模式渲染 children 不引入额外限宽容器", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <PageShell>
          <span data-testid="plain">x</span>
        </PageShell>
      );
    });

    expect(container.querySelector('[data-testid="plain"]')?.textContent).toBe("x");
  });
});
